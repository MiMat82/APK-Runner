using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace APKRunner
{
    internal static class ShellBranding
    {
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(uint change, uint flags, IntPtr item1, IntPtr item2);
        static readonly object IconLock = new object();

        public static string EnsureIcon(string settingsDirectory)
        {
            lock (IconLock) {
                Directory.CreateDirectory(settingsDirectory);
                string path = Path.Combine(settingsDirectory, "apk-runner.ico");
                byte[] bytes;
                using (var source = Branding.Resource("APKRunner.BrandIcon.ico"))
                using (var target = new MemoryStream()) { source.CopyTo(target); bytes = target.ToArray(); }
                bool same = File.Exists(path) && Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(bytes);
                if (!same) File.WriteAllBytes(path, bytes);
                // Explorer does not inherit a packaged parent process's filesystem virtualization.
                return NativeFileLocation.Resolve(path);
            }
        }

        public static string RepairShortcuts(string launcher, string settingsDirectory)
        {
            launcher = NativeFileLocation.Resolve(launcher);
            string icon = EnsureIcon(settingsDirectory);
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            try {
                string[] links = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "APK Runner.lnk"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "APK Runner.lnk")
                };
                foreach (string path in links) {
                    if (!File.Exists(path)) continue;
                    object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                    try {
                        Type type = link.GetType();
                        type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { launcher });
                        type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { Path.GetDirectoryName(launcher) });
                        type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { icon + ",0" });
                        type.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
                        IntPtr native = Marshal.StringToHGlobalUni(path);
                        try { SHChangeNotify(0x2000, 0x0005, native, IntPtr.Zero); } finally { Marshal.FreeHGlobal(native); }
                    } finally { Marshal.FinalReleaseComObject(link); }
                }
            } finally { Marshal.FinalReleaseComObject(shell); }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            return icon;
        }
    }
}
