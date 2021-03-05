using System.Collections.Generic;
using System.Diagnostics;

namespace LiCo
{
    public class Nuget
    {
        public static List<string> _paths;
        public static List<string> Paths => _paths ??= LoadNugetPaths();

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
    }
}