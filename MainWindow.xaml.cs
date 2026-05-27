using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

namespace AutoSales
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Extend our content all the way to the top so the blue header IS the title bar.
            // The system still draws min/max/close on top of our content; LeftInset/RightInset
            // tell us how much space to reserve so our buttons don't sit under the system ones.
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            var appWindow = this.AppWindow;
            if (appWindow != null)
            {
                appWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
                // AppWindow.Changed fires for size, position, presentation, and the
                // LeftInset/RightInset reserved by the system caption-button area.
                appWindow.Changed += (sender, args) => UpdateTitleBarInsets();
                UpdateTitleBarInsets();
            }
        }

        private void UpdateTitleBarInsets()
        {
            var tb = this.AppWindow?.TitleBar;
            if (tb == null) return;
            /*LeftPaddingColumn.Width  = new GridLength(tb.LeftInset);
            RightPaddingColumn.Width = new GridLength(tb.RightInset);*/
        }

        private void OnMapNavClicked(object sender, RoutedEventArgs e)
        {
            // Map view is the default; placeholder for navigation.
        }

        private void OnInfoClicked(object sender, RoutedEventArgs e)
        {
            // About dialog placeholder.
        }
    }
}
