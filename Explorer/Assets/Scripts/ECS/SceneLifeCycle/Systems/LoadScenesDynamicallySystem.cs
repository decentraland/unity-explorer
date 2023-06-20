using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.StreamableLoading.AssetBundles.Manifest;
using ECS.Unity.Transforms.Components;
using Ipfs;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using ManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.Manifest.GetAssetBundleManifestIntention>;

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
                IReadOnlyList<Vector2Int> parcelsInRange = staticParcelsToLoad ?? ParcelMathHelper.ParcelsInRange(position, state.SceneLoadRadius);

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

                    if (scene.pointers.Count == 0) continue;

                    var scenePointer = new ScenePointer(scene, ManifestPromise.Create(World, new GetAssetBundleManifestIntention(scene.id)));

                    foreach (string encodedPointer in scene.pointers)
                    {
                        Vector2Int pointer = IpfsHelper.DecodePointer(encodedPointer);
                        parcelsToLoad.Remove(pointer);
                        state.ScenePointers.TryAdd(pointer, scenePointer);
                    }
                }

                // load empty parcels!
                for (var i = 0; i < parcelsToLoad.Count; i++)
                {
                    Vector2Int emptyParcel = parcelsToLoad[i];
                    state.ScenePointers.Add(emptyParcel, new ScenePointer(emptyParcel));
                }

                pointerRequest = null;
            }
        }
    }
}
