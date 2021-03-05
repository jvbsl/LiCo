using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        private Package(string name, string version)
        {
            Name = name;
            Version = version;

            const string packagesPath = "/home/julian/.nuget/packages";

            string path = Path.Combine(packagesPath, Name, Version, $"{Name}.{Version}.nupkg");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Nuget package not found: {path}");

            using var fs = File.OpenRead(path);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
            using var nuspecStream = archive.Entries.First(x => string.Compare(x.Name, $"{Name}.nuspec", StringComparison.InvariantCultureIgnoreCase) == 0).Open();

            var doc = XDocument.Load(nuspecStream, LoadOptions.None);
            if (doc.Root == null)
                throw new InvalidOperationException();
            var nmspace = doc.Root.Name.NamespaceName;
            var metadata = doc.Root.Element(XName.Get("metadata", nmspace));
            if (metadata == null)
                throw new InvalidOperationException();
            
            
            Licenses = new HashSet<License>();
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
                    using var licenseStream = archive.GetEntry(license.Value)?.Open() ?? throw new FileNotFoundException("License file not found");
                    using var licenseReader = new StreamReader(licenseStream);
                    Licenses.Add(License.GetLicense(LicenseType.File, licenseReader.ReadToEnd()));
                }
                else
                {
                    Licenses.Add(License.GetLicense(LicenseType.Expression, license.Value));
                }
            }
            
            Dependencies = new HashSet<Package>();
            
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

                    Dependencies.Add(Package.GetPackage(depName, depVersion));
                }
            }
        }
        
        public HashSet<License> Licenses { get; }
        
        public HashSet<Package> Dependencies { get; }

        public static Package GetPackage(string name, string version)
        {
            name = name.ToLower();
            version = version.ToLower();
            var key = new PackageCache.PackageIdentifier(name, version);
            if (PackageCache.Packages.TryGetValue(key, out var package))
                return package;
            var p = new Package(name, version);
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
            return HashCode.Combine(Name, Version);
        }
    }
}