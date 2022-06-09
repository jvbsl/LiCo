using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LiCo
{
    class Program
    {
        private const int BoxLength = 150;
        private const int LeftBoxLength = 20;
        private static void Collect(Package p, Dictionary<License, HashSet<Package>> collectedLicenses,
            HashSet<Package> alreadyNoticedPackages)
        {
            if (alreadyNoticedPackages.Contains(p))
                return;
            alreadyNoticedPackages.Add(p);
            p.LoadPackage();
            foreach (var l in p.Licenses)
            {
                if (!collectedLicenses.TryGetValue(l, out var packages))
                {
                    packages = new HashSet<Package>();
                    collectedLicenses.Add(l, packages);
                }

                packages.Add(p);
            }

            foreach (var d in p.Dependencies)
            {
                Collect(d, collectedLicenses, alreadyNoticedPackages);
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage: LiCo [-o output file] [-m merge file] PACKAGES...");
            Console.WriteLine("  --help   Shows this help.");
            Console.WriteLine("  -o       The output file to write the third party notices to[Default=ThirdPartyNotice.txt]");
            Console.WriteLine("  -m       A merge file to add to the collected licenses");
            Console.WriteLine("PACKAGES   Space separated list of packages in the following format:");
            Console.WriteLine("               <PACKAGE_NAME>=<PACKAGE_VERSION> ...");
        }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var packages = new HashSet<Package>();

            string output = "ThirdPartyNotice.txt";

            var mergeFiles = new List<string>();

            bool nextIsOutput = false, nextIsMerge = false;
            foreach (var packageArg in args)
            {
                switch (packageArg)
                {
                    case "-o":
                        nextIsOutput = true;
                        continue;
                    case "--help":
                        PrintHelp();
                        return;
                    case "-m":
                        nextIsMerge = true;
                        continue;
                }

                if (nextIsOutput)
                {
                    output = packageArg;
                    nextIsOutput = false;
                    continue;
                }

                if (nextIsMerge)
                {
                    mergeFiles.Add(packageArg);
                    nextIsMerge = false;
                    continue;
                }
                
                var splt = packageArg.Split('=');
                packages.Add(Package.GetPackage(splt[0], splt[1], false));
            }

            if (packages.Count == 0 && mergeFiles.Count == 0)
            {
                Console.WriteLine("No packages and no LiCo files to merge specified.");
                PrintHelp();
                return;
            }

            var alreadyNoticedPackages = new HashSet<Package>();
            var collectedLicenses = new Dictionary<License, HashSet<Package>>();

            if (File.Exists(output))
                CollectMergeFile(output, collectedLicenses, alreadyNoticedPackages);
            
            CollectMergeFiles(mergeFiles, collectedLicenses, alreadyNoticedPackages);

            foreach(var p in packages)
            {
                Collect(p, collectedLicenses, alreadyNoticedPackages);
            }


            using var thirdParty = new FileStream(output, FileMode.Create);
            using var thirdPartyNotice = new StreamWriter(thirdParty, Encoding.UTF8);
            WriteThirdPartyNotice(collectedLicenses, thirdPartyNotice);
        }

        private static readonly Regex BoxRegex = new Regex($"={{{BoxLength}}}\\s*\n(?<content>.*?)={{{BoxLength}}}", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex DepPackageRegex =
            new Regex("=+ Dependent packages (?<name>\\S*) (?<version>\\S*?) (?<thirdParty>ThirdPartyInfo|) =+\\s*\n", RegexOptions.Compiled);
        private static void CollectMergeFiles(List<string> mergeFiles, Dictionary<License, HashSet<Package>> collectedLicenses, HashSet<Package> alreadyNoticedPackages)
        {
            foreach (var f in mergeFiles)
            {
                CollectMergeFile(f, collectedLicenses, alreadyNoticedPackages);
            }
        }

        private static void CollectMergeFile(string mergeFile, Dictionary<License, HashSet<Package>> collectedLicenses,
            HashSet<Package> alreadyNoticedPackages)
        {
            var fl = File.ReadAllText(mergeFile).Replace("\r", "");

            var matches = BoxRegex.Matches(fl).
                Select(x => (match: x, packages: DepPackageRegex.Matches(x.Groups["content"].Value).ToArray())).
                Where(x => x.packages.Length > 0).ToArray();

            for (int i = 0; i < matches.Length; i++)
            {
                var (match, pkgs) = matches[i];
                var start = match.Index + match.Length;
                int end = i < matches.Length - 1 ? matches[i + 1].match.Index : fl.Length;
                var license = fl[(start + 2)..(end - 2)];
                var l = License.GetLicense(pkgs.Length == 1 && pkgs[0].Groups["thirdParty"].Value == "ThirdPartyInfo" ? LicenseType.ThirdPartyFile : LicenseType.File, license);
                foreach (var p in pkgs)
                {
                    var package = Package.GetPackage(p.Groups["name"].Value, p.Groups["version"].Value, false);
                    // if (alreadyNoticedPackages.Contains(package))
                    //     continue;
                    alreadyNoticedPackages.Add(package);
                    if (!collectedLicenses.TryGetValue(l, out var packagesMatching))
                    {
                        packagesMatching = new();
                        collectedLicenses.Add(l, packagesMatching);
                    }
                    
                    packagesMatching.Add(package);
                }
                
            }
        }

        private static void WriteThirdPartyNotice(Dictionary<License, HashSet<Package>> collectedLicenses,
            StreamWriter thirdPartyNotice)
        {
            var box = string.Concat(Enumerable.Repeat("=", BoxLength));
            var leftBox = string.Concat(Enumerable.Repeat("=", LeftBoxLength));

            foreach (var (l, packages) in collectedLicenses)
            {
                thirdPartyNotice.WriteLine(box);
                foreach (var p in packages)
                {
                    string part =
                        $"{leftBox} Dependent packages {p.Name} {p.Version} {(l.LicenseType == LicenseType.ThirdPartyFile ? "ThirdPartyInfo" : "")} "
                            .PadRight(BoxLength, '=');
                    thirdPartyNotice.WriteLine(part);
                }

                thirdPartyNotice.WriteLine(box);

                thirdPartyNotice.WriteLine();
                thirdPartyNotice.WriteLine(l.LicenseText);
                thirdPartyNotice.WriteLine();
            }
        }
    }
}