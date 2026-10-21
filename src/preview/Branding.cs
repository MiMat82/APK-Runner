using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace APKRunner
{
    internal static class Branding
    {
        public static readonly Image Logo = LoadLogo();
        static readonly Icon MasterIcon = LoadIcon();

        internal static Stream Resource(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) throw new InvalidOperationException("Missing APK Runner branding resource: " + name);
            return stream;
        }
        static Image LoadLogo()
        {
            using (Stream stream = Resource("APKRunner.BrandLogo.png"))
            using (Image source = Image.FromStream(stream)) return new Bitmap(source);
        }
        static Icon LoadIcon()
        {
            using (Stream stream = Resource("APKRunner.BrandIcon.ico"))
            using (var icon = new Icon(stream, 256, 256)) return (Icon)icon.Clone();
        }
        public static void Apply(Form form)
        {
            var icon = (Icon)MasterIcon.Clone();
            form.Icon = icon;
            form.Disposed += delegate { icon.Dispose(); };
        }
    }
}
