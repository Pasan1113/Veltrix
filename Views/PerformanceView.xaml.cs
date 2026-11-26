using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Veltrix.Helpers;
using Veltrix.Models;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using Polyline = System.Windows.Shapes.Polyline;
namespace Veltrix.Views;
public partial class PerformanceView : System.Windows.Controls.UserControl
{
    private readonly Queue<long?> _samples = new();
    private readonly Stopwatch _uptime = new();
    private readonly Stopwatch _cpuClock = Stopwatch.StartNew();
    private TimeSpan? _previousCpu;
    private bool _busy;
    private DateTime _nextProbe;
    private double _peakDown, _peakUp;
    public PerformanceView() { InitializeComponent(); }
    public void Observe(NetworkStats stats) { _peakDown = Math.Max(_peakDown, stats.DownloadSpeed); _peakUp = Math.Max(_peakUp, stats.UploadSpeed); }
    public async void Refresh()
    {
        PeakDown.Text = DataFormatter.FormatSpeed(_peakDown); PeakUp.Text = DataFormatter.FormatSpeed(_peakUp);
        if (_busy || DateTime.UtcNow < _nextProbe) return;
        _busy = true; _nextProbe = DateTime.UtcNow.AddSeconds(2);
        try {
            long? sample = null;
            try { using var ping = new Ping(); var reply = await ping.SendPingAsync("1.1.1.1", 1500); if (reply.Status == IPStatus.Success) sample = reply.RoundtripTime; }
            catch (PingException) { } catch (System.Net.Sockets.SocketException) { }
            _samples.Enqueue(sample); while (_samples.Count > 60) _samples.Dequeue();
            if (sample.HasValue) { if (!_uptime.IsRunning) _uptime.Start(); } else _uptime.Reset();
            Latency.Text = sample.HasValue ? $"{sample} ms" : "—";
            Loss.Text = $"{100.0 * _samples.Count(x => !x.HasValue) / _samples.Count:F1}%";
            Uptime.Text = _uptime.Elapsed.ToString(@"d\.hh\:mm\:ss");
            var values = _samples.ToArray(); var differences = new List<double>();
            for (int i=1;i<values.Length;i++) if(values[i].HasValue && values[i-1].HasValue) differences.Add(Math.Abs((double)values[i]!.Value-values[i-1]!.Value));
            Jitter.Text = differences.Count == 0 ? "—" : $"{differences.Average():F1} ms";
            using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var elapsed = _cpuClock.Elapsed.TotalMilliseconds; _cpuClock.Restart();
            var percent = _previousCpu.HasValue && elapsed>0 ? Math.Clamp(100*(cpu-_previousCpu.Value).TotalMilliseconds/elapsed/Environment.ProcessorCount,0,100) : 0; _previousCpu=cpu;
            ResourcesText.Text = $"CPU: {percent:F1}%  •  RAM: {process.WorkingSet64/1000000.0:F1} MB";
            ProbeStatus.Text = sample.HasValue ? "Probe reachable" : "Probe failed — ICMP may be blocked"; Draw();
        } catch(Exception ex) { ProbeStatus.Text = ex.Message; } finally { _busy=false; }
    }
    private void Plot_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    private void Draw() {
        if (Plot == null) return; Plot.Children.Clear(); double w=Plot.ActualWidth,h=Plot.ActualHeight; if(w<=0||h<=0) return;
        var a=_samples.ToArray(); double max=Math.Max(10,a.Where(x=>x.HasValue).Select(x=>(double)x!.Value).DefaultIfEmpty(0).Max()*1.1);
        Polyline? line=null; for(int i=0;i<a.Length;i++) { if(!a[i].HasValue){line=null;continue;} if(line==null){line=new Polyline{Stroke=Brushes.Cyan,StrokeThickness=2};Plot.Children.Add(line);} line.Points.Add(new Point(w*i/59,h-2-(h-4)*a[i]!.Value/max)); }
    }
}
