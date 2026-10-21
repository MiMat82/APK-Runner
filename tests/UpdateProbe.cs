using System;
using System.IO;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Velopack;

#if NEXT
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
#else
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
#endif

namespace APKRunner
{
    internal static class UpdateProbe
    {
        static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }
        static void Write(string path, object value) { File.WriteAllText(path, new JavaScriptSerializer().Serialize(value)); }
        [STAThread]
        static void Main(string[] args)
        {
            VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
            string report = args[1];
            try {
                var manager = new UpdateManager(args[0]);
                string root = RuntimePaths.Resolve(AppDomain.CurrentDomain.BaseDirectory, args[2], true);
                string saved = Path.Combine(root, "userdata-qemu.img.qcow2");
                string hash = Hash(saved);
                var update = manager.CheckForUpdates();
                if (manager.CurrentVersion.ToString() == "1.0.0") {
                    if (update == null) throw new Exception("The fixture update was not found.");
                    var policy = new UpdatePolicy { Current = new Version("1.0.0"), Required = new Version(update.TargetFullRelease.Version.ToString()) };
                    if (!policy.BlocksNewApps) throw new Exception("Required update did not block new app launches.");
                    try { manager.DownloadUpdates(update); }
                    catch (Velopack.Exceptions.ChecksumFailedException) {
                        Write(report, new { result = "corrupt-package-rejected", current = manager.CurrentVersion.ToString(), data_sha256 = Hash(saved) });
                        return;
                    }
                    Write(report + ".before.json", new { version = manager.CurrentVersion.ToString(), data_sha256 = hash, required = policy.Required.ToString() });
                    manager.ApplyUpdatesAndRestart(update.TargetFullRelease, args);
                } else {
                    Write(report, new { result = "updated-and-restarted", current = manager.CurrentVersion.ToString(),
                        assembly = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3), data_sha256 = Hash(saved), runtime = root, update_available = update != null });
                }
            } catch (Exception error) { Write(report, new { result = "error", error = error.ToString() }); }
        }
    }
}
