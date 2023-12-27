using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.ResourcesUnloading;
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

        public ProfilePlugin(IProfileRepository profileRepository, IProfileCache profileCache, CacheCleaner cacheCleaner)
        {
            this.profileRepository = profileRepository;
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
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            LoadProfileSystem.InjectToWorld(ref builder,
                new ProfileIntentionCache(),
                mutexSync, profileRepository);
        }
    }
}
