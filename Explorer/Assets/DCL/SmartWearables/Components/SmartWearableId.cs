using ScenePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.SceneLifeCycle.Systems.GetSmartWearableSceneIntention.Result, ECS.SceneLifeCycle.Systems.GetSmartWearableSceneIntention>;

namespace ECS.SceneLifeCycle.Components
{
    public struct SmartWearableId
    {
        public string Value;
        public ScenePromise ScenePromise;
    }
}
