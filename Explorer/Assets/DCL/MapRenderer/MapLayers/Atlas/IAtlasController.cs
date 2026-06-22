using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    internal interface IAtlasController : IMapLayerController
    {
        new UniTask InitializeAsync(CancellationToken ct);
    }
}
