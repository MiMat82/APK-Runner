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
                var choice = MessageBox.Show("Har du allerede APK Runner med installerede apps eller gemte data?\n\nJa: vælg den eksisterende mappe, så dine apps og data genbruges.\nNej: opret en ny installation.\nAnnuller: afslut uden at ændre data.", "APK Runner - behold dine apps", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return false;
                if (choice == DialogResult.Yes) {
                    using (var picker = new FolderBrowserDialog { Description = "Vælg den eksisterende APK Runner-mappe eller dens runtime-mappe", ShowNewFolderButton = false }) {
                        if (picker.ShowDialog() != DialogResult.OK) return false;
                        root = picker.SelectedPath;
                        if (Directory.Exists(Path.Combine(root, "runtime", "avd"))) root = Path.Combine(root, "runtime");
                        if (!Directory.Exists(Path.Combine(root, "avd")))
                            throw new InvalidOperationException("Den valgte mappe indeholder ikke dine eksisterende Android-enheder. Ingen data er flyttet eller slettet. Start igen og vælg den oprindelige runtime-mappe.");
                    }
                    // Validate the registered location before writing any configuration.
                    string managedRoot = Velopack.Locators.VelopackLocator.Current.RootAppDir.TrimEnd('\\');
                    if (root.Equals(managedRoot, StringComparison.OrdinalIgnoreCase) || root.StartsWith(managedRoot + "\\", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Vælg den oprindelige mappe uden for den nye APK Runner-installation.");
                } else Directory.CreateDirectory(root);
            }
            Directory.CreateDirectory(RuntimePaths.SettingsDirectory);
            using (var file = new FileStream(registration, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file)) writer.Write(Path.GetFullPath(root));
            return true;
        }
    }
}
