using System;
using System.IO;

namespace APKRunner
{
    internal static class UpdateChecks
    {
        static int checks;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        static bool Refuses(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
        static int Main(string[] args)
        {
            try {
                string fixture = Path.GetFullPath(args[0]);
                string app = Path.Combine(fixture, "managed", "current");
                string settings = Path.Combine(fixture, "settings");
                string external = Path.Combine(fixture, "existing-data");
                Directory.CreateDirectory(app); Directory.CreateDirectory(settings); Directory.CreateDirectory(external);
                string saved = Path.Combine(external, "userdata-qemu.img.qcow2"); File.WriteAllText(saved, "EXISTING APPS AND SAVES");
                string physical = NativeFileLocation.Resolve(saved);
                Check(File.Exists(physical) && File.ReadAllText(physical) == "EXISTING APPS AND SAVES", "Windows shell receives an existing physical path rather than a package-virtualized alias");
                File.WriteAllText(Path.Combine(settings, "runtime-path.txt"), external);
                Check(RuntimePaths.Resolve(app, settings, true) == external, "Managed installation adopts the existing runtime in place");
                Check(File.ReadAllText(saved) == "EXISTING APPS AND SAVES", "Runtime resolution preserves saved data bytes");
                Directory.CreateDirectory(Path.Combine(app, "runtime"));
                Check(RuntimePaths.Resolve(app, settings, false) == Path.Combine(app, "runtime"), "Existing portable installation keeps its own runtime");
                Check(RuntimePaths.Resolve(app, settings, true) == external, "Managed installation never adopts a runtime inside its replaced app directory");
                File.WriteAllText(Path.Combine(settings, "runtime-path.txt"), Path.Combine(fixture, "missing-data"));
                Check(Refuses(delegate { RuntimePaths.Resolve(app, settings, true); }), "Missing registered data fails instead of silently creating an empty installation");
                File.WriteAllText(Path.Combine(settings, "runtime-path.txt"), Path.Combine(app, "runtime"));
                Check(Refuses(delegate { RuntimePaths.Resolve(app, settings, true); }), "Runtime underneath the updater-managed installation is refused");
                File.WriteAllText(Path.Combine(settings, "runtime-path.txt"), "relative-runtime");
                Check(Refuses(delegate { RuntimePaths.Resolve(app, settings, true); }), "Relative registered data paths are refused");
                File.Delete(Path.Combine(settings, "runtime-path.txt"));
                Check(RuntimePaths.Resolve(app, settings, true) == Path.Combine(settings, "runtime"), "New managed installation uses persistent external data");
                Check(!Directory.Exists(Path.Combine(settings, "runtime")), "Resolving a new data path does not create or overwrite data");
                var policy = new UpdatePolicy { Current = new Version(12, 10, 0), Checking = true };
                Check(policy.BlocksNewApps, "APK launches wait until the startup update check completes");
                policy.Checking = false;
                Check(!policy.BlocksNewApps, "Offline check failure without a known required update keeps the app usable");
                policy.Required = new Version(12, 10, 1);
                Check(policy.BlocksNewApps, "A known newer required release blocks new APK launches even when offline");
                policy.Current = new Version(12, 10, 1);
                Check(!policy.BlocksNewApps, "Installed required version releases the launch gate");
                policy.Current = new Version(13, 0, 0);
                Check(!policy.BlocksNewApps, "A newer installed version is never forced to downgrade");
                foreach (var screen in new[] { new System.Drawing.Size(1920, 1040), new System.Drawing.Size(1536, 816), new System.Drawing.Size(1366, 728), new System.Drawing.Size(3840, 2080), new System.Drawing.Size(800, 560) }) {
                    var size = WindowSizing.InitialSize(new System.Drawing.Rectangle(0, 0, screen.Width, screen.Height));
                    Check(size.Width <= Math.Floor(screen.Width * .60) && size.Height <= Math.Floor(screen.Height * .92), "Startup window respects the screenshot proportion at " + screen);
                    Check(size.Width > 0 && size.Height > 0, "Startup bounds remain usable positive dimensions at " + screen);
                }
                Console.WriteLine(checks + " update checks passed."); return 0;
            } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
