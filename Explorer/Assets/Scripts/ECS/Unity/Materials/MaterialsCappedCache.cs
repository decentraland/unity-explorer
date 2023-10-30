using ECS.Unity.Materials.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Materials
{
    public delegate void DestroyMaterial(in MaterialData materialData, Material material);

    /// <summary>
    ///     Holds no more than maxSize not-used materials, releases exceeding materials
    /// </summary>
    public class MaterialsCappedCache : IMaterialsCache
    {
        internal const int MIN_SIZE = 16;

        private readonly Dictionary<MaterialData, CacheEntry> cachedMaterials;
        private readonly DestroyMaterial destroyMaterial;
        private readonly int maxSize;

        private readonly Stack<LinkedListNode<MaterialData>> nodePool;
        private readonly LinkedList<MaterialData> notReferencedMaterials;

        internal int Count => cachedMaterials.Count;

        public MaterialsCappedCache(int maxSize, DestroyMaterial destroyMaterial)
        {
            maxSize = Mathf.Min(MIN_SIZE, maxSize);

            this.maxSize = maxSize;
            this.destroyMaterial = destroyMaterial;

            cachedMaterials = new Dictionary<MaterialData, CacheEntry>(maxSize * 2, MaterialDataEqualityComparer.INSTANCE);
            notReferencedMaterials = new LinkedList<MaterialData>();

            // Preallocate stack of nodes that will be used later, it can't grow beyond that
            nodePool = new Stack<LinkedListNode<MaterialData>>(maxSize);

            for (var i = 0; i < maxSize; i++)
                nodePool.Push(new LinkedListNode<MaterialData>(default(MaterialData)));
        }

        internal bool TryGetCacheEntry(in MaterialData materialData, out (Material material, int refCount) entry)
        {
            if (cachedMaterials.TryGetValue(materialData, out CacheEntry cacheEntry))
            {
                entry = (cacheEntry.Material, cacheEntry.RefCount);
                return true;
            }

            entry = default((Material material, int refCount));
            return false;
        }

        public bool TryReferenceMaterial(in MaterialData materialData, out Material material)
        {
            if (cachedMaterials.TryGetValue(materialData, out CacheEntry cacheEntry))
            {
                cacheEntry.RefCount++;

                TryReviveNode(ref cacheEntry);

                cachedMaterials[materialData] = cacheEntry;
                material = cacheEntry.Material;

                return true;
            }

            material = default(Material);
            return false;
        }

        public void Add(in MaterialData materialData, Material material)
        {
            var entry = new CacheEntry { Material = material, RefCount = 1 };
            cachedMaterials.Add(materialData, entry);
        }

        public void Dereference(in MaterialData materialData)
        {
            if (!cachedMaterials.TryGetValue(materialData, out CacheEntry entry)) return;

            if (--entry.RefCount == 0)
            {
                // Release 25% nodes if capacity is exceeded
                if (notReferencedMaterials.Count >= maxSize)
                {
                    int releaseCount = maxSize / 4;

                    LinkedListNode<MaterialData> head = notReferencedMaterials.First;

                    while (head != null && releaseCount-- > 0)
                    {
                        LinkedListNode<MaterialData> currentNode = head;
                        head = head.Next;

                        CacheEntry cached = cachedMaterials[currentNode.Value];

                        // Destroy the cached Material
                        destroyMaterial(currentNode.Value, cached.Material);

                        cachedMaterials.Remove(currentNode.Value);
                        notReferencedMaterials.Remove(currentNode);
                        nodePool.Push(currentNode);
                    }
                }

                // Add the node as not referenced
                entry.NotReferencedNode = nodePool.Pop();
                entry.NotReferencedNode.Value = materialData;
                notReferencedMaterials.AddLast(entry.NotReferencedNode);
            }

            // Apply the changes (it's a struct)
            cachedMaterials[materialData] = entry;
        }

        private void TryReviveNode(ref CacheEntry entry)
        {
            if (entry.NotReferencedNode == null) return;

            nodePool.Push(entry.NotReferencedNode);
            entry.NotReferencedNode = null;
        }

        private struct CacheEntry
        {
            public Material Material;
            public int RefCount;
            public LinkedListNode<MaterialData> NotReferencedNode;
        }
    }
}
