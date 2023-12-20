using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using ECS.StreamableLoading.Cache;
using System.Threading;
using Utility.Multithreading;

namespace DCL.PluginSystem.Global
{
    public class ProfilePlugin : IDCLGlobalPlugin
    {
        private readonly IProfileRepository profileRepository;
        private IDCLGlobalPlugin idclGlobalPluginImplementation;

        public ProfilePlugin(IProfileRepository profileRepository)
        {
            this.profileRepository = profileRepository;
        }

        public void Dispose() { }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // not synced by mutex, for compatibility only
            var mutexSync = new MutexSync();

            LoadProfileSystem.InjectToWorld(ref builder,
                new NoCache<Profile, GetProfileIntention>(false, false),
                mutexSync, profileRepository);
        }
    }
}
