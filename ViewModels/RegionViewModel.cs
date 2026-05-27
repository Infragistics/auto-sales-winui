using Infragistics.Controls;
using System.Collections.Generic;
using Windows.Foundation;

namespace AutoSales.ViewModels
{
    public class RegionViewModel
    {
        public RegionViewModel() { }
        public RegionViewModel(string id, string name, double value) { }

        public string Id { get; set; }
        public string Name { get; set; }
        internal DashboardViewModel Parent { get; set; }
        public bool RevenueMeasureVisibility => Parent != null && Parent.RevenueMeasureVisibility;
        public bool VolumeMeasureVisibility => Parent != null && Parent.VolumeMeasureVisibility;

        public double Volume { get; set; }
        public double VolumePercent => 100 * VolumeFraction;
        public double VolumeFraction { get; set; }

        public double Revenue { get; set; }
        public double RevenuePercent => 100 * RevenueFraction;
        public double RevenueFraction { get; set; }

        public List<PublicPointCollection> Shape { get; set; }
        public bool HasDealers { get; set; }
        public Rect WorldRect { get; set; }
    }
}
