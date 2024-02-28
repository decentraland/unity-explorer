using System;
using UnityEngine.AddressableAssets;

namespace DCL.Utilities.Addressables
{
    public static class ValidatableExtensions
    {
        public static void EnsureValid(this AssetReference assetReference, string name)
        {
            if (assetReference.EnsureValidWithException(name) is { } e)
                throw e;
        }

        public static Exception? EnsureValidWithException(this AssetReference assetReference, string name) =>
            assetReference.IsValid() == false ? new Exception("AssetReference is not valid: " + name) : null;
    }
}
