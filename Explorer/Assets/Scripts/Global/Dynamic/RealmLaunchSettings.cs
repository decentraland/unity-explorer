using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Linq;
using SceneRunner.Scene;
using Unity.Mathematics;
using UnityEngine;

namespace Global.Dynamic
{
    [Serializable]
    public class RealmLaunchSettings
    {
        [Serializable]
        public struct PredefinedScenes
        {
            [SerializeField] public bool enabled;
            [SerializeField] public Vector2Int[] parcels;
        }

        [SerializeField] internal InitialRealm initialRealm;
        [SerializeField] internal Vector2Int targetScene;
        [SerializeField] internal PredefinedScenes predefinedScenes;
        [SerializeField] internal string targetWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] internal string customRealm = IRealmNavigator.GOERLI_URL;
        [SerializeField] internal string remoteHibridWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] internal HybridSceneContentServer remoteHybridSceneContentServer = HybridSceneContentServer.Goerli;
        [SerializeField] internal bool useRemoteAssetsBundles = true;

        public Vector2Int TargetScene => targetScene;

        public IReadOnlyList<int2> GetPredefinedParcels() => predefinedScenes.enabled
            ? predefinedScenes.parcels.Select(p => new int2(p.x, p.y)).ToList()
            : Array.Empty<int2>();

        public HybridSceneParams CreateHybridSceneParams(Vector2Int startParcel)
        {
            if (initialRealm == InitialRealm.Localhost)
            {
                return new HybridSceneParams
                {
                    StartParcel = startParcel, EnableHybridScene = useRemoteAssetsBundles, HybridSceneContentServer = remoteHybridSceneContentServer, World = remoteHybridSceneContentServer.Equals(HybridSceneContentServer.World) ? remoteHibridWorld : "",
                };
            }

            return new HybridSceneParams();
        }

        public string GetStartingRealm()
        {
            return initialRealm switch
                   {
                       InitialRealm.GenesisCity => IRealmNavigator.GENESIS_URL,
                       InitialRealm.SDK => IRealmNavigator.SDK_TEST_SCENES_URL,
                       InitialRealm.Goerli => IRealmNavigator.GOERLI_URL,
                       InitialRealm.StreamingWorld => IRealmNavigator.STREAM_WORLD_URL,
                       InitialRealm.TestScenes => IRealmNavigator.TEST_SCENES_URL,
                       InitialRealm.World => IRealmNavigator.WORLDS_DOMAIN + "/" + targetWorld,
                       InitialRealm.Localhost => IRealmNavigator.LOCALHOST,
                       InitialRealm.Custom => customRealm,
                       _ => IRealmNavigator.GENESIS_URL,
                   };
        }

        public void SetTargetScene(Vector2Int newTargetScene) => targetScene = newTargetScene;

        public void SetWorldRealm(string targetWorld)
        {
            this.targetWorld = targetWorld;
            initialRealm = InitialRealm.World;
        }

        public void SetLocalSceneDevelopmentRealm(string targetRealm)
        {
            customRealm = targetRealm;
            initialRealm = InitialRealm.Custom;
            useRemoteAssetsBundles = false;
        }
    }
}
