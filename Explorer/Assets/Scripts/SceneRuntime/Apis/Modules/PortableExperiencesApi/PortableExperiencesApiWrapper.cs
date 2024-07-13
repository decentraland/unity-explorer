using JetBrains.Annotations;
using SceneRunner.Scene.ExceptionsHandling;
using System.Threading;

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
            cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose() { }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Spawn(string pid, string ens) =>
            api.SpawnAsync(pid, ens, cancellationTokenSource.Token);
    }
}
