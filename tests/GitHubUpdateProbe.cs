using System;
using System.IO;
using System.Web.Script.Serialization;
using Velopack;

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
                string expectedVersion = args[1];
                string settings = Environment.GetEnvironmentVariable("APK_RUNNER_INTEGRATION_SETTINGS");
                if (string.IsNullOrEmpty(settings) || !Path.IsPathRooted(settings))
                    throw new Exception("The isolated settings directory is required.");
                var updates = new AppUpdates();
                string root = AppUpdates.RuntimeRoot;
                using (new FileStream(Path.Combine(root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                    updates.Policy.Checking = true;
                    try { updates.Check().GetAwaiter().GetResult(); }
                    finally { updates.Policy.Checking = false; }
                    if (updates.Available == null || !updates.Policy.BlocksNewApps) throw new Exception("The public required release was not detected.");
                    if (updates.Policy.Required.ToString() != expectedVersion)
                        throw new Exception("Unexpected latest public version: " + updates.Policy.Required);
                    updates.Download(null).GetAwaiter().GetResult();
                    File.WriteAllText(report + ".before.json", new JavaScriptSerializer().Serialize(new { repository = AppUpdates.Repository,
                        from = AppUpdates.DisplayVersion, target = updates.Policy.Required.ToString(), required = updates.Policy.BlocksNewApps,
                        runtime = root, settings = settings, downloaded_and_verified = true, restart_exercised = false }));
                    // A production GUI restart would resolve the user's real settings.
                    // Apply the actual public bytes in this isolated installation, then
                    // let the outer harness inspect the new manifest and runtime resolver.
                    new UpdateManager(new Velopack.Sources.GithubSource(AppUpdates.Repository, null, false))
                        .ApplyUpdatesAndExit(updates.Available.TargetFullRelease);
                }
            } catch (Exception error) { File.WriteAllText(report, new JavaScriptSerializer().Serialize(new { error = error.ToString() })); }
        }
    }
}
