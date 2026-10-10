using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;

namespace ECS.SceneLifeCycle.Components
{
    public struct PortableExperienceScenePointers
    {
        public readonly AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] Promises;

        // Quick path to avoid an iteration
        public bool AllPromisesResolved;

        public PortableExperienceScenePointers(AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] promises)
        {
            Promises = promises;
            AllPromisesResolved = false;
        }
    }
}
