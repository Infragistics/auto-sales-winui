using Infragistics.Controls;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace AutoSales.ViewModels
{
    public class USStateViewModel
    {
        public USStateViewModel() { }
        public USStateViewModel(string id, string name, double value) { }

        internal DashboardViewModel Parent { get; set; }
        public bool RevenueMeasureVisibility => Parent != null && Parent.RevenueMeasureVisibility;
        public bool VolumeMeasureVisibility => Parent != null && Parent.VolumeMeasureVisibility;

        public string Id { get; set; }
        public string NameAbbreviation { get; set; }
        public string Name { get; set; }
        public string RegionName { get; set; }
        public string SubRegionName { get; set; }

        public double Revenue { get; set; }
        public double RevenuePercent => 100 * RevenueFraction;
        public double RevenueFraction { get; set; }

        public double Volume { get; set; }
        public double VolumePercent => 100 * VolumeFraction;
        public double VolumeFraction { get; set; }

        public List<PublicPointCollection> Shape { get; set; }
        public bool HasDealers { get; set; }

        // WinUI's Windows.Foundation.Rect doesn't have Empty; sentinel via NaN width.
        private bool _worldRectComputed;
        private Rect _worldRect;
        public Rect WorldRect
        {
            get
            {
                if (!_worldRectComputed)
                {
                    _worldRect = GetBoundingRectFromPoints();
                    _worldRectComputed = true;
                }
                return _worldRect;
            }
        }

        private Rect GetBoundingRectFromPoints()
        {
            if (Shape == null || Shape.Count == 0 || Shape[0].Count == 0)
                return new Rect(0, 0, 0, 0);

            var minX = Shape[0].Min(p => p.X);
            var minY = Shape[0].Min(p => p.Y);
            var maxX = Shape[0].Max(p => p.X);
            var maxY = Shape[0].Max(p => p.Y);
            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }
    }
}
