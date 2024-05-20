using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.ResourcesUnloading;
using ECS.LifeCycle.Systems;
using ECS.StreamableLoading.Cache;
using System.Threading;
using Utility.Multithreading;

namespace DCL.PluginSystem.Global
{
    public class ProfilePlugin : IDCLGlobalPlugin
    {
        private readonly IProfileRepository profileRepository;
        private readonly IProfileCache profileCache;
        private readonly CacheCleaner cacheCleaner;
        private readonly IStreamableCache<Profile, GetProfileIntention> profileIntentionCache;

        public ProfilePlugin(IProfileRepository profileRepository, IProfileCache profileCache, CacheCleaner cacheCleaner,
            IStreamableCache<Profile, GetProfileIntention> profileIntentionCache)
        {
            this.profileRepository = profileRepository;
            this.profileCache = profileCache;
            this.cacheCleaner = cacheCleaner;
            this.profileIntentionCache = profileIntentionCache;
        }

        public void Dispose() { }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            cacheCleaner.Register(profileCache);
            cacheCleaner.Register(profileIntentionCache);
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadProfileSystem.InjectToWorld(ref builder, profileIntentionCache, profileRepository);
            ResolveProfilePictureSystem.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<Profile>.InjectToWorld(ref builder);
        }
    }
}
