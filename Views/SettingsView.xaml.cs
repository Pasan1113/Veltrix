using System;
using System.Windows;
using Veltrix.Services;
namespace Veltrix.Views;
public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly PersonalizationService _settings;
    private readonly Action _apply;
    public SettingsView(PersonalizationService settings, Action apply){_settings=settings;_apply=apply;InitializeComponent();Refresh();}
    public void Refresh()=>Display(_settings);
    private void Display(PersonalizationService s){Startup.IsChecked=s.StartWithWindows;Minimized.IsChecked=s.StartMinimized;OpenMini.IsChecked=s.OpenMini;AlwaysTop.IsChecked=s.AlwaysOnTop;Remember.IsChecked=s.RememberPosition;OpacitySlider.Value=s.Opacity*100;AutoUnits.IsChecked=s.Units=="Auto";KbUnits.IsChecked=s.Units=="KB/s";MbpsUnits.IsChecked=s.Units=="Mbps";Half.IsChecked=s.Interval==.5;One.IsChecked=s.Interval==1;Two.IsChecked=s.Interval==2;Five.IsChecked=s.Interval==5;}
    private void Save_Click(object sender,RoutedEventArgs e){
        try {
            _settings.StartWithWindows=Startup.IsChecked==true;_settings.StartMinimized=Minimized.IsChecked==true;_settings.OpenMini=OpenMini.IsChecked==true;_settings.AlwaysOnTop=AlwaysTop.IsChecked==true;_settings.RememberPosition=Remember.IsChecked==true;_settings.Opacity=OpacitySlider.Value/100;_settings.Units=KbUnits.IsChecked==true?"KB/s":MbpsUnits.IsChecked==true?"Mbps":"Auto";_settings.Interval=Half.IsChecked==true?.5:Two.IsChecked==true?2:Five.IsChecked==true?5:1;
            if(!_settings.RememberPosition){_settings.Left=null;_settings.Top=null;}
            _settings.ApplyStartup();_settings.Save();_apply();Status.Text="Settings saved.";
        }catch(Exception ex){Status.Text="Could not save settings: "+ex.Message;}
    }
    private void Reset_Click(object sender,RoutedEventArgs e){Display(new());Status.Text="Defaults selected. Click Save settings to apply.";}
}
