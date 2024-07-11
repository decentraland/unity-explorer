using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System;

namespace CrdtEcsBridge.PortableExperiencesApi
{
    public class PortableExperiencesApi : IPortableExperiencesApi
    {


        public async UniTask<object> SpawnAsync(string pid, string ens)
        {

            return null;
        }

        public bool KillAsync(string pid)
        {
            return true;
        }

        public bool ExitAsync(string predefinedEmote)
        {
            return true;
        }

        public bool GetPortableExperiencesLoadedAsync() =>
            throw new NotImplementedException();

        public struct SpawnResponse
        {
            private string pid;
            private string parent_cid;
            private string name;
            private string ens;
        }
    }
}
