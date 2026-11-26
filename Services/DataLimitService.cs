using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
namespace Veltrix.Services;
public sealed class DataLimitSettings
{
    public double LimitGb { get; set; } = 100;
    public int BillingDay { get; set; } = 1;
    public bool Enabled { get; set; }
    public int[] Thresholds { get; set; } = new[] {50,80,90,100};
    public string Cycle { get; set; } = "";
    public List<int> Sent { get; set; } = new();
}
public sealed class DataLimitService
{
    private readonly UsageTrackingService _usage;
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veltrix", "data-limit.json");
    public DataLimitSettings Settings { get; private set; } = new();
    public string Error { get; private set; } = "";
    public DataLimitService(UsageTrackingService usage)
    {
        _usage = usage;
        try { if (File.Exists(_path)) Settings = JsonSerializer.Deserialize<DataLimitSettings>(File.ReadAllText(_path)) ?? new();
            if (!double.IsFinite(Settings.LimitGb) || Settings.LimitGb <= 0 || Settings.BillingDay < 1 || Settings.BillingDay > 31 || Settings.Thresholds == null || Settings.Sent == null) throw new InvalidDataException("Invalid settings"); }
        catch (Exception ex) { Settings = new(); Error = ex.Message; }
    }
    private void Persist(DataLimitSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(settings));
        File.Move(_path + ".tmp", _path, true);
    }
    public void Save(double gb, int day, bool enabled, int[] thresholds)
    {
        if (!double.IsFinite(gb) || gb <= 0 || gb > 1000000 || day < 1 || day > 31) throw new ArgumentException("Enter a positive limit (up to 1,000,000 GB) and billing day 1–31.");
        var next = new DataLimitSettings { LimitGb=gb, BillingDay=day, Enabled=enabled, Thresholds=thresholds, Cycle=Settings.Cycle, Sent=new(Settings.Sent) };
        Persist(next); Settings=next; Error="";
    }
    private DateTime Boundary(DateTime month) => new(month.Year, month.Month, Math.Min(Settings.BillingDay, DateTime.DaysInMonth(month.Year, month.Month)));
    public (DateTime Start, DateTime End, double Used, double Percent, double Average) Snapshot()
    {
        var today=DateTime.Today; var month=new DateTime(today.Year,today.Month,1);
        if(today < Boundary(month)) month=month.AddMonths(-1);
        var start=Boundary(month); var end=Boundary(month.AddMonths(1));
        double used=_usage.GetUsage(start,today).TotalBytes/1e9;
        return (start,end,used,used/Settings.LimitGb*100,used/((today-start).Days+1));
    }
    public void Check(Action<string> notify)
    {
        if(!Settings.Enabled) return;
        try {
            var s=Snapshot(); var cycle=s.Start.ToString("yyyy-MM-dd");
            var sent=Settings.Cycle==cycle ? new List<int>(Settings.Sent) : new List<int>();
            var crossed=Settings.Thresholds.Where(t=>s.Percent>=t && !sent.Contains(t)).OrderBy(t=>t).ToArray();
            if(crossed.Length==0 && Settings.Cycle==cycle) return;
            sent.AddRange(crossed);
            var next=new DataLimitSettings { LimitGb=Settings.LimitGb, BillingDay=Settings.BillingDay, Enabled=Settings.Enabled, Thresholds=Settings.Thresholds, Cycle=cycle, Sent=sent };
            Persist(next); Settings=next; Error="";
            if(crossed.Length>0) notify($"{crossed.Last()}% threshold reached. {s.Used:F2} / {Settings.LimitGb:F2} GB used.");
        } catch(Exception ex) { Error=ex.Message; }
    }
}
