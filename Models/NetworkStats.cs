namespace Veltrix.Models
{
    public class NetworkStats
    {
        public double DownloadSpeed { get; set; }

        public double UploadSpeed { get; set; }

        public long TotalDownloaded { get; set; }

        public long TotalUploaded { get; set; }

        public long DownloadedDelta { get; set; }

        public long UploadedDelta { get; set; }

        public long TotalUsage =>
            TotalDownloaded + TotalUploaded;

        public bool IsConnected { get; set; }
    }
}