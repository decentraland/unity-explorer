using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.ResourcesUnloading;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class ProfilePlugin : IDCLGlobalPlugin
    {
        private readonly RealmProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly CacheCleaner cacheCleaner;

        public ProfilePlugin(RealmProfileRepository profileRepository, IProfileCache profileCache, CacheCleaner cacheCleaner)
        {
            this.profileCache = profileCache;
            this.cacheCleaner = cacheCleaner;
        }

        public void Dispose() { }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            cacheCleaner.Register(profileCache);
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ResolveProfilePictureSystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<Profile>.InjectToWorld(ref builder);
        }
    }
}
