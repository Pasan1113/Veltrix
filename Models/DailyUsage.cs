using System;

namespace Veltrix.Models
{
    public class DailyUsage
    {
        public DateTime Date { get; set; }

        public long DownloadedBytes { get; set; }

        public long UploadedBytes { get; set; }

        public long TotalBytes =>
            DownloadedBytes + UploadedBytes;
    }
}