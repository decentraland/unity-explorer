using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Fixed pointers are created once if <see cref="RealmComponent.ScenesAreFixed" />
    /// </summary>
    public struct FixedScenePointers
    {
        public readonly AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>[] Promises;

        // Quick path to avoid an iteration
        public bool AllPromisesResolved;

        public int EmptyParcelsLastProcessedIndex;

        public FixedScenePointers(AssetPromise<IpfsTypes.SceneEntityDefinition, GetSceneDefinition>[] promises)
        {
            Promises = promises;
            AllPromisesResolved = false;
            EmptyParcelsLastProcessedIndex = 0;
        }
    }
}
