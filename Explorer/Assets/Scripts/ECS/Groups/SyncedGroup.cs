using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using SceneRunner.Scene;
using Utility.Multithreading;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SyncedInitializationSystemGroup : SyncedGroup
    {
        public SyncedInitializationSystemGroup(MultithreadSync multithreadSync, ISceneStateProvider sceneStateProvider)
            : base(multithreadSync, sceneStateProvider)
        {
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SyncedSimulationSystemGroup : SyncedGroup
    {
        public SyncedSimulationSystemGroup(MultithreadSync multithreadSync, ISceneStateProvider sceneStateProvider) :
            base(multithreadSync, sceneStateProvider)
        {
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SyncedPresentationSystemGroup : SyncedGroup
    {
        public SyncedPresentationSystemGroup(MultithreadSync multithreadSync, ISceneStateProvider sceneStateProvider) :
            base(multithreadSync, sceneStateProvider)
        {
        }
    }

    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    public partial class SyncedPreRenderingSystemGroup : SyncedGroup
    {
        public SyncedPreRenderingSystemGroup(MultithreadSync multithreadSync, ISceneStateProvider sceneStateProvider) :
            base(multithreadSync, sceneStateProvider)
        {
        }
    }

    /// <summary>
    ///     <para>Group is:</para>
    ///     <para>Synchronized by mutex so no changes to the ECS World can be made from Systems and CRDT Bridge simultaneously;</para>
    ///     <para>Preventing systems from running if the scene is disposing (and thus Unity objects may be already destroyed, especially critical on application exit)</para>
    /// </summary>
    public abstract class SyncedGroup : CustomGroupBase<float>
    {
        private readonly MultithreadSync? multithreadSync;
        private readonly ISceneStateProvider sceneStateProvider;

        protected SyncedGroup(MultithreadSync? multithreadSync, ISceneStateProvider sceneStateProvider)
        {
            this.multithreadSync = multithreadSync;
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

            // If Mutex is not acquired throttle the system
            if (multithreadSync is not null && !multithreadSync.Acquired) return;

            BeforeUpdateInternal(in t, throttle);
        }

        public override void Update(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            // If Mutex is not acquired throttle the system
            if (multithreadSync is not null && !multithreadSync.Acquired) return;

            UpdateInternal(in t, throttle);
        }

        public override void AfterUpdate(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            // If Mutex is not acquired throttle the system
            if (multithreadSync is not null && !multithreadSync.Acquired) return;

            AfterUpdateInternal(in t, throttle);
        }

        public override void Dispose()
        {
            // If Mutex is not acquired throttle the system
            if (multithreadSync is not null && !multithreadSync.Acquired) return;

            DisposeInternal();
        }
    }
}
