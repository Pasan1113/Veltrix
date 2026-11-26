using System;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32;

namespace Veltrix.Services;

public sealed class PersonalizationService
{
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool OpenMini { get; set; }
    public bool RememberPosition { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;

    public double Opacity { get; set; } = 1;

    public double? Left { get; set; }
    public double? Top { get; set; }

    public string Units { get; set; } = "Auto";
    public double Interval { get; set; } = 1;

    private const string StartupTaskName = "VeltrixStartup";

    private static string PathName => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "Veltrix",
        "personalization.json");

    public static PersonalizationService Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<PersonalizationService>(
                File.ReadAllText(PathName)) ?? new();

            if (s.Interval != .5 &&
                s.Interval != 1 &&
                s.Interval != 2 &&
                s.Interval != 5)
            {
                s.Interval = 1;
            }

            s.Opacity = Math.Clamp(s.Opacity, .5, 1);

            if (s.Units != "KB/s" && s.Units != "Mbps")
                s.Units = "Auto";

            return s;
        }
        catch
        {
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(PathName)!);

        File.WriteAllText(
            PathName + ".tmp",
            JsonSerializer.Serialize(this));

        File.Move(
            PathName + ".tmp",
            PathName,
            true);
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(
            WindowsBuiltInRole.Administrator);
    }

    public void ApplyStartup()
    {
        if (!IsAdministrator())
        {
            throw new InvalidOperationException(
                "Administrator privileges are required " +
                "to configure Windows startup.");
        }

        if (StartWithWindows)
        {
            CreateStartupTask();
        }
        else
        {
            DeleteStartupTask();
        }

        RemoveLegacyStartup();
    }

    private static void CreateStartupTask()
    {
        string exe = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Executable unavailable.");

        if (Path.GetFileNameWithoutExtension(exe)
            .Equals("dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Run the Veltrix executable to enable startup.");
        }

        using var identity = WindowsIdentity.GetCurrent();

        string user = identity.Name;

        RunScheduler(
            "/Create",
            "/TN", StartupTaskName,
            "/TR", $"\"{exe}\"",
            "/SC", "ONLOGON",
            "/RU", user,
            "/RL", "HIGHEST",
            "/IT",
            "/F");
    }

    private static void DeleteStartupTask()
    {
        int result = RunScheduler(
            false,
            "/Query",
            "/TN", StartupTaskName);

        if (result == 0)
        {
            RunScheduler(
                "/Delete",
                "/TN", StartupTaskName,
                "/F");
        }
    }

    private static void RemoveLegacyStartup()
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");

        key?.DeleteValue("Veltrix", false);
    }

    private static void RunScheduler(params string[] arguments)
    {
        RunScheduler(true, arguments);
    }

    private static int RunScheduler(
        bool throwOnError,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.SystemDirectory,
                "schtasks.exe"),

            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start Task Scheduler.");

        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Startup configuration failed: " + error);
        }

        return process.ExitCode;
    }
}