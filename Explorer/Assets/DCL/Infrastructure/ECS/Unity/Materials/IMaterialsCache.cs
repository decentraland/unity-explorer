using ECS.Unity.Materials.Components;
using UnityEngine;

namespace ECS.Unity.Materials
{
    /// <summary>
    ///     Shared Material Cache between different scenes, it's meant to be accessed from the main thread
    /// </summary>
    public interface IMaterialsCache
    {
        /// <summary>
        ///     Try get a material to use it for a unique entity (increasing the ref counter)
        /// </summary>
        bool TryReferenceMaterial(in MaterialData materialData, out Material material);

        /// <summary>
        ///     Adds an entry to the cache, it must be ensured beforehand that such entry does not exist in the cache
        ///     by calling <see cref="TryReferenceMaterial" />
        /// </summary>
        void Add(in MaterialData materialData, Material material);

        /// <summary>
        ///     Removes a reference to material, it must be called only once for each unique entity,
        ///     otherwise the ref counting mechanism will be faulty
        /// </summary>
        void Dereference(in MaterialData materialData);
    }
}
