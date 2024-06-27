using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using System;

namespace CrdtEcsBridge.PortableExperiencesApi
{
    public class PortableExperiencesApi : IPortableExperiencesApi
    {
        public async UniTask<object> SpawnAsync(string pid, string ens)
        {
            await UniTask.SwitchToMainThread();

            //We basically need to create the entity with the promise for the scene (scene definition component)
            //which we must load from portableExperienceUrn: PortableExperienceUrn = `urn:decentraland:off-chain:static-portable-experiences:${parcelIdentity.cid}`
            //Check https://github.com/decentraland/explorer/blob/5a2cae92e9c050c5f9eace0211ad0f917810302c/kernel/packages/shared/apis/PortableExperiences.ts

            //CreateSceneFacadePromise.Execute(World, data.Entity, ipfsRealm, components.t0, components.t1.Value);

            return null;
        }

        public bool KillAsync(string pid) =>
            throw new NotImplementedException();

        public bool ExitAsync(string predefinedEmote) =>
            throw new NotImplementedException();

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
