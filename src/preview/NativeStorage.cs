using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace APKRunner
{
    public static class NativeStorage
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetCompressedFileSizeW(string path, out uint high);

        public static long DiskBytes(string path)
        {
            uint high;
            uint low = GetCompressedFileSizeW(path, out high);
            if (low == uint.MaxValue) {
                int error = Marshal.GetLastWin32Error();
                if (error != 0) throw new Win32Exception(error, "Cannot measure file storage: " + path);
            }
            return checked((long)(((ulong)high << 32) | low));
        }
    }
}
