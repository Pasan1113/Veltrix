using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Veltrix.Helpers;
using Veltrix.Services;
namespace Veltrix.Views;
public partial class ApplicationUsageView : System.Windows.Controls.UserControl
{
    private readonly ApplicationNetworkMonitorService _monitor;
    private bool _monthly, _sortSpeed;
    public ApplicationUsageView(ApplicationNetworkMonitorService monitor){_monitor=monitor;InitializeComponent();Refresh();}
    public void Refresh(){if(Rows==null||Search==null)return;var all=_monitor.Read(_monthly);var total=all.Sum(x=>x.Total); if(TopRows!=null) TopRows.ItemsSource=all.Where(x=>x.Total>0).OrderByDescending(x=>x.Total).Take(4).Select(x=>new {x.Name,Percent=total>0?100.0*x.Total/total:0,Usage=DataFormatter.FormatBytes(x.Total)}).ToList(); var filtered=all.Where(x=>x.Name.Contains(Search.Text,StringComparison.OrdinalIgnoreCase));if(_sortSpeed)filtered=filtered.OrderByDescending(x=>x.DownloadSpeed+x.UploadSpeed);
        Rows.ItemsSource=filtered.Select(x=>new {x.Name,Down=DataFormatter.FormatSpeed(x.DownloadSpeed),Up=DataFormatter.FormatSpeed(x.UploadSpeed),Total=DataFormatter.FormatBytes(x.Total)}).ToList();
        Summary.Text=$"{(_monthly?"This month":"Today")}  •  Active apps: {all.Count(x=>x.DownloadSpeed+x.UploadSpeed>0)}  •  Total: {DataFormatter.FormatBytes(all.Sum(x=>x.Total))}";
        Status.Text=_monitor.Status+(string.IsNullOrEmpty(_monitor.StorageStatus)?"":" | Storage: "+_monitor.StorageStatus);}
    private void Today_Click(object sender,RoutedEventArgs e){_monthly=false;Refresh();}
    private void Month_Click(object sender,RoutedEventArgs e){_monthly=true;Refresh();}
    private void Sort_Click(object sender,RoutedEventArgs e){_sortSpeed=!_sortSpeed;Refresh();}
    private void Search_Changed(object sender,TextChangedEventArgs e)=>Refresh();
}
