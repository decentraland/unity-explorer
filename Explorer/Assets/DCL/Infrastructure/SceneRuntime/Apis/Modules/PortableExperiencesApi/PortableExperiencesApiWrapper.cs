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
    public class PortableExperiencesApiWrapper : JsApiWrapper
    {
        private readonly IJavaScriptApiExceptionsHandler exceptionsHandler;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly ISceneData sceneData;

        public PortableExperiencesApiWrapper(IPortableExperiencesController portableExperiencesController, IJavaScriptApiExceptionsHandler exceptionsHandler, CancellationTokenSource disposeCts)
            : base(disposeCts)
        {
            this.portableExperiencesController = portableExperiencesController;
            this.exceptionsHandler = exceptionsHandler;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Spawn(string pid, string ens) =>
            SpawnAsync(new URN(pid), new ENS(ens), disposeCts.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(this);

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Kill(string ens) =>
            KillAsync(new ENS(ens), disposeCts.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(this);

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Exit() =>
            ExitAsync().ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(this);

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object GetLoadedPortableExperiences() =>
            GetLoadedPortableExperiences(disposeCts.Token);

        private async UniTask<IPortableExperiencesController.SpawnResponse> SpawnAsync(URN pid, ENS ens, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            //We should check if pid is valid, if not, check if ens is valid, else, return error, for now we only support loading by ens.
            return await portableExperiencesController.CreatePortableExperienceByEnsAsync(ens, ct);
        }

        private async UniTask<IPortableExperiencesController.ExitResponse> KillAsync(ENS ens, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            if (!portableExperiencesController.CanKillPortableExperience(ens))
                return new IPortableExperiencesController.ExitResponse { status = false };

            return portableExperiencesController.UnloadPortableExperienceByEns(ens);
        }

        private async UniTask<IPortableExperiencesController.ExitResponse> ExitAsync()
        {
            await UniTask.SwitchToMainThread();
            return portableExperiencesController.UnloadPortableExperienceByEns(new ENS(sceneData.SceneEntityDefinition.id));
        }

        private List<IPortableExperiencesController.SpawnResponse> GetLoadedPortableExperiences(CancellationToken ct) =>
            portableExperiencesController.GetAllPortableExperiences();
    }
}
