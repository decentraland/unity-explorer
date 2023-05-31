using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Castle.Components.DictionaryAdapter.Xml;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SceneDynamicLoaderSystem : BaseUnityLoopSystem
    {
        internal readonly SceneLifeCycleState state;

        internal (UnityWebRequestAsyncOperation, List<Vector2Int>)? pointerRequest;

        public SceneDynamicLoaderSystem(World world, SceneLifeCycleState state) : base(world)
        {
            this.state = state;
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
                    pointerRequest = (Ipfs.RequestActiveEntities("https://sdk-test-scenes.decentraland.zone/content", parcelsToLoad), parcelsToLoad);
            }
            else if (pointerRequest.Value.Item1.isDone)
            {
                var (request, requestedParcels) = pointerRequest.Value;
                var retrievedScenes = JsonConvert.DeserializeObject<Ipfs.EntityDefinition[]>(request.webRequest.downloadHandler.text);

                if (retrievedScenes == null)
                {
                    Debug.LogWarning("failed to retrieve active scenes, will retry");
                    return;
                }

                Debug.Log($"loading {retrievedScenes.Length} scenes from {requestedParcels.Count} parcels");

                foreach (var scene in retrievedScenes)
                {
                    foreach (var encodedPointer in scene.pointers)
                    {
                        Vector2Int pointer = DecodePointer(encodedPointer);
                        requestedParcels.Remove(pointer);
                        state.ScenePointers.TryAdd(pointer, scene);
                    }
                }

                // load empty parcels!
                foreach (var emptyParcel in requestedParcels) { state.ScenePointers.Add(emptyParcel, new Ipfs.EntityDefinition()
                {
                    id = $"empty-parcel-{emptyParcel.x}-{emptyParcel.y}"
                }); }

                pointerRequest = null;
            }
        }

        internal static Vector2Int DecodePointer(string pointer)
        {
            string[] decoded = pointer.Split(",");
            return new Vector2Int(int.Parse(decoded[0]), int.Parse(decoded[1]));
        }
    }
}
