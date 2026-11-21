using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Translumo.Infrastructure.Constants
{
    public static class Global
    {
        public static string AppPath;

        public static string PythonPath;

        public static string PipPath;

        public static string ModelsPath;

        static Global()
        {
#if DEBUG
            AppPath = System.AppDomain.CurrentDomain.BaseDirectory;
#else
            AppPath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
#endif
            PythonPath = Path.Combine(AppPath, "Python");
            PipPath = Path.Combine(PythonPath, "Scripts/pip.exe");
            ModelsPath = Path.Combine(AppPath, "models");
        }


        public static Version GetVersion()
        {
            try
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    var rawVersion = FileVersionInfo.GetVersionInfo(entryAssembly.Location).ProductVersion;
                    if (!string.IsNullOrEmpty(rawVersion))
                    {
                        var cleanVersion = rawVersion.Split('+', '-')[0].Trim();
                        if (Version.TryParse(cleanVersion, out var ver))
                        {
                            return ver;
                        }
                    }

                    if (entryAssembly.GetName().Version != null)
                    {
                        return entryAssembly.GetName().Version;
                    }
                }
            }
            catch
            {
            }

            return new Version(1, 2, 0);
        }
    }
}
