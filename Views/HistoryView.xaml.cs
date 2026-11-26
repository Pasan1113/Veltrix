using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Veltrix.Helpers;
using Veltrix.Models;
using Veltrix.Services;
using Microsoft.Data.Sqlite;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;

namespace Veltrix.Views
{
    public partial class HistoryView : UserControl
    {
        private readonly UsageTrackingService _tracking;
        private List<DailyUsage> _rows = new();
        private DateTime _start, _end;
        private int _page;
        private const int PageSize = 7;
        private string _sort = "date";
        private bool _descending = true;
        public HistoryView(UsageTrackingService tracking)
        {
            InitializeComponent();
            _tracking = tracking;
            SetPeriod("month");
        }
        private void SetPeriod(string period)
        {
            _end = DateTime.Today;
            _start = period == "week" ? _end.AddDays(-(((int)_end.DayOfWeek + 6) % 7)) : new DateTime(_end.Year, period == "year" ? 1 : _end.Month, 1);
            StartText.Text = _start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            EndText.Text = _end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _page = 0;
            Refresh();
        }
        public void Refresh()
        {
            try
            {
                _rows = _tracking.GetHistory(_start, _end);
                long total = _rows.Sum(r => r.TotalBytes);
                TotalText.Text = DataFormatter.FormatBytes(total);
                AverageText.Text = DataFormatter.FormatBytes((double)total / ((_end - _start).Days + 1));
                var highest = _rows.OrderByDescending(r => r.TotalBytes).FirstOrDefault();
                HighestText.Text = highest == null ? "—" : $"{DataFormatter.FormatBytes(highest.TotalBytes)} · {highest.Date:MMM dd}";
                Render();
            }
            catch (SqliteException) { StatusText.Text = "Could not load usage. Please refresh."; }
        }
        private IEnumerable<DailyUsage> Sorted() => _sort == "total" ? (_descending ? _rows.OrderByDescending(r => r.TotalBytes) : _rows.OrderBy(r => r.TotalBytes)) : (_descending ? _rows.OrderByDescending(r => r.Date) : _rows.OrderBy(r => r.Date));
        private void Render()
        {
            int pages = Math.Max(1, (_rows.Count + PageSize - 1) / PageSize);
            _page = Math.Clamp(_page, 0, pages - 1);
            HistoryGrid.ItemsSource = Sorted().Skip(_page * PageSize).Take(PageSize).Select(r => new HistoryRow(r)).ToList();
            PrevButton.IsEnabled = _page > 0;
            NextButton.IsEnabled = _page + 1 < pages;
            StatusText.Text = _rows.Count == 0 ? "No recorded usage" : $"Page {_page + 1}/{pages} · {_rows.Count} records";
        }
        private void Period_Click(object sender, RoutedEventArgs e) => SetPeriod((string)((Button)sender).Tag);
        private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!DateTime.TryParseExact(StartText.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) || !DateTime.TryParseExact(EndText.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || start > end || end > DateTime.Today)
            { StatusText.Text = "Enter valid dates up to today, start before end."; return; }
            _start = start; _end = end; _page = 0; Refresh();
        }
        private void Prev_Click(object sender, RoutedEventArgs e) { _page--; Render(); }
        private void Next_Click(object sender, RoutedEventArgs e) { _page++; Render(); }
        private void Sort_Click(object sender, RoutedEventArgs e) { var key = (string)((Button)sender).Tag; _descending = key != _sort || !_descending; _sort = key; _page = 0; Render(); }
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _rows = _tracking.GetHistory(_start, _end); Render();
                var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"Veltrix_{_start:yyyyMMdd}_{_end:yyyyMMdd}.csv", DefaultExt = ".csv", AddExtension = true };
                if (dialog.ShowDialog() != true) return;
                var lines = new List<string> { "Date,DownloadedBytes,UploadedBytes,TotalBytes" };
                lines.AddRange(Sorted().Select(r => FormattableString.Invariant($"{r.Date:yyyy-MM-dd},{r.DownloadedBytes},{r.UploadedBytes},{r.TotalBytes}")));
                File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                StatusText.Text = $"Exported {_rows.Count} records";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SqliteException) { StatusText.Text = "Export failed: " + ex.Message; }
        }
        public sealed class HistoryRow
        {
            public string DateLabel { get; }
            public string DownloadLabel { get; }
            public string UploadLabel { get; }
            public string TotalLabel { get; }
            public HistoryRow(DailyUsage r) { DateLabel = r.Date.ToString("yyyy-MM-dd"); DownloadLabel = DataFormatter.FormatBytes(r.DownloadedBytes); UploadLabel = DataFormatter.FormatBytes(r.UploadedBytes); TotalLabel = DataFormatter.FormatBytes(r.TotalBytes); }
        }
    }
}
