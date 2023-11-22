using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCLServices.MapRenderer.MapLayers.Atlas
{
    internal interface IAtlasController : IMapLayerController
    {
        UniTask InitializeAsync(CancellationToken ct);
    }
}
