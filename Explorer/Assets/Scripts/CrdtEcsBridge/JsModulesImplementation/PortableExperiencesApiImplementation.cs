using Cysharp.Threading.Tasks;
using PortableExperiences.Controller;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CrdtEcsBridge.PortableExperiencesApi
{
    public class PortableExperiencesApiImplementation : IPortableExperiencesApi
    {
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly ISceneData sceneData;

        public PortableExperiencesApiImplementation(IPortableExperiencesController portableExperiencesController, ISceneData sceneData)
        {
            this.portableExperiencesController = portableExperiencesController;
            this.sceneData = sceneData;
        }

        public void Dispose() { }

        public async UniTask<IPortableExperiencesApi.SpawnResponse> SpawnAsync(string pid, string ens, CancellationToken ct) =>
            await portableExperiencesController.CreatePortableExperienceAsync(ens, pid, ct);

        public async UniTask<IPortableExperiencesApi.ExitResponse> KillAsync(string ens, CancellationToken ct)
        {
            if (!portableExperiencesController.CanKillPortableExperience(ens))
                return new IPortableExperiencesApi.ExitResponse
                    { status = false };

            return await portableExperiencesController.UnloadPortableExperienceAsync(ens, ct);
        }

        public async UniTask<IPortableExperiencesApi.ExitResponse> ExitAsync(CancellationToken ct) =>
            await portableExperiencesController.UnloadPortableExperienceAsync(sceneData.SceneEntityDefinition.id, ct);

        public List<IPortableExperiencesApi.SpawnResponse> GetxLoadedgetPortableExperiences(CancellationToken ct) =>
            portableExperiencesController.GetAllPortableExperiences();
    }
}
