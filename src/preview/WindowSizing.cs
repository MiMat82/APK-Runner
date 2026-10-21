using System;
using System.Drawing;

namespace APKRunner
{
    internal static class WindowSizing
    {
        public static Size InitialSize(Rectangle workingArea)
        {
            return new Size(Math.Max(1, Math.Min(1040, (int)Math.Floor(workingArea.Width * .60))),
                Math.Max(1, Math.Min(820, (int)Math.Floor(workingArea.Height * .92))));
        }
    }
}
