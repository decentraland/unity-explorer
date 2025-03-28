using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.SDKComponents.NFTShape.Frames.Pool
{
    public class FramesPool : IFramesPool
    {
        private readonly Transform framePoolParent;
        private readonly IReadOnlyFramePrefabs framePrefabs;
        private readonly Dictionary<NftFrameType, IObjectPool<AbstractFrame>> pools = new ();
        private readonly Dictionary<AbstractFrame, NftFrameType> types = new ();

        public FramesPool(IReadOnlyFramePrefabs framePrefabs, IComponentPoolsRegistry? componentPoolsRegistry = null)
        {
            this.framePrefabs = framePrefabs;
            var poolRoot = componentPoolsRegistry?.RootContainerTransform();
            framePoolParent = new GameObject("POOL_CONTAINER_NFT_FRAMES").transform;
            framePoolParent.parent = poolRoot;
        }

        public bool IsInitialized => framePrefabs.IsInitialized;

        public AbstractFrame NewFrame(NftFrameType frameType, Transform parent)
        {
            var g = Pool(frameType).Get()!;
            g.transform.SetParent(parent, false);
            g.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            types[g] = frameType;
            return g;
        }

        public void Release(AbstractFrame frame)
        {
            Pool(types[frame]).Release(frame);
            types.Remove(frame);
        }

        private IObjectPool<AbstractFrame> Pool(NftFrameType frameType)
        {
            if (pools.TryGetValue(frameType, out var pool) == false)
            {
                pool = pools[frameType] = new ObjectPool<AbstractFrame>(
                    () => Object.Instantiate(framePrefabs.FrameOrDefault(frameType)),
                    g => g.gameObject.SetActive(true),
                    g =>
                    {
                        g.transform.SetParent(framePoolParent);
                        g.gameObject.SetActive(false);
                    },
                    g => UnityObjectUtils.SafeDestroyGameObject(g.transform)
                );
            }

            return pool!;
        }
    }
}
