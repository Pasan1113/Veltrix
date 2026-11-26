using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Veltrix.Models;

namespace Veltrix.Data
{
    public sealed class DatabaseService
    {
        private readonly string _connectionString;

        public string DatabasePath { get; }

        public DatabaseService()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Veltrix");

            Directory.CreateDirectory(folder);

            DatabasePath = Path.Combine(
                folder,
                "veltrix.db");

            _connectionString =
                new SqliteConnectionStringBuilder
                {
                    DataSource = DatabasePath
                }.ToString();

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection =
                new SqliteConnection(_connectionString);

            connection.Open();

            using var command =
                connection.CreateCommand();

            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS DailyUsage
                (
                    UsageDate TEXT PRIMARY KEY,
                    DownloadedBytes INTEGER NOT NULL DEFAULT 0,
                    UploadedBytes INTEGER NOT NULL DEFAULT 0
                );
            ";

            command.ExecuteNonQuery();
        }

        public void SaveUsage(
            IEnumerable<DailyUsage> records)
        {
            using var connection =
                new SqliteConnection(_connectionString);

            connection.Open();

            using var transaction =
                connection.BeginTransaction();

            foreach (var record in records)
            {
                using var command =
                    connection.CreateCommand();

                command.Transaction = transaction;

                command.CommandText = @"
                    INSERT INTO DailyUsage
                    (
                        UsageDate,
                        DownloadedBytes,
                        UploadedBytes
                    )
                    VALUES
                    (
                        $date,
                        $download,
                        $upload
                    )
                    ON CONFLICT(UsageDate)
                    DO UPDATE SET
                        DownloadedBytes =
                            DailyUsage.DownloadedBytes
                            + excluded.DownloadedBytes,

                        UploadedBytes =
                            DailyUsage.UploadedBytes
                            + excluded.UploadedBytes;
                ";

                command.Parameters.AddWithValue(
                    "$date",
                    FormatDate(record.Date));

                command.Parameters.AddWithValue(
                    "$download",
                    record.DownloadedBytes);

                command.Parameters.AddWithValue(
                    "$upload",
                    record.UploadedBytes);

                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        public UsageSummary GetUsage(
            DateTime startDate,
            DateTime endDate)
        {
            using var connection =
                new SqliteConnection(_connectionString);

            connection.Open();

            using var command =
                connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    COALESCE(SUM(DownloadedBytes), 0),
                    COALESCE(SUM(UploadedBytes), 0)
                FROM DailyUsage
                WHERE UsageDate >= $start
                  AND UsageDate <= $end;
            ";

            command.Parameters.AddWithValue(
                "$start",
                FormatDate(startDate));

            command.Parameters.AddWithValue(
                "$end",
                FormatDate(endDate));

            using var reader =
                command.ExecuteReader();

            if (reader.Read())
            {
                return new UsageSummary
                {
                    DownloadedBytes = reader.GetInt64(0),
                    UploadedBytes = reader.GetInt64(1)
                };
            }

            return new UsageSummary();
        }

        public List<DailyUsage> GetHistory(
            DateTime startDate,
            DateTime endDate)
        {
            var history = new List<DailyUsage>();

            using var connection =
                new SqliteConnection(_connectionString);

            connection.Open();

            using var command =
                connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    UsageDate,
                    DownloadedBytes,
                    UploadedBytes
                FROM DailyUsage
                WHERE UsageDate >= $start
                  AND UsageDate <= $end
                ORDER BY UsageDate ASC;
            ";

            command.Parameters.AddWithValue(
                "$start",
                FormatDate(startDate));

            command.Parameters.AddWithValue(
                "$end",
                FormatDate(endDate));

            using var reader =
                command.ExecuteReader();

            while (reader.Read())
            {
                history.Add(new DailyUsage
                {
                    Date = DateTime.ParseExact(
                        reader.GetString(0),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),

                    DownloadedBytes = reader.GetInt64(1),

                    UploadedBytes = reader.GetInt64(2)
                });
            }

            return history;
        }

        private static string FormatDate(DateTime date)
        {
            return date.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
        }
    }
}