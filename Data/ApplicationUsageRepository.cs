using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
namespace Veltrix.Data;
public sealed class ApplicationUsageRepository
{
    private readonly string _connection;
    public ApplicationUsageRepository()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Veltrix");
        Directory.CreateDirectory(folder);
        _connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(folder,"veltrix.db") }.ToString();
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS ApplicationUsage (Day TEXT NOT NULL, Name TEXT NOT NULL, Down INTEGER NOT NULL, Up INTEGER NOT NULL, PRIMARY KEY(Day,Name))"; cmd.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(_connection); c.Open(); return c; }
    public void Save(Dictionary<(string Day,string Name),(long Down,long Up)> rows)
    {
        if(rows.Count==0)return;
        using var c=Open(); using var tx=c.BeginTransaction();
        foreach(var r in rows) { using var cmd=c.CreateCommand(); cmd.Transaction=tx;
            cmd.CommandText="INSERT INTO ApplicationUsage VALUES($d,$n,$a,$b) ON CONFLICT(Day,Name) DO UPDATE SET Down=Down+excluded.Down, Up=Up+excluded.Up";
            cmd.Parameters.AddWithValue("$d",r.Key.Day);cmd.Parameters.AddWithValue("$n",r.Key.Name);cmd.Parameters.AddWithValue("$a",r.Value.Down);cmd.Parameters.AddWithValue("$b",r.Value.Up);cmd.ExecuteNonQuery(); }
        tx.Commit();
    }
    public Dictionary<string,(long Down,long Up)> Read(string start,string end)
    {
        var result=new Dictionary<string,(long,long)>(StringComparer.OrdinalIgnoreCase);
        using var c=Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT Name,SUM(Down),SUM(Up) FROM ApplicationUsage WHERE Day BETWEEN $s AND $e GROUP BY Name";cmd.Parameters.AddWithValue("$s",start);cmd.Parameters.AddWithValue("$e",end);
        using var r=cmd.ExecuteReader();while(r.Read())result[r.GetString(0)]=(r.GetInt64(1),r.GetInt64(2));return result;
    }
}
