using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal static class Forwarder
{
    // Windows command-line quoting, including backslashes before quotes and the closing quote.
    static string Quote(string argument)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char character in argument) {
            if (character == '\\') { slashes++; continue; }
            if (character == '"') result.Append('\\', slashes * 2 + 1);
            else result.Append('\\', slashes);
            result.Append(character); slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    [STAThread]
    static void Main(string[] args)
    {
        APKRunner.AppLanguage.Initialize();
        try {
            string registration = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "APKRunnerData", "launcher-path.txt");
            string target = File.ReadAllText(registration).Trim();
            if (!Path.IsPathRooted(target) || !File.Exists(target) || string.Equals(Path.GetFullPath(target), Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The updated APK Runner installation was not found. Your Android data has not been changed.");
            Process.Start(new ProcessStartInfo(target, string.Join(" ", args.Select(Quote))) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) });
        } catch (Exception error) { MessageBox.Show(error.Message, "APK Runner", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
