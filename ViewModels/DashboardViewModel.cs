using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using AutoSales.Helpers;
using AutoSales.Models;
using AutoSales.Services;
using Infragistics.Controls.Maps;
using Microsoft.UI.Dispatching;
using Windows.Foundation;

using Infragistics.Controls;

namespace AutoSales.ViewModels
{
    /// <summary>
    /// Singleton orchestrator for the Auto Sales dashboard. Mirrors the WPF DashboardViewModel
    /// API surface; differences from the WPF original:
    /// * Uses Windows.Foundation.Rect (no Empty/Union/IsEmpty) plus RectHelpers.
    /// * Filtered collections are exposed as plain ObservableCollection&lt;T&gt; refreshed when
    ///   selection changes (WinUI's ICollectionView doesn't have the WPF Filter delegate).
    /// * The shape-file parser is gated on the WinUI Maps assembly; if it isn't built yet,
    ///   region/state shapes are absent and the geographic adornments don't draw — but the
    ///   chart and dealer grid still work end-to-end.
    /// </summary>
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region Singleton
        private static volatile DashboardViewModel _vm;
        private static readonly object _syncRoot = new object();
        public static DashboardViewModel ViewModel
        {
            get
            {
                if (_vm == null)
                {
                    lock (_syncRoot)
                    {
                        if (_vm == null) _vm = new DashboardViewModel();
                    }
                }
                return _vm;
            }
        }
        #endregion

        #region Workers and state
        private readonly BackgroundWorker _workerDealers;
        private readonly BackgroundWorker _workerReportData;
        private readonly BackgroundWorker _workerTransactions;

        private bool _isBusy;
        private bool _isReportDataLoaded;
        private bool _isDealersLoaded;

        private IEnumerable<Transaction> _transactions = new List<Transaction>();
        private IEnumerable<PlotPoint> _sales;
        private IEnumerable<SalesPersonPerformance> _salesPersonPerformance;
        private IEnumerable<ProductPerformance> _carModelPerformance;

        private ReportPeriod _selectedPeriod;
        private MeasureType _selectedMeasure;
        private FilterType _filterByType;
        private string _filter;

        private readonly ObservableCollection<BreadCrumbViewModel> _breadCrumbs;
        private readonly Dictionary<string, ReportData> _cachedReportData = new Dictionary<string, ReportData>();
        #endregion

        private DashboardViewModel()
        {
            _selectedMeasure = MeasureType.Revenue;
            _selectedPeriod = ReportPeriod.Quarter;
            _filterByType = FilterType.All;
            _filter = null;

            _breadCrumbs = new ObservableCollection<BreadCrumbViewModel>
            {
                new BreadCrumbViewModel(null, "All Regions")
            };

            _workerDealers = new BackgroundWorker();
            _workerReportData = new BackgroundWorker();
            _workerTransactions = new BackgroundWorker();

            _workerDealers.DoWork += (o, args) => args.Result = AutoSalesService.GetDealers();
            _workerReportData.DoWork += (o, args) =>
                args.Result = AutoSalesService.GetReportData(_selectedMeasure, _selectedPeriod, _filterByType, _filter);
            _workerTransactions.DoWork += (o, args) => args.Result = AutoSalesService.GetTransactions(ReportPeriod.TwelveMonths);

            _workerDealers.RunWorkerCompleted += GetDealersCompleted;
            _workerReportData.RunWorkerCompleted += GetReportCompleted;
            _workerTransactions.RunWorkerCompleted += GetTransactionsCompleted;

            LoadDealers();
            LoadReportData();
            LoadShapeFiles();
        }

        #region Public properties (data)

        public IEnumerable<Transaction> Transactions
        {
            get
            {
                if (!_workerTransactions.IsBusy && !_transactions.Any())
                    LoadTransactions();
                return _transactions;
            }
            private set
            {
                if (_transactions != value)
                {
                    _transactions = value;
                    OnPropertyChanged(nameof(Transactions));
                }
            }
        }

        public IEnumerable<PlotPoint> Sales
        {
            get => _sales;
            private set
            {
                if (_sales != value)
                {
                    _sales = value;
                    OnPropertyChanged(nameof(Sales));
                }
            }
        }

        public IEnumerable<ProductPerformance> CarModelPerformance
        {
            get => _carModelPerformance;
            private set
            {
                if (_carModelPerformance != value)
                {
                    _carModelPerformance = value;
                    OnPropertyChanged(nameof(CarModelPerformance));
                }
            }
        }

        public IEnumerable<SalesPersonPerformance> SalesPersonPerformance
        {
            get => _salesPersonPerformance;
            private set
            {
                if (_salesPersonPerformance != value)
                {
                    _salesPersonPerformance = value;
                    OnPropertyChanged(nameof(SalesPersonPerformance));
                }
            }
        }

        public ObservableCollection<BreadCrumbViewModel> BreadCrumbs => _breadCrumbs;

        #endregion

        #region Filters and selections

        public ReportPeriod SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (_selectedPeriod != value)
                {
                    _selectedPeriod = value;
                    LoadReportData();
                }
                OnPropertyChanged(nameof(SelectedPeriod));
            }
        }

        public MeasureType SelectedMeasure
        {
            get => _selectedMeasure;
            set
            {
                if (_selectedMeasure != value)
                {
                    _selectedMeasure = value;
                    LoadReportData();
                }
                OnPropertyChanged(nameof(SelectedMeasure));
                OnPropertyChanged(nameof(RevenueMeasureVisibility));
                OnPropertyChanged(nameof(VolumeMeasureVisibility));
            }
        }

        public bool RevenueMeasureVisibility => SelectedMeasure == MeasureType.Revenue;
        public bool VolumeMeasureVisibility => SelectedMeasure == MeasureType.Volume;

        private Dealer _selectedDealer;
        public Dealer SelectedDealer
        {
            get => _selectedDealer;
            set
            {
                if (_selectedDealer != null) _selectedDealer.IsSelected = false;
                _selectedDealer = value;
                if (_selectedDealer != null) _selectedDealer.IsSelected = true;
                SyncMapBreadcrumbsAndReportsWithSelections();
                OnPropertyChanged(nameof(SelectedDealer));
            }
        }

        private BreadCrumbViewModel _selectedBreadCrumb;
        public BreadCrumbViewModel SelectedBreadCrumb
        {
            get => _selectedBreadCrumb;
            set
            {
                if (_selectedBreadCrumb == value) return;
                _selectedBreadCrumb = value;
                switch (BreadCrumbs.IndexOf(_selectedBreadCrumb))
                {
                    case 0:
                        SelectedRegion = null;
                        break;
                    case 1:
                        SelectedRegion = string.IsNullOrEmpty(_selectedBreadCrumb.Value)
                            ? null
                            : RegionVMs.FirstOrDefault(r => r.Name == _selectedBreadCrumb.Value);
                        break;
                    case 2:
                        SelectedUSState = string.IsNullOrEmpty(_selectedBreadCrumb.Value)
                            ? null
                            : USStateVMs.FirstOrDefault(s => s.NameAbbreviation == _selectedBreadCrumb.Value);
                        break;
                    case 3:
                        SelectedDealer = string.IsNullOrEmpty(_selectedBreadCrumb.Value)
                            ? null
                            : Dealers.FirstOrDefault(d => d.Name == _selectedBreadCrumb.Value);
                        break;
                }
            }
        }

        private Rect InitialWorldRect { get; set; }

        private Rect _selectedWorldRect;
        public Rect SelectedWorldRect
        {
            get => _selectedWorldRect;
            set
            {
                if (!_selectedWorldRect.Equals(value))
                {
                    _selectedWorldRect = value;
                    OnPropertyChanged(nameof(SelectedWorldRect));
                }
            }
        }

        private double _maxRevenueFraction;
        public double MaxRevenueFraction
        {
            get => _maxRevenueFraction;
            set { _maxRevenueFraction = value; OnPropertyChanged(nameof(MaxRevenueFraction)); }
        }

        private double _minRevenueFraction;
        public double MinRevenueFraction
        {
            get => _minRevenueFraction;
            set { _minRevenueFraction = value; OnPropertyChanged(nameof(MinRevenueFraction)); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); } }
        }

        #endregion

        #region Sync / breadcrumbs

        private void SyncMapBreadcrumbsAndReportsWithSelections()
        {
            RefreshFilteredViews();

            if (SelectedDealer != null)
            {
                _filterByType = FilterType.ByDealership;
                _filter = SelectedDealer.Id;
                SetBreadCrumbs(
                    new BreadCrumbViewModel(SelectedDealer.Region, SelectedDealer.Region),
                    new BreadCrumbViewModel(SelectedDealer.State, SelectedDealer.State),
                    new BreadCrumbViewModel(SelectedDealer.Id, SelectedDealer.Name));
            }
            else if (SelectedUSState != null)
            {
                _filterByType = FilterType.ByState;
                _filter = _selectedUSState.NameAbbreviation;
                SetBreadCrumbs(
                    new BreadCrumbViewModel(_selectedUSState.RegionName, _selectedUSState.RegionName),
                    new BreadCrumbViewModel(_selectedUSState.NameAbbreviation, _selectedUSState.NameAbbreviation));
            }
            else if (_selectedRegion == null)
            {
                _filterByType = FilterType.All;
                _filter = string.Empty;
                SetBreadCrumbs(null);
            }
            else
            {
                _filterByType = FilterType.ByRegion;
                _filter = _selectedRegion.Name;
                SetBreadCrumbs(new BreadCrumbViewModel(_selectedRegion.Name, _selectedRegion.Name));
            }
            LoadReportData();
        }

        private void SetBreadCrumbs(params BreadCrumbViewModel[] path)
        {
            for (int i = BreadCrumbs.Count - 1; i > 0; --i) BreadCrumbs.RemoveAt(i);
            if (path != null)
                foreach (var node in path) BreadCrumbs.Add(node);
        }

        #endregion

        #region Data loading

        private void LoadReportData()
        {
            var key = GetCacheReportDataKey();
            if (_cachedReportData.TryGetValue(key, out var cached))
            {
                ApplyReportData(cached);
                return;
            }
            if (!_workerReportData.IsBusy)
            {
                _isReportDataLoaded = false;
                if (_filterByType != FilterType.ByDealership) IsBusy = true;
                _workerReportData.RunWorkerAsync();
            }
        }

        private void LoadDealers()
        {
            _isDealersLoaded = false;
            _workerDealers.RunWorkerAsync();
        }

        private void LoadTransactions()
        {
            IsBusy = true;
            _workerTransactions.RunWorkerAsync();
        }

        private void GetDealersCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled) return;
            var result = (e.Result as IEnumerable<Dealer>)?.ToList();
            if (result == null || result.Count == 0) return;

            double totalRevenue = result.Sum(x => x.Revenue);
            double totalVolume = result.Sum(x => x.Volume);
            if (totalRevenue > 0)
            {
                foreach (var dealer in result)
                {
                    dealer.RevenueFraction = dealer.Revenue / totalRevenue;
                    dealer.VolumeFraction = totalVolume > 0 ? (double)dealer.Volume / totalVolume : 0;
                    dealer.Parent = this;
                }
            }

            Dealers = new ObservableCollection<Dealer>(result.OrderByDescending(r => r.RevenueFraction));
            if (Dealers.Count > 0)
            {
                MaxRevenueFraction = Dealers[0].RevenueFraction;
                MinRevenueFraction = Dealers[Dealers.Count - 1].RevenueFraction;
            }

            ComputeUSStateRevenuePercent();
            ComputeRegionRevenuePercent();
            _isDealersLoaded = true;
            if (_isDealersLoaded && _isReportDataLoaded) IsBusy = false;

            RefreshFilteredViews();
            ShowRegionsView();
            OnPropertyChanged(nameof(Dealers));
            OnPropertyChanged(nameof(DealersForMap));
            OnPropertyChanged(nameof(DealersForGrid));
            OnPropertyChanged(nameof(VisibleRegions));
        }

        private void GetTransactionsCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null || e.Cancelled) return;
            if (e.Result is IEnumerable<Transaction> result)
                Transactions = result as IList<Transaction> ?? result.ToList();
        }

        private void GetReportCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null) return;
            if (!(e.Result is ReportData reportData)) return;

            var key = GetCacheReportDataKey();
            if (!_cachedReportData.ContainsKey(key)) _cachedReportData[key] = reportData;
            ApplyReportData(reportData);
        }

        private void ApplyReportData(ReportData reportData)
        {
            Sales = reportData.Sales;
            CarModelPerformance = reportData.Products;
            SalesPersonPerformance = reportData.SalesPeople;

            _isReportDataLoaded = true;
            if (_isDealersLoaded && _isReportDataLoaded) IsBusy = false;
        }

        private string GetCacheReportDataKey() =>
            SelectedMeasure + SelectedPeriod.ToString() + _filterByType + _filter;

        #endregion

        #region Shape files (region / state polygons)

        private void LoadShapeFiles()
        {
            // ShapeDataSource's WinRT URI handler only loads from the app package when the URI
            // uses ms-appx://. file:// URIs fall through to WebClient.OpenReadAsync, which is
            // unreliable for local files in packaged WinUI apps and silently fails — so the
            // shapefile import never completes. Use ms-appx:/// to hit the StorageFile path.
            ShapeFileRegions = new ShapeDataSource
            {
                ShapefileSource = new Uri("ms-appx:///ShapeFiles/regions.shp", UriKind.Absolute),
                DatabaseSource  = new Uri("ms-appx:///ShapeFiles/regions.dbf", UriKind.Absolute),
            };
            ShapeFileRegions.ImportCompleted += OnShapefileImportCompleted;

            ShapeFileUSStates = new ShapeDataSource
            {
                ShapefileSource = new Uri("ms-appx:///ShapeFiles/usa_st.shp", UriKind.Absolute),
                DatabaseSource  = new Uri("ms-appx:///ShapeFiles/usa_st.dbf", UriKind.Absolute),
            };
            ShapeFileUSStates.ImportCompleted += OnShapefileImportCompleted;
        }

        private ShapeDataSource ShapeFileRegions;
        private ShapeDataSource ShapeFileUSStates;

        private void OnShapefileImportCompleted(object sender, AsyncCompletedEventArgs e) => ParseAllShapefiles();

        private void ParseAllShapefiles()
        {
            if (ShapeFileRegions != null && ShapeFileRegions.Count > 0 && RegionVMs.Count == 0) LoadRegionsData();
            else if (ShapeFileUSStates != null && ShapeFileUSStates.Count > 0 && USStateVMs.Count == 0) LoadUSStatesData();

            ComputeUSStateRevenuePercent();
            ComputeRegionRevenuePercent();
            RefreshFilteredViews();
        }

        internal void LoadRegionsData()
        {
            foreach (ShapefileRecord record in ShapeFileRegions)
            {
                var regionVM = new RegionViewModel
                {
                    Parent = this,
                    Id = record.Fields["ID"].ToString(),
                    Shape = record.Points,
                    WorldRect = RectHelpers.Empty,
                    Name = record.Fields["NAME"].ToString(),
                };
                RegionVMs.Add(regionVM);
            }
        }

        internal void LoadUSStatesData()
        {
            foreach (ShapefileRecord record in ShapeFileUSStates)
            {
                var usStateVM = new USStateViewModel
                {
                    Parent = this,
                    Id = record.Fields["STATE_FIPS"].ToString(),
                    Shape = record.Points,
                    Name = record.Fields["STATE_NAME"].ToString(),
                    NameAbbreviation = record.Fields["STATE_ABBR"].ToString(),
                    RegionName = record.Fields["REGION"].ToString(),
                    SubRegionName = record.Fields["SUB_REGION"].ToString(),
                };
                USStateVMs.Add(usStateVM);
            }
        }

        #endregion

        #region Region / state / dealer collections + filtered views

        private RegionViewModel _selectedRegion;
        public RegionViewModel SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (value != null && !value.HasDealers) return;
                _selectedRegion = value;
                SelectedUSState = null;
                if (_selectedRegion == null)
                {
                    RegionSeriesVisibility = true;
                    USStatesSeriesVisibility = false;
                }
                else
                {
                    RegionSeriesVisibility = false;
                    USStatesSeriesVisibility = true;
                    ComputeWorldRectOfRegion();
                    SelectedWorldRect = _selectedRegion.WorldRect;
                }
                RefreshFilteredViews();
                OnPropertyChanged(nameof(SelectedRegion));
            }
        }

        private USStateViewModel _selectedUSState;
        public USStateViewModel SelectedUSState
        {
            get => _selectedUSState;
            set
            {
                if (value != null && !value.HasDealers) return;
                _selectedUSState = value;
                SelectedDealer = null;
                if (_selectedUSState == null)
                {
                    RegionSeriesVisibility = true;
                    USStatesSeriesVisibility = false;
                }
                else
                {
                    RegionSeriesVisibility = false;
                    USStatesSeriesVisibility = true;
                    SelectedWorldRect = _selectedUSState.WorldRect;
                }
                RefreshFilteredViews();
                OnPropertyChanged(nameof(SelectedUSState));
            }
        }

        private bool _regionSeriesVisibility;
        public bool RegionSeriesVisibility
        {
            get => _regionSeriesVisibility;
            set
            {
                if (_regionSeriesVisibility != value)
                {
                    _regionSeriesVisibility = value;
                    if (_regionSeriesVisibility) SelectedWorldRect = InitialWorldRect;
                    OnPropertyChanged(nameof(RegionSeriesVisibility));
                }
            }
        }

        private bool _usStatesSeriesVisibility;
        public bool USStatesSeriesVisibility
        {
            get => _usStatesSeriesVisibility;
            set
            {
                if (_usStatesSeriesVisibility != value)
                {
                    _usStatesSeriesVisibility = value;
                    OnPropertyChanged(nameof(USStatesSeriesVisibility));
                }
            }
        }

        public ObservableCollection<RegionViewModel> RegionVMs { get; } = new ObservableCollection<RegionViewModel>();
        public ObservableCollection<USStateViewModel> USStateVMs { get; } = new ObservableCollection<USStateViewModel>();

        private ObservableCollection<Dealer> _dealers = new ObservableCollection<Dealer>();
        public ObservableCollection<Dealer> Dealers
        {
            get => _dealers;
            private set { _dealers = value; OnPropertyChanged(nameof(Dealers)); }
        }

        // Filtered projections — refreshed on selection change. Bind to these from the view.
        public ObservableCollection<RegionViewModel> VisibleRegions { get; } = new ObservableCollection<RegionViewModel>();
        public ObservableCollection<USStateViewModel> VisibleUSStates { get; } = new ObservableCollection<USStateViewModel>();
        public ObservableCollection<Dealer> DealersForMap { get; } = new ObservableCollection<Dealer>();
        public ObservableCollection<Dealer> DealersForGrid { get; } = new ObservableCollection<Dealer>();

        private void RefreshFilteredViews()
        {
            VisibleRegions.Clear();
            foreach (var r in RegionVMs.Where(RegionFilter)) VisibleRegions.Add(r);

            VisibleUSStates.Clear();
            foreach (var s in USStateVMs.Where(USStateFilter)) VisibleUSStates.Add(s);

            DealersForMap.Clear();
            foreach (var d in Dealers.Where(DealerFilterForMap)) DealersForMap.Add(d);

            DealersForGrid.Clear();
            foreach (var d in Dealers.Where(DealerFilterForDealerGrid)) DealersForGrid.Add(d);
        }

        private bool RegionFilter(RegionViewModel region) =>
            SelectedRegion == null || region.Name == SelectedRegion.Name;

        private bool USStateFilter(USStateViewModel state) =>
            (SelectedRegion != null && SelectedUSState == null && state.RegionName == SelectedRegion.Name) ||
            (SelectedUSState != null && SelectedUSState == state);

        private bool DealerFilterForMap(Dealer dealer)
        {
            try
            {
                return (SelectedRegion == null && SelectedUSState == null && dealer.RevenueFraction > 0.4 * MaxRevenueFraction)
                    || (SelectedUSState == null && SelectedRegion != null && dealer.Region == SelectedRegion.Name)
                    || (SelectedUSState != null && dealer.State == SelectedUSState.NameAbbreviation);
            }
            catch { return false; }
        }

        private bool DealerFilterForDealerGrid(Dealer dealer) =>
            (SelectedRegion == null && SelectedUSState == null)
            || (SelectedUSState == null && dealer.Region == SelectedRegion?.Name)
            || (SelectedUSState != null && dealer.State == SelectedUSState.NameAbbreviation);

        private void ComputeWorldRectOfRegion()
        {
            if (SelectedRegion == null) return;
            if (!SelectedRegion.WorldRect.IsEmpty()) return;

            var regionRect = Windows.Foundation.Rect.Empty;
            foreach (var s in VisibleUSStates) regionRect.Union(s.WorldRect);
            SelectedRegion.WorldRect = regionRect;
        }

        private void ComputeUSStateRevenuePercent()
        {
            foreach (var st in USStateVMs)
            {
                var matching = Dealers.Where(d => d.State == st.NameAbbreviation).ToList();
                st.Revenue = matching.Sum(x => x.Revenue);
                st.RevenueFraction = matching.Sum(x => x.RevenueFraction);
                st.HasDealers = matching.Count > 0;
                st.Volume = matching.Sum(x => x.Volume);
                st.VolumeFraction = matching.Sum(x => x.VolumeFraction);
            }
        }

        private void ComputeRegionRevenuePercent()
        {
            foreach (var r in RegionVMs)
            {
                var matching = Dealers.Where(d => d.Region == r.Name).ToList();
                r.Revenue = matching.Sum(x => x.Revenue);
                r.RevenueFraction = matching.Sum(x => x.RevenueFraction);
                r.HasDealers = matching.Count > 0;
                r.Volume = matching.Sum(x => x.Volume);
                r.VolumeFraction = matching.Sum(x => x.VolumeFraction);
            }
        }

        private void ShowRegionsView()
        {
            RegionSeriesVisibility = true;
            USStatesSeriesVisibility = false;
            InitialWorldRect = ComputeInitialWorldRect();
            SelectedWorldRect = InitialWorldRect;
        }

        private Rect ComputeInitialWorldRect()
        {
            // Hard-coded to a USA-ish world rect so that even without the maps assembly we
            // still set a sensible viewport. When the WinUI maps assembly is in place this
            // can come from ShapeFileRegions.WorldRect.
            const double zoomFactor = 0.3;
            // Approx mainland-USA bounding rect in EPSG:4326-style longitude/latitude coords.
            var worldRect = new Rect(-130, 23, 65, 27);
            var wx = zoomFactor * worldRect.Width;
            var wy = (zoomFactor + 0.1) * worldRect.Height;
            return new Rect(worldRect.X + wx / 2, worldRect.Y + wy / 2, worldRect.Width - wx, worldRect.Height - wy);
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
