using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LiCo
{
    class Program
    {
        private static void Collect(Package p, Dictionary<License, HashSet<Package>> collectedLicenses,
            HashSet<Package> alreadyNoticedPackages)
        {
            if (alreadyNoticedPackages.Contains(p))
                return;
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
            Console.WriteLine("Usage: LiCo [-o output file] PACKAGES...");
            Console.WriteLine("  --help   Shows this help.");
            Console.WriteLine("  -o       The output file to write the third party notices to[Default=ThirdPartyNotice.txt]");
            Console.WriteLine("PACKAGES   Space separated list of packages in the following format:");
            Console.WriteLine("               <PACKAGE_NAME>=<PACKAGE_VERSION> ...");
        }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var packages = new HashSet<Package>();

            string output = "ThirdPartyNotice.txt";

            bool nextIsOutput = false;
            foreach (var packageArg in args)
            {
                if (packageArg == "-o")
                {
                    nextIsOutput = true;
                    continue;
                }

                if (packageArg == "--help")
                {
                    PrintHelp();
                    return;
                }

                if (nextIsOutput)
                {
                    output = packageArg;
                    nextIsOutput = false;
                    continue;
                }
                var splt = packageArg.Split('=');
                packages.Add(Package.GetPackage(splt[0], splt[1]));
            }

            if (packages.Count == 0)
            {
                Console.WriteLine("No packages specified.");
                PrintHelp();
                return;
            }

            var alreadyNoticedPackages = new HashSet<Package>();
            var collectedLicenses = new Dictionary<License, HashSet<Package>>();

            using var thirdParty = new FileStream(output, FileMode.Create);
            using var thirdPartyNotice = new StreamWriter(thirdParty, Encoding.UTF8);


            foreach(var p in packages)
            {
                Collect(p, collectedLicenses, alreadyNoticedPackages);
            }
            WriteThirdPartyNotice(collectedLicenses, thirdPartyNotice);
        }

        private static void WriteThirdPartyNotice(Dictionary<License, HashSet<Package>> collectedLicenses,
            StreamWriter thirdPartyNotice)
        {
            const int boxLength = 150;
            const int leftBoxLength = 20;
            var box = string.Concat(Enumerable.Repeat("=", boxLength));
            var leftBox = string.Concat(Enumerable.Repeat("=", leftBoxLength));

            foreach (var (l, packages) in collectedLicenses)
            {
                thirdPartyNotice.WriteLine(box);
                foreach (var p in packages)
                {
                    string part =
                        $"{leftBox} Dependent packages {p.Name} {p.Version} {(l.LicenseType == LicenseType.ThirdPartyFile ? "ThirdPartyInfo" : "")} "
                            .PadRight(boxLength, '=');
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