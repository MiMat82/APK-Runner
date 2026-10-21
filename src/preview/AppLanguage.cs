using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace APKRunner
{
    internal static class AppLanguage
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetProcessPreferredUILanguages(uint flags, string languages, out uint count);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetThreadPreferredUILanguages(uint flags, string languages, out uint count);

        public static void Initialize()
        {
            // English is the application's sole UI language, regardless of Windows locale.
            // These preferences affect this process, not the user's Windows settings.
            var english = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = english;
            CultureInfo.DefaultThreadCurrentUICulture = english;
            Thread.CurrentThread.CurrentCulture = english;
            Thread.CurrentThread.CurrentUICulture = english;
            uint count;
            SetProcessPreferredUILanguages(0x8, "en-US\0\0", out count);
            SetThreadPreferredUILanguages(0x8, "en-US\0\0", out count);
        }
    }
}
