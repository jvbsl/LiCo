using System;
using System.Collections.Generic;

namespace LiCo
{
    public static class PackageCache
    {
        public readonly struct PackageIdentifier : IEquatable<PackageIdentifier>
        {
            public PackageIdentifier(string name, string version)
            {
                Name = name;
                Version = version;
            }

            public string Name { get; }
            public string Version { get; }

            public bool Equals(PackageIdentifier other)
            {
                return Name == other.Name && Version == other.Version;
            }

            public override bool Equals(object obj)
            {
                return obj is PackageIdentifier other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, Version);
            }
        }
        private static Dictionary<PackageIdentifier, Package> _packages;
        public static Dictionary<PackageIdentifier, Package> Packages => _packages ??= new Dictionary<PackageIdentifier, Package>();
    }
}