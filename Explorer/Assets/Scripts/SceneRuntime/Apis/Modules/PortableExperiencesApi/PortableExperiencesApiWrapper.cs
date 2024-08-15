using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using PortableExperiences.Controller;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules.PortableExperiencesApi
{
    public class PortableExperiencesApiWrapper : IJsApiWrapper
    {
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IJavaScriptApiExceptionsHandler exceptionsHandler;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly ISceneData sceneData;

        public PortableExperiencesApiWrapper(IPortableExperiencesController portableExperiencesController, IJavaScriptApiExceptionsHandler exceptionsHandler)
        {
            this.portableExperiencesController = portableExperiencesController;
            this.exceptionsHandler = exceptionsHandler;
            cancellationTokenSource = new CancellationTokenSource();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Spawn(string pid, string ens)
        {
            try { return SpawnAsync(new URN(pid), new ENS(ens), cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Kill(string ens)
        {
            try { return KillAsync(new ENS(ens), cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Exit()
        {
            try { return ExitAsync(cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object GetLoadedPortableExperiences() =>
            GetLoadedPortableExperiences(cancellationTokenSource.Token);


        private async UniTask<IPortableExperiencesController.SpawnResponse> SpawnAsync(URN pid, ENS ens, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            //Check if pid is valid, if not, check if ens is valid, else, return error.
            return await portableExperiencesController.CreatePortableExperienceByEnsAsync(ens, ct);
        }

        private async UniTask<IPortableExperiencesController.ExitResponse> KillAsync(ENS ens, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            if (!portableExperiencesController.CanKillPortableExperience(ens))
                return new IPortableExperiencesController.ExitResponse { status = false };

            return await portableExperiencesController.UnloadPortableExperienceByEnsAsync(ens, ct);
        }

        private async UniTask<IPortableExperiencesController.ExitResponse> ExitAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            return await portableExperiencesController.UnloadPortableExperienceByEnsAsync(new ENS(sceneData.SceneEntityDefinition.id), ct);
        }

        private List<IPortableExperiencesController.SpawnResponse> GetLoadedPortableExperiences(CancellationToken ct) =>
            portableExperiencesController.GetAllPortableExperiences();


        public void Dispose()
        {
            cancellationTokenSource.Dispose();
        }

    }
}
