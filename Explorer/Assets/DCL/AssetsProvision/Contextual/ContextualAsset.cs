using Cysharp.Threading.Tasks;
using RichTypes;
using System;
using System.Threading;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DCL.AssetsProvision
{
    public class ContextualAsset<T> : IDisposable where T: Object
    {
        private readonly AssetReferenceT<T> reference;
        private Owned<T>? asset;

        public enum State
        {
            Unloaded,
            Loading,
            Loaded,
        }

        public State CurrentState { get; private set; }

        public ContextualAsset(AssetReferenceT<T> reference)
        {
            this.reference = reference;
            asset = null;
            CurrentState = State.Unloaded;
        }

        public async UniTask<Weak<T>> AssetAsync(CancellationToken token)
        {
            try
            {
                if (asset == null)
                {
                    CurrentState = State.Loading;
                    var handle = reference.LoadAssetAsync();
                    await handle.Task!.AsUniTask().AttachExternalCancellation(token);
                    if (handle.Status != AsyncOperationStatus.Succeeded) throw new Exception($"Load failed: {reference.RuntimeKey}");
                    T value = handle.Result!;
                    asset = new Owned<T>(value);
                    CurrentState = State.Loaded;
                }

                return asset!.Downgrade();
            }
            catch (Exception)
            {
                CurrentState = State.Unloaded;
                throw;
            }
        }

        public void Release()
        {
            if (asset == null) return;
            reference.ReleaseAsset();
            asset.Dispose(out _);
            asset = null;
            CurrentState = State.Unloaded;
        }

        public void Dispose()
        {
            Release();
        }
    }
}
