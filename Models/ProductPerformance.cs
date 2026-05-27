namespace AutoSales.Models
{
    public class ProductPerformance
    {
        public ProductPerformance() { }

        public ProductPerformance(string name, string description, PlotPoint[] sales,
            double value, double target, string category, int hp, int doorsCount, string model)
        {
            Name = CorrectModelName(name);
            Description = description;
            Sales = sales;
            Value = value;
            Target = target;
            Category = category;
            Hp = hp;
            DoorsCount = doorsCount;
            Model = model;
        }

        public object Name { get; private set; }
        public string Description { get; private set; }
        public PlotPoint[] Sales { get; private set; }
        public double Value { get; private set; }
        public double Target { get; private set; }
        public double Percent { get; set; }
        public double Max { get; set; }
        public string Category { get; private set; }
        public int Hp { get; private set; }
        public int DoorsCount { get; private set; }
        public string Model { get; private set; }
        public bool IsTargetReached { get; set; }

        public static string CorrectModelName(string model)
        {
            switch (model)
            {
                case "Magarcedes": return "Mercedes";
                case "Paushe": return "Porche";
                case "Masda": return "Mazda";
                case "Auti": return "Audi";
                case "McLargen": return "McLaren";
                case "Hoida": return "Honda";
                case "BMV": return "BMW";
                default: return "Unknown";
            }
        }
    }
}
