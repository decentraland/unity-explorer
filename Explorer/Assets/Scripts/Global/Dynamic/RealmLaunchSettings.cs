using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [SerializeField] private InitialRealm initialRealm;
        [SerializeField] private Vector2Int targetScene;
        [SerializeField] private PredefinedScenes predefinedScenes;
        [SerializeField] private string targetWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] private string customRealm = IRealmNavigator.GOERLI_URL;
        [SerializeField] private string remoteSceneID = "bafkreihpuayzjkiiluobvq5lxnvhrjnsl24n4xtrtauhu5cf2bk6sthv5q";
        [SerializeField] private ContentServer remoteSceneContentServer = ContentServer.World;
        public Vector2Int TargetScene => targetScene;

        public IReadOnlyList<int2> GetPredefinedParcels() => predefinedScenes.enabled
            ? predefinedScenes.parcels.Select(p => new int2(p.x, p.y)).ToList()
            : Array.Empty<int2>();

        public HybridSceneParams CreateHybridSceneParams()
        {
            if (initialRealm == InitialRealm.Localhost)
            {
                return new HybridSceneParams
                {
                    EnableHybridScene = true,
                    HybridSceneID = remoteSceneID,
                    HybridSceneContent = remoteSceneContentServer switch
                                         {
                                             ContentServer.Genesis => IRealmNavigator.GENESIS_CONTENT_URL,
                                             ContentServer.Goerli => IRealmNavigator.GOERLI_CONTENT_URL,
                                             ContentServer.World => IRealmNavigator.WORLDS_CONTENT_URL,
                                         }
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
    }
}
