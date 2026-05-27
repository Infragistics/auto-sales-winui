namespace AutoSales.Models
{
    public enum FilterType
    {
        All,
        ByRegion,
        ByState,
        ByDealership
    }

    public enum MeasureType
    {
        Revenue,
        Volume
    }

    public enum ReportPeriod
    {
        TwelveMonths,
        YearToDate,
        Quarter,
        Month,
        Week
    }
}
