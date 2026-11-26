namespace Veltrix.Models;
public record ApplicationUsage(string Name, long Download, long Upload, double DownloadSpeed, double UploadSpeed)
{
    public long Total => Download + Upload;
}
