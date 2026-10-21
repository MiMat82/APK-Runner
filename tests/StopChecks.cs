using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace APKRunner
{
    internal static class StopChecks
    {
        static int checks;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
            Console.WriteLine("PASS " + message);
        }

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path)) return Convert.ToBase64String(sha.ComputeHash(stream));
        }

        sealed class Fixture
        {
            public readonly string Root;
            public readonly string AdbFolder;
            public readonly Runtime Runtime;
            readonly Dictionary<string, string> protectedHashes = new Dictionary<string, string>();

            public Fixture(string parent, string name, params string[] devices)
            {
                Root = Path.Combine(parent, name);
                AdbFolder = Path.Combine(Root, "android-sdk", "platform-tools");
                Directory.CreateDirectory(AdbFolder);
                File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(AdbFolder, "adb.exe"));
                File.WriteAllLines(Path.Combine(AdbFolder, "devices.txt"), devices, new UTF8Encoding(false));
                foreach (string device in new[] { "APKRunner", "APKRunnerTV" }) {
                    string folder = Path.Combine(Root, "avd", device + ".avd");
                    Directory.CreateDirectory(folder);
                    foreach (string file in new[] { "userdata-qemu.img.qcow2", "encryptionkey.img", "config.ini" }) {
                        string path = Path.Combine(folder, file);
                        File.WriteAllText(path, "PRESERVE " + name + " " + device + " " + file);
                        protectedHashes.Add(path, Hash(path));
                    }
                }
                Runtime = new Runtime(Root);
            }

            public string[] Calls { get { string path = Path.Combine(AdbFolder, "calls.txt"); return File.Exists(path) ? File.ReadAllLines(path) : new string[0]; } }
            public string[] Devices { get { return File.ReadAllLines(Path.Combine(AdbFolder, "devices.txt")); } }
            public string[] Kills { get { return Calls.Where(line => line.EndsWith(" emu kill", StringComparison.Ordinal)).ToArray(); } }
            public void CheckProtected()
            {
                Check(protectedHashes.All(entry => File.Exists(entry.Key) && Hash(entry.Key) == entry.Value), "Stop preserves both devices' installed-app, encryption and configuration bytes: " + Path.GetFileName(Root));
            }
        }

        // This process is copied into each fixture as adb.exe. It never invokes the real SDK or Android.
        static int FakeAdb(string[] args)
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            string statePath = Path.Combine(folder, "devices.txt");
            File.AppendAllText(Path.Combine(folder, "calls.txt"), string.Join(" ", args) + "\n");
            string[] devices = File.ReadAllLines(statePath);
            if (args.Length == 1 && args[0] == "devices") {
                if (File.Exists(Path.Combine(folder, "fail-devices"))) { Console.Error.WriteLine("Fixture device enumeration failed"); return 1; }
                Console.WriteLine("List of devices attached");
                foreach (string row in devices) Console.WriteLine(row.Split('|')[0] + "\tdevice");
                return 0;
            }
            if (args.Length >= 4 && args[0] == "-s" && args[2] == "emu") {
                string serial = args[1];
                string selected = devices.FirstOrDefault(row => row.Split('|')[0] == serial);
                if (selected == null) { Console.Error.WriteLine("Fixture device not found"); return 1; }
                if (args.Length == 5 && args[3] == "avd" && args[4] == "name") {
                    if (selected.Split('|')[1] == "QUERY_ERROR") { Console.Error.WriteLine("Fixture identity query failed"); return 1; }
                    Console.WriteLine(selected.Split('|')[1]); Console.WriteLine("OK"); return 0;
                }
                if (args.Length == 4 && args[3] == "kill") {
                    File.WriteAllLines(statePath, devices.Where(row => row.Split('|')[0] != serial), new UTF8Encoding(false));
                    Console.WriteLine("OK: killing emulator, bye bye"); return 0;
                }
            }
            Console.Error.WriteLine("Unexpected fixture adb command");
            return 2;
        }

        static bool Refuses(Action action)
        {
            try { action(); return false; } catch (Exception) { return true; }
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location).Equals("adb", StringComparison.OrdinalIgnoreCase)) return FakeAdb(args);
            try {
                string root = Path.GetFullPath(args[0]);
                Directory.CreateDirectory(root);

                var phone = new Fixture(root, "phone-only", "emulator-5580|APKRunner");
                phone.Runtime.Stop();
                Check(phone.Kills.SequenceEqual(new[] { "-s emulator-5580 emu kill" }) && phone.Devices.Length == 0, "Stop gracefully closes only the managed phone device");
                phone.CheckProtected();

                var tv = new Fixture(root, "legacy-only", "emulator-5582|APKRunnerTV");
                tv.Runtime.Stop();
                Check(tv.Kills.SequenceEqual(new[] { "-s emulator-5582 emu kill" }) && tv.Devices.Length == 0, "Stop gracefully closes the legacy x86/TV device when the phone is absent");
                tv.CheckProtected();

                var both = new Fixture(root, "both-and-unrelated", "emulator-5580|APKRunner", "emulator-5582|APKRunnerTV", "emulator-5554|UnrelatedProject");
                both.Runtime.Stop();
                Check(both.Kills.SequenceEqual(new[] { "-s emulator-5580 emu kill", "-s emulator-5582 emu kill" }), "Stop closes both managed devices");
                Check(both.Devices.SequenceEqual(new[] { "emulator-5554|UnrelatedProject" }), "Stop preserves an unrelated emulator");
                string[] commands = both.Calls;
                int firstKill = Array.FindIndex(commands, line => line.EndsWith(" emu kill", StringComparison.Ordinal));
                Check(Array.IndexOf(commands, "-s emulator-5580 emu avd name") < firstKill && Array.IndexOf(commands, "-s emulator-5582 emu avd name") < firstKill, "Both device identities are checked before the first stop command");
                both.CheckProtected();

                var none = new Fixture(root, "already-stopped");
                none.Runtime.Stop(); none.Runtime.Stop();
                Check(none.Kills.Length == 0 && none.Devices.Length == 0, "Stopping already stopped devices is harmless and repeatable");
                none.CheckProtected();

                var wrongPhone = new Fixture(root, "wrong-phone", "emulator-5580|OtherPhone", "emulator-5582|APKRunnerTV");
                Check(Refuses(wrongPhone.Runtime.Stop), "Wrong phone AVD identity refuses the operation");
                Check(wrongPhone.Kills.Length == 0 && wrongPhone.Devices.Length == 2, "Wrong phone identity preserves both running devices");
                wrongPhone.CheckProtected();

                var wrongTv = new Fixture(root, "wrong-legacy", "emulator-5580|APKRunner", "emulator-5582|OtherTV");
                Check(Refuses(wrongTv.Runtime.Stop), "Wrong legacy AVD identity refuses the operation");
                Check(wrongTv.Kills.Length == 0 && wrongTv.Devices.Length == 2, "Wrong second device prevents a partial stop of the valid phone");
                wrongTv.CheckProtected();

                var similarName = new Fixture(root, "similar-name", "emulator-5580|APKRunner-clone");
                Check(Refuses(similarName.Runtime.Stop) && similarName.Kills.Length == 0, "A similar AVD name is not accepted as the managed identity");
                similarName.CheckProtected();

                var similarSerial = new Fixture(root, "similar-serial", "emulator-55800|APKRunner", "emulator-55820|APKRunnerTV");
                similarSerial.Runtime.Stop();
                Check(similarSerial.Kills.Length == 0 && similarSerial.Calls.Length == 1 && similarSerial.Devices.Length == 2, "Serial prefixes do not match APK Runner's exact ports");
                similarSerial.CheckProtected();

                var locked = new Fixture(root, "operation-locked", "emulator-5580|APKRunner");
                using (var guard = new FileStream(Path.Combine(locked.Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
                    Check(Refuses(locked.Runtime.Stop), "Concurrent runtime operation refuses Stop Android");
                }
                Check(locked.Calls.Length == 0 && locked.Devices.Length == 1, "Operation lock refusal occurs before contacting adb");
                locked.CheckProtected();

                var failedList = new Fixture(root, "enumeration-failure", "emulator-5580|APKRunner");
                File.WriteAllText(Path.Combine(failedList.AdbFolder, "fail-devices"), "fixture");
                Check(Refuses(failedList.Runtime.Stop) && failedList.Kills.Length == 0, "Failed device enumeration cannot stop any emulator");
                failedList.CheckProtected();

                var failedIdentity = new Fixture(root, "identity-failure", "emulator-5580|APKRunner", "emulator-5582|QUERY_ERROR");
                Check(Refuses(failedIdentity.Runtime.Stop) && failedIdentity.Kills.Length == 0, "Failed second identity query prevents a partial stop");
                failedIdentity.CheckProtected();

                var missingAdb = new Fixture(root, "missing-adb");
                File.Delete(Path.Combine(missingAdb.AdbFolder, "adb.exe"));
                missingAdb.Runtime.Stop();
                Check(missingAdb.Calls.Length == 0, "An uninstalled runtime with no adb can be stopped harmlessly");
                missingAdb.CheckProtected();

                Console.WriteLine("Passed " + checks + " Stop Android checks using isolated fake adb child processes.");
                return 0;
            } catch (Exception error) {
                Console.Error.WriteLine(error);
                return 1;
            }
        }
    }
}
