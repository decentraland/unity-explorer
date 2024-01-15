using DCL.ECSComponents;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.NftShape.Frame
{
    public class FramesPool : IFramesPool
    {
        private readonly IReadOnlyDictionary<NftFrameType, GameObject> prefabs;
        private readonly GameObject defaultPrefab;
        private readonly Dictionary<NftFrameType, IObjectPool<GameObject>> pools = new ();
        private readonly Dictionary<GameObject, NftFrameType> types = new ();

        public FramesPool(NftShapeSettings settings) : this(settings.FramePrefabs(), settings.DefaultFrame())
        {
        }

        public FramesPool(IReadOnlyDictionary<NftFrameType, GameObject> prefabs, GameObject defaultPrefab)
        {
            this.prefabs = prefabs;
            this.defaultPrefab = defaultPrefab;
        }

        public GameObject NewFrame(NftFrameType frameType, Transform parent)
        {
            var g = Pool(frameType).Get()!;
            g.transform.SetParent(parent, false);
            types[g] = frameType;
            return g;
        }

        public void Release(GameObject frame)
        {
            Pool(types[frame]).Release(frame);
            types.Remove(frame);
        }

        private IObjectPool<GameObject> Pool(NftFrameType frameType)
        {
            if (pools.TryGetValue(frameType, out var pool) == false)
            {
                pool = pools[frameType] = new ObjectPool<GameObject>(
                    () => GameObject.Instantiate(Prefab(frameType)),
                    g => g.SetActive(true),
                    g => g.SetActive(false),
                    g => UnityObjectUtils.SafeDestroyGameObject(g.transform)
                );
            }

            return pool!;
        }

        private GameObject Prefab(NftFrameType frameType) =>
            prefabs.TryGetValue(frameType, out var prefab)
                ? prefab!
                : defaultPrefab;
    }
}
