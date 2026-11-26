using System;
using System.Windows;
using Veltrix.Services;
namespace Veltrix;
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!StabilityService.Acquire()) { System.Windows.MessageBox.Show("Veltrix is already running. Check the system tray.","Veltrix"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) => StabilityService.Log("UI exception",args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => { if(args.ExceptionObject is Exception ex) StabilityService.Log("Unhandled exception",ex); };
        StabilityService.ConfigureDatabase();
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { StabilityService.Release(); base.OnExit(e); }
}
