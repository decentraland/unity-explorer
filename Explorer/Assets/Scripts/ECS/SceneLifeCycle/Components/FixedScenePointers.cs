using DCL.Ipfs;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Fixed pointers are created once if <see cref="RealmComponent.ScenesAreFixed" />
    /// </summary>
    public struct FixedScenePointers
    {
        public readonly AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] URNScenePromises;
        public AssetPromise<SceneDefinitions, GetSceneDefinitionList> PointerScenesPromise { get; private set; }

        // Quick path to avoid an iteration
        public bool AllPromisesResolved;

        public int EmptyParcelsLastProcessedIndex;

        public FixedScenePointers(AssetPromise<SceneEntityDefinition, GetSceneDefinition>[] urnScenePromises, AssetPromise<SceneDefinitions, GetSceneDefinitionList> pointerScenesPromise = default)
        {
            URNScenePromises = urnScenePromises;
            PointerScenesPromise = pointerScenesPromise;
            AllPromisesResolved = false;
            EmptyParcelsLastProcessedIndex = 0;
        }
    }
}
