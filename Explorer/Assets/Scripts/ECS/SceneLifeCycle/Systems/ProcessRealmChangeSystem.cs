using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using Ipfs;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine.Networking;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ProcessRealmChangeSystem : BaseUnityLoopSystem
    {
        private readonly SceneLifeCycleState state;

        private string newRealm;

        private UnityWebRequestAsyncOperation pointerRequest;

        // cache
        private readonly IpfsTypes.ServerAbout serverAbout = new ();

        public ProcessRealmChangeSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
        }

        public void ChangeRealm(string realm)
        {
            newRealm = realm;
        }

        protected override void Update(float t)
        {
            if (state.NewRealm) // newRealm=true only for one frame
            {
                state.NewRealm = false;
            }

            if (pointerRequest is { isDone: true })
            {
                JsonConvert.PopulateObject(pointerRequest.webRequest.downloadHandler.text, serverAbout);

                state.NewRealm = true;
                state.IpfsRealm = new IpfsRealm(newRealm, serverAbout);
                state.ScenePointers.Clear();
                state.FixedScenes.Clear();

                if (state.IpfsRealm.SceneUrns is { Count: > 0 })
                {
                    foreach (string sceneUrn in state.IpfsRealm.SceneUrns) { state.ScenesMetadataToLoad.Add(IpfsHelper.ParseUrn(sceneUrn)); }
                }

                pointerRequest = null;
                newRealm = "";
            }

            if (pointerRequest == null && newRealm.Length > 0) { pointerRequest = UnityWebRequest.Get(newRealm + "/about").SendWebRequest(); }
        }
    }
}
