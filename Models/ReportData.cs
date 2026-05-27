namespace AutoSales.Models
{
    public class ReportData
    {
        public ReportData(PlotPoint[] sales, ProductPerformance[] products, SalesPersonPerformance[] salesPeople)
        {
            Sales = sales;
            Products = products;
            SalesPeople = salesPeople;
        }

        public PlotPoint[] Sales { get; private set; }
        public ProductPerformance[] Products { get; private set; }
        public SalesPersonPerformance[] SalesPeople { get; private set; }
    }
}
