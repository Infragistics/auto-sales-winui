using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AutoSales.Models;
using Microsoft.Data.Sqlite;

namespace AutoSales.Services
{
    /// <summary>
    /// Data service backed by a bundled SQLite database (igAutoDealership.db3).
    /// Mirrors the WPF AutoSalesService API. Uses Microsoft.Data.Sqlite (works on
    /// .NET 9 / WinUI; the WPF original used System.Data.SQLite via DbProviderFactories
    /// which doesn't exist on modern .NET).
    /// </summary>
    public static class AutoSalesService
    {
        private const string DatabaseBinFile = "igAutoDealership.db3";
        private const string DatabaseZipFile = "igAutoDealership.zip";
        private static readonly string ConnectionString;

        static AutoSalesService()
        {
            try
            {
                // Source zip is bundled next to the exe (in AppX install dir for packaged apps).
                // Extract to a writable location — the AppX dir is read-only at runtime in
                // packaged apps, which causes ZipFile.ExtractToDirectory to silently produce
                // a truncated file (header-valid but body short → SQLite returns
                // "database disk image is malformed" later).
                var sourceZip = Path.Combine(AppContext.BaseDirectory, "Data", DatabaseZipFile);
                var workingDir = GetWritableDataDirectory();
                var dbPath = Path.Combine(workingDir, DatabaseBinFile);

                EnsureDatabaseExtracted(sourceZip, workingDir, dbPath);
                ConnectionString = $"Data Source={dbPath};Mode=ReadOnly";
            }
            catch (Exception ex)
            {
                throw new Exception("AutoSalesService failed on initialization", ex);
            }
        }

        private static string GetWritableDataDirectory()
        {
            // Use temp + a per-app subdir so we don't pollute the user's temp root and
            // so multiple debug sessions / launches share the same extracted DB.
            var dir = Path.Combine(Path.GetTempPath(), "Infragistics", "AutoSales", "Data");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        private static void EnsureDatabaseExtracted(string sourceZip, string workingDir, string binFile)
        {
            if (!File.Exists(sourceZip))
                throw new Exception("could not find zipped database file: " + sourceZip);

            // Read the zip's declared uncompressed size for the .db3 entry; we'll use it
            // both as a quick "is the existing extracted file the right size" check and
            // to detect partial extracts.
            long expectedSize = -1;
            using (var archive = ZipFile.OpenRead(sourceZip))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.Equals(entry.Name, DatabaseBinFile, StringComparison.OrdinalIgnoreCase))
                    {
                        expectedSize = entry.Length;
                        break;
                    }
                }
            }
            if (expectedSize <= 0)
                throw new Exception("zipped database file did not contain entry: " + DatabaseBinFile);

            // Reuse the extracted file only if it matches the expected size. This handles
            // the previous-run-truncated case (where AppX-dir extraction produced a header-
            // valid but body-short file that SQLite rejects with "database disk image is
            // malformed").
            if (File.Exists(binFile))
            {
                var existingSize = new FileInfo(binFile).Length;
                if (existingSize == expectedSize) return;
                File.Delete(binFile);
            }

            // Extract just the .db3 entry directly to the target path so we don't depend
            // on ZipFile.ExtractToDirectory's path conventions.
            using (var archive = ZipFile.OpenRead(sourceZip))
            {
                var entry = archive.Entries.FirstOrDefault(e =>
                    string.Equals(e.Name, DatabaseBinFile, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    throw new Exception("zipped database file did not contain entry: " + DatabaseBinFile);
                entry.ExtractToFile(binFile, overwrite: true);
            }

            var actual = new FileInfo(binFile).Length;
            if (actual != expectedSize)
                throw new Exception($"unzipped database file is the wrong size: {actual} bytes, expected {expectedSize}");
        }

        #region Public API

        public static IEnumerable<Transaction> GetTransactions(ReportPeriod period)
        {
            var transactions = new List<Transaction>();
            DateTime startDate, endDate;
            GetStartEndDates(period, out startDate, out endDate);

            const string sql =
                "select s.ProductTotalCost, s.ProductQuantity, d.Name Dealer, p.Name Model, d.Region, d.City City, datetime(s.PurchaseDate,'start of day') Date " +
                "from (SalesTransactions s " +
                "inner join Dealerships d on s.DealershipID = d.ID " +
                "inner join Products p on s.ProductSKU = p.SKU) " +
                "where s.PurchaseDate >= @startDate and s.PurchaseDate < @endDate";

            var dt = ExecuteQuery(sql, new[]
            {
                ("@startDate", (object)startDate),
                ("@endDate", (object)endDate)
            });

            foreach (var row in dt.AsEnumerable())
            {
                var model = row["Model"].ToString();
                var quantity = int.Parse(row["ProductQuantity"].ToString());
                var totalCost = double.Parse(row["ProductTotalCost"].ToString());
                var region = row["Region"].ToString();
                var city = row["City"].ToString();
                var dealer = row["Dealer"].ToString();
                var date = DateTime.Parse(row["Date"].ToString());
                transactions.Add(new Transaction(model, quantity, totalCost, 0.0, dealer, region, city, date));
            }

            var allSales = transactions.Sum(x => x.TotalCost);
            if (allSales > 0)
            {
                foreach (var t in transactions)
                    t.Percent = t.TotalCost / allSales;
            }

            return transactions.ToArray();
        }

        public static IEnumerable<Dealer> GetDealers()
        {
            var dealers = new List<Dealer>();
            double scaleMaxRevenue = 0;
            int scaleMaxVolume = 0;

            DateTime startDate, endDate;
            GetStartEndDates(ReportPeriod.TwelveMonths, out startDate, out endDate);

            const string sql =
                "select dealers.ID, Name, Region, State, County, City, Address, PostalCode, Longitude, Latitude, " +
                "total(sales.ProductTotalCost) Revenue, total(sales.ProductQuantity) Quantity " +
                "from Dealerships dealers " +
                "left join SalesTransactions sales on dealers.ID = sales.DealershipID " +
                "where sales.DealershipID IS NULL or (sales.PurchaseDate >= @startDate and sales.PurchaseDate < @endDate) " +
                "group by dealers.ID order by Revenue desc";

            var dt = ExecuteQuery(sql, new[]
            {
                ("@startDate", (object)startDate),
                ("@endDate", (object)endDate)
            });

            foreach (var row in dt.AsEnumerable())
            {
                var id = row["ID"].ToString();
                var name = row["Name"].ToString();
                var region = row["Region"].ToString();
                var state = row["State"].ToString();
                var county = row["County"].ToString();
                var city = row["City"].ToString();
                var address = row["Address"].ToString();
                var code = row["PostalCode"].ToString();
                var longitude = double.Parse(row["Longitude"].ToString());
                var latitude = double.Parse(row["Latitude"].ToString());
                var revenue = double.Parse(row["Revenue"].ToString());
                var quantity = int.Parse(row["Quantity"].ToString());
                dealers.Add(new Dealer(id, name, region, state, county, city, address, code,
                    longitude, latitude, revenue, quantity));
            }

            if (dealers.Count > 0)
            {
                scaleMaxRevenue = CalculateScaleMax(dealers.Max(x => x.Revenue));
                scaleMaxVolume = (int)CalculateScaleMax(dealers.Max(x => x.Volume));
            }

            foreach (var dealer in dealers)
            {
                dealer.ScaleMaxRevenue = scaleMaxRevenue;
                dealer.ScaleMaxVolume = scaleMaxVolume;
            }

            return dealers.ToArray();
        }

        public static ReportData GetReportData(MeasureType measure, ReportPeriod period,
            FilterType filter, string filterParam)
        {
            DateTime startDate, endDate;
            GetStartEndDates(period, out startDate, out endDate);

            return new ReportData(
                GetOverallPerformance(measure, filter, filterParam, startDate, endDate),
                GetProductPerformance(measure, filter, filterParam, startDate, endDate),
                GetSalesPersonPerformance(measure, filter, filterParam, startDate, endDate)
            );
        }

        #endregion

        #region Date helpers

        private static void GetStartEndDates(ReportPeriod period, out DateTime startDate, out DateTime endDate)
        {
            endDate = DateTime.UtcNow.Date.AddDays(1);
            // The bundled DB only has data through 2010 — pin the "current" year accordingly.
            endDate = endDate.AddYears(2010 - endDate.Year);

            switch (period)
            {
                case ReportPeriod.TwelveMonths: startDate = endDate.AddMonths(-12); break;
                case ReportPeriod.YearToDate: startDate = new DateTime(endDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc); break;
                case ReportPeriod.Quarter: startDate = endDate.AddMonths(-3); break;
                case ReportPeriod.Month: startDate = endDate.AddMonths(-1); break;
                case ReportPeriod.Week: startDate = endDate.AddDays(-7); break;
                default: throw new ArgumentOutOfRangeException(nameof(period));
            }
        }

        #endregion

        #region Per-section query builders

        private static PlotPoint[] GetOverallPerformance(MeasureType measure, FilterType filter,
            string filterParam, DateTime startDate, DateTime endDate)
        {
            var sb = new StringBuilder();
            sb.Append("select datetime(s.PurchaseDate,'start of day') Date, total(s.");
            sb.Append(measure == MeasureType.Revenue ? "ProductTotalCost" : "ProductQuantity");
            sb.Append(") Value from SalesTransactions s ");
            var args = new List<(string, object)>();
            AppendFilterClause(sb, filter, filterParam, args);
            sb.Append("s.PurchaseDate >= @startDate and s.PurchaseDate < @endDate group by Date order by Date;");
            args.Add(("@startDate", startDate));
            args.Add(("@endDate", endDate));

            var dt = ExecuteQuery(sb.ToString(), args.ToArray());
            return dt.AsEnumerable().Select(row =>
                new PlotPoint(DateTime.Parse(row["Date"].ToString()), double.Parse(row["Value"].ToString()))).ToArray();
        }

        private static ProductPerformance[] GetProductPerformance(MeasureType measure, FilterType filter,
            string filterParam, DateTime startDate, DateTime endDate)
        {
            var dtSales = ExecuteQuery(BuildSalesSql(GroubBy.Product, measure, filter, filterParam, startDate, endDate, out var salesArgs), salesArgs);
            var dtProduct = ExecuteQuery(BuildProductSql(measure, filter, filterParam, startDate, endDate, out var prodArgs), prodArgs);
            return BuildProducts(dtSales, dtProduct, startDate, endDate);
        }

        private static SalesPersonPerformance[] GetSalesPersonPerformance(MeasureType measure, FilterType filter,
            string filterParam, DateTime startDate, DateTime endDate)
        {
            var dtSales = ExecuteQuery(BuildSalesSql(GroubBy.SalesPerson, measure, filter, filterParam, startDate, endDate, out var salesArgs), salesArgs);
            var dtPerson = ExecuteQuery(BuildSalesPersonSql(measure, filter, filterParam, startDate, endDate, out var personArgs), personArgs);
            return BuildSalesPeople(dtSales, dtPerson, startDate, endDate);
        }

        private static string BuildSalesSql(GroubBy groupBy, MeasureType measure, FilterType filter,
            string filterParam, DateTime startDate, DateTime endDate, out (string, object)[] args)
        {
            var groupByColumn = groupBy == GroubBy.Product ? "ProductSKU" : "SalesRepID";
            var sb = new StringBuilder();
            sb.Append("select s.");
            sb.Append(groupByColumn);
            sb.Append(" ID, datetime(s.PurchaseDate,'start of day') Date, total(s.");
            sb.Append(measure == MeasureType.Revenue ? "ProductTotalCost" : "ProductQuantity");
            sb.Append(") Value from SalesTransactions s ");
            var argList = new List<(string, object)>();
            AppendFilterClause(sb, filter, filterParam, argList);
            // group by the underlying column rather than the "ID" alias — when filter is
            // ByRegion/ByState, AppendFilterClause inner-joins Dealerships d which also has
            // an ID column, making bare "group by ID" ambiguous to Microsoft.Data.Sqlite.
            sb.Append("s.PurchaseDate >= @startDate and s.PurchaseDate < @endDate group by s.");
            sb.Append(groupByColumn);
            sb.Append(", Date order by Date;");
            argList.Add(("@startDate", startDate));
            argList.Add(("@endDate", endDate));
            args = argList.ToArray();
            return sb.ToString();
        }

        private static string BuildProductSql(MeasureType measure, FilterType filter, string filterParam,
            DateTime startDate, DateTime endDate, out (string, object)[] args)
        {
            var sb = new StringBuilder();
            sb.Append("select p.SKU ID, p.Name, p.Description, p.Category, p.HP, p.Doors, p.Model, total(t.");
            sb.Append(measure == MeasureType.Revenue ? "YearlyRevenueTarget" : "YearlyVolumeTarget");
            sb.Append(") YearlyTarget from products p inner join (select distinct s.ProductSKU SKU from SalesTransactions s ");
            var argList = new List<(string, object)>();
            AppendFilterClause(sb, filter, filterParam, argList);
            sb.Append("s.PurchaseDate >= @startDate and s.PurchaseDate < @endDate) p1 on p1.SKU = p.SKU ");
            argList.Add(("@startDate", startDate));
            argList.Add(("@endDate", endDate));
            sb.Append("inner join DealershipProductTargets t on t.ProductSKU = p.SKU ");
            switch (filter)
            {
                case FilterType.All: break;
                case FilterType.ByRegion: sb.Append("inner join Dealerships d on d.ID = t.DealershipID where d.Region = @region "); break;
                case FilterType.ByState: sb.Append("inner join Dealerships d on d.ID = t.DealershipID where d.State = @state "); break;
                case FilterType.ByDealership: sb.Append("where t.DealershipID = @dealerId "); break;
            }
            // Group by the underlying column rather than the "ID" alias — when filter is
            // ByRegion/ByState the outer query joins Dealerships d which also has an ID
            // column, making a bare "group by ID" ambiguous to SQLite.
            sb.Append("group by p.SKU");
            args = argList.ToArray();
            return sb.ToString();
        }

        private static string BuildSalesPersonSql(MeasureType measure, FilterType filter, string filterParam,
            DateTime startDate, DateTime endDate, out (string, object)[] args)
        {
            var sb = new StringBuilder();
            sb.Append("select e.ID, e.FirstName||' '||e.LastName Name, e.gender Gender, e.HireDate HireDate, cast(e.");
            sb.Append(measure == MeasureType.Revenue ? "YearlyRevenueTarget" : "YearlyVolumeTarget");
            sb.Append(" as real) YearlyTarget from Employees e inner join (select distinct s.SalesRepID ID from SalesTransactions s ");
            var argList = new List<(string, object)>();
            AppendFilterClause(sb, filter, filterParam, argList);
            sb.Append("s.PurchaseDate >= @startDate and s.PurchaseDate < @endDate) t on t.ID = e.ID;");
            argList.Add(("@startDate", startDate));
            argList.Add(("@endDate", endDate));
            args = argList.ToArray();
            return sb.ToString();
        }

        private static void AppendFilterClause(StringBuilder sb, FilterType filter, string filterParam,
            List<(string, object)> args)
        {
            switch (filter)
            {
                case FilterType.All:
                    sb.Append("where ");
                    break;
                case FilterType.ByRegion:
                    sb.Append("inner join Dealerships d on d.ID = s.DealershipID where d.Region = @region and ");
                    args.Add(("@region", filterParam));
                    break;
                case FilterType.ByState:
                    sb.Append("inner join Dealerships d on d.ID = s.DealershipID where d.State = @state and ");
                    args.Add(("@state", filterParam));
                    break;
                case FilterType.ByDealership:
                    sb.Append("where s.DealershipID = @dealerId and ");
                    args.Add(("@dealerId", filterParam));
                    break;
            }
        }

        private static SalesPersonPerformance[] BuildSalesPeople(DataTable sales, DataTable lookup,
            DateTime startDate, DateTime endDate)
        {
            var lookupDic = lookup.AsEnumerable().ToDictionary(x => x.Field<string>("ID"));
            var targetMultiplier = (endDate - startDate).Days / 365d;

            var result = sales.AsEnumerable().GroupBy(x => x.Field<string>("ID")).Select(x =>
                new SalesPersonPerformance(
                    lookupDic[x.Key].Field<string>("Name"),
                    lookupDic[x.Key].Field<string>("Gender") == "M",
                    x.Select(y => new PlotPoint(DateTime.Parse(y.Field<string>("Date")),
                        double.Parse(y["Value"].ToString()))).ToArray(),
                    x.Sum(y => double.Parse(y["Value"].ToString())),
                    Convert.ToDouble(lookupDic[x.Key]["YearlyTarget"]) * targetMultiplier,
                    DateTime.Parse(lookupDic[x.Key]["HireDate"].ToString()).ToShortDateString(),
                    "1234567890",
                    string.Format("{0}@autodealers.com",
                        lookupDic[x.Key].Field<string>("Name").ToLower().Replace(' ', '.'))
                )
            ).OrderByDescending(x => x.Value).Take(20).ToArray();

            if (result.Length > 0)
            {
                var max = CalculateScaleMax(result.Max(x => Math.Max(x.Value, x.Target)));
                var allSales = result.Sum(x => x.Value);
                foreach (var p in result)
                {
                    p.Percent = allSales > 0 ? (p.Value / allSales) * max : 0;
                    p.Max = max;
                    p.IsTargetReached = p.Value - p.Target > 0;
                }
            }
            return result;
        }

        private static ProductPerformance[] BuildProducts(DataTable sales, DataTable lookup,
            DateTime startDate, DateTime endDate)
        {
            var lookupDic = lookup.AsEnumerable().ToDictionary(x => x.Field<string>("ID"));
            var targetMultiplier = (endDate - startDate).Days / 365d;

            var result = sales.AsEnumerable().GroupBy(x => x.Field<string>("ID")).Select(x =>
                new ProductPerformance(
                    lookupDic[x.Key].Field<string>("Name"),
                    lookupDic[x.Key].Field<string>("Description"),
                    x.Select(y => new PlotPoint(DateTime.Parse(y.Field<string>("Date")),
                        double.Parse(y["Value"].ToString()))).ToArray(),
                    x.Sum(y => double.Parse(y["Value"].ToString())),
                    Convert.ToDouble(lookupDic[x.Key]["YearlyTarget"]) * targetMultiplier,
                    lookupDic[x.Key].Field<string>("Category"),
                    Convert.ToInt32(lookupDic[x.Key]["HP"]),
                    Convert.ToInt32(lookupDic[x.Key]["Doors"]),
                    lookupDic[x.Key].Field<string>("Model")
                )
            ).ToArray();

            if (result.Length > 0)
            {
                var max = CalculateScaleMax(result.Max(x => Math.Max(x.Value, x.Target)));
                var allSales = result.Sum(x => x.Value);
                foreach (var p in result)
                {
                    p.Percent = allSales > 0 ? (p.Value / allSales) * max : 0;
                    p.Max = max;
                    p.IsTargetReached = p.Value - p.Target > 0;
                }
                Array.Sort(result, (x, y) => -Comparer<double>.Default.Compare(x.Value, y.Value));
            }
            return result;
        }

        private static double CalculateScaleMax(double value)
        {
            if (value < 1.0) return 1.0;
            var power = Math.Floor(Math.Log10(value));
            var tenToPower = Math.Pow(10, power);
            return tenToPower * Math.Ceiling(value / tenToPower);
        }

        #endregion

        #region SQLite execution

        private static DataTable ExecuteQuery(string sql, (string Name, object Value)[] parameters)
        {
            var dt = new DataTable();
            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                            cmd.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
                    }
                    using (var reader = cmd.ExecuteReader())
                    {
                        dt.Load(reader);
                    }
                }
            }
            return dt;
        }

        private enum GroubBy
        {
            Product,
            SalesPerson
        }

        #endregion
    }
}
