namespace AutoSales.ViewModels
{
    public sealed class MapElementViewModel
    {
        public MapElementViewModel(string name, double value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; private set; }
        public double Value { get; private set; }
    }
}
