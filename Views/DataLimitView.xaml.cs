using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Veltrix.Services;
namespace Veltrix.Views;
public partial class DataLimitView : System.Windows.Controls.UserControl
{
    private readonly DataLimitService _service;
    public DataLimitView(DataLimitService service)
    {
        InitializeComponent(); _service=service; var s=service.Settings;
        LimitBox.Text=s.LimitGb.ToString(); DayBox.Text=s.BillingDay.ToString(); EnabledBox.IsChecked=s.Enabled;
        T50.IsChecked=s.Thresholds.Contains(50); T80.IsChecked=s.Thresholds.Contains(80); T90.IsChecked=s.Thresholds.Contains(90); T100.IsChecked=s.Thresholds.Contains(100); Refresh();
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { if(!double.TryParse(LimitBox.Text,out var gb)||!int.TryParse(DayBox.Text,out var day)) throw new ArgumentException("Enter valid numbers.");
            var thresholds=new[]{(50,T50.IsChecked),(80,T80.IsChecked),(90,T90.IsChecked),(100,T100.IsChecked)}.Where(x=>x.Item2==true).Select(x=>x.Item1).ToArray();
            _service.Save(gb,day,EnabledBox.IsChecked==true,thresholds); StatusText.Text="Settings saved."; Refresh(); }
        catch(Exception ex){StatusText.Text=ex.Message;}
    }
    public void Refresh()
    {
        try { var s=_service.Snapshot(); var limit=_service.Settings.LimitGb;
            PercentText.Text=$"{s.Percent:F1}%"; UsedText.Text=$"{s.Used:F2} / {limit:F2} GB used"; RemainingText.Text=$"{Math.Max(0,limit-s.Used):F2} GB remaining"; CycleText.Text=$"{s.Start:dd MMM} – {s.End.AddDays(-1):dd MMM yyyy}";
            double days=s.Average>0?Math.Max(0,limit-s.Used)/s.Average:double.PositiveInfinity;
            ForecastText.Text=s.Used>=limit?"Package limit reached":days<(s.End-DateTime.Today).Days?$"Estimated limit date: {DateTime.Today.AddDays(Math.Ceiling(days)):dd MMM}":"No exhaustion predicted before cycle ends.";
            double p=Math.Clamp(s.Percent/100,0,1); var center=new System.Windows.Point(80,80); var start=new System.Windows.Point(80,6);
            if(p>=1) Ring.Data=new EllipseGeometry(center,74,74); else if(p<=0) Ring.Data=Geometry.Empty; else { double a=p*2*Math.PI; var end=new System.Windows.Point(80+74*Math.Sin(a),80-74*Math.Cos(a)); var f=new PathFigure{StartPoint=start}; f.Segments.Add(new ArcSegment(end,new System.Windows.Size(74,74),0,p>0.5,SweepDirection.Clockwise,true)); Ring.Data=new PathGeometry(new[]{f}); }
            Ring.Stroke=new SolidColorBrush(System.Windows.Media.Color.FromRgb(s.Percent>=90?(byte)248:(byte)34,s.Percent>=90?(byte)113:(byte)211,s.Percent>=90?(byte)113:(byte)238));
            if(!string.IsNullOrEmpty(_service.Error)) StatusText.Text=_service.Error;
        } catch(Exception ex){StatusText.Text=ex.Message;}
    }
}
