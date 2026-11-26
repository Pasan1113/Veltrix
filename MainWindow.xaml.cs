using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Veltrix.Helpers;
using Veltrix.Services;
using Veltrix.Views;
using Veltrix.Models;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Veltrix.Data;
using Microsoft.Data.Sqlite;

namespace Veltrix
{
    public partial class MainWindow : Window
    {

        private readonly PerformanceView _performance = new();
        private void Performance_Click(object sender, RoutedEventArgs e) {
            ResetHistoryNav(); OverviewPanel.Visibility = Visibility.Collapsed; UsageHost.Content = _performance; UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty); OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty); UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            PerformanceNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59)); PerformanceNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            _performance.Refresh();
        }
        private MiniSpeedMeter? _miniSpeedMeter;
        private readonly PersonalizationService _preferences = PersonalizationService.Load();
        private SettingsView? _settingsView;
        private readonly ApplicationNetworkMonitorService _appMonitor = new();
        private ApplicationUsageView? _appView;
        private void Applications_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            _appView ??= new ApplicationUsageView(_appMonitor);
            UsageHost.Content = _appView;
            OverviewPanel.Visibility = Visibility.Collapsed;
            UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            ApplicationsNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            ApplicationsNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            _appView.Refresh();
        }
        private void SavePreferences() { try { _preferences.Save(); } catch(Exception ex) { System.Diagnostics.Debug.WriteLine(ex); } }
        private void ApplyPreferences() {
            DataFormatter.SpeedUnits = _preferences.Units;
            _monitorTimer.Interval = TimeSpan.FromSeconds(_preferences.Interval);
            if (_miniSpeedMeter != null) { _miniSpeedMeter.Topmost = _preferences.AlwaysOnTop; _miniSpeedMeter.Opacity = _preferences.Opacity; }
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            OverviewPanel.Visibility = Visibility.Collapsed;
            UsageHost.Content = new AboutView();
            UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            AboutNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            AboutNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
        }
        private void Settings_Click(object sender, RoutedEventArgs e) {
            ResetHistoryNav();
            _settingsView ??= new SettingsView(_preferences, ApplyPreferences);
            _settingsView.Refresh(); UsageHost.Content = _settingsView;
            OverviewPanel.Visibility = Visibility.Collapsed; UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty); OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty); UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            SettingsNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59)); SettingsNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
        }
        private DataUsageView? _usageView;
        private HistoryView? _historyView;
        private NetworkView? _networkView;
        private DataLimitView? _limitView;
        private readonly DataLimitService _dataLimit;
        private DateTime _nextLimitCheck;
        private System.Windows.Forms.NotifyIcon? _limitNotifier;

        private NetworkStats? _latestStats;

        private TrayIconService? _trayIconService;

        private bool _isExiting = false;

        private bool _backgroundNotificationShown = false;

        private readonly NetworkMonitorService
            _networkMonitor = new();

        private readonly DispatcherTimer
            _monitorTimer = new();

        private readonly Queue<double>
            _downloadHistory = new();

        private readonly Queue<double>
            _uploadHistory = new();

        private const int MaxGraphPoints = 60;

        private readonly DatabaseService _databaseService;

        private readonly UsageTrackingService _usageTracking;

        private readonly DispatcherTimer _databaseTimer = new();

        public MainWindow()
        {
            InitializeComponent();

            _databaseService = new DatabaseService();

            _usageTracking =
                new UsageTrackingService(_databaseService);

            _dataLimit = new DataLimitService(_usageTracking);
            InitializeGraph();

            GraphCanvas.SizeChanged +=
                (_, _) => UpdateGraph();

            InitializeSystemTray();

            UpdateUsageCards();

            StartDatabaseTimer();

            StartMonitoring();
            _appMonitor.Start();
            ApplyPreferences();
            Loaded += (_, _) => {
                if (_preferences.OpenMini && _miniSpeedMeter == null) ToggleMiniMeter();
                if (_preferences.StartMinimized) Dispatcher.BeginInvoke(new Action(Hide));
            };
        }

        private void Overview_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            OverviewPanel.Visibility = Visibility.Visible;
            UsageHost.Visibility = Visibility.Collapsed;
            OverviewNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            OverviewNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UpdateGraph();
        }

        private void DataUsage_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            _usageView ??= new DataUsageView(_usageTracking);
            UsageHost.Content = _usageView;
            OverviewPanel.Visibility = Visibility.Collapsed;
            UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            UsageNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            _usageView.Refresh();
        }

        private void ResetHistoryNav()
        {
            AboutNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty); AboutNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            PerformanceNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty); PerformanceNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            ApplicationsNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            ApplicationsNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            SettingsNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            SettingsNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            LimitNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            LimitNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            NetworkNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            NetworkNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            HistoryNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            HistoryNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            _historyView ??= new HistoryView(_usageTracking);
            UsageHost.Content = _historyView;
            OverviewPanel.Visibility = Visibility.Collapsed;
            UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            HistoryNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            HistoryNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            _historyView.Refresh();
        }

        private void Network_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            _networkView ??= new NetworkView(_networkMonitor, () => MonitorTimer_Tick(this, EventArgs.Empty));
            UsageHost.Content = _networkView;
            OverviewPanel.Visibility = Visibility.Collapsed;
            UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            NetworkNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            NetworkNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            _networkView.Refresh();
        }

        private void DataLimit_Click(object sender, RoutedEventArgs e)
        {
            ResetHistoryNav();
            _limitView ??= new DataLimitView(_dataLimit);
            UsageHost.Content = _limitView;
            OverviewPanel.Visibility = Visibility.Collapsed;
            UsageHost.Visibility = Visibility.Visible;
            OverviewNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            OverviewNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            UsageNav.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            LimitNav.Background = new SolidColorBrush(Color.FromRgb(30,41,59));
            LimitNav.Foreground = new SolidColorBrush(Color.FromRgb(34,211,238));
            _limitView.Refresh();
        }
        private void ShowLimitNotification(string message)
        {
            _limitNotifier ??= new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information, Text = "Veltrix data alerts" };
            _limitNotifier.Visible = true;
            _limitNotifier.ShowBalloonTip(5000, "Veltrix Data Limit", message, System.Windows.Forms.ToolTipIcon.Warning);
        }

        private void InitializeGraph()
        {
            for (int i = 0; i < MaxGraphPoints; i++)
            {
                _downloadHistory.Enqueue(0);
                _uploadHistory.Enqueue(0);
            }
        }

        private void InitializeSystemTray()
        {
            _trayIconService = new TrayIconService();

            _trayIconService.OpenDashboardRequested += () =>
            {
                Dispatcher.Invoke(ShowDashboard);
            };

            _trayIconService.ToggleMiniMeterRequested += () =>
            {
                Dispatcher.Invoke(ToggleMiniMeter);
            };

            _trayIconService.ExitRequested += () =>
            {
                Dispatcher.Invoke(ExitApplication);
            };
        }

        private void ShowDashboard()
        {
            Show();

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
        }

        private bool SaveUsageToDatabase()
        {
            try
            {
                _usageTracking.Flush();

                DatabaseStatusText.Text =
                    "● Saved locally";

                DatabaseStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(52, 211, 153));

                return true;
            }
            catch (SqliteException ex)
            {
                DatabaseStatusText.Text =
                    "● Save failed - retry pending";

                DatabaseStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(248, 113, 113));

                StabilityService.Log("Database save", ex);

                return false;
            }
        }

        private void UpdateUsageCards()
        {
            try
            {
                TodayUsageText.Text =
                    DataFormatter.FormatBytes(
                        _usageTracking.GetTodayUsage().TotalBytes);

                WeeklyUsageText.Text =
                    DataFormatter.FormatBytes(
                        _usageTracking.GetWeeklyUsage().TotalBytes);

                MonthlyUsageText.Text =
                    DataFormatter.FormatBytes(
                        _usageTracking.GetMonthlyUsage().TotalBytes);

                YearlyUsageText.Text =
                    DataFormatter.FormatBytes(
                        _usageTracking.GetYearlyUsage().TotalBytes);
            }
            catch (SqliteException ex)
            {
                StabilityService.Log("Database save", ex);
            }
        }

        private void StartMonitoring()
        {
            _monitorTimer.Interval =
                TimeSpan.FromSeconds(1);

            _monitorTimer.Tick += MonitorTimer_Tick;

            _monitorTimer.Start();
        }

        private void StartDatabaseTimer()
        {
            _databaseTimer.Interval = TimeSpan.FromSeconds(5);

            _databaseTimer.Tick += DatabaseTimer_Tick;

            _databaseTimer.Start();
        }

        private void DatabaseTimer_Tick(object? sender, EventArgs e)
        {
            _appMonitor.Flush();
            SaveUsageToDatabase();
        }

        private void MonitorTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _appMonitor.Tick();
            if (_appView != null && ReferenceEquals(UsageHost.Content, _appView) && UsageHost.Visibility == Visibility.Visible) _appView.Refresh();
            var stats = _networkMonitor.GetStats();

            _usageTracking.TrackUsage(stats);
            if (DateTime.UtcNow >= _nextLimitCheck)
            {
                _nextLimitCheck = DateTime.UtcNow.AddSeconds(5);
                _dataLimit.Check(ShowLimitNotification);
                if (ReferenceEquals(UsageHost.Content, _limitView) && UsageHost.Visibility == Visibility.Visible) _limitView?.Refresh();
            }

            UpdateUsageCards();
            if (IsVisible && UsageHost.Visibility == Visibility.Visible && ReferenceEquals(UsageHost.Content, _usageView)) _usageView?.RefreshIfDue();
            if (_networkView != null && ReferenceEquals(UsageHost.Content, _networkView) && UsageHost.Visibility == Visibility.Visible) _networkView.Refresh();

            _latestStats = stats;
            _performance.Observe(stats);
            _performance.Refresh();

            _miniSpeedMeter?.UpdateStats(stats);

            _trayIconService?.UpdateSpeed(
    DataFormatter.FormatSpeed(stats.DownloadSpeed),
    DataFormatter.FormatSpeed(stats.UploadSpeed)
);

            DownloadSpeedText.Text =
                DataFormatter.FormatSpeed(
                    stats.DownloadSpeed);

            UploadSpeedText.Text =
                DataFormatter.FormatSpeed(
                    stats.UploadSpeed);

            TotalDownloadText.Text =
                DataFormatter.FormatBytes(
                    stats.TotalDownloaded);

            TotalUploadText.Text =
                DataFormatter.FormatBytes(
                    stats.TotalUploaded);

            TotalUsageText.Text =
                DataFormatter.FormatBytes(
                    stats.TotalUsage);

            ConnectionStatus.Text =
                stats.IsConnected
                    ? "● Network active"
                    : "● Disconnected";

            ConnectionStatus.Foreground =
                stats.IsConnected
                    ? new SolidColorBrush(
                        Color.FromRgb(52, 211, 153))
                    : new SolidColorBrush(
                        Color.FromRgb(248, 113, 113));

            AddGraphPoint(
                _downloadHistory,
                stats.DownloadSpeed);

            AddGraphPoint(
                _uploadHistory,
                stats.UploadSpeed);

            if (IsVisible && OverviewPanel.Visibility == Visibility.Visible) UpdateGraph();
        }

        private void MiniMeter_Click(
    object sender,
    RoutedEventArgs e)
        {
            ToggleMiniMeter();
        }

        protected override void OnClosing(
    System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!_isExiting)
            {
                e.Cancel = true;

                Hide();

                if (!_backgroundNotificationShown)
                {
                    _trayIconService?.ShowBackgroundNotification();

                    _backgroundNotificationShown = true;
                }
            }
        }
        private void ExitApplication()
{
    if (_isExiting)
        return;

    _monitorTimer.Stop();

    _databaseTimer.Stop();

    var finalStats = _networkMonitor.GetStats();

    _usageTracking.TrackUsage(finalStats);

    if (!SaveUsageToDatabase())
    {
        var result = System.Windows.MessageBox.Show(
            "Some usage data could not be saved. " +
            "Do you want to exit anyway?",
            "Veltrix",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            _monitorTimer.Start();

            _databaseTimer.Start();

            return;
        }
    }

    _isExiting = true;
    _appMonitor.Dispose();

    _miniSpeedMeter?.Close();

    _limitNotifier?.Dispose();
            _limitNotifier = null;
            _trayIconService?.Dispose();

    _trayIconService = null;

    System.Windows.Application.Current.Shutdown();
}

        private void ToggleMiniMeter()
        {
            if (_miniSpeedMeter == null)
            {
                _miniSpeedMeter = new MiniSpeedMeter();
                var meter = _miniSpeedMeter;
                ApplyPreferences();
                if (_preferences.RememberPosition && _preferences.Left is double x && _preferences.Top is double y && x >= SystemParameters.VirtualScreenLeft && y >= SystemParameters.VirtualScreenTop && x+meter.Width <= SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth && y+meter.Height <= SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight) {
                    meter.WindowStartupLocation = WindowStartupLocation.Manual; meter.Left=x; meter.Top=y;
                }
                meter.LocationChanged += (_, _) => { if (_preferences.RememberPosition && meter.IsVisible) { _preferences.Left=meter.Left; _preferences.Top=meter.Top; } };
                meter.Closing += (_, _) => {
                    if (_preferences.RememberPosition) { _preferences.Left=meter.Left; _preferences.Top=meter.Top; }
                    _preferences.AlwaysOnTop=meter.Topmost; _preferences.Opacity=meter.Opacity;
                    if (!_isExiting) _preferences.OpenMini=false;
                    SavePreferences();
                };

                _miniSpeedMeter.Closed += (_, _) =>
                {
                    _miniSpeedMeter = null;

                    MiniMeterButton.Content =
                        "▣    Enable Mini Meter";

                    _trayIconService?.SetMiniMeterEnabled(false);
                };

                if (_latestStats != null)
                {
                    _miniSpeedMeter.UpdateStats(_latestStats);
                }

                _miniSpeedMeter.Show();
                _preferences.OpenMini=true; SavePreferences();

                MiniMeterButton.Content =
                    "▣    Disable Mini Meter";

                _trayIconService?.SetMiniMeterEnabled(true);
            }
            else
            {
                _miniSpeedMeter.Close();
            }
        }

        private void AddGraphPoint(
            Queue<double> history,
            double value)
        {
            history.Enqueue(value);

            while (history.Count > MaxGraphPoints)
            {
                history.Dequeue();
            }
        }

        private void UpdateGraph()
        {
            double width = GraphCanvas.ActualWidth;
            double height = GraphCanvas.ActualHeight;

            if (width <= 0 || height <= 0)
                return;

            double maxValue = Math.Max(
                _downloadHistory.Max(),
                _uploadHistory.Max());

            maxValue = Math.Max(
                maxValue * 1.15, 1000);

            DownloadLine.Points = CreateGraphPoints(
                _downloadHistory,
                width,
                height,
                maxValue);

            UploadLine.Points = CreateGraphPoints(
                _uploadHistory,
                width,
                height,
                maxValue);
        }

        private PointCollection CreateGraphPoints(
            Queue<double> history,
            double width,
            double height,
            double maxValue)
        {
            var points = new PointCollection();

            var values = history.ToArray();

            double step =
                width / (MaxGraphPoints - 1);

            for (int i = 0; i < values.Length; i++)
            {
                double x = i * step;

                double y = height - 2 -
                    (values[i] / maxValue) * (height - 4);

                points.Add(new Point(x, y));
            }

            return points;
        }

        // CUSTOM TITLE BAR

        private void TitleBar_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            if (WindowState == WindowState.Normal)
            {
                DragMove();
            }
        }

        private void Minimize_Click(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(
            object sender,
            RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            WindowState =
                WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
        }

        private void Close_Click(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _appMonitor.Dispose();
            _monitorTimer.Stop();

            _monitorTimer.Tick -= MonitorTimer_Tick;

            _databaseTimer.Stop();

            _databaseTimer.Tick -= DatabaseTimer_Tick;

            _miniSpeedMeter?.Close();

            _limitNotifier?.Dispose();
            _limitNotifier = null;
            _trayIconService?.Dispose();

            base.OnClosed(e);
        }
    }
}