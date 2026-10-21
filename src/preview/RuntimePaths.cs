using System;
using System.IO;

namespace APKRunner
{
    internal static class RuntimePaths
    {
        public static readonly string SettingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "APKRunnerData");
        public static string Resolve(string appDirectory, string settingsDirectory, bool managed)
        {
            appDirectory = Path.GetFullPath(appDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string adjacent = Path.Combine(appDirectory, "runtime");
            if (!managed && Directory.Exists(adjacent)) return adjacent;
            string registration = Path.Combine(settingsDirectory, "runtime-path.txt");
            if (!File.Exists(registration)) return Path.Combine(settingsDirectory, "runtime");
            string path = File.ReadAllText(registration).Trim();
            if (!Path.IsPathRooted(path) || path.Length < 3 || path[1] != ':')
                throw new InvalidOperationException("Den gemte runtime-sti skal være en absolut lokal sti. Ingen data er ændret.");
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string managedRoot = managed ? Path.GetDirectoryName(appDirectory) : appDirectory;
            if (managed && (path.Equals(managedRoot, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Android-data må ikke ligge i den mappe, som opdateringsprogrammet erstatter.");
            if (!Directory.Exists(path))
                throw new InvalidOperationException("Den eksisterende runtime blev ikke fundet: " + path + ". Gendan forbindelsen til mappen; opret ikke en tom installation.");
            return path;
        }
    }
}
