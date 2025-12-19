using DCL.Platforms;
using System;

namespace DCL.Ipfs
{
    public static class AssetBundleManifestHelper
    {
        public static bool IsQmEntity(string entityID)
        {
            var span = entityID.AsSpan();
            return span.Length >= 2 && span[0] == 'Q' && span[1] == 'm';
        }

        public static string SanitizeEntityHash(string inputHash)
        {
            if (IsQmEntity(inputHash) && IPlatform.DEFAULT.Is(IPlatform.Kind.Mac))
                return inputHash.ToLowerInvariant();

            return inputHash;
        }
    }
}