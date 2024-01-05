using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.SDKComponents.SceneUI.Systems;
using ECS.Groups;
using UITransformInstantiationSystem = DCL.SDKComponents.SceneUI.Systems.UITransform.UITransformInstantiationSystem;

namespace DCL.SDKComponents.SceneUI.Groups
{
    /// <summary>
    ///     Denotes the group that instantiates specific components right after the entity transform is handled
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformInstantiationSystem))]
    [ThrottlingEnabled]
    public partial class SceneUIComponentInstantiationGroup { }
}
