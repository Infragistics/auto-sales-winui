namespace AutoSales.Models
{
    public class SalesPersonPerformance
    {
        public SalesPersonPerformance() { }

        public SalesPersonPerformance(string name, bool gender, PlotPoint[] sales,
            double value, double target, string hireDate, string phone, string eMail)
        {
            Name = name;
            Gender = gender;
            Sales = sales;
            Value = value;
            Target = target;
            HireDate = hireDate;
            Phone = phone;
            Email = eMail;
        }

        public object Name { get; private set; }
        public bool Gender { get; private set; }
        public PlotPoint[] Sales { get; private set; }
        public double Value { get; private set; }
        public double Target { get; private set; }
        public double Percent { get; set; }
        public double Max { get; set; }
        public string HireDate { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public bool IsTargetReached { get; set; }
    }
}
