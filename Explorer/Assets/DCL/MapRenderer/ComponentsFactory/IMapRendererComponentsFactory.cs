using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.MapRenderer.ComponentsFactory
{
    /// <summary>
    /// Abstraction needed to produce different types of elements and resolve dependencies between them
    /// based on the type of `MapRenderer`: e.g. Chunk based vs Shader based
    /// </summary>
    public interface IMapRendererComponentsFactory
    {
        internal UniTask<MapRendererComponents> CreateAsync(CancellationToken cancellationToken);
    }
}
