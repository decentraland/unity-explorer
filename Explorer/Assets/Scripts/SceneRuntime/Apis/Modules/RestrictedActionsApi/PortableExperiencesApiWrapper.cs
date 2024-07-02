using JetBrains.Annotations;

namespace SceneRuntime.Apis.Modules.PortableExperiencesApi
{
    public class PortableExperiencesApiWrapper : IJsApiWrapper
    {
        private readonly IPortableExperiencesApi api;

        public PortableExperiencesApiWrapper(IPortableExperiencesApi api)
        {
            this.api = api;
        }

        public void Dispose() { }

        [PublicAPI("Used by StreamingAssets/Js/Modules/PortableExperiences.js")]
        public object Spawn(string pid, string ens) =>
            api.SpawnAsync(pid, ens);
    }
}
