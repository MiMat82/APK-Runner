using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace APKRunner
{
    internal sealed class StorageItem
    {
        public string Path;
        public long Bytes;
        public long DiskBytes;
        public int Files;
    }

    internal sealed class StorageInventory
    {
        public string RuntimeRoot;
        public long TotalBytes;
        public long DiskBytes;
        public int TotalFiles;
        public List<StorageItem> Components = new List<StorageItem>();
        public List<StorageItem> CleanupCandidates = new List<StorageItem>();
        public List<string> Warnings = new List<string>();
    }

    internal static class StorageReport
    {
        public static void AssertNoLinks(string root, string path)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!path.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The path is outside the runtime folder: " + path);
            for (string current = path; ; current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic links and junctions cannot be used here: " + current);
                if (current.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
            }
        }

        static IEnumerable<FileInfo> Files(string root, string directory, List<string> warnings)
        {
            AssertNoLinks(root, directory);
            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count != 0)
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        warnings.Add("Skipped link: " + entry);
                        continue;
                    }
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                    else yield return new FileInfo(entry);
                }
            }
        }

        public static StorageInventory Scan(string root)
        {
            root = Path.GetFullPath(root);
            var result = new StorageInventory { RuntimeRoot = root };
            if (!Directory.Exists(root)) return result;
            AssertNoLinks(root, root);
            List<FileInfo> files = Files(root, root, result.Warnings).ToList();
            result.TotalBytes = files.Sum(x => x.Length);
            var disk = files.ToDictionary(x => x.FullName, x => NativeStorage.DiskBytes(x.FullName), StringComparer.OrdinalIgnoreCase);
            result.DiskBytes = disk.Values.Sum();
            result.TotalFiles = files.Count;
            result.Components = files.GroupBy(x => x.FullName.Substring(root.Length + 1).Split(Path.DirectorySeparatorChar)[0])
                .Select(x => new StorageItem { Path = x.Key, Bytes = x.Sum(f => f.Length), DiskBytes = x.Sum(f => disk[f.FullName]), Files = x.Count() })
                .OrderByDescending(x => x.Bytes).ToList();
            Action<string> candidate = relative => {
                string prefix = Path.Combine(root, relative) + Path.DirectorySeparatorChar;
                var selected = files.Where(x => x.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                if (selected.Count != 0)
                    result.CleanupCandidates.Add(new StorageItem { Path = relative, Bytes = selected.Sum(x => x.Length), DiskBytes = selected.Sum(x => disk[x.FullName]), Files = selected.Count });
            };
            candidate("backups");
            candidate(Path.Combine("avd", "APKRunner.avd", "snapshots"));
            candidate(Path.Combine("avd", "APKRunnerTV.avd", "snapshots"));
            string retiredImage = "system-images\\android-35\\default\\x86_64\\";
            bool referenced = files.Where(x => x.Name.Equals("config.ini", StringComparison.OrdinalIgnoreCase) &&
                    x.FullName.StartsWith(Path.Combine(root, "avd") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .Any(x => File.ReadAllLines(x.FullName).Any(line => {
                    int separator = line.IndexOf('=');
                    return separator >= 0 && line.Substring(0, separator).Trim().Equals("image.sysdir.1", StringComparison.OrdinalIgnoreCase) &&
                        line.Substring(separator + 1).Trim().Replace('/', '\\').TrimEnd('\\').Equals(retiredImage.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                }));
            if (!referenced) candidate(Path.Combine("android-sdk", "system-images", "android-35", "default", "x86_64"));
            return result;
        }

        static string Size(long bytes)
        {
            return (bytes / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GiB";
        }

        public static string Describe(StorageInventory report)
        {
            var text = new StringBuilder();
            text.AppendLine("APK Runner — storage usage").AppendLine(report.RuntimeRoot).AppendLine();
            text.AppendLine("Space on disk: " + Size(report.DiskBytes));
            text.AppendLine("Logical file size: " + Size(report.TotalBytes) + " / " + report.TotalFiles + " files");
            text.AppendLine("Windows reports disk usage, accounting for compression and sparse files.").AppendLine();
            foreach (StorageItem item in report.Components)
                text.AppendLine(Size(item.DiskBytes).PadLeft(12) + "   " + item.Path);
            text.AppendLine().AppendLine("Potential cleanup targets (nothing is deleted here):");
            foreach (StorageItem item in report.CleanupCandidates)
                text.AppendLine(Size(item.DiskBytes).PadLeft(12) + "   " + item.Path);
            text.AppendLine().AppendLine("Backups and the old default system image belong to earlier versions and their recovery files.");
            text.AppendLine("Snapshots save boot and session memory. Removing them requires a cold boot and loses the saved session.");
            text.AppendLine("The proposed cleanup keeps user data, installed apps, the Google APIs image and Android TV.");
            text.AppendLine().AppendLine("Choose portrait or landscape before opening an app. The app's own orientation requirements still apply.");
            text.AppendLine("On the x86/TV device, open apps share the same orientation.");
            foreach (string warning in report.Warnings) text.AppendLine(warning);
            return text.ToString();
        }

        public static Form CreateWindow(string report, string root)
        {
                var form = new Form { Text = "APK Runner / Storage usage", Width = 900, Height = 680,
                    BackColor = FlightTheme.Background, ForeColor = FlightTheme.Text, Font = FlightTheme.Body(10),
                    Padding = new Padding(24), StartPosition = FormStartPosition.CenterParent, MinimumSize = new System.Drawing.Size(760, 560) };
                Branding.Apply(form);
                var display = new TextBox { Text = report, ReadOnly = true, Multiline = true, WordWrap = false,
                    ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, BackColor = FlightTheme.Surface,
                    ForeColor = FlightTheme.Text, Font = FlightTheme.Mono(10), BorderStyle = BorderStyle.None,
                    AccessibleName = "Storage usage report" };
                var compact = new CheckBox { Text = "Save snapshot space on the next Android start (cold boot)",
                    AutoSize = false, Height = 48, Dock = DockStyle.Bottom, ForeColor = FlightTheme.Amber,
                    AccessibleName = "Enable compact Android startup",
                    Checked = File.Exists(Path.Combine(root, "compact-boot.enabled")) };
                compact.CheckedChanged += delegate {
                    try {
                        AssertNoLinks(root, root);
                        string marker = Path.Combine(root, "compact-boot.enabled");
                        AssertNoLinks(root, marker);
                        using (new FileStream(Path.Combine(root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                        {
                            if (compact.Checked) File.WriteAllText(marker, "Use -no-snapshot on the next emulator process start.\r\n");
                            else if (File.Exists(marker)) File.Delete(marker);
                        }
                    } catch (Exception ex) { MessageBox.Show(form, ex.Message, "APK Runner"); form.Close(); }
                };
                form.Controls.Add(display);
                form.Controls.Add(compact);
                form.Shown += delegate { FlightTheme.DarkTitle(form); };
                return form;
        }

        public static void Show(IWin32Window owner, string report, string root)
        {
            using (Form form = CreateWindow(report, root)) form.ShowDialog(owner);
        }
    }
}
