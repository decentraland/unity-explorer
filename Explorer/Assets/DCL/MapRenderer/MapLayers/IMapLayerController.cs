using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.MapRenderer.MapCameraController;
using System;
using System.Threading;

namespace DCL.MapRenderer.MapLayers
{
    internal interface IMapLayerController<in T> : IMapLayerController
    {
        void SetParameter(T param);

        void IMapLayerController.SetParameter(IMapLayerParameter mapLayerParameter) =>
            SetParameter((T)mapLayerParameter);
    }

    internal interface IMapLayerController : IDisposable
    {
        UniTask InitializeAsync(CancellationToken cancellationToken);

        void CreateSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder) { }

        /// <summary>
        /// Enable layer
        /// </summary>
        /// <param name="cancellationToken">Cancellation Token is bound to both `Abort` (changing to the `Disabled` state) and `Dispose`</param>
        UniTask Enable(CancellationToken cancellationToken);

        /// <summary>
        /// Disable layer
        /// </summary>
        /// <param name="cancellationToken">Cancellation Token is bound to both `Abort` (changing to the `Enabled` state) and `Dispose`</param>
        UniTask Disable(CancellationToken cancellationToken);

        void SetParameter(IMapLayerParameter layerParameter) { }
    }
}
