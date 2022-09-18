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
                
                packages.Add(ParsePackageTuple(packageArg));
            }

            if (packages.Count == 0 && mergeFiles.Count == 0)
            {
                Console.WriteLine("No packages and no LiCo files to merge specified.");
                PrintHelp();
                return;
            }

            var lico = new LiCo();

            lico.GenerateLicense(output, mergeFiles, packages);
        }

        public static Package ParsePackageTuple(string packageTuple)
        {
            var splt = packageTuple.Split('=');
            return Package.GetPackage(splt[0], splt[1], false);
        }
    }
}