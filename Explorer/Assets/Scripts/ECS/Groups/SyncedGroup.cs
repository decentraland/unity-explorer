using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Utility.Multithreading;

namespace ECS.Groups
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SyncedInitializationSystemGroup : SyncedGroup
    {
        public SyncedInitializationSystemGroup(MutexSync mutexSync) : base(mutexSync) { }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SyncedSimulationSystemGroup : SyncedGroup
    {
        public SyncedSimulationSystemGroup(MutexSync mutexSync) : base(mutexSync) { }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SyncedPresentationSystemGroup : SyncedGroup
    {
        public SyncedPresentationSystemGroup(MutexSync mutexSync) : base(mutexSync) { }
    }

    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class SyncedPostRenderingSystemGroup : SyncedGroup
    {
        public SyncedPostRenderingSystemGroup(MutexSync mutexSync) : base(mutexSync) { }
    }

    /// <summary>
    ///     Group synchronized by mutex, unique instance for each world
    /// </summary>
    public abstract class SyncedGroup : CustomGroupBase<float>
    {
        private readonly MutexSync mutexSync;

        protected SyncedGroup(MutexSync mutexSync)
        {
            this.mutexSync = mutexSync;
        }

        public override void BeforeUpdate(in float t)
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            BeforeUpdateInternal(in t);
        }

        public override void Update(in float t)
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            UpdateInternal(in t);
        }

        public override void AfterUpdate(in float t)
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            AfterUpdateInternal(in t);
        }

        public override void Dispose()
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            DisposeInternal();
        }

        public override void Initialize()
        {
            using MutexSync.Scope scope = mutexSync.GetScope();
            InitializeInternal();
        }
    }
}
