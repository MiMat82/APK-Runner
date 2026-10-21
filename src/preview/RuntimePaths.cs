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
                throw new InvalidOperationException("The saved runtime location must be an absolute local path. No data has been changed.");
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string managedRoot = managed ? Path.GetDirectoryName(appDirectory) : appDirectory;
            if (managed && (path.Equals(managedRoot, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(managedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Android data must be stored outside the folder replaced by the updater.");
            if (!Directory.Exists(path))
                throw new InvalidOperationException("The existing runtime was not found: " + path + ". Reconnect the original folder rather than creating an empty installation.");
            return path;
        }
    }
}
