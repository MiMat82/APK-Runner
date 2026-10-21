using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Globalization;
using System.Threading.Tasks;

namespace APKRunner
{
    internal sealed class RenderWindow : Window
    {
        public RenderWindow() : base(null, true) {}
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams value = base.CreateParams; value.ExStyle |= 0x08000000; return value; }
        }
    }
    internal static class PreviewChecks
    {
        static int checks;
        static void Check(bool success, string message)
        {
            if (!success) throw new Exception(message);
            checks++;
            Console.WriteLine("PASS " + message);
        }
        static void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls) {
                yield return child;
                foreach (Control nested in Descendants(child)) yield return nested;
            }
        }
        static T Field<T>(Window window, string name)
        {
            return (T)typeof(Window).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        }
        static void WaitForIdle(Window window)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (Field<bool>(window, "busy") && clock.Elapsed.TotalSeconds < 10) {
                Application.DoEvents(); System.Threading.Thread.Sleep(5);
            }
            Application.DoEvents();
        }
        static void PumpUntil(Func<bool> condition)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!condition() && clock.Elapsed.TotalSeconds < 5) {
                Application.DoEvents(); System.Threading.Thread.Sleep(5);
            }
            Application.DoEvents();
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool GetThreadPreferredUILanguages(uint flags, out uint count, StringBuilder languages, ref uint size);
        static void Render(Window window, string destination, Size size, List<Status> statuses, string log, string activity)
        {
            window.Size = size;
            typeof(Window).GetMethod("UpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { statuses });
            Field<TextBox>(window, "log").Text = log;
            Field<Label>(window, "activityLabel").Text = activity;
            window.PerformLayout();
            Application.DoEvents();
            using (var image = new Bitmap(window.Width, window.Height)) {
                window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                image.Save(destination);
            }
        }
        [STAThread]
        static int Main(string[] args)
        {
            // Separate fixture child processes exercise the real startup/fallback path.
            string executable = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);
            if (executable == "adb") { Console.WriteLine("1"); return 0; }
            if (executable == "emulator") {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "calls.txt"), string.Join(" ", args) + "\n");
                return 1;
            }
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
                System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");
                AppLanguage.Initialize();
                Check(CultureInfo.CurrentCulture.Name == "en-US" && CultureInfo.CurrentUICulture.Name == "en-US", "App uses English even when launched with Danish culture");
                Check(Task.Run(() => CultureInfo.CurrentUICulture.Name).Result == "en-US", "Background operations inherit the English UI language");
                uint languageCount, languageSize = 0;
                GetThreadPreferredUILanguages(0x8, out languageCount, null, ref languageSize);
                var nativeLanguages = new StringBuilder((int)languageSize);
                Check(GetThreadPreferredUILanguages(0x8, out languageCount, nativeLanguages, ref languageSize) && nativeLanguages.ToString().StartsWith("en-US"), "Native dialog resource preference is English for the app thread");
                string fixture = Path.GetFullPath(args[0]);
                Directory.CreateDirectory(fixture);
                string root = Path.Combine(fixture, "runtime");
                string avd = Path.Combine(root, "avd", "APKRunner.avd");
                string ini = Path.Combine(root, "avd", "APKRunner.ini");
                string config = Path.Combine(avd, "config.ini");
                string before = "avd.ini.encoding=UTF-8\npath=C:\\old location\\APKRunner.avd\npath.rel=old\ntarget=android-35\n";
                Write(config, "abi.type = x86_64\nimage.sysdir.1 = system-images\\android-35\\google_apis\\x86_64\\\n");
                Write(ini, before);
                string data = Path.Combine(avd, "userdata-qemu.img.qcow2");
                Write(data, "PRESERVE INSTALLED APPS AND SAVE DATA");
                string userdata = File.ReadAllText(data);
                var runtime = new Runtime(root);
                runtime.RepairAvdPointers();
                Check(File.ReadAllText(ini).Contains("path=" + avd), "Moved AVD registration repaired");
                Check(!File.ReadAllText(ini).Contains("path.rel="), "Stale relative registration removed");
                string[] backups = Directory.GetFiles(Path.GetDirectoryName(ini), "APKRunner.ini.before-portable-*");
                Check(backups.Length == 1 && File.ReadAllText(backups[0]) == before, "Original registration preserved exactly");
                runtime.RepairAvdPointers();
                Check(Directory.GetFiles(Path.GetDirectoryName(ini), "APKRunner.ini.before-portable-*").Length == 1, "Path repair is idempotent");
                Check(File.ReadAllText(data) == userdata && File.ReadAllText(config).Contains("google_apis"), "Repair preserves userdata and device configuration");
                using (FileStream guard = File.Open(Path.Combine(root, "operation.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    bool refused = false;
                    try { runtime.RepairAvdPointers(); } catch (IOException) { refused = true; }
                    Check(refused, "Concurrent runtime operation refused");
                }
                runtime.Portrait = false;
                Check(runtime.DisplayArguments(false).Contains("--new-display=1280x720/160"), "Landscape phone display arguments");
                runtime.Portrait = true;
                Check(runtime.DisplayArguments(false).Contains("--new-display=720x1280/160"), "Portrait phone display arguments");
                Check(!runtime.DisplayArguments(true).Any(x => x.StartsWith("--new-display")) && runtime.DisplayArguments(true).Contains("--window-width=480"), "Legacy x86/TV uses primary display with selected portrait size");
                Check(!runtime.CompactBoot, "Existing snapshot policy remains default");
                Check(!runtime.EmulatorArguments(false).Contains("-QuickbootFileBacked"), "Snapshot-enabled mode keeps file-backed Quickboot available");
                Write(Path.Combine(root, "compact-boot.enabled"), "enabled");
                Check(runtime.CompactBoot, "Compact boot preference detected");
                Check(runtime.EmulatorArguments(false).Contains("-QuickbootFileBacked") && runtime.EmulatorArguments(true).Contains("-QuickbootFileBacked"), "Compact boot avoids unused RAM-file preallocation on both Android runtimes");
                MethodInfo renderArgs = typeof(Runtime).GetMethod("EmulatorArguments");
                MethodInfo videoArgs = typeof(Runtime).GetMethod("VideoArguments");
                Check(renderArgs != null && videoArgs != null, "Measured rendering profile has explicit launch arguments");
                Func<bool, string[]> emulatorArguments = legacy => (string[])renderArgs.Invoke(runtime, new object[] { legacy });
                Func<bool, string, string[]> videoArguments = (legacy, codecs) => (string[])videoArgs.Invoke(runtime, new object[] { legacy, codecs });
                Check(!emulatorArguments(false).Contains("host"), "Hardware graphics are not forced on other installations");
                Write(Path.Combine(root, "fast-rendering.enabled"), "ANOTHER-UNVALIDATED-PC");
                Check(!videoArguments(false, "--video-codec=vp8").Contains("--video-codec=vp8"), "Moving the runtime does not enable an untested computer's profile");
                Write(Path.Combine(root, "fast-rendering.enabled"), Environment.MachineName);
                Check(emulatorArguments(false).Contains("auto") && emulatorArguments(false).Contains("-accel"), "Measured phone profile keeps the faster automatic backend and VM acceleration");
                Check(!emulatorArguments(true).Contains("host"), "Legacy x86 runtime keeps its existing graphics behavior");
                string supported = "--video-codec=vp8 --video-encoder=c2.android.vp8.encoder";
                Check(videoArguments(false, supported).Contains("--video-codec=vp8"), "Fast phone video uses a supported VP8 encoder");
                Check(videoArguments(false, supported).Contains("--max-fps=30") && videoArguments(false, supported).Contains("--video-buffer=33"), "Normal phone mode paces video at 30 FPS with one frame of jitter buffering");
                runtime.SetPerformanceMode(true);
                Check(videoArguments(false, supported).Contains("--max-fps=60") && videoArguments(false, supported).Contains("--video-buffer=0"), "High performance keeps the 60 FPS target and minimum video latency");
                runtime.SetPerformanceMode(false);
                Check(videoArguments(false, "--video-codec=h264").Contains("--video-codec=h264"), "Missing VP8 capability falls back to H.264");
                Check(videoArguments(true, supported).Contains("--video-codec=h264"), "Legacy x86 video retains H.264");
                Check(!emulatorArguments(false).Any(x => x == "-wipe-data" || x == "-data" || x == "-initdata") && File.ReadAllText(data) == userdata, "Rendering profile preserves the existing userdata path and bytes");
                MethodInfo idleMethod = typeof(Runtime).GetMethod("CpuIdleFraction");
                Check(idleMethod != null, "Cold boot readiness measures CPU work after boot-complete");
                Func<string, string, double> idle = (a, b) => (double)idleMethod.Invoke(null, new object[] { a, b });
                Check(Math.Abs(idle("cpu 100 0 100 100 0 0 0 0", "cpu 110 0 110 180 0 0 0 0") - .8) < .001, "Quiet CPU sample is recognized from counter deltas");
                Check(idle("cpu 100 0 100 100 0 0 0 0", "cpu 140 0 150 110 0 0 0 0") < .75, "Busy startup cannot be mistaken for a settled boot");
                Check(idle("bad", "data") == 0 && idle("cpu 100 0 100 100 0 0 0 0", "cpu 0 0 0 0 0 0 0 0") == 0, "Missing/reset CPU counters do not report false readiness");
                string emulatorDir = Path.GetDirectoryName(runtime.Emulator);
                Directory.CreateDirectory(emulatorDir); Directory.CreateDirectory(Path.GetDirectoryName(runtime.Adb));
                File.Copy(Assembly.GetExecutingAssembly().Location, runtime.Emulator);
                File.Copy(Assembly.GetExecutingAssembly().Location, runtime.Adb);
                var startupLog = new List<string>(); runtime.Log = line => { lock (startupLog) startupLog.Add(line); };
                bool startupFailed = false;
                try { typeof(Runtime).GetMethod("StartAndroid", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(runtime, new object[] { false }); }
                catch (TargetInvocationException ex) { startupFailed = ex.InnerException.Message.Contains("exited during startup"); }
                string[] calls = File.ReadAllLines(Path.Combine(emulatorDir, "calls.txt"));
                Check(startupFailed && calls.Length == 1 && calls[0].EndsWith("-gpu auto"), "Actual early emulator failure is reported without duplicate VM starts");
                Check(File.ReadAllText(data) == userdata, "Startup failure preserves saved data");
                foreach (var child in System.Diagnostics.Process.GetProcessesByName("emulator")) {
                    try { if (child.MainModule.FileName == runtime.Emulator) child.WaitForExit(10000); } catch (System.ComponentModel.Win32Exception) {}
                }
                string retired = Path.Combine(root, "android-sdk", "system-images", "android-35", "default", "x86_64");
                Write(Path.Combine(retired, "system.img"), "RETIRED IMAGE");
                Write(Path.Combine(avd, "snapshots", "default_boot", "ram.img"), "SNAPSHOT");
                StorageInventory inventory = StorageReport.Scan(root);
                Check(inventory.CleanupCandidates.Any(x => x.Path.EndsWith("default\\x86_64")), "Unreferenced retired image identified");
                Check(!inventory.CleanupCandidates.Any(x => x.Path.Contains("userdata")), "Userdata excluded from cleanup candidates");
                File.AppendAllText(config, "image.sysdir.1=system-images/android-35/default/x86_64/\n");
                Check(!StorageReport.Scan(root).CleanupCandidates.Any(x => x.Path.EndsWith("default\\x86_64")), "Referenced image never proposed for removal");
                Check(StorageReport.Scan(root).TotalBytes == Directory.GetFiles(root, "*", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length), "Inventory byte accounting");
                bool outside = false;
                try { StorageReport.AssertNoLinks(root, Path.Combine(root, "..", "outside")); } catch (IOException) { outside = true; }
                Check(outside, "Path outside runtime refused");
                string empty = Path.Combine(fixture, "not-installed");
                Check(StorageReport.Scan(empty).TotalBytes == 0 && !Directory.Exists(empty), "Read-only scan does not install or create runtime");

                Application.EnableVisualStyles();
                using (var window = new RenderWindow())
                {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(-32000, -32000);
                    window.ShowInTaskbar = false;
                    window.Show();
                    Application.DoEvents();
                    IntPtr handle = window.Handle;
                    typeof(Window).GetMethod("UpdateStatus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window,
                        new object[] { new System.Collections.Generic.List<Status> { new Status("Preview layout", true, "UI rendering only; Android not started") } });
                    window.PerformLayout();
                    using (var image = new Bitmap(window.Width, window.Height))
                    {
                        window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                        image.Save(Path.Combine(fixture, "preview-window.png"));
                    }
                    Check(window.Controls.Count != 0, "WinForms preview instantiated and rendered without starting Android");
                    bool profileRendered = false;
                    using (var dialogTimer = new System.Windows.Forms.Timer { Interval = 150 }) {
                        dialogTimer.Tick += delegate {
                            Form dialog = Application.OpenForms.OfType<Form>().FirstOrDefault(x => x.Text == "APK Runner / Runtime profile");
                            if (dialog == null) return;
                            profileRendered = dialog.Controls.OfType<Button>().Any(x => x.Text.Contains("30 FPS")) && dialog.Controls.OfType<Button>().Any(x => x.Text.Contains("60 FPS"));
                            using (var snapshot = new Bitmap(dialog.Width, dialog.Height)) {
                                dialog.DrawToBitmap(snapshot, new Rectangle(0, 0, dialog.Width, dialog.Height));
                                snapshot.Save(Path.Combine(fixture, "runtime-profile.png"));
                            }
                            dialogTimer.Stop(); dialog.DialogResult = DialogResult.No; dialog.Close();
                        };
                        dialogTimer.Start();
                        Check(!FlightTheme.ChoosePerformance(window, true) && profileRendered, "Native profile dialog presents both 30 and 60 FPS choices and defaults to Normal");
                    }
                    ComboBox orientation = Field<ComboBox>(window, "orientation");
                    orientation.SelectedIndex = 1;
                    Check(Field<Runtime>(window, "runtime").Portrait, "Orientation selector updates runtime setting");
                    orientation.SelectedIndex = 0;
                    Field<CheckBox>(window, "consent").Checked = true;
                    Check(Field<Button>(window, "install").Enabled, "SDK consent enables installation control");
                    Field<CheckBox>(window, "consent").Checked = false;
                    Check(!Field<Button>(window, "install").Enabled, "Missing SDK consent disables installation control");
                    Check(Descendants(window).OfType<Button>().All(x => x.TabStop && !string.IsNullOrWhiteSpace(x.AccessibleName)), "Buttons have keyboard tab stops and accessible names");
                    var statuses = new List<Status> {
                        new Status("Windows x64", true, "Windows 10 22H2 / Windows 11 x64"),
                        new Status("Virtualization", true, "Windows Hypervisor Platform / WHPX"),
                        new Status("Private Java 17", true, "runtime\\java"),
                        new Status("SDK tools", true, "Private Android SDK"),
                        new Status("ADB", true, "Android platform-tools"),
                        new Status("Android Emulator", true, "Private emulator"),
                        new Status("AAPT", true, "Build-tools 35.0.0"),
                        new Status("Android system", true, "Android 15 Google APIs / x86_64 / ARM translation"),
                        new Status("Android device", false, "Moved runtime: registration will be updated on first startup"),
                        new Status("App window", true, "scrcpy")
                    };
                    Directory.CreateDirectory(Path.Combine(fixture, "ui"));
                    Render(window, Path.Combine(fixture, "ui", "partial-1040x860.png"), new Size(1040, 860), statuses,
                        "VISUAL PREVIEW / Sample data\r\n9 of 10 components ready. Check the Android device registration.\r\nNo Android emulator was started in this UI test.", "ACTION REQUIRED");
                    Check(Field<Label>(window, "statusSummary").Text == "09 / 10 READY", "Partial readiness count is accurate");
                    Check(Field<Button>(window, "refresh").Text == "Check runtime" && Field<Button>(window, "storage").Text == "Storage usage", "Primary maintenance controls use English labels");
                    Check(orientation.Items.Cast<string>().SequenceEqual(new[] { "Landscape", "Portrait" }), "Both orientation choices use English only");
                    Check(!Descendants(window).Any(c => c.Text.IndexOfAny(new[] {'æ', 'ø', 'å', 'Æ', 'Ø', 'Å'}) >= 0), "Rendered interface contains no Danish letters");
                    Render(window, Path.Combine(fixture, "ui", "empty-940x760.png"), new Size(940, 760), new List<Status>(), "", "STANDBY");
                    Check(Field<Label>(window, "emptyState").Visible, "Empty state explains next action");
                    Field<Label>(window, "emptyState").Text = "Checking runtime components …";
                    Field<Label>(window, "activityLabel").Text = "WORKING";
                    Field<Button>(window, "refresh").Enabled = false;
                    using (var image = new Bitmap(window.Width, window.Height)) {
                        window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                        image.Save(Path.Combine(fixture, "ui", "loading-940x760.png"));
                    }
                    Field<Button>(window, "refresh").Enabled = true;
                    Render(window, Path.Combine(fixture, "ui", "error-1040x860.png"), new Size(1040, 860), statuses,
                        "VISUAL PREVIEW / Simulated error\r\nAndroid was not ready within four minutes.\r\nCheck virtualization and try Check runtime again.", "ACTION REQUIRED");
                    foreach (Status status in statuses) status.Ready = true;
                    // Exercise the real asynchronous control state transition using
                    // a harmless worker, with no APK, network or emulator calls.
                    typeof(Window).GetMethod("Work", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { (Action)delegate {} });
                    WaitForIdle(window);
                    Check(!Field<bool>(window, "busy"), "Async operation returns to idle");
                    Render(window, Path.Combine(fixture, "ui", "success-1280x900.png"), new Size(1280, 900), statuses,
                        "VISUAL PREVIEW / Sample data\r\nAll components ready. Choose or drop an APK/XAPK file.", "STANDBY");
                    // The real Work completion consumes the readiness Tag set above.
                    typeof(Window).GetMethod("Work", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { (Action)delegate {} });
                    WaitForIdle(window);
                    Check(Field<Button>(window, "open").Enabled, "Successful readiness enables primary launch action");
                    // Hold the online portion unresolved while a local readiness task
                    // completes. The actual operation runner must keep a single busy
                    // scope across asynchronous waits and restore controls afterward.
                    var online = new TaskCompletionSource<bool>();
                    var local = new TaskCompletionSource<bool>();
                    bool launchReached = false;
                    Func<Task> startup = async delegate {
                        await Task.WhenAll(Task.Run(delegate { local.SetResult(true); }), online.Task);
                        launchReached = true;
                    };
                    Task startupOperation = (Task)typeof(Window).GetMethod("RunFlightOperationAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { startup });
                    PumpUntil(() => local.Task.IsCompleted);
                    Check(local.Task.IsCompleted && !startupOperation.IsCompleted, "Local readiness progresses while the network check is pending");
                    Check(Field<bool>(window, "busy") && !launchReached && !Field<Button>(window, "open").Enabled, "A single busy scope protects app launches during both startup checks");
                    online.SetResult(true);
                    PumpUntil(() => startupOperation.IsCompleted);
                    Check(startupOperation.Status == TaskStatus.RanToCompletion && launchReached && !Field<bool>(window, "busy"), "Startup operation completes only after both checks finish");
                    Render(window, Path.Combine(fixture, "ui", "success-1280x900.png"), new Size(1280, 900), statuses,
                        "VISUAL PREVIEW / Sample data\r\nAll components ready. Choose or drop an APK/XAPK file.", "STANDBY");
                    Field<CheckBox>(window, "consent").Checked = false;
                    Render(window, Path.Combine(fixture, "ui", "permission-940x760.png"), new Size(940, 760), statuses,
                        "VISUAL PREVIEW / SDK terms\r\nInstallation requires accepting the Android SDK terms. Read the terms before accepting.", "STANDBY");
                    var viewport = Descendants(window).OfType<Panel>().First(p => p.Name == "ContentViewport" || p.AutoScroll);
                    Check(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible, "A normally sized window has no unnecessary outer scrollbars");
                    var required = new UpdatePolicy { Current = new Version(12, 10, 0), Required = new Version(12, 10, 1) };
                    typeof(Window).GetField("updatePolicy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, required);
                    typeof(Window).GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, "missing-must-not-launch.apk");
                    typeof(Window).GetMethod("RunPending", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[0]);
                    Check(Field<string>(window, "pending") == "missing-must-not-launch.apk", "Mandatory update prevents a queued APK from launching or being discarded");
                    required.Checking = true; required.Required = null;
                    typeof(Window).GetMethod("RunPending", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[0]);
                    Check(Field<string>(window, "pending") == "missing-must-not-launch.apk", "Startup update check also gates command-line APK launches");
                    required.Checking = false;
                    typeof(Window).GetField("pending", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, null);
                    Render(window, Path.Combine(fixture, "ui", "compact-920x750.png"), new Size(920, 750), statuses, "Compact startup window", "STANDBY");
                    Check(!viewport.HorizontalScroll.Visible && !viewport.VerticalScroll.Visible, "Compact startup keeps controls visible without outer scrollbars");
                    Render(window, Path.Combine(fixture, "ui", "small-640x520.png"), new Size(640, 520), statuses, "Small screen - scroll to see all controls", "STANDBY");
                    Check(viewport.AutoScroll && viewport.HorizontalScroll.Visible && viewport.VerticalScroll.Visible, "Small screens retain access to the complete interface by scrolling");
                    Render(window, Path.Combine(fixture, "ui", "focus-940x760.png"), new Size(940, 760), statuses, "Compact layout check", "STANDBY");
                    Button storage = Field<Button>(window, "storage");
                    Rectangle visible = storage.RectangleToScreen(storage.ClientRectangle);
                    for (Control parent = storage.Parent; parent != null; parent = parent.Parent)
                        Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(visible), "Storage usage stays inside its container at minimum window size");
                    Check(window.SelectNextControl(null, true, true, true, false), "Native keyboard navigation can select a focusable control");
                    storage.Select();
                    using (var image = new Bitmap(window.Width, window.Height)) {
                        window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                        image.Save(Path.Combine(fixture, "ui", "focus-940x760.png"));
                    }
                }
                Console.WriteLine(checks + " checks passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
