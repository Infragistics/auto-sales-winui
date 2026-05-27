namespace AutoSales.ViewModels
{
    /// <summary>
    /// Simple node in the navigation breadcrumb (region / state / dealer).
    /// Mirrors the WPF Logic project's BreadCrumbViewModel.
    /// </summary>
    public class BreadCrumbViewModel
    {
        public BreadCrumbViewModel(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; }
        public string Label { get; }
    }
}
