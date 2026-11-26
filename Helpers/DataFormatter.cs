using System;
namespace Veltrix.Helpers;
public static class DataFormatter
{
    public static string SpeedUnits {get;set;} = "Auto";
    public static string FormatBytes(double bytes)
    {
        string[] units={"B","KB","MB","GB","TB"}; int i=0;
        while(bytes>=1000 && i<units.Length-1){bytes/=1000;i++;}
        return $"{bytes:F2} {units[i]}";
    }
    public static string FormatSpeed(double bytes) => SpeedUnits switch {
        "KB/s" => $"{bytes/1000:F2} KB/s", "Mbps" => $"{bytes*8/1000000:F2} Mbps", _ => FormatBytes(bytes)+"/s"};
}
