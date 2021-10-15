using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LiCo
{
    public class Nuget
    {
        private static List<string> _paths;
        private static List<Uri> _sources;
        public static List<string> Paths => _paths ??= LoadNugetPaths();
        public static List<Uri> Sources => _sources ??= LoadSources();

        private static List<Uri> LoadSources()
        {
            var s = new ProcessStartInfo("dotnet", " nuget list source --format Short")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            var p = Process.Start(s);

            List<Uri> sourcePaths = new List<Uri>();
            if (p == null || !p.WaitForExit(10000))
                return sourcePaths;

            string line = null;
            while ((line = p.StandardOutput.ReadLine()) != null)
            {
                const string enabledPrefix = "E ";
                if (line.StartsWith(enabledPrefix))
                {
                    sourcePaths.Add(new Uri(line.Substring(enabledPrefix.Length)));
                }
            }

            return sourcePaths;
        }
        private static List<string> LoadNugetPaths()
        {
            var s = new ProcessStartInfo("dotnet", " nuget locals global-packages --list")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            var p = Process.Start(s);

            List<string> packagePaths = new List<string>();
            if (p == null || !p.WaitForExit(1000))
                return packagePaths;

            string line = null;
            while ((line = p.StandardOutput.ReadLine()) != null)
            {
                const string globPackPrefix = "global-packages: ";
                if (line.StartsWith(globPackPrefix))
                {
                    packagePaths.Add(line.Substring(globPackPrefix.Length));
                }
            }

            return packagePaths;
        }

        public static Uri? GetPackage(string name, string version, Uri source)
        {
            if (source.IsFile)
            {
                string path = Path.Combine(source.AbsolutePath, name, version, $"{name}.{version}.nupkg");
                return File.Exists(path) ? new Uri(path) : null;
            }

            if (source.Authority == "api.nuget.org")
            {
                return new Uri($"https://www.nuget.org/api/v2/package/{name}/{version}");
            }

            throw new NotSupportedException("Currently only Nuget.org package sources and local sources are supported");
        }
    }
}