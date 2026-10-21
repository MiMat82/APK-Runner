using System;
using System.IO;
using System.Web.Script.Serialization;
using Velopack;
using Velopack.Sources;

[assembly: System.Reflection.AssemblyVersion("12.9.99.0")]
namespace APKRunner
{
    internal static class GitHubUpdateProbe
    {
        [STAThread]
        static void Main(string[] args)
        {
            VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            string report = args[0];
            try {
                var updates = new AppUpdates();
                string root = AppUpdates.RuntimeRoot;
                if (AppUpdates.AndroidIsRunning(root)) throw new Exception("Android is running; no test update applied.");
                using (new FileStream(Path.Combine(root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                    updates.Check().GetAwaiter().GetResult();
                    if (updates.Available == null || !updates.Policy.BlocksNewApps) throw new Exception("The public required release was not detected.");
                    updates.Download(null).GetAwaiter().GetResult();
                    File.WriteAllText(report + ".before.json", new JavaScriptSerializer().Serialize(new { repository = AppUpdates.Repository,
                        from = AppUpdates.DisplayVersion, target = updates.Policy.Required.ToString(), required = updates.Policy.BlocksNewApps, runtime = root, downloaded_and_verified = true }));
                    var manager = new UpdateManager(new GithubSource(AppUpdates.Repository, null, false));
                    manager.ApplyUpdatesAndRestart(updates.Available.TargetFullRelease, new[] { "--update-status", report });
                }
            } catch (Exception error) { File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { error = error.ToString() })); }
        }
    }
}
