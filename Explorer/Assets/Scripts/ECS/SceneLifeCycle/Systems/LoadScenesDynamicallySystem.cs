using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using Ipfs;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LoadScenesDynamicallySystem : BaseUnityLoopSystem
    {
        private readonly IIpfsRealm ipfsRealm;
        private readonly List<Vector2Int> parcelsToLoad = new ();

        // cache
        private readonly List<IpfsTypes.SceneEntityDefinition> retrievedScenes = new ();

        internal readonly SceneLifeCycleState state;

        [CanBeNull] private readonly List<Vector2Int> staticParcelsToLoad;

        internal (UnityWebRequestAsyncOperation, List<Vector2Int>)? pointerRequest;

        public LoadScenesDynamicallySystem(World world, IIpfsRealm ipfsRealm, SceneLifeCycleState state, [CanBeNull] List<Vector2Int> staticParcelsToLoad = null) : base(world)
        {
            this.state = state;
            this.ipfsRealm = ipfsRealm;
            this.staticParcelsToLoad = staticParcelsToLoad;
        }

        protected override void Update(float dt)
        {
            // TODO: Drop web request on realm change
            if (!pointerRequest.HasValue)
            {
                // If we don't have a pointer request, we check the parcels in range, filter the parcels that are not loaded, and we create the request
                Vector3 position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

                parcelsToLoad.Clear();
                List<Vector2Int> parcelsInRange = staticParcelsToLoad ?? ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

                foreach (Vector2Int parcel in parcelsInRange)
                {
                    if (!state.ScenePointers.ContainsKey(parcel)) { parcelsToLoad.Add(parcel); }
                }

                if (parcelsToLoad.Count > 0)
                    pointerRequest = (ipfsRealm.RequestActiveEntitiesByPointers(parcelsToLoad), parcelsToLoad);
            }
            else if (pointerRequest.Value.Item1.isDone)
            {
                (UnityWebRequestAsyncOperation request, List<Vector2Int> requestedParcels) = pointerRequest.Value;
                JsonConvert.PopulateObject(request.webRequest.downloadHandler.text, retrievedScenes);

                Debug.Log($"loading {retrievedScenes.Count} scenes from {requestedParcels.Count}");

                foreach (IpfsTypes.SceneEntityDefinition scene in retrievedScenes)
                {
                    foreach (string encodedPointer in scene.pointers)
                    {
                        Vector2Int pointer = IpfsHelper.DecodePointer(encodedPointer);
                        requestedParcels.Remove(pointer);
                        state.ScenePointers.TryAdd(pointer, scene);
                    }
                }

                // load empty parcels!
                foreach (Vector2Int emptyParcel in requestedParcels)
                {
                    state.ScenePointers.Add(emptyParcel, new IpfsTypes.SceneEntityDefinition
                    {
                        id = $"empty-parcel-{emptyParcel.x}-{emptyParcel.y}",
                    });
                }

                pointerRequest = null;
            }
        }
    }
}
