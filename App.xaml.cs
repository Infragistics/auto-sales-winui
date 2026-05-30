using Infragistics.Controls.Grids;
using Microsoft.UI.Xaml;

namespace AutoSales
{
    public partial class App : Application
    {
        private Window _window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Register Infragistics WinUI platform services (matches NewWinUISample pattern).
            Infragistics.Core.WinUIPlatformRegistration.Register();
            Infragistics.SkiaSharpRenderer.Use();
            XamDataGrid.IsCanvasModeDisabled = true;

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
