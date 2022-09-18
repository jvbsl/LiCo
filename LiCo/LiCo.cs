using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LiCo;

public class LiCo
{
    private const int BoxLength = 150;
    private const int LeftBoxLength = 20;

    public event EventHandler<string> OnError; 

    private void Collect(Package p, Dictionary<License, HashSet<Package>> collectedLicenses,
        HashSet<Package> alreadyNoticedPackages)
    {
        if (alreadyNoticedPackages.Contains(p))
            return;
        alreadyNoticedPackages.Add(p);
        try
        {
            p.LoadPackage();
        }
        catch (FileNotFoundException e)
        {
            OnError?.Invoke(this, e.Message);
            return;
        }
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

    public void GenerateLicense(string output, List<string> mergeFiles, HashSet<Package> packages)
    {
        var alreadyNoticedPackages = new HashSet<Package>();
        var collectedLicenses = new Dictionary<License, HashSet<Package>>();

        if (File.Exists(output))
            CollectMergeFile(output, collectedLicenses, alreadyNoticedPackages);

        CollectMergeFiles(mergeFiles, collectedLicenses, alreadyNoticedPackages);

        foreach (var p in packages)
        {
            Collect(p, collectedLicenses, alreadyNoticedPackages);
        }


        using var thirdParty = new FileStream(output, FileMode.Create);
        using var thirdPartyNotice = new StreamWriter(thirdParty, Encoding.UTF8);
        WriteThirdPartyNotice(collectedLicenses, thirdPartyNotice);
    }

    public void GenerateLicenseContent(string output, List<string> mergeFileContents, HashSet<Package> packages)
    {
        var alreadyNoticedPackages = new HashSet<Package>();
        var collectedLicenses = new Dictionary<License, HashSet<Package>>();

        if (File.Exists(output))
            CollectMergeFile(output, collectedLicenses, alreadyNoticedPackages);

        CollectMergeFileContents(mergeFileContents, collectedLicenses, alreadyNoticedPackages);

        foreach (var p in packages)
        {
            Collect(p, collectedLicenses, alreadyNoticedPackages);
        }


        using var thirdParty = new FileStream(output, FileMode.Create);
        using var thirdPartyNotice = new StreamWriter(thirdParty, Encoding.UTF8);
        WriteThirdPartyNotice(collectedLicenses, thirdPartyNotice);
    }

    private readonly Regex BoxRegex = new Regex($"={{{BoxLength}}}\\s*\n(?<content>.*?)={{{BoxLength}}}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly Regex DepPackageRegex =
        new Regex("=+ Dependent packages (?<name>\\S*) (?<version>\\S*?) (?<thirdParty>ThirdPartyInfo|) =+\\s*\n",
            RegexOptions.Compiled);

    private void CollectMergeFiles(List<string> mergeFiles, Dictionary<License, HashSet<Package>> collectedLicenses,
        HashSet<Package> alreadyNoticedPackages)
    {
        foreach (var f in mergeFiles)
        {
            CollectMergeFile(f, collectedLicenses, alreadyNoticedPackages);
        }
    }

    private void CollectMergeFileContents(List<string> mergeFilesContents,
        Dictionary<License, HashSet<Package>> collectedLicenses, HashSet<Package> alreadyNoticedPackages)
    {
        foreach (var f in mergeFilesContents)
        {
            CollectMergeFileContent(f, collectedLicenses, alreadyNoticedPackages);
        }
    }

    private void CollectMergeFile(string mergeFile, Dictionary<License, HashSet<Package>> collectedLicenses,
        HashSet<Package> alreadyNoticedPackages)
    {
        var fl = File.ReadAllText(mergeFile).Replace("\r", "");

        CollectMergeFileContent(fl, collectedLicenses, alreadyNoticedPackages);
    }

    private void CollectMergeFileContent(string mergeFileContent,
        Dictionary<License, HashSet<Package>> collectedLicenses,
        HashSet<Package> alreadyNoticedPackages)
    {
        var matches = BoxRegex.Matches(mergeFileContent).OfType<Match>()
            .Select(x => (match: x,
                        packages: DepPackageRegex.Matches(x.Groups["content"].Value).OfType<Match>().ToArray()))
            .Where(x => x.packages.Length > 0).ToArray();

        for (int i = 0; i < matches.Length; i++)
        {
            var (match, pkgs) = matches[i];
            var start = match.Index + match.Length;
            int end = i < matches.Length - 1 ? matches[i + 1].match.Index : mergeFileContent.Length;
            var license = mergeFileContent[(start + 2)..(end - 2)];
            var l = License.GetLicense(
                pkgs.Length == 1 && pkgs[0].Groups["thirdParty"].Value == "ThirdPartyInfo"
                    ? LicenseType.ThirdPartyFile
                    : LicenseType.File, license);

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

    private void WriteThirdPartyNotice(Dictionary<License, HashSet<Package>> collectedLicenses,
        StreamWriter thirdPartyNotice)
    {
        var box = string.Concat(Enumerable.Repeat("=", BoxLength));
        var leftBox = string.Concat(Enumerable.Repeat("=", LeftBoxLength));

        foreach (var pair in collectedLicenses)
        {
            var l = pair.Key;
            var packages = pair.Value;
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