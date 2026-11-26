using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
namespace Veltrix.Services;
public static class StabilityService
{
    private static Mutex? _mutex;
    private static readonly object Gate = new();
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veltrix");
    public static bool Acquire()
    {
        _mutex = new Mutex(true, @"Local\Veltrix-Desktop-SingleInstance", out bool created);
        if (!created) { _mutex.Dispose(); _mutex = null; }
        return created;
    }
    public static void Release() { if (_mutex == null) return; _mutex.ReleaseMutex(); _mutex.Dispose(); _mutex = null; }
    public static void Log(string context, Exception ex)
    {
        try { lock (Gate) { Directory.CreateDirectory(Folder); var path = Path.Combine(Folder,"errors.log"); if(File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path,path+".old",true); File.AppendAllText(path,$"{DateTimeOffset.Now:O} {context}: {ex}{Environment.NewLine}"); } } catch { }
    }
    public static void ConfigureDatabase()
    {
        try { Directory.CreateDirectory(Folder); using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(Folder,"veltrix.db") }.ToString()); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="PRAGMA journal_mode=WAL;"; cmd.ExecuteScalar(); }
        catch(Exception ex) { Log("SQLite WAL",ex); }
    }
}
