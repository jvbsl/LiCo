using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace LiCo
{
    public class Package : IEquatable<Package>
    {
        private static HashSet<string> _ignoredPackages = new HashSet<string>() {"NETStandard.Library"};
        private void AddThirdPartyIfExists(ZipArchive archive, string name)
        {
            var thirdParty = archive.GetEntry(name);
            if (thirdParty != null)
            {
                using var thirdPartyStream = thirdParty.Open();
                using var licenseReader = new StreamReader(thirdPartyStream);
                Licenses.Add(License.GetLicense(LicenseType.ThirdPartyFile, licenseReader.ReadToEnd()));
            }
        }
        private Package(string name, string version, bool loadPackage)
        {
            Name = name;
            Version = version;
            
            Licenses = new HashSet<License>();
            Dependencies = new HashSet<Package>();

            if (loadPackage)
                LoadPackage();
        }

        public void LoadPackage()
        {
            if (Licenses.Count != 0 || Dependencies.Count != 0)
                return;
            string path = null;
            foreach (var packagesPath in Nuget.Paths)
            {
                string testPath = Path.Combine(packagesPath, Name, Version, $"{Name}.{Version}.nupkg");
                if (File.Exists(testPath))
                {
                    path = testPath;
                    break;
                }
            }

            if (path == null)
            {
                foreach (var nugetSource in Nuget.Sources)
                {
                    var packageUri = Nuget.GetPackage(Name, Version, nugetSource);
                    if (packageUri == null)
                        continue;
                    if (packageUri.IsFile)
                    {
                        path = packageUri.AbsolutePath;
                        break;
                    }

                    path = Path.GetTempFileName();
                    try
                    {
                        var wc = new WebClient();
                        wc.DownloadFile(packageUri, path);
                    }
                    catch (WebException)
                    {
                    }
                    break;
                }

                if (path == null)
                    throw new FileNotFoundException($"Nuget package not found: {Name}.{Version}.nupkg");
            }

            using var fs = File.OpenRead(path);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
            using var nuspecStream = archive.Entries
                .First(x => string.Compare(x.Name, $"{Name}.nuspec", StringComparison.InvariantCultureIgnoreCase) == 0).Open();

            var doc = XDocument.Load(nuspecStream, LoadOptions.None);
            if (doc.Root == null)
                throw new InvalidOperationException();
            var nmspace = doc.Root.Name.NamespaceName;
            var metadata = doc.Root.Element(XName.Get("metadata", nmspace));
            if (metadata == null)
                throw new InvalidOperationException();


            var license = metadata.Element(XName.Get("license", nmspace));
            var licenseUrl = metadata.Element(XName.Get("licenseUrl", nmspace))?.Value;

            AddThirdPartyIfExists(archive, "ThirdPartyNotice.txt");
            AddThirdPartyIfExists(archive, "NOTICE");


            if (!string.IsNullOrWhiteSpace(licenseUrl) && !licenseUrl.EndsWith("deprecateLicenseUrl"))
            {
                var lic = License.GetLicense(LicenseType.Url, licenseUrl);
                if (lic != null)
                    Licenses.Add(lic);
            }

            if (license != null)
            {
                var tp = license.Attribute(XName.Get("type", string.Empty));
                if (tp?.Value == "file")
                {
                    using var licenseStream = archive.GetEntry(license.Value)?.Open() ??
                                              throw new FileNotFoundException("License file not found");
                    using var licenseReader = new StreamReader(licenseStream);
                    Licenses.Add(License.GetLicense(LicenseType.File, licenseReader.ReadToEnd()));
                }
                else
                {
                    Licenses.Add(License.GetLicense(LicenseType.Expression, license.Value));
                }
            }


            var depGroups = metadata.Element(XName.Get("dependencies", nmspace));

            if (depGroups == null)
                return;

            foreach (var dGroup in depGroups.Elements(XName.Get("group", nmspace)))
            {
                foreach (var dep in dGroup.Elements(XName.Get("dependency", nmspace)))
                {
                    var depName = dep.Attribute(XName.Get("id", string.Empty))?.Value;
                    var depVersion = dep.Attribute(XName.Get("version", string.Empty))?.Value;
                    if (_ignoredPackages.Contains(depName))
                        continue;
                    if (string.IsNullOrWhiteSpace(depName) || string.IsNullOrWhiteSpace(depVersion))
                        continue;

                    Dependencies.Add(Package.GetPackage(depName, depVersion, true));
                }
            }
        }

        public override string ToString()
        {
            return $"{Name}={Version}";
        }

        public HashSet<License> Licenses { get; }
        
        public HashSet<Package> Dependencies { get; }

        private struct VersionRangeItem
        {
            public VersionRangeItem(string version, bool inclusive)
            {
                Version = version;
                Inclusive = inclusive;
            }
            public string Version { get; }
            public bool Inclusive { get; }

            public override string ToString()
            {
                return $"{(Inclusive ? "+" : "-")}{Version}";
            }
        }

        private static (VersionRangeItem from, VersionRangeItem? to) ParseVersionRange(string version)
        {
            version = version.Trim();
            var splt = version.TrimmedSplit(new [] { ',' }, 2);
            if (splt.Length == 1)
            {
                return (new VersionRangeItem(version.ToLower(), true), null);
            }

            if (splt.Length != 2)
                throw new FormatException("Not a valid version range format");

            bool fromInclusive = splt[0].StartsWith('[');
            bool fromExclusive = splt[0].StartsWith('(');
            bool toInclusive = splt[1].EndsWith(']');
            bool toExclusive = splt[1].EndsWith(')');
            if (!fromExclusive && !fromInclusive)
                throw new FormatException("Range needs to start with either '(' or '[' respectively.");
            if (!toExclusive && !toInclusive)
                throw new FormatException("Range needs to end with either ')' or ']' respectively.");

            return (new VersionRangeItem(splt[0][1..].Trim().ToLower(), fromInclusive),
                new VersionRangeItem(splt[1][..^1].Trim().ToLower(), toInclusive));
        }
        
        public static Package GetPackage(string name, string version, bool loadPackage)
        {
            name = name.ToLower();
            var parsedVersion = ParseVersionRange(version);
            version = parsedVersion.from.Version; // TODO: handle version ranges correctly
            var key = new PackageCache.PackageIdentifier(name, version);
            if (PackageCache.Packages.TryGetValue(key, out var package))
                return package;
            var p = new Package(name, version, loadPackage);
            PackageCache.Packages.Add(key, p);
            return p;
        }

        public string Name { get; }
        public string Version { get; }

        public bool Equals(Package other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Package) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Version != null ? Version.GetHashCode() : 0);
            }
        }
    }
}