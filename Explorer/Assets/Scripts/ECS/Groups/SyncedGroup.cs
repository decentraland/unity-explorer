using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using SceneRunner.Scene;
using Utility.Multithreading;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SyncedInitializationSystemGroup : SyncedGroup
    {
        public SyncedInitializationSystemGroup(MutexSync mutexSync, ISceneStateProvider sceneStateProvider) : base(mutexSync, sceneStateProvider) { }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SyncedSimulationSystemGroup : SyncedGroup
    {
        public SyncedSimulationSystemGroup(MutexSync mutexSync, ISceneStateProvider sceneStateProvider) : base(mutexSync, sceneStateProvider) { }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SyncedPresentationSystemGroup : SyncedGroup
    {
        public SyncedPresentationSystemGroup(MutexSync mutexSync, ISceneStateProvider sceneStateProvider) : base(mutexSync, sceneStateProvider) { }
    }

    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class SyncedPostRenderingSystemGroup : SyncedGroup
    {
        public SyncedPostRenderingSystemGroup(MutexSync mutexSync, ISceneStateProvider sceneStateProvider) : base(mutexSync, sceneStateProvider) { }
    }

    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    public partial class SyncedPostPhysicsSystemGroup : SyncedGroup
    {
        public SyncedPostPhysicsSystemGroup(MutexSync mutexSync, ISceneStateProvider sceneStateProvider) : base(mutexSync, sceneStateProvider) { }
    }

    /// <summary>
    ///     <para>Group is:</para>
    ///     <para>Synchronized by mutex so no changes to the ECS World can be made from Systems and CRDT Bridge simultaneously;</para>
    ///     <para>Preventing systems from running if the scene is disposing (and thus Unity objects may be already destroyed, especially critical on application exit)</para>
    /// </summary>
    public abstract class SyncedGroup : CustomGroupBase<float>
    {
        private readonly MutexSync mutexSync;
        private readonly ISceneStateProvider sceneStateProvider;

        protected SyncedGroup(MutexSync mutexSync, ISceneStateProvider sceneStateProvider)
        {
            this.mutexSync = mutexSync;
            this.sceneStateProvider = sceneStateProvider;
        }

        public override void Initialize()
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            InitializeInternal();
        }

        public override void BeforeUpdate(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            using MutexSync.Scope scope = mutexSync.GetScope();
            BeforeUpdateInternal(in t, throttle);
        }

        public override void Update(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            using MutexSync.Scope scope = mutexSync.GetScope();
            UpdateInternal(in t, throttle);
        }

        public override void AfterUpdate(in float t, bool throttle)
        {
            if (sceneStateProvider.State != SceneState.Running)
                return;

            using MutexSync.Scope scope = mutexSync.GetScope();
            AfterUpdateInternal(in t, throttle);
        }

        public override void Dispose()
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            DisposeInternal();
        }
    }
}
