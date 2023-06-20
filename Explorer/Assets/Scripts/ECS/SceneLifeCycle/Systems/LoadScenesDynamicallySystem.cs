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
    [UpdateAfter(typeof(LoadSceneMetadataSystem))]
    public partial class LoadScenesDynamicallySystem : BaseUnityLoopSystem
    {
        private readonly List<Vector2Int> parcelsToLoad = new ();

        // cache
        private readonly List<IpfsTypes.SceneEntityDefinition> retrievedScenes = new ();

        internal readonly SceneLifeCycleState state;

        [CanBeNull] private readonly List<Vector2Int> staticParcelsToLoad;

        public LoadScenesDynamicallySystem(World world, SceneLifeCycleState state, [CanBeNull] List<Vector2Int> staticParcelsToLoad = null) : base(world)
        {
            this.state = state;
            this.staticParcelsToLoad = staticParcelsToLoad;
        }

        internal UnityWebRequestAsyncOperation pointerRequest { get; private set; }

        protected override void Update(float dt)
        {
            // we don't try to find parcels, if we don't have a realm set
            if (state.IpfsRealm == null) return;

            // we just process scenes dynamically when we don't have fixed scenes
            if (state.IpfsRealm.SceneUrns is { Count: > 0 }) { return; }

            // If we're changing realm on this frame, we drop the request if we have one...
            if (state.NewRealm) { pointerRequest = null; }

            if (pointerRequest == null)
            {
                // If we don't have a pointer request, we check the parcels in range, filter the parcels that are not loaded, and we create the request
                Vector3 position = World.Get<TransformComponent>(state.PlayerEntity).Transform.position;

                parcelsToLoad.Clear();
                IReadOnlyList<Vector2Int> parcelsInRange = staticParcelsToLoad ?? ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

                for (var i = 0; i < parcelsInRange.Count; i++)
                {
                    Vector2Int parcel = parcelsInRange[i];
                    if (!state.ScenePointers.ContainsKey(parcel)) parcelsToLoad.Add(parcel);
                }

                if (parcelsToLoad.Count > 0)
                    pointerRequest = state.IpfsRealm.RequestActiveEntitiesByPointers(parcelsToLoad);
            }
            else if (pointerRequest.isDone)
            {
                JsonConvert.PopulateObject(pointerRequest.webRequest.downloadHandler.text, retrievedScenes);

                Debug.Log($"loading {retrievedScenes.Count} scenes from {parcelsToLoad.Count} parcels");

                for (var i = 0; i < retrievedScenes.Count; i++)
                {
                    IpfsTypes.SceneEntityDefinition scene = retrievedScenes[i];

                    scene.urn = new IpfsTypes.IpfsPath()
                    {
                        Urn = "",
                        BaseUrl = "",
                        EntityId = scene.id,
                    };

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
