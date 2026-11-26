using System;
using System.Collections.Generic;
using System.Linq;
using Veltrix.Data;
using Veltrix.Models;

namespace Veltrix.Services
{
    public sealed class UsageTrackingService
    {
        private readonly DatabaseService _database;
        private readonly Dictionary<DateTime, DailyUsage> _pendingUsage = new();
        public UsageTrackingService(DatabaseService database) => _database = database;
        public void TrackUsage(NetworkStats stats)
        {
            var today = DateTime.Today;
            if (!_pendingUsage.TryGetValue(today, out var usage))
                _pendingUsage[today] = usage = new DailyUsage { Date = today };
            usage.DownloadedBytes += stats.DownloadedDelta;
            usage.UploadedBytes += stats.UploadedDelta;
        }
        public void Flush()
        {
            if (_pendingUsage.Count == 0) return;
            _database.SaveUsage(_pendingUsage.Values);
            _pendingUsage.Clear();
        }
        public List<DailyUsage> GetHistory(DateTime start, DateTime end)
        {
            var rows = _database.GetHistory(start.Date, end.Date).ToDictionary(r => r.Date.Date);
            foreach (var pending in _pendingUsage.Values)
            {
                if (pending.Date < start.Date || pending.Date > end.Date) continue;
                if (!rows.TryGetValue(pending.Date, out var row))
                    rows[pending.Date] = row = new DailyUsage { Date = pending.Date };
                row.DownloadedBytes += pending.DownloadedBytes;
                row.UploadedBytes += pending.UploadedBytes;
            }
            return rows.Values.OrderBy(r => r.Date).ToList();
        }
        public UsageSummary GetUsage(DateTime start, DateTime end)
        {
            var summary = _database.GetUsage(start, end);
            foreach (var usage in _pendingUsage.Values)
                if (usage.Date >= start.Date && usage.Date <= end.Date)
                {
                    summary.DownloadedBytes += usage.DownloadedBytes;
                    summary.UploadedBytes += usage.UploadedBytes;
                }
            return summary;
        }
        public UsageSummary GetTodayUsage() => GetUsage(DateTime.Today, DateTime.Today);
        public UsageSummary GetWeeklyUsage() => GetUsage(DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7)), DateTime.Today);
        public UsageSummary GetMonthlyUsage() => GetUsage(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today);
        public UsageSummary GetYearlyUsage() => GetUsage(new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today);
    }
}
