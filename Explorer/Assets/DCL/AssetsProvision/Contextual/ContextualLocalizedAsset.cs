using Cysharp.Threading.Tasks;
using RichTypes;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Utility.Ownership;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    public sealed class ContextualLocalizedAsset<T> : IDisposable where T: Object
    {
        private readonly LocalizedAsset<T> localizedAsset;
        private readonly AssetTable assetTable;
        private readonly CancellationTokenSource cancellationTokenSource;

        private Owned<T>? asset;

        public ContextualLocalizedAsset(LocalizedAsset<T> localizedAsset, AssetTable assetTable)
        {
            this.localizedAsset = localizedAsset;
            this.assetTable = assetTable;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public async UniTask<Weak<T>> AssetAsync()
        {
            if (asset == null)
            {
                T value = await localizedAsset.LoadAssetAsync().ToUniTask();
                asset = new Owned<T>(value);
            }

            return asset!.Downgrade();
        }

        public void Release()
        {
            if (asset == null) return;
            asset!.Dispose(out T? _); // T : UnityEngine.Object doesn't require additional release logic
            assetTable.ReleaseAsset(localizedAsset.TableEntryReference);
            asset = null;
        }

        public void Dispose()
        {
            Release();
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
    }
}
