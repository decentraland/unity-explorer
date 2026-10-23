using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UnityFacades;
using Object = UnityEngine.Object;
using System;
using UnityEngine.AddressableAssets;

namespace DCL.Utilities.Addressables
{
    public static class ValidatableExtensions
    {
        public static async UniTask<Exception?> EnsureValidWithExceptionAsync(this AssetReference? assetReference, string name)
        {
            ReportHub.Log(ReportData.UNSPECIFIED, "Validate AssetReference: " + name);

            if (assetReference == null)
                return new Exception("AssetReference is null: " + name);

            using var ensureInitialState = new EnsureInitialState(assetReference);

            try
            {
                await assetReference.LoadAssetAsync<Object>().ToUniTask();
                assetReference.ReleaseAsset();
                return null;
            }
            catch (Exception e) { return new Exception("AssetReference is not valid: " + name, e); }
        }

        private readonly struct EnsureInitialState : IDisposable
        {
            private readonly AssetReference reference;
            private readonly bool isLoaded;

            public EnsureInitialState(AssetReference reference) : this()
            {
                this.reference = reference;
                isLoaded = reference.IsLoadDone();

                if (this.reference.IsLoadDone())
                    this.reference.ReleaseAsset();
            }

            public void Dispose()
            {
                if (isLoaded && !reference.IsLoadDone())
                    reference.LoadAssetAsync<Object>().ToUniTask().Forget();
            }
        }
    }
}
