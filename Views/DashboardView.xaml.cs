using System;
using AutoSales.Models;
using AutoSales.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Microsoft.UI.Xaml.Controls.Primitives;  // ToggleButton

using Infragistics.Controls.Charts;     // XamDataChart, AreaSeries, axes, DataChartMouseButtonEventArgs
using Infragistics.Controls.Grids;      // XamDataGrid, TemplateColumn, TemplateCellUpdatingEventArgs, CellStyleRequestedEventArgs
using Infragistics.Controls.Maps;       // XamGeographicMap, GeographicShapeSeries, GeographicSymbolSeries
using Infragistics.Controls.Gauges;
using Windows.UI;     // XamBulletGraph

namespace AutoSales.Views
{
    /// <summary>
    /// Main dashboard view. Wires:
    /// * Geographic map series mouse interactions (region/state/dealer drill-down).
    /// * Overall-performance XamDataChart in the right pane.
    /// * Two XamDataGrids (Model Performance + Salesperson Performance) using
    ///   TemplateColumn.CellUpdating + CellStyleKeyRequested for the sparkline-like
    ///   "Last week" column and the XamBulletGraph "Percentage of Target" column.
    ///   Style keys are paired with cell visual type so virtualized cells get pooled
    ///   and reused with matching cells (sparkline cells with sparkline cells, bullet
    ///   cells with bullet cells).
    /// * Bottom dealer grid that filters everything else when the user picks a dealer.
    /// </summary>
    public sealed partial class DashboardView : UserControl
    {
        private readonly DashboardViewModel _vm;

        // Style keys — must be unique per visual shape so the grid recycles cells
        // with their like kind. The CellStyleKeyRequested handlers return these.
        private const string SparklineStyleKey = "AutoSales.Sparkline";
        private const string BulletStyleKey    = "AutoSales.Bullet";

        public DashboardView()
        {
            _vm = DashboardViewModel.ViewModel;
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DashboardViewModel.SelectedWorldRect))
                {
                    GeoMap.ZoomToGeographic(_vm.SelectedWorldRect);
                }
            };
            this.DataContext = _vm;
            this.InitializeComponent();
            
            this.GeoMap.BackgroundContent = new OpenStreetMapImagery();
            this.GeoMap.BackgroundContent = null;
            //dt.ExcludedColumns = new string[] { "X", "Y" };
            GeoMap.ZoomToGeographic(_vm.SelectedWorldRect);
            WireMapInteractions();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Tint the chart's built-in scrollbar (horizontal one is set Visible in XAML).
            try
            {
                OverallChart.HorizontalViewScrollbarFill = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0xAA, 0xDE));
                //OverallChart.HorizontalZoombarFill       = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0xAA, 0xDE));
            }
            catch
            {
                // Property names may differ slightly across builds; ignore if absent.
            }
        }

        // ===================================================================================================
        // Map interactions
        // ===================================================================================================
        private void WireMapInteractions()
        {
            try
            {
                GeoMap.SeriesPointerReleased += GeoMap_SeriesMouseLeftButtonUp;
            }
            catch
            {
                // Tolerate any minor event-name drift in the WinUI maps build.
            }
        }

        private void GeoMap_SeriesMouseLeftButtonUp(object sender, DataChartPointerEventArgs e)
        {
            switch (e.Item)
            {
                case RegionViewModel r: _vm.SelectedRegion = r; break;
                case USStateViewModel s: _vm.SelectedUSState = s; break;
                case Dealer d: _vm.SelectedDealer = d; break;
            }
        }

        // Clicking any breadcrumb navigates back to that level. The VM's SelectedBreadCrumb
        // setter cascades the unwind (clearing region/state/dealer selections as needed and
        // re-running the filter / report queries).
        private void OnBreadCrumbClicked(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var btn = sender as Button;
            var crumb = btn?.Tag as BreadCrumbViewModel;
            if (crumb != null) _vm.SelectedBreadCrumb = crumb;
        }

        // ===================================================================================================
        // Filter panel — ToggleButton segmented groups (manual mutual exclusion).
        // RadioButton was the natural fit but its WinUI default theme rendered the unchecked
        // Content text invisible against the white card. ToggleButton matches the WPF original's
        // segmented look (Year/Quarter/Month/Week, Revenue/Volume) and renders cleanly.
        // ===================================================================================================
        private void PeriodToggled(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var clicked = sender as ToggleButton;
            if (clicked == null) return;

            // Force the clicked one on, all others off (a click on the already-checked one
            // would otherwise toggle it off — we want one-of-N behavior).
            ToggleExclusively(clicked, PeriodYear, PeriodQuarter, PeriodMonth, PeriodWeek);

            if (Enum.TryParse(clicked.Tag?.ToString(), out ReportPeriod p))
                _vm.SelectedPeriod = p;
        }

        private void MeasureToggled(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var clicked = sender as ToggleButton;
            if (clicked == null) return;

            ToggleExclusively(clicked, MeasureRevenue, MeasureVolume);

            if (Enum.TryParse(clicked.Tag?.ToString(), out MeasureType m))
                _vm.SelectedMeasure = m;
        }

        private static void ToggleExclusively(ToggleButton clicked, params ToggleButton[] group)
        {
            clicked.IsChecked = true;
            foreach (var b in group)
                if (b != clicked) b.IsChecked = false;
        }

        // ===================================================================================================
        // Dealer-grid selection
        // ===================================================================================================
        private void OnDealerActiveItemChanged(object sender, object e)
        {
            try
            {
                var grid = sender as XamDataGrid;
                var active = grid?.GetType().GetProperty("ActiveItem")?.GetValue(grid);
                if (active is Dealer d) _vm.SelectedDealer = d;
            }
            catch { }
        }

        // ===================================================================================================
        // TemplateColumn: "Last week" sparkline-style cell
        //
        // Pattern (mirrors the JS sample): inspect e.Content, check whether we've already
        // installed our visual; if not, CREATE; otherwise UPDATE. This is what makes
        // recycling cheap — the chart instance survives across rows.
        //
        // CellStyleKeyRequested gives the cell a stable per-visual-shape pool key so the
        // grid recycles sparkline cells with other sparkline cells (and never with bullet cells).
        // ===================================================================================================
        private void OnSparklineCellStyleKeyRequested(object sender, CellStyleRequestedEventArgs e)
        {
            e.StyleKey = SparklineStyleKey;
        }

        private void OnSparklineCellUpdating(object sender, TemplateCellUpdatingEventArgs e)
        {
            var host = e.Content as ContentControl;
            if (host == null) return;

            // CREATE OR UPDATE
            XamDataChart spark = host.Content as XamDataChart;
            if (spark == null)
            {
                spark = new XamDataChart
                {
                    Margin = new Thickness(2),
                    IsHitTestVisible = false,
                };
                var x = new CategoryXAxis();
                x.LabelVisibility = Infragistics.Core.Visibility.Collapsed;
                var y = new NumericYAxis();
                y.LabelVisibility = Infragistics.Core.Visibility.Collapsed;
                y.MajorStroke = new SolidColorBrush(Colors.Transparent);
                spark.Axes.Add(x);
                spark.Axes.Add(y);
                var series = new AreaSeries
                {
                    XAxis = x,
                    YAxis = y,
                    MarkerType = MarkerType.Hidden,
                    ValueMemberPath = "Value",
                    Brush   = (SolidColorBrush)Application.Current.Resources["PrimaryBrushLightest"],
                    Outline = (SolidColorBrush)Application.Current.Resources["PrimaryBrush"],
                    Thickness = 1.0,
                };
                spark.Series.Add(series);
                host.Content = spark;
            }

            // Update from the bound row.
            var rowItem = e.CellInfo?.RowItem;
            var sales   = rowItem?.GetType().GetProperty("Sales")?.GetValue(rowItem) as System.Collections.IEnumerable;
            if (sales != null)
            {
                ((CategoryXAxis)spark.Axes[0]).ItemsSource = sales;
                ((AreaSeries)spark.Series[0]).ItemsSource = sales;
            }
        }

        // ===================================================================================================
        // TemplateColumn: "Percentage of Target" bullet cell, using the real XamBulletGraph.
        // ===================================================================================================
        private void OnBulletCellStyleKeyRequested(object sender, CellStyleRequestedEventArgs e)
        {
            e.StyleKey = BulletStyleKey;
        }

        private double ConvertInterval(double value)
        {
            if (value != null && !double.IsNaN((double)value))
            {
                double max = (double)value;
                return max / 5;
            }
            else
            {
                return 0;
            }
        }

        private void OnBulletCellUpdating(object sender, TemplateCellUpdatingEventArgs e)
        {
            var host = e.Content as ContentControl;
            if (host == null) return;

            XamBulletGraph bullet = host.Content as XamBulletGraph;
            if (bullet == null)
            {
                host.Padding = new Thickness(2);
                bullet = new XamBulletGraph
                {
                    
                    ValueBrush = (SolidColorBrush)Application.Current.Resources["PrimaryBrush"],
                    ScaleEndExtent = .90,
                    TargetValueBrush = (SolidColorBrush)Application.Current.Resources["ForegroundBrushSubtle"],
                    TargetValueOutline = (SolidColorBrush)Application.Current.Resources["ForegroundBrushPrimary"],
                    ValueInnerExtent = .35,
                    ValueOuterExtent = .75,
                    FontSize = 7
                };
                try
                {
                    bullet.FormatLabel += (s, args) =>
                    {
                        var v = args.Value;
                        if (Math.Abs(v) >= 1_000_000) args.Label = (v / 1_000_000d).ToString("0") + "M";
                        else if (Math.Abs(v) >= 1_000) args.Label = (v / 1_000d).ToString("0") + "K";
                        else args.Label = v.ToString("0");
                    };
                }
                catch
                {
                    // FormatLabel signature can vary across WinUI gauge builds; ignore if absent.
                }
                host.Content = bullet;
            }

            var rowItem = e.CellInfo?.RowItem;
            if (rowItem != null)
            {
                var t = rowItem.GetType();
                double value  = ToDouble(t.GetProperty("Value")?.GetValue(rowItem));
                double target = ToDouble(t.GetProperty("Target")?.GetValue(rowItem));
                double max    = ToDouble(t.GetProperty("Max")?.GetValue(rowItem));
                double interval = ConvertInterval(max);
                if (max <= 0) max = Math.Max(value, target);

                bullet.MaximumValue = max > 0 ? max : 1;
                bullet.Value        = value;
                bullet.TargetValue  = target;
                bullet.Interval     = interval;
            }
        }

        private static double ToDouble(object v)
        {
            try { return v == null ? 0.0 : Convert.ToDouble(v); }
            catch { return 0.0; }
        }

        private void RegionsSeries_ChartToolTipUpdating(object sender, ChartToolTipUpdatingEventArgs args)
        {
            if (args.CurrentView == null)
            {
                args.CurrentView = new ContentControl
                {
                    Content = args.CurrentData,
                    ContentTemplate = this.Resources["MapToolTip"] as DataTemplate
                };
            }
            else
            {
                var cc = args.CurrentView as ContentControl;
                if (cc != null)
                {
                    cc.Content = args.CurrentData;
                }
            }
        }

        private void StatesSeries_ChartToolTipUpdating(object sender, ChartToolTipUpdatingEventArgs args)
        {
            if (args.CurrentView == null)
            {
                args.CurrentView = new ContentControl
                {
                    Content = args.CurrentData,
                    ContentTemplate = this.Resources["MapToolTip"] as DataTemplate
                };
            }
            else
            {
                var cc = args.CurrentView as ContentControl;
                if (cc != null)
                {
                    cc.Content = args.CurrentData;
                }
            }
        }

        private void DealersSeries_ChartToolTipUpdating(object sender, ChartToolTipUpdatingEventArgs args)
        {
            if (args.CurrentView == null)
            {
                args.CurrentView = new ContentControl
                {
                    Content = args.CurrentData,
                    ContentTemplate = this.Resources["MapToolTip"] as DataTemplate
                };
            }
            else
            {
                var cc = args.CurrentView as ContentControl;
                if (cc != null)
                {
                    cc.Content = args.CurrentData;
                }
            }
        }
    }
}
