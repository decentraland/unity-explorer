using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Linq;
using SceneRunner.Scene;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField] private string remoteHibridWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] private HibridSceneContentServer remoteHibridSceneContentServer = HibridSceneContentServer.Goerli;
        [SerializeField] private bool useRemoteAssetsBundles = true;

        [SerializeField] private string[] portableExperiencesEnsToLoadAtGameStart;

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
                    StartParcel = startParcel, EnableHybridScene = useRemoteAssetsBundles, HybridSceneContentServer = remoteHibridSceneContentServer, World = remoteHibridSceneContentServer.Equals(HibridSceneContentServer.World) ? remoteHibridWorld : ""
                };
            }

            return new HybridSceneParams();
        }

        public string GetStartingRealm()
        {
            // when started in preview mode (local scene development) a command line argument is used
            string[] cmdArgs = Environment.GetCommandLineArgs();
            for (var i = 0; i < cmdArgs.Length; i++)
            {
                if (cmdArgs[i].StartsWith("-realm"))
                {
                    return cmdArgs[i+1];
                }
            }

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
