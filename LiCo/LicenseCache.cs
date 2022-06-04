using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LiCo
{
    public class LicenseCache
    {
        public readonly struct LicenseIdentifier : IEquatable<LicenseIdentifier>
        {
            public LicenseIdentifier(LicenseType type, string value)
            {
                Value = value;
                LicenseType = type;
            }

            public string Value { get; }
            public LicenseType LicenseType { get; }

            public bool Equals(LicenseIdentifier other)
            {
                return Value == other.Value && LicenseType == other.LicenseType;
            }

            public override bool Equals(object obj)
            {
                return obj is LicenseIdentifier other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Value, LicenseType);
            }
        }
        private static Dictionary<LicenseIdentifier, License> _licenses;
        public static Dictionary<LicenseIdentifier, License> Licenses => _licenses ??= new Dictionary<LicenseIdentifier, License>();
        private static HashSet<License> _licenseSet = new();

        public static bool TryGetValue(LicenseIdentifier key, [MaybeNullWhen(false)] out License license)
        {
            return Licenses.TryGetValue(key, out license);
        }

        public static License TryAdd(LicenseIdentifier key, License license)
        {
            if (_licenseSet.TryGetValue(license, out var actualLicense))
            {
                license = actualLicense;
            }
            else
            {
                _licenseSet.Add(license);
            }
            Licenses.Add(key, license);
            return license;
        }
    }
}