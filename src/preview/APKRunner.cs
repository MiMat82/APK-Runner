// Recovered from the user-owned V12.9 executable using ILSpy 9.1.
// Historical recovery files are not required to build this maintained source.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Win32;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
namespace APKRunner
{
	internal class Result
	{
		public int Code;

		public string Text;
	}
	internal class Status
	{
		public string Name;

		public string Detail;

		public bool Ready;

		public Status(string n, bool r, string d)
		{
			Name = n;
			Ready = r;
			Detail = d;
		}
	}
	internal class ObbFile
	{
		public string Package;

		public string Name;

		public string Local;
	}
	internal class InstallPayload : IDisposable
	{
		public string[] Apks;

		public List<ObbFile> Obbs = new List<ObbFile>();

		public string Stage;

		public void Dispose()
		{
			if (Stage != null && Directory.Exists(Stage))
			{
				Directory.Delete(Stage, true);
			}
		}
	}
	internal class Runtime
	{
		private struct SYSTEM_INFO
		{
			public ushort architecture;

			public ushort reserved;

			public uint pageSize;

			public IntPtr min;

			public IntPtr max;

			public IntPtr mask;

			public uint count;

			public uint type;

			public uint granularity;

			public ushort level;

			public ushort revision;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct MEMORYSTATUSEX
		{
			public uint dwLength;

			public uint dwMemoryLoad;

			public ulong ullTotalPhys;

			public ulong ullAvailPhys;

			public ulong ullTotalPageFile;

			public ulong ullAvailPageFile;

			public ulong ullTotalVirtual;

			public ulong ullAvailVirtual;

			public ulong ullAvailExtendedVirtual;
		}

		public const string ImageId = "system-images;android-35;google_apis;x86_64";

		public const string AvdName = "APKRunner";

		public const string TvImageId = "system-images;android-34;android-tv;x86";

		public const string TvAvdName = "APKRunnerTV";

		public readonly string Root;

		public readonly string Sdk;

		public readonly string Java;

		public readonly string Avd;

		public string ActiveSerial;

		public bool HighPerformance;

		public bool Portrait;
		public bool CompactBoot { get { return File.Exists(Path.Combine(Root, "compact-boot.enabled")); } }
		public bool FastRendering {
            get {
                string path = Path.Combine(Root, "fast-rendering.enabled");
                try { return File.Exists(path) && File.ReadAllText(path).Trim().Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase); }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
            }
        }

		public Action<string> Log = delegate
		{
		};

		public string Adb
		{
			get
			{
				return Path.Combine(Sdk, "platform-tools", "adb.exe");
			}
		}

		public string Emulator
		{
			get
			{
				return Path.Combine(Sdk, "emulator", "emulator.exe");
			}
		}

		public string Aapt
		{
			get
			{
				return Path.Combine(Sdk, "build-tools", "35.0.0", "aapt.exe");
			}
		}

		public string Scrcpy
		{
			get
			{
				return Path.Combine(Root, "scrcpy", "scrcpy.exe");
			}
		}

		private string Tools
		{
			get
			{
				return Path.Combine(Sdk, "cmdline-tools", "12.0");
			}
		}

		public Runtime(string root)
		{
			Root = Path.GetFullPath(root);
			Sdk = Path.Combine(Root, "android-sdk");
			Java = Path.Combine(Root, "java", "bin", "java.exe");
			Avd = Path.Combine(Root, "avd");
		}

		[DllImport("kernel32.dll")]
		private static extern void GetNativeSystemInfo(out SYSTEM_INFO info);

		[DllImport("kernel32.dll")]
		private static extern bool IsProcessorFeaturePresent(uint feature);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);

		[DllImport("shell32.dll")]
		public static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

		public static bool IsX64()
		{
			SYSTEM_INFO info;
			GetNativeSystemInfo(out info);
			return info.architecture == 9;
		}

		private static int HostRamMiB()
		{
			MEMORYSTATUSEX status = new MEMORYSTATUSEX
			{
				dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX))
			};
			if (!GlobalMemoryStatusEx(ref status))
			{
				return 8192;
			}
			return (int)(status.ullTotalPhys / 1024 / 1024);
		}

		private static void SetIniValue(List<string> lines, string key, string value)
		{
			int num = lines.FindIndex((string x) => x.TrimStart().StartsWith(key + " =", StringComparison.OrdinalIgnoreCase) || x.TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));
			string text = key + " = " + value;
			if (num >= 0)
			{
				lines[num] = text;
			}
			else
			{
				lines.Add(text);
			}
		}


        public void RepairAvdPointers()
        {
            Directory.CreateDirectory(Root);
            using (new FileStream(Path.Combine(Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                foreach (string name in new[] { "APKRunner", "APKRunnerTV" })
                {
                    string folder = Path.Combine(Avd, name + ".avd");
                    string ini = Path.Combine(Avd, name + ".ini");
                    if (!File.Exists(Path.Combine(folder, "config.ini")) || !File.Exists(ini)) continue;
                    StorageReport.AssertNoLinks(Root, folder);
                    StorageReport.AssertNoLinks(Root, ini);
                    string[] lines = File.ReadAllLines(ini);
                    string wanted = "path=" + folder;
                    if (lines.Any(x => x.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase)) &&
                        !lines.Any(x => x.TrimStart().StartsWith("path.rel=", StringComparison.OrdinalIgnoreCase))) continue;
                    string saved = ini + ".before-portable-" + Guid.NewGuid().ToString("N");
                    File.Copy(ini, saved, false);
                    var repaired = lines.Where(x => !x.TrimStart().StartsWith("path=", StringComparison.OrdinalIgnoreCase) &&
                        !x.TrimStart().StartsWith("path.rel=", StringComparison.OrdinalIgnoreCase)).ToList();
                    repaired.Add(wanted);
                    File.WriteAllLines(ini, repaired, new UTF8Encoding(false));
                    Log("Android-sti opdateret: " + name + ". Tidligere registrering gemt i " + Path.GetFileName(saved));
                }
            }
        }

		public void SetPerformanceMode(bool high)
		{
			HighPerformance = high;
		}

		private bool HasPerformanceProfile(string name)
		{
			string path = Path.Combine(Avd, name + ".avd", "config.ini");
			string wanted = "apkRunner.performanceProfile = " + (HighPerformance ? "high" : "normal");
			if (File.Exists(path))
			{
				return File.ReadAllLines(path).Any((string x) => x.Trim().Equals(wanted, StringComparison.OrdinalIgnoreCase));
			}
			return false;
		}

		private void TuneAvd(string name)
		{
			string path = Path.Combine(Avd, name + ".avd", "config.ini");
			if (File.Exists(path))
			{
				int num = HostRamMiB();
				int num2 = ((!HighPerformance) ? ((num >= 16384) ? 4096 : ((num >= 8192) ? 3072 : 2048)) : ((num >= 16384) ? 6144 : ((num >= 8192) ? 4096 : 2048)));
				int num3 = ((!HighPerformance) ? ((Environment.ProcessorCount >= 12) ? 4 : ((Environment.ProcessorCount >= 6) ? 3 : 2)) : ((Environment.ProcessorCount >= 12) ? 6 : ((Environment.ProcessorCount >= 6) ? 4 : 2)));
				List<string> list = File.ReadAllLines(path).ToList();
				SetIniValue(list, "hw.ramSize", num2 + "M");
				SetIniValue(list, "vm.heapSize", HighPerformance ? "768M" : "512M");
				SetIniValue(list, "hw.cpu.ncore", num3.ToString());
				SetIniValue(list, "hw.gpu.enabled", "yes");
				SetIniValue(list, "hw.gpu.mode", "auto");
				SetIniValue(list, "showDeviceFrame", "no");
				SetIniValue(list, "apkRunner.performanceProfile", HighPerformance ? "high" : "normal");
				File.WriteAllLines(path, list.ToArray(), new UTF8Encoding(false));
				Log("Ydeevneprofil gemt: " + (HighPerformance ? "Høj ydeevne" : "Normal") + " · " + num3 + " CPU-kerner, " + num2 + " MB Android-RAM og automatisk GPU-acceleration.");
			}
		}

		public static bool SupportedWindows()
		{
			using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"))
			{
				int result;
				return IsX64() && registryKey != null && int.TryParse(Convert.ToString(registryKey.GetValue("CurrentBuildNumber")), out result) && result >= 19045;
			}
		}

		public static string Arg(string s)
		{
			if (s.IndexOf('\0') >= 0)
			{
				throw new ArgumentException("Invalid argument");
			}
			StringBuilder stringBuilder = new StringBuilder("\"");
			int num = 0;
			foreach (char c in s)
			{
				switch (c)
				{
				case '\\':
					num++;
					continue;
				case '"':
					stringBuilder.Append('\\', num * 2 + 1);
					stringBuilder.Append(c);
					break;
				default:
					stringBuilder.Append('\\', num);
					stringBuilder.Append(c);
					break;
				}
				num = 0;
			}
			stringBuilder.Append('\\', num * 2);
			stringBuilder.Append('"');
			return stringBuilder.ToString();
		}

		private static string[] AbisFromArchive(ZipArchive archive)
		{
			return (from x in (from e in archive.Entries
					select e.FullName into n
					where n.StartsWith("lib/", StringComparison.OrdinalIgnoreCase) && n.Split('/').Length >= 3
					select n.Split('/')[1]).Distinct(StringComparer.OrdinalIgnoreCase)
				orderby x
				select x).ToArray();
		}

		public static string[] GetNativeAbis(string file)
		{
			try
			{
				using (ZipArchive zipArchive = ZipFile.OpenRead(file))
				{
					if (Path.GetExtension(file).Equals(".apk", StringComparison.OrdinalIgnoreCase))
					{
						return AbisFromArchive(zipArchive);
					}
					if (Path.GetExtension(file).Equals(".xapk", StringComparison.OrdinalIgnoreCase))
					{
						return (from x in zipArchive.Entries.Where((ZipArchiveEntry e) => e.FullName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)).SelectMany(delegate(ZipArchiveEntry e)
							{
								using (Stream stream = e.Open())
								{
									using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
									{
										return AbisFromArchive(archive);
									}
								}
							}).Distinct(StringComparer.OrdinalIgnoreCase)
							orderby x
							select x).ToArray();
					}
					throw new Exception("Vælg en APK- eller XAPK-fil.");
				}
			}
			catch (InvalidDataException)
			{
				throw new Exception("Filen er ikke en gyldig APK- eller XAPK-fil.");
			}
		}

		private InstallPayload Prepare(string file)
		{
			if (Path.GetExtension(file).Equals(".apk", StringComparison.OrdinalIgnoreCase))
			{
				InstallPayload installPayload = new InstallPayload();
				installPayload.Apks = new string[1] { file };
				return installPayload;
			}
			if (!Path.GetExtension(file).Equals(".xapk", StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("Vælg en APK- eller XAPK-fil.");
			}
			string text = Path.Combine(Path.GetTempPath(), "AR", Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(text);
			try
			{
				using (ZipArchive zipArchive = ZipFile.OpenRead(file))
				{
					ZipArchiveEntry[] array = (from e in zipArchive.Entries
						where e.FullName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains("..") && !e.FullName.Contains("\\")
						orderby (!Path.GetFileName(e.FullName).Equals("base.apk", StringComparison.OrdinalIgnoreCase)) ? 1 : 0
						select e).ThenBy((ZipArchiveEntry e) => e.FullName, StringComparer.OrdinalIgnoreCase).ToArray();
					if (array.Length == 0)
					{
						throw new Exception("XAPK-filen indeholder ingen APK-filer.");
					}
					InstallPayload installPayload2 = new InstallPayload();
					installPayload2.Stage = text;
					installPayload2.Apks = new string[array.Length];
					InstallPayload installPayload3 = installPayload2;
					for (int num = 0; num < array.Length; num++)
					{
						installPayload3.Apks[num] = Path.Combine(text, "a" + num + ".apk");
						using (Stream stream = array[num].Open())
						{
							using (FileStream destination = File.Create(installPayload3.Apks[num]))
							{
								stream.CopyTo(destination);
							}
						}
					}
					foreach (ZipArchiveEntry item in zipArchive.Entries.Where((ZipArchiveEntry e) => e.FullName.StartsWith("Android/obb/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".obb", StringComparison.OrdinalIgnoreCase)))
					{
						string[] array2 = item.FullName.Split('/');
						if (array2.Length != 4 || !Regex.IsMatch(array2[2], "^[A-Za-z0-9_.]+$") || Path.GetFileName(array2[3]) != array2[3])
						{
							continue;
						}
						string text2 = Path.Combine(text, "o" + installPayload3.Obbs.Count + ".obb");
						using (Stream stream2 = item.Open())
						{
							using (FileStream destination2 = File.Create(text2))
							{
								stream2.CopyTo(destination2);
							}
						}
						installPayload3.Obbs.Add(new ObbFile
						{
							Package = array2[2],
							Name = array2[3],
							Local = text2
						});
					}
					return installPayload3;
				}
			}
			catch
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, true);
				}
				throw;
			}
		}

		private ProcessStartInfo StartInfo(string file, string[] args)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(file, string.Join(" ", args.Select(Arg)));
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			processStartInfo.RedirectStandardInput = true;
			ProcessStartInfo processStartInfo2 = processStartInfo;
			processStartInfo2.EnvironmentVariables["JAVA_HOME"] = Path.GetDirectoryName(Path.GetDirectoryName(Java));
			processStartInfo2.EnvironmentVariables["ANDROID_HOME"] = Sdk;
			processStartInfo2.EnvironmentVariables["ANDROID_SDK_ROOT"] = Sdk;
			processStartInfo2.EnvironmentVariables["ANDROID_AVD_HOME"] = Avd;
			processStartInfo2.EnvironmentVariables["ANDROID_USER_HOME"] = Path.Combine(Root, "android-user");
			processStartInfo2.EnvironmentVariables["ADB"] = Adb;
			return processStartInfo2;
		}

		public Result Run(string file, string[] args, int seconds = 60, string input = null)
		{
			using (Process process = new Process())
			{
				process.StartInfo = StartInfo(file, args);
				process.Start();
				Task<string> task = process.StandardOutput.ReadToEndAsync();
				Task<string> task2 = process.StandardError.ReadToEndAsync();
				if (input != null)
				{
					process.StandardInput.Write(input);
				}
				process.StandardInput.Close();
				if (!process.WaitForExit(seconds * 1000))
				{
					try
					{
						process.Kill();
					}
					catch
					{
					}
					throw new TimeoutException(Path.GetFileName(file) + " timed out. See logs.");
				}
				Task.WaitAll(task, task2);
				Result result = new Result();
				result.Code = process.ExitCode;
				result.Text = task.Result + task2.Result;
				return result;
			}
		}

		private Result Must(string file, string[] args, int seconds = 60, string input = null)
		{
			Result result = Run(file, args, seconds, input);
			Log(result.Text);
			if (result.Code != 0)
			{
				throw new Exception(Path.GetFileName(file) + " failed (" + result.Code + ").\r\n" + result.Text);
			}
			return result;
		}

		private bool Probe(string file, string[] args, string expected)
		{
			try
			{
				if (!File.Exists(file))
				{
					return false;
				}
				Result result = Run(file, args, 25);
				return result.Code == 0 && result.Text.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return false;
			}
		}

		public List<Status> Check()
		{
			List<Status> list = new List<Status>();
			list.Add(new Status("Windows x64", SupportedWindows(), SupportedWindows() ? "Windows 10 22H2 / Windows 11 x64" : "Kræver Windows 10 22H2 eller Windows 11 x64"));
			bool r = false;
			string d = "Installer emulatoren for at kontrollere hypervisoren";
			if (File.Exists(Emulator))
			{
				try
				{
					Result result = Run(Emulator, new string[1] { "-accel-check" }, 30);
					r = result.Code == 0;
					d = result.Text.Trim();
				}
				catch (Exception ex)
				{
					d = ex.Message;
				}
			}
			else if (!IsProcessorFeaturePresent(21u))
			{
				d = "CPU-virtualisering kunne ikke bekræftes; emulatoren afgør om systemet er klar";
			}
			list.Add(new Status("Virtualisering", r, d));
			list.Add(new Status("Privat Java 17", Probe(Java, new string[1] { "-version" }, "version \"17."), "runtime\\java"));
			bool r2 = false;
			try
			{
				if (File.Exists(Path.Combine(Tools, "lib", "sdkmanager-classpath.jar")))
				{
					r2 = SdkRun(false, new string[1] { "--version" }, 60).Code == 0;
				}
			}
			catch
			{
			}
			list.Add(new Status("SDK værktøjer", r2, "Fast version 12.0 til Java 17"));
			list.Add(new Status("ADB", Probe(Adb, new string[1] { "version" }, "Android Debug Bridge"), "Android platform-tools"));
			list.Add(new Status("Android Emulator", Probe(Emulator, new string[1] { "-version" }, "Android emulator"), "Privat emulator"));
			list.Add(new Status("AAPT", Probe(Aapt, new string[1] { "version" }, "Android Asset Packaging Tool"), "Build-tools 35.0.0"));
			list.Add(new Status("Android system", ImagePresent(), "Android 15 Google APIs / x86_64 / ARM-oversættelse"));
			list.Add(new Status("Android enhed", AvdPresent(), "APKRunner"));
			list.Add(new Status("App-vindue", Probe(Scrcpy, new string[1] { "--version" }, "scrcpy"), "scrcpy"));
			return list;
		}

		private bool ImagePresent()
		{
			string path = Path.Combine(Sdk, "system-images", "android-35", "google_apis", "x86_64");
			if (File.Exists(Path.Combine(path, "package.xml")) && File.Exists(Path.Combine(path, "system.img")))
			{
				return File.Exists(Path.Combine(path, "ramdisk.img"));
			}
			return false;
		}

		private bool AvdPresent()
		{
			string path = Path.Combine(Avd, "APKRunner.ini");
			string path2 = Path.Combine(Avd, "APKRunner.avd", "config.ini");
			if (!File.Exists(path) || !File.Exists(path2))
			{
				return false;
			}
			string text = File.ReadAllText(path2).Replace(" ", "");
			if (text.Contains("abi.type=x86_64") && text.Contains("image.sysdir.1=system-images\\android-35\\google_apis\\x86_64\\"))
			{
				return File.ReadAllLines(path).Any((string x) => x.Trim().Equals("path=" + Path.Combine(Avd, "APKRunner.avd"), StringComparison.OrdinalIgnoreCase));
			}
			return false;
		}

		private bool TvPresent()
		{
			string path = Path.Combine(Sdk, "system-images", "android-34", "android-tv", "x86");
			string path2 = Path.Combine(Avd, "APKRunnerTV.ini");
			string path3 = Path.Combine(Avd, "APKRunnerTV.avd", "config.ini");
			if (!File.Exists(Path.Combine(path, "package.xml")) || !File.Exists(Path.Combine(path, "system.img")) || !File.Exists(path2) || !File.Exists(path3))
			{
				return false;
			}
			string text = File.ReadAllText(path3).Replace(" ", "");
			if (text.Contains("abi.type=x86") && text.Contains("image.sysdir.1=system-images\\android-34\\android-tv\\x86\\"))
			{
				return File.ReadAllLines(path2).Any((string x) => x.Trim().Equals("path=" + Path.Combine(Avd, "APKRunnerTV.avd"), StringComparison.OrdinalIgnoreCase));
			}
			return false;
		}

		private Result SdkRun(bool avd, string[] args, int seconds, string input = null)
		{
			List<string> list = new List<string>();
			list.Add("-D" + (avd ? "com.android.sdkmanager.toolsdir=" : "com.android.sdklib.toolsdir=") + Tools);
			list.Add("-cp");
			list.Add(Path.Combine(Tools, "lib", avd ? "avdmanager-classpath.jar" : "sdkmanager-classpath.jar"));
			list.Add(avd ? "com.android.sdklib.tool.AvdManagerCli" : "com.android.sdklib.tool.sdkmanager.SdkManagerCli");
			List<string> list2 = list;
			list2.AddRange(args);
			if (!avd)
			{
				list2.Add("--sdk_root=" + Sdk);
			}
			return Run(Java, list2.ToArray(), seconds, input);
		}

		private void SdkMust(bool avd, string[] args, int seconds, string input = null)
		{
			Result result = SdkRun(avd, args, seconds, input);
			Log(result.Text);
			if (result.Code != 0)
			{
				throw new Exception("Android SDK fejl:\r\n" + result.Text);
			}
		}

		private void StopManagedEmulatorForUpgrade()
		{
			if (!File.Exists(Adb))
			{
				return;
			}
			try
			{
				string text = Run(Adb, new string[1] { "devices" }, 20).Text;
				if (!text.Contains("emulator-5580"))
				{
					return;
				}
				string text2 = Run(Adb, new string[5] { "-s", "emulator-5580", "emu", "avd", "name" }, 20).Text;
				if (!text2.Split('\n').Any((string x) => x.Trim() == "APKRunner"))
				{
					return;
				}
				Log("Lukker den gamle Android-enhed for at opgradere den...");
				Must(Adb, new string[4] { "-s", "emulator-5580", "emu", "kill" });
				DateTime dateTime = DateTime.UtcNow.AddSeconds(45.0);
				while (DateTime.UtcNow < dateTime)
				{
					if (!Run(Adb, new string[1] { "devices" }, 15).Text.Contains("emulator-5580"))
					{
						return;
					}
					Thread.Sleep(1000);
				}
				throw new Exception("Android-enheden lukkede ikke inden for 45 sekunder.");
			}
			catch (Exception ex)
			{
				throw new Exception("Kunne ikke lukke APK Runners gamle Android-enhed før opgradering. Luk APK Runner og prøv igen.\r\n\r\n" + ex.Message);
			}
		}

		private void StopManagedEmulator(string serial, string name)
		{
			if (!File.Exists(Adb))
			{
				return;
			}
			string text = Run(Adb, new string[1] { "devices" }, 20).Text;
			if (!text.Contains(serial) || !Run(Adb, new string[5] { "-s", serial, "emu", "avd", "name" }, 20).Text.Split('\n').Any((string x) => x.Trim() == name))
			{
				return;
			}
			Must(Adb, new string[4] { "-s", serial, "emu", "kill" });
			DateTime dateTime = DateTime.UtcNow.AddSeconds(45.0);
			while (DateTime.UtcNow < dateTime)
			{
				if (!Run(Adb, new string[1] { "devices" }, 15).Text.Contains(serial))
				{
					return;
				}
				Thread.Sleep(1000);
			}
			throw new Exception("Android TV-enheden lukkede ikke inden for 45 sekunder.");
		}

		private void EnsureTvRuntime()
		{
			if (TvPresent())
			{
				return;
			}
			Log("Installerer Android TV x86 til denne 32-bit x86-app...");
			if (!File.Exists(Path.Combine(Sdk, "system-images", "android-34", "android-tv", "x86", "system.img")))
			{
				SdkMust(false, new string[1] { "system-images;android-34;android-tv;x86" }, 3600, "y\n");
			}
			string text = Path.Combine(Avd, "APKRunnerTV.avd");
			string text2 = Path.Combine(Avd, "APKRunnerTV.ini");
			if (!TvPresent())
			{
				StopManagedEmulator("emulator-5582", "APKRunnerTV");
				Directory.CreateDirectory(Path.Combine(Root, "backups"));
				string text3 = ".previous-" + Guid.NewGuid().ToString("N");
				if (Directory.Exists(text))
				{
					Directory.Move(text, Path.Combine(Root, "backups", "APKRunnerTV" + text3 + ".avd"));
				}
				if (File.Exists(text2))
				{
					File.Move(text2, Path.Combine(Root, "backups", "APKRunnerTV" + text3 + ".ini"));
				}
				SdkMust(true, new string[8] { "create", "avd", "--name", "APKRunnerTV", "--package", "system-images;android-34;android-tv;x86", "--path", text }, 180, "no\n");
				File.WriteAllText(text2, "avd.ini.encoding=UTF-8\r\npath=" + text + "\r\ntarget=android-34\r\n", new UTF8Encoding(false));
				if (!TvPresent())
				{
					throw new Exception("Android TV x86-enheden blev ikke oprettet korrekt.");
				}
			}
		}

		private static WebClient Client()
		{
			WebClient webClient = new WebClient();
			webClient.Headers[HttpRequestHeader.UserAgent] = "APKRunner/11";
			return webClient;
		}

		private string Get(string url)
		{
			using (WebClient webClient = Client())
			{
				return webClient.DownloadString(url);
			}
		}

		private void Download(string url, string file, string hash, string algorithm)
		{
			if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("HTTPS required");
			}
			Log("Henter " + url);
			using (WebClient webClient = Client())
			{
				int percent = -10;
				webClient.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
				{
					if (e.ProgressPercentage >= percent + 10)
					{
						percent = e.ProgressPercentage;
						Log("Download: " + percent + " % (" + e.BytesReceived / 1048576 + " MB)");
					}
				};
				Task task = webClient.DownloadFileTaskAsync(new Uri(url), file);
				if (!task.Wait(TimeSpan.FromMinutes(30.0)))
				{
					webClient.CancelAsync();
					throw new TimeoutException("Download tog mere end 30 minutter. Prøv igen.");
				}
			}
			using (HashAlgorithm hashAlgorithm = ((algorithm == "SHA1") ? ((HashAlgorithm)SHA1.Create()) : ((HashAlgorithm)SHA256.Create())))
			{
				using (FileStream inputStream = File.OpenRead(file))
				{
					string text = BitConverter.ToString(hashAlgorithm.ComputeHash(inputStream)).Replace("-", "");
					if (!text.Equals(hash, StringComparison.OrdinalIgnoreCase))
					{
						throw new Exception("Download checksum mismatch: " + Path.GetFileName(file));
					}
				}
			}
		}

		private void InstallZip(string url, string hash, string alg, string destination, string marker)
		{
			string text = Path.Combine(Path.GetTempPath(), "AR");
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(text2);
			string text3 = Path.Combine(text2, "d.zip");
			string text4 = Path.Combine(text2, "u");
			try
			{
				Download(url, text3, hash, alg);
				ZipFile.ExtractToDirectory(text3, text4);
				string text5 = Directory.GetFiles(text4, marker, SearchOption.AllDirectories).FirstOrDefault();
				if (text5 == null)
				{
					throw new Exception("Archive missing " + marker);
				}
				string directoryName = Path.GetDirectoryName(text5);
				if (marker == "java.exe" || marker == "sdkmanager.bat")
				{
					directoryName = Path.GetDirectoryName(directoryName);
				}
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				Directory.CreateDirectory(Path.Combine(Root, "backups"));
				string text6 = Path.Combine(Root, "backups", Path.GetFileName(destination) + "-" + Guid.NewGuid().ToString("N"));
				if (Directory.Exists(destination))
				{
					Directory.Move(destination, text6);
				}
				try
				{
					Directory.Move(directoryName, destination);
				}
				catch
				{
					if (Directory.Exists(text6) && !Directory.Exists(destination))
					{
						Directory.Move(text6, destination);
					}
					throw;
				}
			}
			finally
			{
				if (Directory.Exists(text2))
				{
					Directory.Delete(text2, true);
				}
			}
		}

		public void Install(Action<List<Status>> update)
		{
			if (!SupportedWindows())
			{
				throw new Exception("Denne udgave kræver Windows 10 22H2 eller Windows 11 x64.");
			}
			Directory.CreateDirectory(Root);
			Directory.CreateDirectory(Sdk);
			Directory.CreateDirectory(Avd);
			Directory.CreateDirectory(Path.Combine(Root, "android-user"));
			using (new FileStream(Path.Combine(Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
			{
				List<Status> list = Check();
				update(list);
				if (!list.First((Status x) => x.Name == "Privat Java 17").Ready)
				{
					object obj = new JavaScriptSerializer().DeserializeObject(Get("https://api.adoptium.net/v3/assets/latest/17/hotspot?architecture=x64&image_type=jre&os=windows&vendor=eclipse"));
					Dictionary<string, object> dictionary = (Dictionary<string, object>)((object[])obj)[0];
					Dictionary<string, object> dictionary2 = (Dictionary<string, object>)dictionary["binary"];
					Dictionary<string, object> dictionary3 = (Dictionary<string, object>)dictionary2["package"];
					InstallZip((string)dictionary3["link"], (string)dictionary3["checksum"], "SHA256", Path.Combine(Root, "java"), "java.exe");
					if (!Probe(Java, new string[1] { "-version" }, "version \"17."))
					{
						throw new Exception("Den private Java 17 kan ikke starte.");
					}
				}
				update(Check());
				if (!list.First((Status x) => x.Name == "SDK værktøjer").Ready)
				{
					XDocument xDocument = XDocument.Parse(Get("https://dl.google.com/android/repository/repository2-1.xml"));
					XElement xElement = xDocument.Descendants().First((XElement x) => x.Name.LocalName == "remotePackage" && (string)x.Attribute("path") == "cmdline-tools;12.0");
					XElement xElement2 = xElement.Descendants().First((XElement x) => x.Name.LocalName == "archive" && x.Elements().Any((XElement e) => e.Name.LocalName == "host-os" && e.Value == "windows"));
					string value = xElement2.Descendants().First((XElement x) => x.Name.LocalName == "url").Value;
					XElement xElement3 = xElement2.Descendants().First((XElement x) => x.Name.LocalName == "checksum");
					InstallZip("https://dl.google.com/android/repository/" + value, xElement3.Value, "SHA1", Tools, "sdkmanager.bat");
				}
				SdkMust(false, new string[1] { "--licenses" }, 180, string.Concat(Enumerable.Repeat("y\n", 100)));
				Dictionary<string, string> dictionary4 = new Dictionary<string, string>();
				dictionary4.Add("ADB", "platform-tools");
				dictionary4.Add("Android Emulator", "emulator");
				dictionary4.Add("AAPT", "build-tools;35.0.0");
				dictionary4.Add("Android system", "system-images;android-35;google_apis;x86_64");
				Dictionary<string, string> dictionary5 = dictionary4;
				list = Check();
				foreach (KeyValuePair<string, string> p in dictionary5)
				{
					List<Status> source = list;
					Func<Status, bool> predicate = delegate(Status x)
					{
						string name = x.Name;
						KeyValuePair<string, string> keyValuePair5 = p;
						return name == keyValuePair5.Key;
					};
					if (!source.First(predicate).Ready)
					{
						Action<string> log = Log;
						KeyValuePair<string, string> keyValuePair = p;
						log("Installerer " + keyValuePair.Key);
						string sdk = Sdk;
						KeyValuePair<string, string> keyValuePair2 = p;
						string text = Path.Combine(sdk, keyValuePair2.Value.Replace(';', Path.DirectorySeparatorChar));
						if (Directory.Exists(text))
						{
							Directory.CreateDirectory(Path.Combine(Root, "backups"));
							string root = Root;
							KeyValuePair<string, string> keyValuePair3 = p;
							Directory.Move(text, Path.Combine(root, "backups", keyValuePair3.Key.Replace(" ", "-") + "-" + Guid.NewGuid().ToString("N")));
						}
						string[] array = new string[1];
						KeyValuePair<string, string> keyValuePair4 = p;
						array[0] = keyValuePair4.Value;
						SdkMust(false, array, 3600, "y\n");
						update(Check());
					}
				}
				if (!AvdPresent())
				{
					string text2 = Path.Combine(Avd, "APKRunner.avd");
					string text3 = Path.Combine(Avd, "APKRunner.ini");
					if (File.Exists(Path.Combine(text2, "config.ini")) && File.ReadAllText(Path.Combine(text2, "config.ini")).Contains("abi.type=x86_64"))
					{
						File.WriteAllText(text3, "avd.ini.encoding=UTF-8\r\npath=" + text2 + "\r\ntarget=android-35\r\n", new UTF8Encoding(false));
					}
					if (!AvdPresent())
					{
						StopManagedEmulatorForUpgrade();
						string text4 = ".previous-" + Guid.NewGuid().ToString("N");
						if (Directory.Exists(text2))
						{
							Directory.Move(text2, text2 + text4);
						}
						if (File.Exists(text3))
						{
							File.Move(text3, text3 + text4);
						}
						SdkMust(true, new string[8] { "create", "avd", "--name", "APKRunner", "--package", "system-images;android-35;google_apis;x86_64", "--path", text2 }, 180, "no\n");
						File.WriteAllText(text3, "avd.ini.encoding=UTF-8\r\npath=" + text2 + "\r\ntarget=android-35\r\n", new UTF8Encoding(false));
					}
				}
				if (!HasPerformanceProfile("APKRunner"))
				{
					TuneAvd("APKRunner");
				}
				if (!Probe(Scrcpy, new string[1] { "--version" }, "scrcpy"))
				{
					Dictionary<string, object> dictionary6 = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(Get("https://api.github.com/repos/Genymobile/scrcpy/releases/tags/v4.1"));
					IEnumerable<Dictionary<string, object>> source2 = ((object[])dictionary6["assets"]).Cast<Dictionary<string, object>>();
					Dictionary<string, object> dictionary7 = source2.First((Dictionary<string, object> x) => ((string)x["name"]).StartsWith("scrcpy-win64-") && ((string)x["name"]).EndsWith(".zip"));
					string text5 = (dictionary7.ContainsKey("digest") ? (dictionary7["digest"] as string) : null);
					if (text5 == null || !text5.StartsWith("sha256:"))
					{
						throw new Exception("scrcpy release has no SHA256 digest; download was not trusted.");
					}
					InstallZip((string)dictionary7["browser_download_url"], text5.Substring(7), "SHA256", Path.Combine(Root, "scrcpy"), "scrcpy.exe");
				}
				list = Check();
				update(list);
				string[] array2 = (from x in list
					where !x.Ready
					select x.Name).ToArray();
				if (array2.Length > 0)
				{
					throw new Exception("Mangler stadig: " + string.Join(", ", array2) + ". Se status. Windows Hypervisor Platform kan kræve aktivering og genstart.");
				}
				Log("Installation kontrolleret. Vælg nu en APK.");
			}
		}

		public void Launch(string file, List<Status> checkedState = null)
		{
			file = Path.GetFullPath(file);
			if (!File.Exists(file) || (!Path.GetExtension(file).Equals(".apk", StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(file).Equals(".xapk", StringComparison.OrdinalIgnoreCase)))
			{
				throw new Exception("Vælg en eksisterende APK- eller XAPK-fil.");
			}
			using (InstallPayload installPayload = Prepare(file))
			{
				string[] nativeAbis = GetNativeAbis(file);
				bool flag = nativeAbis.Any((string abi) => abi.Equals("x86", StringComparison.OrdinalIgnoreCase)) && !nativeAbis.Any((string abi) => abi.Equals("x86_64", StringComparison.OrdinalIgnoreCase) || abi.Equals("arm64-v8a", StringComparison.OrdinalIgnoreCase));
				if (nativeAbis.Any((string abi) => abi.Equals("arm64-v8a", StringComparison.OrdinalIgnoreCase)) && !nativeAbis.Any((string abi) => abi.Equals("x86", StringComparison.OrdinalIgnoreCase) || abi.Equals("x86_64", StringComparison.OrdinalIgnoreCase)))
				{
					Log("ARM64-APK fundet. Android bruger Google APIs-image med NDK-oversættelse.");
				}
				else if (nativeAbis.Length > 0 && !nativeAbis.Any((string abi) => abi.Equals("x86", StringComparison.OrdinalIgnoreCase) || abi.Equals("x86_64", StringComparison.OrdinalIgnoreCase)))
				{
					throw new Exception("Denne APK har kun 32-bit ARM-biblioteker: " + string.Join(", ", nativeAbis) + ". APK Runner understøtter x86, x86_64 og arm64-v8a.");
				}
				List<Status> source = checkedState ?? Check();
				if (source.Any((Status x) => !x.Ready))
				{
					throw new Exception("Installér manglende dele og kontrollér virtualisering først.");
				}
				if (flag)
				{
					EnsureTvRuntime();
				}
				string text = Must(Aapt, new string[3]
				{
					"dump",
					"badging",
					installPayload.Apks[0]
				}).Text;
				Match match = Regex.Match(text, "package: name='([^']+)'");
				if (!match.Success)
				{
					throw new Exception("APK-pakken kunne ikke læses.");
				}
				string value = match.Groups[1].Value;
				Match match2 = Regex.Match(text, "versionCode='([^']+)'");
				if (!match2.Success)
				{
					throw new Exception("APK-versionen kunne ikke læses.");
				}
				string value2 = match2.Groups[1].Value;
				if (!Regex.IsMatch(value, "^[a-zA-Z0-9_]+(\\.[a-zA-Z0-9_]+)+$"))
				{
					throw new Exception("Ugyldigt pakkenavn.");
				}
				string text2 = (flag ? "emulator-5582" : "emulator-5580");
				string avdName = (flag ? "APKRunnerTV" : "APKRunner");
				string text3 = (flag ? "5582" : "5580");
				using (new FileStream(Path.Combine(Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
				{
					Must(Adb, new string[1] { "start-server" });
					string text4 = Must(Adb, new string[1] { "devices" }).Text;
					if (!HasPerformanceProfile(avdName) && text4.Contains(text2))
					{
						Log("Skifter ydeevneprofil og genstarter Android.");
						StopManagedEmulator(text2, avdName);
						text4 = Must(Adb, new string[1] { "devices" }).Text;
					}
					if (text4.Contains(text2))
					{
						string text5 = Must(Adb, new string[5] { "-s", text2, "emu", "avd", "name" }).Text;
						if (!text5.Split('\n').Any((string x) => x.Trim() == avdName))
						{
							throw new Exception("Android-porten bruges af en anden emulator.");
						}
					}
					else
					{
						if (!HasPerformanceProfile(avdName))
						{
							TuneAvd(avdName);
						}
						Log("Starter Android. Første opstart kan tage flere minutter.");
						StartAndroid(flag);
						if (FastRendering && !flag) WaitForQuietBoot(text2);
					}
					bool flag2 = false;
					Stopwatch stopwatch = Stopwatch.StartNew();
					while (stopwatch.Elapsed.TotalSeconds < 240.0)
					{
						Result result = Run(Adb, new string[5] { "-s", text2, "shell", "getprop", "sys.boot_completed" }, 15);
						if (result.Code == 0 && result.Text.Trim() == "1")
						{
							flag2 = true;
							break;
						}
						Thread.Sleep(1500);
					}
					if (!flag2)
					{
						throw new Exception("Android blev ikke klar inden for fire minutter.");
					}
					if (flag)
					{
						Must(Adb, new string[6] { "-s", text2, "shell", "wm", "size", Portrait ? "720x1280" : "1280x720" });
						Must(Adb, new string[6] { "-s", text2, "shell", "wm", "density", "160" });
					}
					ActiveSerial = text2;
					SetMediaVolume();
					string text6 = Run(Adb, new string[6] { "-s", text2, "shell", "dumpsys", "package", value }, 30).Text;
					if (Regex.IsMatch(text6, "versionCode=" + Regex.Escape(value2) + "(?:\\s|$)"))
					{
						Log("Samme appversion er allerede installeret. Springer geninstallation over.");
					}
					else
					{
						Log("Installerer " + ((installPayload.Apks.Length > 1) ? "XAPK-pakke" : "APK") + " i APK Runners Android-enhed...");
						List<string> list = new List<string>();
						list.Add("-s");
						list.Add(text2);
						list.Add((installPayload.Apks.Length > 1) ? "install-multiple" : "install");
						list.Add("-r");
						List<string> list2 = list;
						list2.AddRange(installPayload.Apks);
						Must(Adb, list2.ToArray(), 300);
					}
					foreach (ObbFile obb in installPayload.Obbs)
					{
						Must(Adb, new string[6]
						{
							"-s",
							text2,
							"shell",
							"mkdir",
							"-p",
							"/sdcard/Android/obb/" + obb.Package
						});
						Must(Adb, new string[5]
						{
							"-s",
							text2,
							"push",
							obb.Local,
							"/sdcard/Android/obb/" + obb.Package + "/" + obb.Name
						}, 300);
					}
					Result result2 = Run(Adb, new string[8] { "-s", text2, "shell", "cmd", "package", "resolve-activity", "--brief", value });
					string text7 = result2.Text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
					if (result2.Code != 0 || text7 == null || !Regex.IsMatch(text7, "^[a-zA-Z0-9_.$]+/[a-zA-Z0-9_.$]+$"))
					{
						throw new Exception("APK installeret, men appen har ingen startbar aktivitet.");
					}
					List<string> list3 = new List<string>();
					list3.Add("--serial");
					list3.Add(text2);
					list3.Add("--mouse=sdk");
					list3.Add("--no-mouse-hover");
					list3.Add("--keyboard=sdk");
					list3.Add("--audio-source=playback");
					list3.Add("--audio-bit-rate=256K");
					list3.Add("--audio-buffer=50");
					list3.Add("--background-color=#FFFFFF");
					List<string> list4 = list3;
					list4.AddRange(DisplayArguments(flag));
					list4.Add("--start-app=" + value);
					list4.Add("--window-title");
					list4.Add(value + " — APK Runner");

					string encoderReport = FastRendering && !flag
						? Run(Scrcpy, new string[] { "--serial", text2, "--list-encoders" }, 20).Text : "";
					string[] videoArguments = VideoArguments(flag, encoderReport);
					list4.AddRange(videoArguments);
					Log("Video: " + (videoArguments.Contains("--video-codec=vp8") ? "VP8" : "H.264") + ", " + (videoArguments.Contains("--max-fps=30") ? "30 FPS / 33 ms buffer" : "60 FPS / 0 ms buffer") + ".");
					Process process2 = new Process();
					process2.StartInfo = StartInfo(Scrcpy, list4.ToArray());
					process2.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
					{
						if (e.Data != null)
						{
							Log(e.Data);
						}
					};
					process2.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
					{
						if (e.Data != null)
						{
							Log(e.Data);
						}
					};
					process2.Start();
					process2.StandardInput.Close();
					process2.BeginOutputReadLine();
					process2.BeginErrorReadLine();
					if (process2.WaitForExit(1500))
					{
						throw new Exception("App-vinduet lukkede uventet. Kontroller scrcpy.");
					}
					Log("Appen kører. Android forbliver startet, når app-vinduet lukkes.");
				}
			}
		}

        public string[] EmulatorArguments(bool legacyX86)
        {
            var args = new List<string> { "-avd", legacyX86 ? TvAvdName : AvdName,
                "-port", legacyX86 ? "5582" : "5580", "-no-window", "-no-audio", "-no-boot-anim",
                CompactBoot ? "-no-snapshot" : "-no-snapshot-save", "-accel", "on" };
            // -no-snapshot alone still preallocates file-backed guest RAM.
            // Cold-boot mode has no Quickboot state to map, so keep RAM anonymous.
            if (CompactBoot) { args.Add("-feature"); args.Add("-QuickbootFileBacked"); }
            // Do not force the host driver: it regressed repeated portrait samples.
            if (FastRendering && !legacyX86) {
                args.Add("-gpu"); args.Add("auto");
            }
            return args.ToArray();
        }

        public string[] VideoArguments(bool legacyX86, string encoderReport)
        {
            bool vp8 = FastRendering && !legacyX86 && Regex.IsMatch(encoderReport ?? "",
                @"--video-codec=vp8(?:\s|$)", RegexOptions.IgnoreCase);
            bool paced = FastRendering && !legacyX86 && !HighPerformance;
            return new string[] { vp8 ? "--video-codec=vp8" : "--video-codec=h264",
                paced ? "--max-fps=30" : "--max-fps=60", "--video-bit-rate=12M", paced ? "--video-buffer=33" : "--video-buffer=0" };
        }

        public static double CpuIdleFraction(string before, string after)
        {
            Func<string, long[]> parse = text => {
                string line = (text ?? "").Split('\n')[0].Trim();
                string[] values = Regex.Split(line, @"\s+");
                if (values.Length < 9 || values[0] != "cpu") return null;
                var counters = new long[8];
                for (int i = 0; i < 8; i++) if (!long.TryParse(values[i + 1], out counters[i]) || counters[i] < 0) return null;
                return counters;
            };
            long[] a = parse(before), b = parse(after);
            if (a == null || b == null) return 0;
            double total = 0;
            for (int i = 0; i < 8; i++) { if (b[i] < a[i]) return 0; total += b[i] - a[i]; }
            return total > 0 ? (b[3] - a[3]) / total : 0;
        }

        public void WaitForQuietBoot(string serial)
        {
            Log("Android er startet; venter på at opstartsarbejdet falder til ro (højst 120 sekunder).");
            var clock = Stopwatch.StartNew();
            string previous = Run(Adb, new string[] { "-s", serial, "shell", "cat", "/proc/stat" }, 5).Text;
            int quiet = 0;
            int nextProgress = 10;
            while (clock.Elapsed.TotalSeconds < 120) {
                Thread.Sleep(1000);
                Result sample = Run(Adb, new string[] { "-s", serial, "shell", "cat", "/proc/stat" }, 5);
                double idle = sample.Code == 0 ? CpuIdleFraction(previous, sample.Text) : 0;
                quiet = idle >= .75 ? quiet + 1 : 0;
                previous = sample.Text;
                if (clock.Elapsed.TotalSeconds >= nextProgress) {
                    Log("Afventer Androids opstartsarbejde: " + ((1 - idle) * 100).ToString("F0") + "% CPU optaget.");
                    nextProgress += 10;
                }
                if (quiet >= 3) {
                    Log("Androids opstartsarbejde er faldet til ro efter " + clock.Elapsed.TotalSeconds.ToString("F1") + " s. Åbner appen.");
                    return;
                }
            }
            Log("Android arbejder stadig i baggrunden. Åbner appen; der kan være midlertidige hak efter kold opstart.");
        }

        private void StartAndroid(bool legacyX86)
        {
                var process = new Process();
                process.StartInfo = StartInfo(Emulator, EmulatorArguments(legacyX86));
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    if (e.Data != null && (e.Data.IndexOf("Graphics Adapter", StringComparison.OrdinalIgnoreCase) >= 0 || e.Data.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0)) Log(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                    if (e.Data != null && e.Data.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0) Log(e.Data);
                };
                Log("Grafik: automatisk valg.");
                process.Start(); process.StandardInput.Close();
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                Stopwatch boot = Stopwatch.StartNew();
                while (boot.Elapsed.TotalSeconds < 240 && !process.WaitForExit(1500)) {
                    Result ready = Run(Adb, new string[] { "-s", legacyX86 ? "emulator-5582" : "emulator-5580", "shell", "getprop", "sys.boot_completed" }, 15);
                    if (ready.Code == 0 && ready.Text.Trim() == "1") return;
                }
                if (!process.HasExited) throw new Exception("Android blev ikke klar inden for fire minutter. Den kørende enhed er bevaret.");
                throw new Exception("Android afsluttede under opstart. Se grafikfejlen i loggen.");
        }

		private void SetMediaVolume()
		{
			if (!string.IsNullOrEmpty(ActiveSerial))
			{
				Must(Adb, new string[10] { "-s", ActiveSerial, "shell", "cmd", "media_session", "volume", "--stream", "3", "--set", "15" });
			}
		}


        public string[] DisplayArguments(bool legacyX86)
        {
            var args = new List<string>();
            bool portrait = Portrait;
            if (!legacyX86)
            {
                args.Add(portrait ? "--new-display=720x1280/160" : "--new-display=1280x720/160");
                args.Add("--no-vd-system-decorations");
            }
            args.Add(portrait ? "--window-width=480" : "--window-width=1280");
            args.Add(portrait ? "--window-height=854" : "--window-height=720");
            return args.ToArray();
        }

		public void VolumeUp()
		{
			if (string.IsNullOrEmpty(ActiveSerial))
			{
				throw new Exception("Start en app først.");
			}
			Must(Adb, new string[6] { "-s", ActiveSerial, "shell", "input", "keyevent", "24" });
		}

		public void VolumeDown()
		{
			if (string.IsNullOrEmpty(ActiveSerial))
			{
				throw new Exception("Start en app først.");
			}
			Must(Adb, new string[6] { "-s", ActiveSerial, "shell", "input", "keyevent", "25" });
		}

		public void ToggleMute()
		{
			if (string.IsNullOrEmpty(ActiveSerial))
			{
				throw new Exception("Start en app først.");
			}
			Must(Adb, new string[6] { "-s", ActiveSerial, "shell", "input", "keyevent", "164" });
		}

		public void Stop()
		{
			Directory.CreateDirectory(Root);
			using (new FileStream(Path.Combine(Root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
			{
				string text = Must(Adb, new string[5] { "-s", "emulator-5580", "emu", "avd", "name" }).Text;
				if (!text.Split('\n').Any((string x) => x.Trim() == "APKRunner"))
				{
					throw new Exception("Denne emulator tilhører ikke APK Runner.");
				}
				Must(Adb, new string[4] { "-s", "emulator-5580", "emu", "kill" });
			}
		}
	}
	internal partial class Window : Form
	{
		private Runtime runtime;

		private ListView list = new ListView();

		private TextBox log = new TextBox();

		private Button install = new FlightButton();

		private Button open = new FlightButton();

		private Button refresh = new FlightButton();

		private Button associate = new FlightButton();

		private Button stop = new FlightButton();

		private CheckBox consent = new CheckBox();

		private bool busy;

		private string pending;

		private List<Status> lastStatus;
		private ComboBox orientation = new FlightCombo();
		private Button storage = new FlightButton();

		public Window(string apk, bool preview = false)
        {
            pending = apk;
            InitializeFlightDeck(preview);
        }

		private void Add(FlowLayoutPanel parent, Button button, string text, Action action)
        {
            AddFlightButton(parent, button, text, action);
        }

        private void AskPerformanceMode()
        {
            runtime.SetPerformanceMode(FlightTheme.ChoosePerformance(this, runtime.FastRendering));
        }

		private void RunPending()
		{
			if (updatePolicy.BlocksNewApps) return;
			if (pending != null)
			{
				string extension = Path.GetExtension(pending);
				if (!File.Exists(pending) || (!extension.Equals(".apk", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".xapk", StringComparison.OrdinalIgnoreCase)))
				{
					pending = null;
					throw new Exception("Vælg en eksisterende APK- eller XAPK-fil.");
				}
				List<Status> state = lastStatus ?? runtime.Check();
				if (state.Any((Status x) => !x.Ready))
				{
					Log("Fil valgt: " + Path.GetFileName(pending) + ". Installér manglende dele først.");
					return;
				}
				string text = pending;
				pending = null;
				runtime.Launch(text, state);
				CreateDesktopShortcut(text);
			}
		}

		private void Log(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			string line = DateTime.Now.ToString("HH:mm:ss") + " " + text.Trim() + Environment.NewLine;
			try
			{
				Directory.CreateDirectory(runtime.Root);
				File.AppendAllText(Path.Combine(runtime.Root, "APKRunner.log"), line);
			}
			catch
			{
			}
			if (base.IsHandleCreated)
			{
				BeginInvoke((Action)delegate
				{
					log.AppendText(line);
				});
			}
		}

		private void UpdateStatus(List<Status> s)
		{
			lastStatus = s;
			Invoke((Action)delegate
			{
				list.Items.Clear();
                statusSummary.Text = s.Count == 0 ? "AFVENTER KONTROL" : s.Count(x => x.Ready).ToString("00") + " / " + s.Count.ToString("00") + " KLAR";
                emptyState.Visible = s.Count == 0;
                if (s.Count == 0) emptyState.Text = "Ingen komponentstatus endnu. Vælg Kontrollér.";
				foreach (Status item in s.OrderBy(x => x.Ready))
				{
					ListViewItem value = new ListViewItem(new string[3]
					{
						item.Name,
						item.Ready ? "KLAR" : "MANGLER",
						item.Detail.Replace("\r", " ").Replace("\n", " ")
					})
					{
						ForeColor = (item.Ready ? FlightTheme.Green : FlightTheme.Amber)
					};
					list.Items.Add(value);
				}
				open.Tag = s.All((Status x) => x.Ready);
			});
		}

		private void Work(Action action)
        {
            RunFlightOperation(action);
        }

		private void Choose()
		{
			if (updatePolicy.BlocksNewApps || applyingUpdate) return;
			OpenFileDialog d = new OpenFileDialog
			{
				Filter = "Android app-pakker (*.apk;*.xapk)|*.apk;*.xapk",
				CheckFileExists = true
			};
			try
			{
				if (d.ShowDialog(this) == DialogResult.OK)
				{
					Work(delegate
					{
						if (updatePolicy.BlocksNewApps || applyingUpdate) return;
						runtime.Launch(d.FileName);
						CreateDesktopShortcut(d.FileName);
					});
				}
			}
			finally
			{
				if (d != null)
				{
					((IDisposable)d).Dispose();
				}
			}
		}

		private void CreateDesktopShortcut(string apk)
		{
			try
			{
				string text = Path.GetFileNameWithoutExtension(apk);
				char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
				foreach (char oldChar in invalidFileNameChars)
				{
					text = text.Replace(oldChar, '_');
				}
				string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), text + " — APK Runner.lnk");
				Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
				if (typeFromProgID == null)
				{
					throw new Exception("Windows kan ikke oprette genveje.");
				}
				object target = Activator.CreateInstance(typeFromProgID);
				object obj = typeFromProgID.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, target, new object[1] { text2 });
				Type type = obj.GetType();
                type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, obj, new object[1] { AppUpdates.StableLauncher });
                type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, obj, new object[1] { ShellBranding.EnsureIcon(RuntimePaths.SettingsDirectory) + ",0" });
				type.InvokeMember("Arguments", BindingFlags.SetProperty, null, obj, new object[1] { Runtime.Arg(Path.GetFullPath(apk)) });
                type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, obj, new object[1] { Path.GetDirectoryName(AppUpdates.StableLauncher) });
				type.InvokeMember("Description", BindingFlags.SetProperty, null, obj, new object[1] { "Start " + text + " i APK Runner" });
				type.InvokeMember("Save", BindingFlags.InvokeMethod, null, obj, null);
				Log("Skrivebordsgenvej oprettet: " + Path.GetFileName(text2));
			}
			catch (Exception ex)
			{
				Log("Kunne ikke oprette skrivebordsgenvej: " + ex.Message);
			}
		}

		private void Register()
		{
			try
			{
				string executablePath = AppUpdates.StableLauncher;
				using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\APKRunner.APK\\shell\\open\\command"))
				{
					registryKey.SetValue("", Runtime.Arg(executablePath) + " \"%1\"");
				}
				using (RegistryKey registryKey2 = Registry.CurrentUser.CreateSubKey("Software\\Classes\\.apk\\OpenWithProgids"))
				{
					registryKey2.SetValue("APKRunner.APK", "", RegistryValueKind.String);
				}
				using (RegistryKey registryKey3 = Registry.CurrentUser.CreateSubKey("Software\\Classes\\.xapk\\OpenWithProgids"))
				{
					registryKey3.SetValue("APKRunner.APK", "", RegistryValueKind.String);
				}
				using (RegistryKey registryKey4 = Registry.CurrentUser.CreateSubKey("Software\\Classes\\.apk"))
				{
					registryKey4.SetValue("", "APKRunner.APK");
				}
				using (RegistryKey registryKey5 = Registry.CurrentUser.CreateSubKey("Software\\Classes\\.xapk"))
				{
					registryKey5.SetValue("", "APKRunner.APK");
				}
				Runtime.SHChangeNotify(134217728u, 0u, IntPtr.Zero, IntPtr.Zero);
				MessageBox.Show(this, "Dobbeltklik på en APK åbner nu APK Runner. Hvis Windows allerede har gemt en anden standardapp, vælg APK Runner én gang under Åbn med. Flyt ikke programmappen bagefter.", Text);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, Text);
			}
		}
	}
	internal static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (args.Length == 2 && args[0] == "--update-status") {
                var updates = new AppUpdates();
                string error = null;
                try { updates.Check().GetAwaiter().GetResult(); } catch (Exception ex) { error = ex.Message; }
                File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(new {
                    repository = AppUpdates.Repository, installed = AppUpdates.IsManaged, version = AppUpdates.DisplayVersion,
                    runtime = AppUpdates.RuntimeRoot, target = updates.Policy.Required == null ? null : updates.Policy.Required.ToString(),
                    blocked = updates.Policy.BlocksNewApps, error = error
                }));
                return;
            }
            if (args.Length == 2 && args[0] == "--storage-report")
            {
                File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(StorageReport.Scan(AppUpdates.RuntimeRoot)));
                return;
            }
			if (args.Length == 2 && args[0] == "--check")
			{
				Runtime runtime = new Runtime(AppUpdates.RuntimeRoot);
				File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(runtime.Check()));
			}
			else
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
                try { if (RuntimeSetup.EnsureConfigured()) Application.Run(new Window((args.Length > 0) ? args[0] : null)); }
                catch (Exception error) { MessageBox.Show(error.Message, "APK Runner - data ikke tilgængelige", MessageBoxButtons.OK, MessageBoxIcon.Error); }
			}
		}
	}
}
