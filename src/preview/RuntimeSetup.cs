using System;
using System.IO;
using System.Windows.Forms;

namespace APKRunner
{
    internal static class RuntimeSetup
    {
        public static bool EnsureConfigured()
        {
            if (!AppUpdates.IsManaged) return true;
            string registration = Path.Combine(RuntimePaths.SettingsDirectory, "runtime-path.txt");
            if (File.Exists(registration)) return true;
            string existingDefault = Path.Combine(RuntimePaths.SettingsDirectory, "runtime");
            string root = existingDefault;
            if (!Directory.Exists(existingDefault)) {
                var choice = MessageBox.Show("Do you already have APK Runner with installed apps or saved data?\n\nYes: select the existing folder to keep your apps and data.\nNo: create a new installation.\nCancel: exit without changing any data.", "APK Runner - keep your apps", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return false;
                if (choice == DialogResult.Yes) {
                    using (var picker = new FolderBrowserDialog { Description = "Select your existing APK Runner folder or its runtime folder", ShowNewFolderButton = false }) {
                        if (picker.ShowDialog() != DialogResult.OK) return false;
                        root = picker.SelectedPath;
                        if (Directory.Exists(Path.Combine(root, "runtime", "avd"))) root = Path.Combine(root, "runtime");
                        if (!Directory.Exists(Path.Combine(root, "avd")))
                            throw new InvalidOperationException("The selected folder does not contain your existing Android devices. No data has been moved or deleted. Restart and select the original runtime folder.");
                    }
                    // Validate the registered location before writing any configuration.
                    string managedRoot = Velopack.Locators.VelopackLocator.Current.RootAppDir.TrimEnd('\\');
                    if (root.Equals(managedRoot, StringComparison.OrdinalIgnoreCase) || root.StartsWith(managedRoot + "\\", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Select the original folder outside the new APK Runner installation.");
                } else Directory.CreateDirectory(root);
            }
            Directory.CreateDirectory(RuntimePaths.SettingsDirectory);
            using (var file = new FileStream(registration, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file)) writer.Write(Path.GetFullPath(root));
            return true;
        }
    }
}
