using Cysharp.Threading.Tasks;
using PortableExperiences.Controller;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System;
using System.Threading;

namespace CrdtEcsBridge.PortableExperiencesApi
{
    public class PortableExperiencesApiImplementation : IPortableExperiencesApi
    {
        private readonly IPortableExperiencesController portableExperiencesController;

        public PortableExperiencesApiImplementation(IPortableExperiencesController portableExperiencesController)
        {
            this.portableExperiencesController = portableExperiencesController;
        }


        public async UniTask<object> SpawnAsync(string pid, string ens, CancellationToken ct)
        {
            await portableExperiencesController.CreatePortableExperienceAsync(ens, pid, ct);
            return null;
        }

        public async UniTask<bool> KillAsync(string ens, CancellationToken ct)
        {
            if (!portableExperiencesController.CanKillPortableExperience(ens)) return false;

            await portableExperiencesController.UnloadPortableExperienceAsync(ens, ct);

            return true;
        }

        public async UniTask<bool> ExitAsync(string ens, CancellationToken ct)
        {
            await portableExperiencesController.UnloadPortableExperienceAsync(ens, ct);
            return true;
        }

        public bool GetPortableExperiencesLoadedAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public struct SpawnResponse
        {
            private string pid;
            private string parent_cid;
            private string name;
            private string ens;
        }

        public void Dispose()
        { }
    }
}
