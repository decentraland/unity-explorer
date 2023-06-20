using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using Ipfs;
using System.Collections.Generic;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProcessRealmChangeSystem))]
    public partial class LoadSceneMetadataSystem : BaseUnityLoopSystem
    {
        private readonly SceneLifeCycleState state;

        private readonly List<(IpfsTypes.IpfsPath, UnityWebRequestAsyncOperation)> scenesToLoad = new ();

        // cache
        private IpfsTypes.SceneEntityDefinition sceneEntityDefinition = new ();

        public LoadSceneMetadataSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
        }

        protected override void Update(float t)
        {
            if (state.ScenesMetadataToLoad.Count > 0)
            {
                foreach (var sceneMetadataToLoad in state.ScenesMetadataToLoad)
                {
                    var url = sceneMetadataToLoad.GetUrl(state.IpfsRealm.ContentBaseUrl);
                    scenesToLoad.Add((sceneMetadataToLoad, UnityWebRequest.Get(url).SendWebRequest()));
                }

                state.ScenesMetadataToLoad.Clear();
            }

            for (int i = scenesToLoad.Count - 1; i >= 0; i--)
            {
                var (entityUrn, pointerRequest) = scenesToLoad[i];

                if (pointerRequest is { isDone: true })
                {
                    // TODO: Using DeserializeObject because PopulateObject doesn't clear the previous data...
                    sceneEntityDefinition = JsonConvert.DeserializeObject<IpfsTypes.SceneEntityDefinition>(pointerRequest.webRequest.downloadHandler.text);

                    if (sceneEntityDefinition != null)
                    {
                        sceneEntityDefinition.id ??= entityUrn.EntityId;
                        sceneEntityDefinition.urn = entityUrn;

                        foreach (string encodedPointer in sceneEntityDefinition.metadata.scene.parcels)
                        {
                            Vector2Int pointer = IpfsHelper.DecodePointer(encodedPointer);
                            state.ScenePointers.TryAdd(pointer, sceneEntityDefinition);
                        }

                        state.FixedScenes.Add(sceneEntityDefinition);
                    }

                    scenesToLoad.RemoveAt(i);
                }
            }
        }
    }
}
