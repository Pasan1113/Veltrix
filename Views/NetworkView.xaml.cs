using System;
using System.Linq;
using System.Windows;
using Veltrix.Services;
using Veltrix.Helpers;
using Veltrix.Models;

namespace Veltrix.Views
{
    public partial class NetworkView :
        System.Windows.Controls.UserControl
    {
        private readonly NetworkMonitorService _monitor;
        private readonly Action _sample;

        private string? _highlight;
        private bool _refreshing;

        public NetworkView(
            NetworkMonitorService monitor,
            Action sample)
        {
            _monitor = monitor;
            _sample = sample;

            InitializeComponent();
            Refresh();
        }

        public sealed class Row
        {
            public NetworkAdapterInfo Info { get; set; } = new();

            public string Id => Info.Id;
            public string Name => Info.Name;
            public string Description => Info.Description;
            public string Status => Info.Status;
            public string IPv4 => Info.IPv4;
            public string IPv6 => Info.IPv6;
            public string Mac => Info.Mac;
            public string LinkSpeed => Info.LinkSpeed;

            public string Live =>
                "↓ " + DataFormatter.FormatSpeed(Info.DownloadSpeed) +
                "  ↑ " + DataFormatter.FormatSpeed(Info.UploadSpeed);
        }

        public void Refresh()
        {
            _refreshing = true;

            try
            {
                var rows = _monitor.Adapters
                    .Select(a => new Row { Info = a })
                    .ToArray();

                AdapterList.ItemsSource = rows;

                AdapterList.SelectedItem = rows
                    .FirstOrDefault(a => a.Id == _highlight);

                string mode = _monitor.SelectedAdapterId;

                if (mode == "auto")
                {
                    string adapterName = rows
                        .FirstOrDefault(a =>
                            a.Id == _monitor.ActiveAutoAdapterId)
                        ?.Name ?? "Waiting for connection";

                    ModeText.Text =
                        "Monitoring: Auto Detect — " + adapterName;
                }
                else if (mode == "all")
                {
                    ModeText.Text =
                        "Monitoring: All eligible physical adapters";
                }
                else
                {
                    ModeText.Text =
                        "Monitoring: " +
                        (rows.FirstOrDefault(a => a.Id == mode)
                            ?.Name ?? "Saved adapter unavailable");
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void Adapter_Changed(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_refreshing)
            {
                _highlight =
                    (AdapterList.SelectedItem as Row)?.Id;
            }
        }

        private void Auto_Click(
            object sender,
            RoutedEventArgs e)
        {
            Select("auto");
        }

        private void All_Click(
            object sender,
            RoutedEventArgs e)
        {
            Select("all");
        }

        private void Selected_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_highlight != null)
                Select(_highlight);
            else
                StatusText.Text = "Select an adapter first.";
        }

        private void Select(string id)
        {
            try
            {
                _sample();

                _monitor.SelectAdapter(id);

                StatusText.Text =
                    "Monitoring preference saved.";

                Refresh();
            }
            catch (System.IO.IOException)
            {
                StatusText.Text =
                    "Could not save preference.";
            }
            catch (UnauthorizedAccessException)
            {
                StatusText.Text =
                    "Could not save preference.";
            }
        }
    }
}