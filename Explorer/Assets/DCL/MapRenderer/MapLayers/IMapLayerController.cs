using Cysharp.Threading.Tasks;
using DCLServices.MapRenderer.MapCameraController;
using System;
using System.Threading;

namespace DCLServices.MapRenderer.MapLayers
{
    internal interface IMapLayerController<in T> : IMapLayerController
    {
        void SetParameter(T param);

        void IMapLayerController.SetParameter(IMapLayerParameter mapLayerParameter) =>
            SetParameter((T)mapLayerParameter);
    }

    internal interface IMapLayerController : IDisposable
    {
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
