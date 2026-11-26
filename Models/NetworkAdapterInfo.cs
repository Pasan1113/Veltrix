namespace Veltrix.Models
{
    public sealed class NetworkAdapterInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string IPv4 { get; set; } = "—";
        public string IPv6 { get; set; } = "—";
        public string Mac { get; set; } = "—";
        public string LinkSpeed { get; set; } = "—";
        public double DownloadSpeed { get; set; }
        public double UploadSpeed { get; set; }
    }
}
