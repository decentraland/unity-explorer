using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.AddressableAssets;

namespace DCL.UnityFacades
{
    /// <summary>
    ///     Load-state queries on an <see cref="AssetReference" /> without exposing its operation handle. Under the shim
    ///     this assembly is replaced by one backed by the content crate (BevyMigration/docs/README.md § 9).
    /// </summary>
    public static class AssetReferenceFacade
    {
        /// <summary>
        ///     True when a load started on this reference and has not been released, finished or not.
        /// </summary>
        public static bool HasActiveLoad(this AssetReference reference) =>
            reference.OperationHandle.IsValid();

        /// <summary>
        ///     True when the reference's load has completed (successfully or not); false while loading or when never loaded.
        /// </summary>
        public static bool IsLoadDone(this AssetReference reference) =>
            reference.IsDone;

        /// <summary>
        ///     Waits for the active load of this reference, if any, to finish.
        /// </summary>
        public static async UniTask WaitForActiveLoadAsync(this AssetReference reference, CancellationToken ct)
        {
            if (reference.OperationHandle.IsValid())
                await reference.OperationHandle.WithCancellation(ct);
        }
    }
}
