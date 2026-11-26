using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Veltrix.Helpers;
using Veltrix.Models;
using MenuItem = System.Windows.Controls.MenuItem;
using MouseButtonEventArgs =
    System.Windows.Input.MouseButtonEventArgs;

namespace Veltrix.Views
{
    public partial class MiniSpeedMeter : Window
    {
        public MiniSpeedMeter()
        {
            InitializeComponent();

            SetInitialPosition();
        }

        private void SetInitialPosition()
        {
            var workArea = SystemParameters.WorkArea;

            Left = workArea.Right - Width - 15;

            Top = workArea.Bottom - Height - 15;
        }

        public void UpdateStats(NetworkStats stats)
        {
            MiniDownloadText.Text =
                DataFormatter.FormatSpeed(
                    stats.DownloadSpeed);

            MiniUploadText.Text =
                DataFormatter.FormatSpeed(
                    stats.UploadSpeed);
        }

        private void Meter_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void AlwaysOnTop_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                Topmost = menuItem.IsChecked;
            }
        }

        private void Opacity_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                double.TryParse(
                    menuItem.Tag?.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double opacity))
            {
                Opacity = opacity;
            }
        }

        private void CloseMeter_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }
    }
}