using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SceneRuntime.Apis.Modules.PortableExperiencesApi
{
    public class PortableExperiencesApiWrapper : JsApiWrapperBase<IPortableExperiencesApi>
    {
        private readonly IPortableExperiencesApi api;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly IJavaScriptApiExceptionsHandler exceptionsHandler;

        public PortableExperiencesApiWrapper(IPortableExperiencesApi api, IJavaScriptApiExceptionsHandler exceptionsHandler) : base(api)
        {
            this.api = api;
            this.exceptionsHandler = exceptionsHandler;
            cancellationTokenSource = new CancellationTokenSource();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Spawn(string pid, string ens)
        {
            try { return api.SpawnAsync(pid, ens, cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Kill(string pid, string ens)
        {
            try { return api.KillAsync(ens, cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Exit()
        {
            try { return api.ExitAsync(cancellationTokenSource.Token).ReportAndRethrowException(exceptionsHandler).ToDisconnectedPromise(); }
            catch (Exception e) { return Task.FromException(e).ToPromise(); }
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object GetLoadedPortableExperiences() =>
            api.GetxLoadedgetPortableExperiences(cancellationTokenSource.Token);
    }
}
