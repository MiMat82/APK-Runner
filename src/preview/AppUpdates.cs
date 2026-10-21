using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace APKRunner
{
    internal sealed class AppUpdates
    {
        public const string Repository = "https://github.com/MiMat82/APK-Runner";
        public static bool IsManaged { get { return VelopackLocator.Current.CurrentlyInstalledVersion != null; } }
        public static string RuntimeRoot { get { return RuntimePaths.Resolve(AppDomain.CurrentDomain.BaseDirectory, RuntimePaths.SettingsDirectory, IsManaged); } }
        public static string DisplayVersion { get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); } }
        public static string StableLauncher {
            get { return NativeFileLocation.Resolve(IsManaged ? Path.Combine(VelopackLocator.Current.RootAppDir, "APK Runner.exe") : System.Windows.Forms.Application.ExecutablePath); }
        }
        readonly UpdateManager manager;
        readonly string requiredFile = Path.Combine(RuntimePaths.SettingsDirectory, "required-version.txt");
        public UpdateInfo Available { get; private set; }
        public readonly UpdatePolicy Policy = new UpdatePolicy { Current = new Version(DisplayVersion) };

        public AppUpdates()
        {
            if (IsManaged) manager = new UpdateManager(new GithubSource(Repository, null, false));
            Version cached;
            if (File.Exists(requiredFile) && Version.TryParse(File.ReadAllText(requiredFile).Trim(), out cached)) Policy.Required = cached;
        }

        public async Task Check()
        {
            if (manager == null) throw new InvalidOperationException("Installér APK Runner med Setup for at aktivere automatiske opdateringer.");
            Task<UpdateInfo> check = manager.CheckForUpdatesAsync();
            if (await Task.WhenAny(check, Task.Delay(15000)) != check) {
                var observation = check.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("Opdateringstjek tog for lang tid. Prøv igen senere.");
            }
            UpdateInfo candidate = await check;
            Version target = candidate == null ? null : new Version(candidate.TargetFullRelease.Version.ToString());
            // Every newer stable release is mandatory, as requested. Never accept a downgrade.
            Available = candidate;
            Policy.Required = target;
            Directory.CreateDirectory(RuntimePaths.SettingsDirectory);
            if (target != null && target > Policy.Current) File.WriteAllText(requiredFile, target.ToString());
            else if (File.Exists(requiredFile)) File.Delete(requiredFile);
        }

        public Task Download(Action<int> progress)
        {
            if (Available == null) throw new InvalidOperationException("Kontrollér for opdateringer først.");
            return manager.DownloadUpdatesAsync(Available, progress);
        }

        public void Apply(string pendingApk)
        {
            if (Available == null) throw new InvalidOperationException("Ingen verificeret opdatering er klar.");
            manager.ApplyUpdatesAndRestart(Available.TargetFullRelease, string.IsNullOrEmpty(pendingApk) ? new string[0] : new[] { pendingApk });
        }

        public static bool AndroidIsRunning(string runtimeRoot)
        {
            // A booting emulator may not appear in adb yet. Do not terminate it to update.
            using (var search = new ManagementObjectSearcher("SELECT Name,ExecutablePath FROM Win32_Process WHERE Name='emulator.exe' OR Name LIKE 'qemu-system-%' OR Name='scrcpy.exe'"))
            using (var processes = search.Get()) {
                foreach (ManagementObject process in processes) {
                    string path = process["ExecutablePath"] as string;
                    if (string.IsNullOrEmpty(path) || path.StartsWith(Path.GetFullPath(runtimeRoot).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }
    }
}
