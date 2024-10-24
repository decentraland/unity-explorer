using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.CommunicationData.URLHelpers;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using SceneRunner.Scene;
using System.Text.RegularExpressions;
using Unity.Mathematics;
using UnityEngine;

namespace Global.Dynamic
{
    [Serializable]
    public class RealmLaunchSettings
    {
        private const string APP_PARAMETER_REALM = "realm";
        private const string APP_PARAMETER_LOCAL_SCENE = "local-scene";
        private const string APP_PARAMETER_POSITION = "position";

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
        [SerializeField] [Tooltip("In Worlds there is one LiveKit room for all scenes so it's possible to communicate changes outside of the scene. "
                                  + "In Genesis City there are individual LiveKit rooms and only one connection at a time is maintained. "
                                  + "Toggle this flag to equalize this behavior")] internal bool isolateSceneCommunication;

        [SerializeField] private string[] portableExperiencesEnsToLoadAtGameStart;

        public Vector2Int TargetScene => targetScene;

        private bool isLocalSceneDevelopmentRealm;
        public bool IsLocalSceneDevelopmentRealm => isLocalSceneDevelopmentRealm
                                                    // This is for development purposes only,
                                                    // so we can easily start local development from the editor without application args
                                                    || initialRealm == InitialRealm.Localhost;

        public IReadOnlyList<int2> GetPredefinedParcels()
        {
            if (predefinedScenes.enabled)
                return predefinedScenes.parcels.Select(p => new int2(p.x, p.y)).ToList();

            return IsLocalSceneDevelopmentRealm ? new List<int2>(){new int2(TargetScene.x, TargetScene.y)}
                : Array.Empty<int2>();
        }

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

        public string? GetLocalSceneDevelopmentRealm(IDecentralandUrlsSource decentralandUrlsSource) =>
            IsLocalSceneDevelopmentRealm ? GetStartingRealm(decentralandUrlsSource) : null;

        public string GetStartingRealm(IDecentralandUrlsSource decentralandUrlsSource)
        {
            return initialRealm switch
                   {
                       InitialRealm.GenesisCity => decentralandUrlsSource.Url(DecentralandUrl.Genesis),
                       InitialRealm.SDK => IRealmNavigator.SDK_TEST_SCENES_URL,
                       InitialRealm.Goerli => IRealmNavigator.GOERLI_URL,
                       InitialRealm.StreamingWorld => IRealmNavigator.STREAM_WORLD_URL,
                       InitialRealm.TestScenes => IRealmNavigator.TEST_SCENES_URL,
                       InitialRealm.World => IRealmNavigator.WORLDS_DOMAIN + "/" + targetWorld,
                       InitialRealm.Localhost => IRealmNavigator.LOCALHOST,
                       InitialRealm.Custom => customRealm,
                       _ => decentralandUrlsSource.Url(DecentralandUrl.Genesis),
                   };
        }

        public void ApplyConfig(IAppArgs applicationParameters)
        {
            if (applicationParameters.TryGetValue(APP_PARAMETER_REALM, out string? realm))
                ParseRealmAppParameter(applicationParameters, realm);

            if (applicationParameters.TryGetValue(APP_PARAMETER_POSITION, out string? position))
                ParsePositionAppParameter(position);
        }

        private void ParseRealmAppParameter(IAppArgs appParameters, string realmParamValue)
        {
            if (string.IsNullOrEmpty(realmParamValue)) return;

            bool isLocalSceneDevelopment = appParameters.TryGetValue(APP_PARAMETER_LOCAL_SCENE, out string localSceneParamValue)
                                    && ParseLocalSceneParameter(localSceneParamValue)
                                    && IsRealmAValidUrl(realmParamValue);

            if (isLocalSceneDevelopment)
                SetLocalSceneDevelopmentRealm(realmParamValue);
            else if (IsRealmAWorld(realmParamValue))
                SetWorldRealm(realmParamValue);
            else
                SetCustomRealm(realmParamValue);
        }

        private void SetCustomRealm(string realm)
        {
            customRealm = realm;
            initialRealm = InitialRealm.Custom;
        }

        private void SetWorldRealm(string world)
        {
            targetWorld = world;
            initialRealm = InitialRealm.World;
        }

        private void SetLocalSceneDevelopmentRealm(string targetRealm)
        {
            customRealm = targetRealm;
            initialRealm = InitialRealm.Custom;
            useRemoteAssetsBundles = false;
            isLocalSceneDevelopmentRealm = true;
        }

        private void ParsePositionAppParameter(string targetPositionParam)
        {
            if (string.IsNullOrEmpty(targetPositionParam)) return;

            Vector2Int targetPosition = Vector2Int.zero;

            MatchCollection matches = new Regex(@"-*\d+").Matches(targetPositionParam);

            if (matches.Count > 1)
            {
                targetPosition.x = int.Parse(matches[0].Value);
                targetPosition.y = int.Parse(matches[1].Value);
            }

            targetScene = targetPosition;
        }

        private bool ParseLocalSceneParameter(string localSceneParameter)
        {
            if (string.IsNullOrEmpty(localSceneParameter)) return false;

            var isLocalScene = false;
            Match match = new Regex(@"true|false").Match(localSceneParameter);

            if (match.Success)
                bool.TryParse(match.Value, out isLocalScene);

            return isLocalScene;
        }

        private bool IsRealmAWorld(string realmParam) =>
            realmParam.IsEns();

        private bool IsRealmAValidUrl(string realmParam) =>
            Uri.TryCreate(realmParam, UriKind.Absolute, out Uri? uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
