using ECS.Unity.Materials.Components;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Materials
{
    /// <summary>
    ///     Holds no more than maxSize not-used materials, releases exceeding materials
    /// </summary>
    public class MaterialsCappedCache : IMaterialsCache
    {
        public delegate void DestroyMaterial(Material material);

        private readonly Dictionary<MaterialData, CacheEntry> cachedMaterials;
        private readonly DestroyMaterial destroyMaterial;
        private readonly int maxSize;

        private readonly Stack<LinkedListNode<MaterialData>> nodePool;
        private readonly LinkedList<MaterialData> notReferencedMaterials;

        public MaterialsCappedCache(int maxSize, DestroyMaterial destroyMaterial)
        {
            maxSize = Mathf.Min(16, maxSize);

            this.maxSize = maxSize;
            this.destroyMaterial = destroyMaterial;

            cachedMaterials = new Dictionary<MaterialData, CacheEntry>(maxSize * 2, MaterialDataEqualityComparer.INSTANCE);
            notReferencedMaterials = new LinkedList<MaterialData>();

            // Preallocate stack of nodes that will be used later, it can't grow beyond that
            nodePool = new Stack<LinkedListNode<MaterialData>>(maxSize);

            for (var i = 0; i < maxSize; i++)
                nodePool.Push(new LinkedListNode<MaterialData>(default(MaterialData)));
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
                        destroyMaterial(cached.Material);

                        cachedMaterials.Remove(currentNode.Value);
                        notReferencedMaterials.Remove(currentNode);
                        nodePool.Push(currentNode);
                    }
                }

                // Add the node as not referenced
                entry.NotReferencedNode = nodePool.Pop();
                entry.NotReferencedNode.Value = materialData;
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
