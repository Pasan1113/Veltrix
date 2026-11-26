using System;
using System.IO;

namespace Veltrix.Services
{
    public static class AppSettingsService
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Veltrix",
            "network-adapter.txt");

        public static string LoadAdapter()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return "auto";

                string value = File.ReadAllText(FilePath).Trim();

                return string.IsNullOrWhiteSpace(value)
                    ? "auto"
                    : value;
            }
            catch (IOException)
            {
                return "auto";
            }
            catch (UnauthorizedAccessException)
            {
                return "auto";
            }
        }

        public static void SaveAdapter(string id)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(FilePath)!);

            File.WriteAllText(FilePath, id);
        }
    }
}