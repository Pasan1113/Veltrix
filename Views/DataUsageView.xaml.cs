using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Veltrix.Helpers;
using Veltrix.Models;
using Veltrix.Services;
using Microsoft.Data.Sqlite;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Veltrix.Views
{
    public partial class DataUsageView : System.Windows.Controls.UserControl
    {
        private readonly UsageTrackingService _tracking;
        private string _period = "week";
        private DateTime _start, _end, _lastRefresh;
        private List<DailyUsage> _bars = new();
        private static readonly Brush Down = new SolidColorBrush(Color.FromRgb(52,211,153));
        private static readonly Brush Up = new SolidColorBrush(Color.FromRgb(129,140,248));
        public DataUsageView(UsageTrackingService tracking)
        {
            _tracking = tracking;
            InitializeComponent();
            StartPicker.SelectedDate = DateTime.Today.AddDays(-6);
            EndPicker.SelectedDate = DateTime.Today;
        }
        private void Period_Click(object sender, RoutedEventArgs e)
        {
            _period = (string)((System.Windows.Controls.Button)sender).Tag;
            CustomPanel.Visibility = _period == "custom" ? Visibility.Visible : Visibility.Collapsed;
            if (_period == "custom") Apply_Click(sender,e); else Refresh();
        }
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (StartPicker.SelectedDate is not DateTime start || EndPicker.SelectedDate is not DateTime end || start > end || end > DateTime.Today || (end-start).TotalDays > 3659)
            { StatusText.Text = "Choose a valid range (up to 10 years, ending today or earlier)."; return; }
            _start = start.Date; _end = end.Date; Refresh();
        }
        public void RefreshIfDue()
        {
            if (IsVisible && (DateTime.UtcNow - _lastRefresh).TotalSeconds >= 5) Refresh();
        }
        public void Refresh()
        {
            _lastRefresh = DateTime.UtcNow;
            var today = DateTime.Today;
            if (_period != "custom")
            {
                _end = today;
                _start = _period == "year" ? new DateTime(today.Year,1,1) : _period == "month" ? new DateTime(today.Year,today.Month,1) : today.AddDays(-(((int)today.DayOfWeek+6)%7));
            }
            if (_start == default || _end < _start) return;
            try
            {
                var history = _tracking.GetHistory(_start,_end);
                long down = history.Sum(r=>r.DownloadedBytes), up = history.Sum(r=>r.UploadedBytes);
                double total = (double)down + up;
                TotalText.Text = DataFormatter.FormatBytes(total);
                DownText.Text = DataFormatter.FormatBytes(down); UpText.Text = DataFormatter.FormatBytes(up);
                double ratio = total == 0 ? 0 : down/total;
                DownColumn.Width = new GridLength(total == 0 ? 1 : ratio, GridUnitType.Star);
                UpColumn.Width = new GridLength(total == 0 ? 0 : 1-ratio, GridUnitType.Star);
                DownBar.Background = total == 0 ? new SolidColorBrush(Color.FromRgb(38,52,73)) : Down;
                BreakdownText.Text = total == 0 ? "No recorded usage" : $"Download {ratio:P1}     •     Upload {1-ratio:P1}";
                RangeText.Text = $"{_start:dd MMM yyyy} – {_end:dd MMM yyyy}";
                int days = (_end-_start).Days+1;
                int step = Math.Max(1,(int)Math.Ceiling(days/30.0));
                _bars = new List<DailyUsage>();
                for (int i=0;i<days;i+=step)
                {
                    var from = _start.AddDays(i); var to = _start.AddDays(Math.Min(days-1,i+step-1));
                    var group = history.Where(r=>r.Date>=from && r.Date<=to).ToList();
                    _bars.Add(new DailyUsage { Date=from, DownloadedBytes=group.Sum(r=>r.DownloadedBytes), UploadedBytes=group.Sum(r=>r.UploadedBytes) });
                }
                StatusText.Text = $"Recorded locally • Refresh every 5 seconds • {(step==1?"Daily bars":$"{step}-day groups")}";
                DrawChart();
            }
            catch (SqliteException) { StatusText.Text = "Unable to read usage. Retrying automatically."; }
        }
        private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();
        private void DrawChart()
        {
            if (ChartCanvas == null) return;
            ChartCanvas.Children.Clear();
            double w=ChartCanvas.ActualWidth, h=ChartCanvas.ActualHeight;
            if(w<80 || h<50 || _bars.Count==0) return;
            double plotH=h-28, left=72, plotW=w-left-4;
            double max=Math.Max(1,_bars.Max(r=>Math.Max(r.DownloadedBytes,r.UploadedBytes)));
            for(int i=0;i<=4;i++)
            {
                double y=plotH*i/4;
                ChartCanvas.Children.Add(new Line { X1=left,X2=w,Y1=y,Y2=y,Stroke=new SolidColorBrush(Color.FromRgb(38,52,73)),StrokeThickness=1 });
                Label(DataFormatter.FormatBytes(max*(4-i)/4),0,y-2);
            }
            double slot=plotW/_bars.Count, bw=Math.Max(1,Math.Min(24,slot*.32));
            for(int i=0;i<_bars.Count;i++)
            {
                var r=_bars[i]; double x=left+i*slot+slot/2;
                Bar(x-bw-1,r.DownloadedBytes,Down,r.Date); Bar(x+1,r.UploadedBytes,Up,r.Date);
                if(i%Math.Max(1,(int)Math.Ceiling(58/slot))==0) Label(r.Date.ToString("dd MMM"),x-15,plotH+5);
            }
            void Bar(double x,long bytes,Brush brush,DateTime date)
            {
                double height=bytes/max*plotH;
                var rect=new Rectangle { Width=bw,Height=height,Fill=brush,RadiusX=2,RadiusY=2,ToolTip=$"{date:dd MMM yyyy} group: {DataFormatter.FormatBytes(bytes)}" };
                Canvas.SetLeft(rect,x); Canvas.SetTop(rect,plotH-height); ChartCanvas.Children.Add(rect);
            }
            void Label(string value,double x,double y)
            {
                var label=new TextBlock { Text=value,FontSize=10,Foreground=new SolidColorBrush(Color.FromRgb(148,163,184)) };
                Canvas.SetLeft(label,x); Canvas.SetTop(label,Math.Max(0,y)); ChartCanvas.Children.Add(label);
            }
        }
    }
}
