using DCL.CommunicationData.URLHelpers;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using SceneRunner.Scene;
using System.Text.RegularExpressions;
using DCL.FeatureFlags;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Prefs;
using DCL.UserInAppInitializationFlow.StartupOperations;
using Global.Dynamic.LaunchModes;
using Unity.Mathematics;
using UnityEngine;

namespace Global.Dynamic
{
    [Serializable]
    public class RealmLaunchSettings : ILaunchMode
    {
        [Serializable]
        public struct PredefinedScenes
        {
            [SerializeField] public bool enabled;
            [SerializeField] public Vector2Int[] parcels;
        }

        [SerializeField] internal InitialRealm initialRealm;
        [SerializeField] internal Vector2Int targetScene;
        [SerializeField] internal bool EditorSceneStartPosition = true;
        [SerializeField] internal PredefinedScenes predefinedScenes;
        [SerializeField] private string targetWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] internal string customRealm = IRealmNavigator.GOERLI_URL;
        [SerializeField] internal string remoteHibridWorld = "MetadyneLabs.dcl.eth";
        [SerializeField] internal HybridSceneContentServer remoteHybridSceneContentServer = HybridSceneContentServer.Goerli;
        [SerializeField] internal bool useRemoteAssetsBundles;
        [SerializeField] [Tooltip("In Worlds there is one LiveKit room for all scenes so it's possible to communicate changes outside of the scene. "
                                  + "In Genesis City there are individual LiveKit rooms and only one connection at a time is maintained. "
                                  + "Toggle this flag to equalize this behavior")] internal bool isolateSceneCommunication;

        [SerializeField] private string[] portableExperiencesEnsToLoadAtGameStart;

        private bool isLocalSceneDevelopmentRealm;

        public LaunchMode CurrentMode => isLocalSceneDevelopmentRealm

                                         // This is for development purposes only,
                                         // so we can easily start local development from the editor without application args
                                         || initialRealm == InitialRealm.Localhost
            ? LaunchMode.LocalSceneDevelopment
            : LaunchMode.Play;

        public string TargetWorld => targetWorld;

        public IReadOnlyList<int2> GetPredefinedParcels()
        {
            if (predefinedScenes.enabled)
                return predefinedScenes.parcels.Select(p => new int2(p.x, p.y)).ToList();

            return CurrentMode switch
                   {
                       LaunchMode.Play => Array.Empty<int2>(),
                       LaunchMode.LocalSceneDevelopment => new List<int2>
                       {
                           new (targetScene.x, targetScene.y)
                       },
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        public HybridSceneParams CreateHybridSceneParams()
        {
            if (initialRealm == InitialRealm.Localhost)
            {
                return new HybridSceneParams
                {
                    EnableHybridScene = useRemoteAssetsBundles,
                    HybridSceneContentServer = remoteHybridSceneContentServer,
                    World = remoteHybridSceneContentServer.Equals(HybridSceneContentServer.World)
                        ? remoteHibridWorld
                        : "",
                };
            }

            return new HybridSceneParams();
        }

        public void ApplyConfig(IAppArgs applicationParameters)
        {
            if (applicationParameters.TryGetValue(AppArgsFlags.REALM, out string? realm))
                ParseRealmAppParameter(applicationParameters, realm);

            if (applicationParameters.TryGetValue(AppArgsFlags.POSITION, out var parcelToTeleportOverride))
                ParsePositionAppParameter(parcelToTeleportOverride);
        }

        private void ParseRealmAppParameter(IAppArgs appParameters, string realmParamValue)
        {
            if (string.IsNullOrEmpty(realmParamValue)) return;

            bool isLocalSceneDevelopment = appParameters.TryGetValue(AppArgsFlags.LOCAL_SCENE, out string localSceneParamValue)
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
            if (!RealmHelper.TryParseParcelFromString(targetPositionParam, out var targetPosition)) return;

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
        
        public void CheckStartParcelOverride(IAppArgs appArgs, FeatureFlagsConfiguration featureFlagsConfigurationCache)
        {
            // Priority 1: App argument position (highest - from command line/Creator Hub)
            if (HasAppArgPosition(appArgs))
                return;
    
            // Priority 2: Editor position override (for development convenience)
            if (HasEditorPositionOverride())
                return;

            string? parcelToTeleportOverride = "0,0";
            bool hasDefaultSpawnFlag = featureFlagsConfigurationCache.IsEnabled(FeatureFlagsStrings.GENESIS_STARTING_PARCEL)
                                       && featureFlagsConfigurationCache.TryGetTextPayload(FeatureFlagsStrings.GENESIS_STARTING_PARCEL,
                                           FeatureFlagsStrings.STRING_VARIANT, out parcelToTeleportOverride)
                                       && parcelToTeleportOverride != null;

            // Priority 3: Serialized home position (used when no feature flag exists, or feature flag is set to "0,0")
            if (HomeMarkerController.HasSerializedPosition() && (!hasDefaultSpawnFlag || parcelToTeleportOverride == "0,0"))
            {
                targetScene = HomeMarkerController.Deserialize()!.Value;
                return;
            }

            // Priority 4: Feature flag override (used as fallback if home position not available or feature flag has specific value)
            // Note: If you don't want the feature flag for localhost, remove it from the feature flag configuration
            // (https://features.decentraland.systems/#/features/strategies/explorer-alfa-genesis-spawn-parcel)
            if (hasDefaultSpawnFlag)
                ParsePositionAppParameter(parcelToTeleportOverride!);
        }

        /// <summary>
        /// Checks if the Editor position override is enabled.
        /// If this is true in the Editor, the position will be the one that has been serialized.
        /// This prevents the feature flag from overriding the developer's start position.
        /// </summary>
        /// <returns>True if running in Editor and EditorSceneStartPosition is enabled.</returns>
        internal bool HasEditorPositionOverride() => Application.isEditor && EditorSceneStartPosition;

        /// <summary>
        /// Checks if the user has passed a position as an argument.
        /// If a position is set through args, the feature flag should not be taken into consideration.
        /// This is the case used on local scene development from Creator Hub/scene args.
        /// See: https://github.com/decentraland/js-sdk-toolchain/blob/2c002ca9e6feb98a771337190db2945e013d7b93/packages/%40dcl/sdk-commands/src/commands/start/explorer-alpha.ts#L29
        /// </summary>
        /// <param name="appArgs">The application arguments to check.</param>
        /// <returns>True if the POSITION flag is present in the arguments.</returns>
        private static bool HasAppArgPosition(IAppArgs appArgs) => appArgs.HasFlag(AppArgsFlags.POSITION);
    }
}
