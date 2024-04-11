using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using SceneRunner.Scene.ExceptionsHandling;
using SceneRuntime.Apis.Modules;
using SceneRuntime.Apis.Modules.CommunicationsControllerApi;
using SceneRuntime.Apis.Modules.Ethereums;
using SceneRuntime.Apis.Modules.Players;
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
    }

    public static class SceneRuntimeExtensions
    {
        public static void RegisterAll(this ISceneRuntime sceneRuntime,
            ISceneExceptionsHandler exceptionsHandler,
            IRoomHub roomHub,
            IProfileRepository profileRepository,
            ISceneApi sceneApi,
            IWebRequestController webRequestController,
            IRestrictedActionsAPI restrictedActionsAPI,
            IRuntime runtime,
            IEthereumApi ethereumApi,
            IWebSocketApi webSocketApi,
            IWeb3IdentityCache web3IdentityCache,
            ICommunicationsControllerAPI communicationsControllerAPI,
            IInstancePoolsProvider instancePoolsProvider)
        {
            sceneRuntime.RegisterPlayers(roomHub, profileRepository);
            sceneRuntime.RegisterSceneApi(sceneApi);
            sceneRuntime.RegisterSignedFetch(webRequestController);
            sceneRuntime.RegisterRestrictedActionsApi(restrictedActionsAPI);
            sceneRuntime.RegisterUserActions(restrictedActionsAPI);
            sceneRuntime.RegisterRuntime(runtime, exceptionsHandler);
            sceneRuntime.RegisterEthereumApi(ethereumApi, web3IdentityCache, exceptionsHandler);
            sceneRuntime.RegisterUserIdentityApi(profileRepository, web3IdentityCache, exceptionsHandler);
            sceneRuntime.RegisterWebSocketApi(webSocketApi, exceptionsHandler);
            sceneRuntime.RegisterCommunicationsControllerApi(communicationsControllerAPI, instancePoolsProvider);
        }

        private static void RegisterPlayers(this ISceneRuntime sceneRuntime, IRoomHub roomHub, IProfileRepository profileRepository)
        {
            sceneRuntime.Register("UnityPlayers", new PlayersWrap(roomHub, profileRepository));
        }

        private static void RegisterSceneApi(this ISceneRuntime sceneRuntime, ISceneApi api)
        {
            sceneRuntime.Register("UnitySceneApi", new SceneApiWrapper(api));
        }

        private static void RegisterSignedFetch(this ISceneRuntime sceneRuntime, IWebRequestController webRequestController)
        {
            sceneRuntime.Register("UnitySignedFetch", new SignedFetchWrap(webRequestController));
        }

        private static void RegisterRestrictedActionsApi(this ISceneRuntime sceneRuntime, IRestrictedActionsAPI api)
        {
            sceneRuntime.Register("UnityRestrictedActionsApi", new RestrictedActionsAPIWrapper(api));
        }

        private static void RegisterUserActions(this ISceneRuntime sceneRuntime, IRestrictedActionsAPI api)
        {
            sceneRuntime.Register("UnityUserActions", new UserActionsWrap(api));
        }

        private static void RegisterRuntime(this ISceneRuntime sceneRuntime, IRuntime api, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityRuntime", new RuntimeWrapper(api, sceneExceptionsHandler));
        }

        private static void RegisterEthereumApi(this ISceneRuntime sceneRuntime, IEthereumApi ethereumApi, IWeb3IdentityCache web3IdentityCache, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityEthereumApi", new EthereumApiWrapper(ethereumApi, sceneExceptionsHandler, web3IdentityCache));
        }

        private static void RegisterUserIdentityApi(this ISceneRuntime sceneRuntime, IProfileRepository profileRepository, IWeb3IdentityCache identityCache, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityUserIdentityApi", new UserIdentityApiWrapper(profileRepository, identityCache, sceneExceptionsHandler));
        }

        private static void RegisterWebSocketApi(this ISceneRuntime sceneRuntime, IWebSocketApi webSocketApi, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            sceneRuntime.Register("UnityWebSocketApi", new WebSocketApiWrapper(webSocketApi));
        }

        private static void RegisterCommunicationsControllerApi(this ISceneRuntime sceneRuntime, ICommunicationsControllerAPI api, IInstancePoolsProvider instancePoolsProvider)
        {
            sceneRuntime.Register("UnityCommunicationsControllerApi", new CommunicationsControllerAPIWrapper(api, instancePoolsProvider));
        }
    }
}
