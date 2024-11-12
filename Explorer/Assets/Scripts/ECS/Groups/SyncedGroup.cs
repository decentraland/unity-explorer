using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using SceneRunner.Scene;
using Utility.Multithreading;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SyncedInitializationSystemGroup : SyncedGroup
    {
        public SyncedInitializationSystemGroup(ISceneStateProvider sceneStateProvider) : base(sceneStateProvider)
        {
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SyncedSimulationSystemGroup : SyncedGroup
    {
        public SyncedSimulationSystemGroup(ISceneStateProvider sceneStateProvider) : base(sceneStateProvider)
        {
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SyncedPresentationSystemGroup : SyncedGroup
    {
        public SyncedPresentationSystemGroup(ISceneStateProvider sceneStateProvider) : base(sceneStateProvider)
        {
        }
    }

    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class SyncedPreRenderingSystemGroup : SyncedGroup
    {
        public SyncedPreRenderingSystemGroup(ISceneStateProvider sceneStateProvider) : base(sceneStateProvider)
        {
        }
    }

    /// <summary>
    ///     <para>Group is:</para>
    ///     <para>no longer synchronized by mutex as it's redundant: the mutex locks out the entire frame cycle</para>
    ///     <para>Preventing systems from running if the scene is disposing (and thus Unity objects may be already destroyed, especially critical on application exit)</para>
    /// </summary>
    public abstract class SyncedGroup : CustomGroupBase<float>
    {
        private readonly ISceneStateProvider sceneStateProvider;

        protected SyncedGroup(ISceneStateProvider sceneStateProvider)
        {
            this.sceneStateProvider = sceneStateProvider;
        }

        public override void Initialize()
        {
            InitializeInternal();
        }

        public override void BeforeUpdate(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            BeforeUpdateInternal(in t, throttle);
        }

        public override void Update(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            UpdateInternal(in t, throttle);
        }

        public override void AfterUpdate(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            AfterUpdateInternal(in t, throttle);
        }

        public override void Dispose()
        {
            DisposeInternal();
        }
    }
}
