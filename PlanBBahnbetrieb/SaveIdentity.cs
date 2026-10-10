using System;
using System.IO;
using System.Security.Cryptography;

namespace PlanBBahnbetrieb
{
    internal static class SaveIdentity
    {
        internal static string Fingerprint(string path)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }
        internal static bool Matches(int version, long expectedLength, long expectedTicks, string expectedHash,
            long actualLength, long actualTicks, string actualHash)
        {
            if (expectedLength != actualLength) return false;
            // randomSeedUsed describes generation initialisation, not the loaded planet.
            // Legacy records have no content hash; preserve their original exact-file check.
            if (version == 1 || version == 2) return expectedTicks == actualTicks;
            return version == 3 && !string.IsNullOrEmpty(expectedHash) &&
                string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
