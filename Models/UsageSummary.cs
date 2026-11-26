namespace Veltrix.Models
{
    public class UsageSummary
    {
        public long DownloadedBytes { get; set; }

        public long UploadedBytes { get; set; }

        public long TotalBytes =>
            DownloadedBytes + UploadedBytes;
    }
}