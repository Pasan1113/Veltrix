using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using Veltrix.Data;
using Veltrix.Models;
namespace Veltrix.Services;
public sealed class ApplicationNetworkMonitorService : IDisposable
{
    private readonly object _gate=new();
    private const string SessionName = "Veltrix-Applications";
    private DateTime _nextRetry = DateTime.MinValue;
    private bool _canRetry = true;
    private readonly Dictionary<(string Day,string Name),(long Down,long Up)> _pending=new();
    private readonly Dictionary<string,(long Down,long Up)> _sample=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int,(string Name,DateTime Expires)> _names=new();
    private Dictionary<string,(double Down,double Up)> _speeds=new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch _clock=Stopwatch.StartNew();
    private ApplicationUsageRepository? _repo;
    private TraceEventSession? _session;
    private Task? _worker;
    private bool _disposed;
    private volatile string _status="Not started";
    public string Status => _status;
    public string StorageStatus {get;private set;}="";
    private static string Day(DateTime d)=>d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    public void Start()
    {
        lock(_gate)
        {
            if(_disposed || !_canRetry || (_worker != null && !_worker.IsCompleted) || DateTime.UtcNow < _nextRetry) return;
            StartCore();
        }
    }
    private void StartCore()
    {
        try { _repo ??=new ApplicationUsageRepository(); } catch(Exception ex){StorageStatus=ex.Message;}
        _worker=Task.Run(()=>{
            TraceEventSession? session = null;
            try
            {
                if (TraceEventSession.IsElevated() != true)
                {
                    lock (_gate) _canRetry = false;
                    _status = "Run Veltrix as administrator to enable application monitoring.";
                    return;
                }
                // MainWindow/App single-instance protection must remain enabled.
                lock (_gate)
                {
                    if (_disposed) return;
                }
                foreach (var name in TraceEventSession.GetActiveSessionNames())
                {
                    if (!IsOwnedSessionName(name)) continue;
                    using var stale = new TraceEventSession(name, TraceEventSessionOptions.Attach);
                    stale.Stop();
                }
                lock (_gate)
                {
                    if (_disposed) return;
                    session = new TraceEventSession(SessionName);
                    session.StopOnDispose = true;
                    _session = session;
                    session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
                var k=session.Source.Kernel;
                k.TcpIpSend += e=>Add(e.ProcessID,e.size,false);
                k.TcpIpRecv += e=>Add(e.ProcessID,e.size,true);
                k.TcpIpSendIPV6 += e=>Add(e.ProcessID,e.size,false);
                k.TcpIpRecvIPV6 += e=>Add(e.ProcessID,e.size,true);
                k.UdpIpSend += e=>Add(e.ProcessID,e.size,false);
                k.UdpIpRecv += e=>Add(e.ProcessID,e.size,true);
                k.UdpIpSendIPV6 += e=>Add(e.ProcessID,e.size,false);
                k.UdpIpRecvIPV6 += e=>Add(e.ProcessID,e.size,true);
                    _status = "Live monitoring active";
                }
                session.Source.Process();
                lock (_gate) { if (!_disposed) _status = "Monitoring stopped; retry pending."; }
            }
            catch (Exception ex)
            {
                lock (_gate) { if (!_disposed) _status = "Monitoring unavailable: " + ex.Message; }
            }
            finally
            {
                lock (_gate)
                {
                    _session = null;
                    _nextRetry = DateTime.UtcNow.AddSeconds(30);
                }
                try { session?.Dispose(); } catch (Exception ex) { Debug.WriteLine(ex); }
            }
        });
    }
    private static bool IsOwnedSessionName(string name)
    {
        if (string.Equals(name, SessionName, StringComparison.OrdinalIgnoreCase)) return true;
        const string prefix = SessionName + "-";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(name.Substring(prefix.Length), "N", out _);
    }
    private void Add(int pid,int size,bool receive)
    {
        if(size<=0)return;
        lock(_gate){if(_disposed)return;
            if(!_names.TryGetValue(pid,out var entry)||entry.Expires<DateTime.UtcNow){string name=pid==0?"System":"Unknown PID "+pid;try{using var p=Process.GetProcessById(pid);name=p.ProcessName.ToLowerInvariant();}catch{} entry=(name,DateTime.UtcNow.AddSeconds(5));_names[pid]=entry;}
            var key=(Day(DateTime.Today),entry.Name);_pending.TryGetValue(key,out var p1);_sample.TryGetValue(entry.Name,out var p2);
            _pending[key]=(p1.Down+(receive?size:0),p1.Up+(receive?0:size));_sample[entry.Name]=(p2.Down+(receive?size:0),p2.Up+(receive?0:size));
        }
    }
    public void Tick(){Start();lock(_gate){var seconds=Math.Max(.001,_clock.Elapsed.TotalSeconds);_clock.Restart();_speeds=_sample.ToDictionary(x=>x.Key,x=>(x.Value.Down/seconds,x.Value.Up/seconds),StringComparer.OrdinalIgnoreCase);_sample.Clear();}}
    public void Flush(){lock(_gate){try{_repo??=new ApplicationUsageRepository();_repo.Save(_pending);_pending.Clear();StorageStatus="";}catch(Exception ex){StorageStatus=ex.Message;}}}
    public List<ApplicationUsage> Read(bool monthly)
    {
        lock(_gate){var today=DateTime.Today;var start=Day(monthly?new DateTime(today.Year,today.Month,1):today);var end=Day(today);
            var totals=new Dictionary<string,(long Down,long Up)>(StringComparer.OrdinalIgnoreCase);
            try{if(_repo!=null)totals=_repo.Read(start,end);}catch(Exception ex){StorageStatus=ex.Message;}
            foreach(var r in _pending){if(string.CompareOrdinal(r.Key.Day,start)<0||string.CompareOrdinal(r.Key.Day,end)>0)continue;totals.TryGetValue(r.Key.Name,out var v);totals[r.Key.Name]=(v.Down+r.Value.Down,v.Up+r.Value.Up);}
            return totals.Keys.Union(_speeds.Keys,StringComparer.OrdinalIgnoreCase).Select(n=>{totals.TryGetValue(n,out var t);_speeds.TryGetValue(n,out var s);return new ApplicationUsage(n,t.Down,t.Up,s.Down,s.Up);}).OrderByDescending(x=>x.Total).ToList();
        }
    }
    public void Dispose(){TraceEventSession? session;lock(_gate){if(_disposed)return;_disposed=true;session=_session;}try{session?.Stop();}catch(Exception ex){Debug.WriteLine(ex);}try{_worker?.Wait(TimeSpan.FromSeconds(3));}catch(Exception ex){Debug.WriteLine(ex);}Flush();}
}
