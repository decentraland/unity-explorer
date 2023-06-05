using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using Ipfs;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LoadSceneDynamicallySystem : BaseUnityLoopSystem
    {
        private readonly IIpfsRealm ipfsRealm;

        internal readonly SceneLifeCycleState state;

        internal (UnityWebRequestAsyncOperation, List<Vector2Int>)? pointerRequest;

        // cache
        private readonly List<IpfsTypes.SceneEntityDefinition> retrievedScenes = new();

        public LoadSceneDynamicallySystem(World world, IIpfsRealm ipfsRealm, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
            this.ipfsRealm = ipfsRealm;
        }

        protected override void Update(float dt)
        {
            // TODO: Drop web request on realm change
            if (!pointerRequest.HasValue)
            {
                // If we don't have a pointer request, we check the parcels in range, filter the parcels that are not loaded, and we create the request
                var position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

                List<Vector2Int> parcelsToLoad = new List<Vector2Int>();
                var parcelsInRange = ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

                foreach (var parcel in parcelsInRange)
                {
                    if (!state.ScenePointers.ContainsKey(parcel)) { parcelsToLoad.Add(parcel); }
                }

                if (parcelsToLoad.Count > 0)
                    pointerRequest = (ipfsRealm.RequestActiveEntitiesByPointers(parcelsToLoad), parcelsToLoad);
            }
            else if (pointerRequest.Value.Item1.isDone)
            {
                var (request, requestedParcels) = pointerRequest.Value;
                JsonConvert.PopulateObject(request.webRequest.downloadHandler.text, retrievedScenes);

                Debug.Log($"loading {retrievedScenes.Count} scenes from {requestedParcels.Count} parcels ({JsonConvert.SerializeObject(requestedParcels)})");

                foreach (var scene in retrievedScenes)
                {
                    foreach (var encodedPointer in scene.pointers)
                    {
                        Vector2Int pointer = IpfsHelper.DecodePointer(encodedPointer);
                        requestedParcels.Remove(pointer);
                        state.ScenePointers.TryAdd(pointer, scene);
                    }
                }

                // load empty parcels!
                foreach (var emptyParcel in requestedParcels) { state.ScenePointers.Add(emptyParcel, new IpfsTypes.SceneEntityDefinition()
                {
                    id = $"empty-parcel-{emptyParcel.x}-{emptyParcel.y}"
                }); }

                pointerRequest = null;
            }
        }
    }
}
