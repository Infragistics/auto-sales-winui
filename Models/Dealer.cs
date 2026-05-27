using System.ComponentModel;
using AutoSales.ViewModels;

namespace AutoSales.Models
{
    public class Dealer : INotifyPropertyChanged
    {
        public Dealer() { }

        public Dealer(string id, string name, string region, string state, string county,
            string city, string address, string postCode, double longitude, double latitude,
            double revenue, int volume)
        {
            Id = id;
            Name = name;
            Region = region;
            State = state;
            County = county;
            City = city;
            Address = address;
            PostCode = postCode;
            Longitude = longitude;
            Latitude = latitude;
            Revenue = revenue;
            Volume = volume;
        }

        internal DashboardViewModel Parent { get; set; }
        public bool RevenueMeasureVisibility => Parent != null && Parent.RevenueMeasureVisibility;
        public bool VolumeMeasureVisibility => Parent != null && Parent.VolumeMeasureVisibility;

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Region { get; private set; }
        public string State { get; private set; }
        public string County { get; private set; }
        public string City { get; private set; }
        public string Address { get; private set; }
        public string PostCode { get; private set; }
        public double Longitude { get; private set; }
        public double Latitude { get; private set; }
        public int Volume { get; set; }

        private double _volumeFraction;
        public double VolumeFraction
        {
            get => _volumeFraction;
            set
            {
                _volumeFraction = value;
                OnPropertyChanged(nameof(VolumePercent));
            }
        }
        public double VolumePercent => 100 * VolumeFraction;

        public double Revenue { get; set; }

        private double _revenueFraction;
        public double RevenueFraction
        {
            get => _revenueFraction;
            set
            {
                _revenueFraction = value;
                OnPropertyChanged(nameof(RevenuePercent));
            }
        }
        public double RevenuePercent => 100 * RevenueFraction;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public double ScaleMaxRevenue { get; set; }
        public double ScaleMaxVolume { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
