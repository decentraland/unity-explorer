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

        [CanBeNull] private readonly List<Vector2Int> staticParcelsToLoad;

        internal readonly SceneLifeCycleState state;

        private readonly List<Vector2Int> parcelsToLoad = new ();

        // cache
        private readonly List<IpfsTypes.SceneEntityDefinition> retrievedScenes = new ();

        public LoadScenesDynamicallySystem(World world, IIpfsRealm ipfsRealm, SceneLifeCycleState state, [CanBeNull] List<Vector2Int> staticParcelsToLoad = null) : base(world)
        {
            this.state = state;
            this.ipfsRealm = ipfsRealm;
            this.staticParcelsToLoad = staticParcelsToLoad;
        }

        internal UnityWebRequestAsyncOperation pointerRequest { get; private set; }

        protected override void Update(float dt)
        {
            // TODO: Drop web request on realm change
            if (pointerRequest == null)
            {
                // If we don't have a pointer request, we check the parcels in range, filter the parcels that are not loaded, and we create the request
                Vector3 position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

                parcelsToLoad.Clear();
                var parcelsInRange = staticParcelsToLoad ?? ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

                for (var i = 0; i < parcelsInRange.Count; i++)
                {
                    Vector2Int parcel = parcelsInRange[i];
                    if (!state.ScenePointers.ContainsKey(parcel)) parcelsToLoad.Add(parcel);
                }

                if (parcelsToLoad.Count > 0)
                    pointerRequest = ipfsRealm.RequestActiveEntitiesByPointers(parcelsToLoad);
            }
            else if (pointerRequest.isDone)
            {
                JsonConvert.PopulateObject(pointerRequest.webRequest.downloadHandler.text, retrievedScenes);

                Debug.Log($"loading {retrievedScenes.Count} scenes from {parcelsToLoad.Count} parcels");

                for (var i = 0; i < retrievedScenes.Count; i++)
                {
                    IpfsTypes.SceneEntityDefinition scene = retrievedScenes[i];

                    foreach (string encodedPointer in scene.pointers)
                    {
                        Vector2Int pointer = IpfsHelper.DecodePointer(encodedPointer);
                        parcelsToLoad.Remove(pointer);
                        state.ScenePointers.TryAdd(pointer, scene);
                    }
                }

                // load empty parcels!
                for (var i = 0; i < parcelsToLoad.Count; i++)
                {
                    Vector2Int emptyParcel = parcelsToLoad[i];

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
