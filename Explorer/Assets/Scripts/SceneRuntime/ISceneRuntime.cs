﻿using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using PortableExperiences.Controller;
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus;
using SceneRuntime.Apis.Modules.EngineApi;
using SceneRuntime.Apis.Modules.EngineApi.SDKObservableEvents;
using SceneRuntime.Apis.Modules.Ethereums;
using SceneRuntime.Apis.Modules.FetchApi;
using SceneRuntime.Apis.Modules.Players;
using SceneRuntime.Apis.Modules.PortableExperiencesApi;
using SceneRuntime.Apis.Modules.RestrictedActionsApi;
using SceneRuntime.Apis.Modules.Runtime;
using SceneRuntime.Apis.Modules.SceneApi;
using SceneRuntime.Apis.Modules.SignedFetch;
using SceneRuntime.Apis.Modules.UserActions;
using SceneRuntime.Apis.Modules.UserIdentityApi;
using System;

namespace SceneRuntime
{
    public interface ISceneRuntime : IDisposable
    {
        void Register<T>(string itemName, T target) where T: IJsApiWrapper;

        UniTask StartScene();

        UniTask UpdateScene(float dt);

        void ApplyStaticMessages(ReadOnlyMemory<byte> data);

        void SetIsDisposing();

        void OnSceneIsCurrentChanged(bool isCurrent);

        void RegisterEngineAPIWrapper(EngineApiWrapper newWrapper);
    }

    public static class SceneRuntimeExtensions
    {
        public static void RegisterAll(this ISceneRuntime sceneRuntime,
            IEngineApi engineApi,
            ISceneExceptionsHandler exceptionsHandler,
            IRoomHub roomHub,
            RealmProfileRepository profileRepository,
            ISceneApi sceneApi,
            IWebRequestController webRequestController,
            IRestrictedActionsAPI restrictedActionsAPI,
            IDecentralandUrlsSource decentralandUrlsSource,
            IRuntime runtime,
            IEthereumApi ethereumApi,
            IWebSocketApi webSocketApi,
            IWeb3IdentityCache web3IdentityCache,
            ICommunicationsControllerAPI communicationsControllerAPI,
            IInstancePoolsProvider instancePoolsProvider,
            ISimpleFetchApi simpleFetchApi,
            ISceneData sceneData,
            IRealmData realmData,
            IPortableExperiencesController portableExperiencesController,
            IRemoteMetadata remoteMetadata
        )
        {
            sceneRuntime.RegisterEngineAPI(engineApi, instancePoolsProvider, exceptionsHandler);
            sceneRuntime.RegisterPlayers(roomHub, profileRepository, remoteMetadata);
            sceneRuntime.RegisterSceneApi(sceneApi);
            sceneRuntime.RegisterSignedFetch(webRequestController, decentralandUrlsSource, sceneData, realmData);
            sceneRuntime.RegisterRestrictedActionsApi(restrictedActionsAPI);
            sceneRuntime.RegisterUserActions(restrictedActionsAPI);
            sceneRuntime.RegisterRuntime(runtime, exceptionsHandler, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterEthereumApi(ethereumApi, web3IdentityCache, exceptionsHandler);
            sceneRuntime.RegisterUserIdentityApi(profileRepository, web3IdentityCache, exceptionsHandler);
            sceneRuntime.RegisterWebSocketApi(webSocketApi, exceptionsHandler, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterSimpleFetchApi(simpleFetchApi, webRequestController, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterCommunicationsControllerApi(communicationsControllerAPI, instancePoolsProvider, exceptionsHandler, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterPortableExperiencesApi(portableExperiencesController, exceptionsHandler);
        }

        public static void RegisterAll(this ISceneRuntime sceneRuntime,
            ISDKObservableEventsEngineApi engineApi,
            ISDKMessageBusCommsControllerAPI commsApiImplementation,
            ISceneExceptionsHandler exceptionsHandler,
            IRoomHub roomHub,
            RealmProfileRepository profileRepository,
            ISceneApi sceneApi,
            IWebRequestController webRequestController,
            IRestrictedActionsAPI restrictedActionsAPI,
            IRuntime runtime,
            IEthereumApi ethereumApi,
            IWebSocketApi webSocketApi,
            IWeb3IdentityCache web3IdentityCache,
            IDecentralandUrlsSource decentralandUrlsSource,
            ICommunicationsControllerAPI communicationsControllerAPI,
            IInstancePoolsProvider instancePoolsProvider,
            ISimpleFetchApi simpleFetchApi,
            ISceneData sceneData,
            IRealmData realmData,
            IPortableExperiencesController portableExperiencesController,
            IRemoteMetadata remoteMetadata
        )
        {
            sceneRuntime.RegisterEngineAPI(engineApi, commsApiImplementation, instancePoolsProvider, exceptionsHandler);
            sceneRuntime.RegisterPlayers(roomHub, profileRepository, remoteMetadata);
            sceneRuntime.RegisterSceneApi(sceneApi);
            sceneRuntime.RegisterSignedFetch(webRequestController, decentralandUrlsSource, sceneData, realmData);
            sceneRuntime.RegisterRestrictedActionsApi(restrictedActionsAPI);
            sceneRuntime.RegisterUserActions(restrictedActionsAPI);
            sceneRuntime.RegisterRuntime(runtime, exceptionsHandler, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterEthereumApi(ethereumApi, web3IdentityCache, exceptionsHandler);
            sceneRuntime.RegisterUserIdentityApi(profileRepository, web3IdentityCache, exceptionsHandler);
            sceneRuntime.RegisterWebSocketApi(webSocketApi, exceptionsHandler, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterSimpleFetchApi(simpleFetchApi, webRequestController, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterCommunicationsControllerApi(communicationsControllerAPI, instancePoolsProvider, exceptionsHandler, realmData.IsLocalSceneDevelopment);
            sceneRuntime.RegisterPortableExperiencesApi(portableExperiencesController, exceptionsHandler);
        }

        internal static void RegisterEngineAPI(this ISceneRuntime sceneRuntime, IEngineApi engineApi, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            var newWrapper = new EngineApiWrapper(engineApi, instancePoolsProvider, sceneExceptionsHandler);
            sceneRuntime.Register("UnityEngineApi", newWrapper);
            sceneRuntime.RegisterEngineAPIWrapper(newWrapper);
        }

        internal static void RegisterEngineAPI(this ISceneRuntime sceneRuntime, ISDKObservableEventsEngineApi engineApi, ISDKMessageBusCommsControllerAPI commsApiImplementation, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            var newWrapper = new SDKObservableEventsEngineApiWrapper(engineApi, commsApiImplementation, instancePoolsProvider, sceneExceptionsHandler);
            sceneRuntime.Register("UnityEngineApi", newWrapper);
            sceneRuntime.RegisterEngineAPIWrapper(newWrapper);
        }

        private static void RegisterPlayers(this ISceneRuntime sceneRuntime, IRoomHub roomHub, RealmProfileRepository profileRepository, IRemoteMetadata remoteMetadata)
        {
            sceneRuntime.Register("UnityPlayers", new PlayersWrap(roomHub, profileRepository, remoteMetadata));
        }

        private static void RegisterSceneApi(this ISceneRuntime sceneRuntime, ISceneApi api)
        {
            sceneRuntime.Register("UnitySceneApi", new SceneApiWrapper(api));
        }

        private static void RegisterSignedFetch(
            this ISceneRuntime sceneRuntime,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneData sceneData,
            IRealmData realmData
        )
        {
            sceneRuntime.Register("UnitySignedFetch", new SignedFetchWrap(webRequestController, decentralandUrlsSource, sceneData, realmData));
        }

        private static void RegisterRestrictedActionsApi(this ISceneRuntime sceneRuntime, IRestrictedActionsAPI api)
        {
            sceneRuntime.Register("UnityRestrictedActionsApi", new RestrictedActionsAPIWrapper(api));
        }

        private static void RegisterUserActions(this ISceneRuntime sceneRuntime, IRestrictedActionsAPI api)
        {
            sceneRuntime.Register("UnityUserActions", new UserActionsWrapper(api));
        }

        private static void RegisterRuntime(this ISceneRuntime sceneRuntime, IRuntime api, ISceneExceptionsHandler sceneExceptionsHandler, bool isLocalSceneDevelopment)
        {
            sceneRuntime.Register("UnityRuntime", new RuntimeWrapper(api, sceneExceptionsHandler, isLocalSceneDevelopment));
        }

        private static void RegisterEthereumApi(this ISceneRuntime sceneRuntime, IEthereumApi ethereumApi, IWeb3IdentityCache web3IdentityCache, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityEthereumApi", new EthereumApiWrapper(ethereumApi, sceneExceptionsHandler, web3IdentityCache));
        }

        private static void RegisterUserIdentityApi(this ISceneRuntime sceneRuntime, RealmProfileRepository profileRepository, IWeb3IdentityCache identityCache, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityUserIdentityApi", new UserIdentityApiWrapper(profileRepository, identityCache, sceneExceptionsHandler));
        }

        private static void RegisterWebSocketApi(this ISceneRuntime sceneRuntime, IWebSocketApi webSocketApi, ISceneExceptionsHandler sceneExceptionsHandler, bool isLocalSceneDevelopment)
        {
            sceneRuntime.Register("UnityWebSocketApi", new WebSocketApiWrapper(webSocketApi, sceneExceptionsHandler, isLocalSceneDevelopment));
        }

        private static void RegisterCommunicationsControllerApi(this ISceneRuntime sceneRuntime, ICommunicationsControllerAPI api, IInstancePoolsProvider instancePoolsProvider, ISceneExceptionsHandler sceneExceptionsHandler, bool isLocalSceneDevelopment)
        {
            sceneRuntime.Register("UnityCommunicationsControllerApi", new CommunicationsControllerAPIWrapper(api, instancePoolsProvider, sceneExceptionsHandler, isLocalSceneDevelopment));
        }

        private static void RegisterSimpleFetchApi(this ISceneRuntime sceneRuntime, ISimpleFetchApi simpleFetchApi, IWebRequestController webRequestController, bool isLocalSceneDevelopment)
        {
            sceneRuntime.Register("UnitySimpleFetchApi", new SimpleFetchApiWrapper(simpleFetchApi, webRequestController, isLocalSceneDevelopment));
        }

        public static void RegisterSDKMessageBusCommsApi(this ISceneRuntime sceneRuntime, ISDKMessageBusCommsControllerAPI api)
        {
            sceneRuntime.Register("UnitySDKMessageBusCommsControllerApi", new SDKMessageBusCommsControllerAPIWrapper(api));
        }

        private static void RegisterPortableExperiencesApi(this ISceneRuntime sceneRuntime, IPortableExperiencesController portableExperiencesController, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityPortableExperiencesApi", new PortableExperiencesApiWrapper(portableExperiencesController, sceneExceptionsHandler));
        }
    }
}
