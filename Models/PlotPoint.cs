using System;

namespace AutoSales.Models
{
    public class PlotPoint
    {
        public PlotPoint(DateTime date, double value)
        {
            Date = date;
            Value = value;
        }

        public DateTime Date { get; private set; }
        public double Value { get; private set; }
    }
}
