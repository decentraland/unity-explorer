using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.DebugUtilities;
using DCL.DebugUtilities.Views;
using DCL.Friends.UserBlocking;
using DCL.Input;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Emotes;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Optimization.Multithreading;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.Quality;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Utility;
using DCL.Utility.Types;
using DCL.Web3;
using DCL.WebRequests.Analytics;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using ECS.SceneLifeCycle.Reporting;
using Global.Dynamic;
using MVC;
using PortableExperiences.Controller;
using SceneRunner.Mapping;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

namespace SceneRuntime.WebClient.Bootstrapper
{
    /// <summary>
    ///     Stub implementations for WebGL scene bootstrapper dependencies.
    ///     These provide minimal no-op implementations of interfaces that aren't needed
    ///     for basic scene rendering in WebGL.
    /// </summary>
    public static class WebClientStubImplementations
    {
        public class StubSceneCommunicationPipe : ISceneCommunicationPipe
        {
            public void AddSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage) { }

            public void RemoveSceneMessageHandler(string sceneId, ISceneCommunicationPipe.MsgType msgType, ISceneCommunicationPipe.SceneMessageHandler onSceneMessage) { }

            public void SendMessage(ReadOnlySpan<byte> message, string sceneId, ISceneCommunicationPipe.ConnectivityAssertiveness assertiveness, CancellationToken ct, string? specialRecipient = null) { }
        }

        public class StubEthereumApi : IEthereumApi
        {
            public void Dispose() { }

            public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
                UniTask.FromResult(new EthApiResponse());
        }

        /// <summary>Stub IEventSystem for WebGL when no Unity EventSystem is in scene.</summary>
        public class StubEventSystem : IEventSystem
        {
            private static readonly List<RaycastResult> EmptyList = new ();

            public IReadOnlyList<RaycastResult> RaycastAll(Vector2 position) =>
                EmptyList;

            public bool IsPointerOverGameObject() => false;
        }

        /// <summary>Stub ICursor for WebGL when cursor assets are not loaded.</summary>
        public class StubCursor : ICursor
        {
            public bool IsStyleForced { get; private set; }

            public bool IsLocked() => Cursor.lockState != CursorLockMode.None;

            public void Lock()
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            public void Unlock()
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            public void SetVisibility(bool visible) => Cursor.visible = visible;

            public void SetStyle(CursorStyle style, bool force = false)
            {
                IsStyleForced = force;
            }
        }

        /// <summary>Stub IEmotesMessageBus for WebGL (no multiplayer emotes).</summary>
        public class StubEmotesMessageBus : IEmotesMessageBus
        {
            private readonly MutexSync mutexSync = new ();
            private readonly HashSet<RemoteEmoteIntention> emptySet = new ();

            public OwnedBunch<RemoteEmoteIntention> EmoteIntentions() =>
                new (mutexSync, emptySet);

            public void Send(URN urn, bool loopCyclePassed) { }

            public void OnPlayerRemoved(string walletId) { }

            public void SaveForRetry(RemoteEmoteIntention intention) { }
        }

        public class StubMVCManager : IMVCManager
        {
            public event Action<IController>? OnViewShowed;
            public event Action<IController>? OnViewClosed;

            public void Dispose() { }

            public UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView: IView =>
                UniTask.CompletedTask;

            public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView: IView { }

            public void SetAllViewsCanvasActive(bool isActive) { }

            public void SetAllViewsCanvasActive(IController except, bool isActive) { }
        }

        public class StubDecentralandUrlsSource : IDecentralandUrlsSource
        {
            public string Url(DecentralandUrl decentralandUrl) =>
                string.Empty;

            public string GetHostnameForFeatureFlag() =>
                string.Empty;
        }

        public class StubPortableExperiencesController : IPortableExperiencesController
        {
            public Dictionary<string, Entity> PortableExperienceEntities { get; } = new ();
            public GlobalWorld GlobalWorld { get; set; } = null!;
            public event Action<string>? PortableExperienceLoaded;
            public event Action<string>? PortableExperienceUnloaded;

            public bool CanKillPortableExperience(string id) =>
                false;

            public UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false) =>
                UniTask.FromResult(new IPortableExperiencesController.SpawnResponse());

            public IPortableExperiencesController.ExitResponse UnloadPortableExperienceById(string id) =>
                new();

            public List<IPortableExperiencesController.SpawnResponse> GetAllPortableExperiences() =>
                new ();

            public void UnloadAllPortableExperiences() { }

            public void AddPortableExperience(string id, Entity portableExperience) { }
        }

        public class StubRemoteMetadata : IRemoteMetadata
        {
            public IReadOnlyDictionary<string, IRemoteMetadata.ParticipantMetadata> Metadata { get; } = new Dictionary<string, IRemoteMetadata.ParticipantMetadata>();

            public void BroadcastSelfParcel(Vector2Int pose) { }

            public void BroadcastSelfMetadata() { }
        }

        public class StubSystemClipboard : ISystemClipboard
        {
            public void SetText(string text) { }

            public string GetText() =>
                string.Empty;

            public void Set(string text) { }

            public string Get() =>
                string.Empty;

            public bool HasValue() =>
                false;
        }

        public class StubPartitionComponent : IPartitionComponent
        {
            public byte Bucket => 0;
            public bool IsBehind => false;
            public bool IsDirty => false;
            public float RawSqrDistance => 0f;
        }

        public class StubWebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
        {
            public IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics() =>
                new Dictionary<Type, Func<IRequestMetric>>();

            public IReadOnlyList<IRequestMetric>? GetMetric(Type requestType) =>
                null;

            void IWebRequestsAnalyticsContainer.OnRequestStarted<T>(T request) { }

            void IWebRequestsAnalyticsContainer.OnRequestFinished<T>(T request) { }

            void IWebRequestsAnalyticsContainer.OnProcessDataStarted<T>(T request) { }

            void IWebRequestsAnalyticsContainer.OnProcessDataFinished<T>(T request) { }
        }

        public class StubBudgetProfiler : IBudgetProfiler
        {
            public long TotalUsedMemoryInBytes => 0;
            public long SystemUsedMemoryInBytes => 0;
            public ulong CurrentFrameTimeValueNs => 0;
            public ulong LastFrameTimeValueNs => 0;
            public ulong LastGpuFrameTimeValueNs => 0;

            public void Dispose() { }
        }

        public class StubSystemMemoryCap : ISystemMemoryCap
        {
            public long MemoryCapInMB => 4 * 1024L;

            public int MemoryCap
            {
                get => 4;
                set => throw new NotImplementedException();
            }
        }

        public class StubReportsHandlingSettings : IReportsHandlingSettings
        {
            public bool DebounceEnabled => false;

            public bool IsEnabled(ReportHandler handler) =>
                true;

            public bool CategoryIsEnabled(string category, LogType logType) =>
                true;

            public ICategorySeverityMatrix GetMatrix(ReportHandler handler) =>
                new StubCategorySeverityMatrix();
        }

        public class StubCategorySeverityMatrix : ICategorySeverityMatrix
        {
            public bool IsEnabled(string category, LogType severity) =>
                true;
        }

        public class StubSceneMapping : ISceneMapping
        {
            public World? GetWorld(string sceneName) =>
                null;

            public World? GetWorld(Vector2Int coordinates) =>
                null;

            public void Register(string sceneName, IReadOnlyList<Vector2Int> coordinates, World world) { }
        }

        public class StubReleasablePerformanceBudget : IReleasablePerformanceBudget
        {
            public bool TrySpendBudget() =>
                true; // Always allow

            public void ReleaseBudget() { }
        }

        public class StubPartitionSettings : IPartitionSettings
        {
            public float AngleTolerance => 1f;
            public float PositionSqrTolerance => 0.01f;
            public IReadOnlyList<int> SqrDistanceBuckets { get; } = new List<int> { 128, 512, 2048 };
            public int FastPathSqrDistance => int.MaxValue;
            public int BehindCameraBaseBucket => 2;
        }

        public class StubCameraSamplingData : IReadOnlyCameraSamplingData
        {
            public Vector3 Position => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public Vector2Int Parcel => Vector2Int.zero;
            public bool IsDirty => false;
        }

        /// <summary>
        ///     Stub implementation of ISceneReadinessReportQueue for WebGL.
        /// </summary>
        public class StubSceneReadinessReportQueue : ISceneReadinessReportQueue
        {
            public void Enqueue(Vector2Int parcel, AsyncLoadProcessReport report) { }

            public bool TryDequeue(IReadOnlyList<Vector2Int> parcels, out PooledLoadReportList? report)
            {
                report = null;
                return false;
            }

            public bool TryDequeue(Vector2Int parcel, out PooledLoadReportList? report)
            {
                report = null;
                return false;
            }
        }

        /// <summary>
        ///     Stub implementation of ILoadingStatus for WebGL.
        /// </summary>
        public class StubLoadingStatus : ILoadingStatus
        {
            public ReactiveProperty<LoadingStatus.LoadingStage> CurrentStage { get; } = new (LoadingStatus.LoadingStage.Init);

            public ReactiveProperty<string> AssetState { get; } = new (string.Empty);

            public void UpdateAssetsLoaded(int assetsLoaded, int assetsToLoad) { }

            public float SetCurrentStage(LoadingStatus.LoadingStage stage)
            {
                CurrentStage.Value = stage;
                return 0f;
            }

            public bool IsLoadingScreenOn() =>
                false;
        }

        /// <summary>
        ///     Stub implementation of IDebugContainerBuilder for WebGL minimal global world.
        /// </summary>
        public class StubDebugContainerBuilder : IDebugContainerBuilder
        {
            public bool IsVisible { get; set; }

            public DebugContainer Container => throw new NotSupportedException("WebGL stub debug builder has no container.");

            public Result<DebugWidgetBuilder> AddWidget(WidgetName name) =>
                Result<DebugWidgetBuilder>.ErrorResult("WebGL stub");

            public IReadOnlyDictionary<string, DebugWidget> Widgets { get; } = new Dictionary<string, DebugWidget>();

            public void BuildWithFlex(UnityEngine.UIElements.UIDocument debugRootCanvas) { }
        }

        /// <summary>
        ///     Stub implementation of ILandscape for WebGL minimal global world.
        /// </summary>
        public class StubLandscape : ILandscape
        {
            public UniTask<EnumResult<LandscapeError>> LoadTerrainAsync(AsyncLoadProcessReport loadReport, CancellationToken ct) =>
                UniTask.FromResult(EnumResult<LandscapeError>.SuccessResult());

            public float GetHeight(float x, float z) => 0f;

            public Result IsParcelInsideTerrain(Vector2Int parcel, bool isLocal) => Result.SuccessResult();
        }

        /// <summary>
        ///     Stub implementation of IScenesCache for WebGL minimal global world.
        /// </summary>
        public class StubScenesCache : IScenesCache
        {
            private readonly ReactiveProperty<Vector2Int> currentParcel = new (Vector2Int.zero);
            private readonly ReactiveProperty<ISceneFacade?> currentScene = new (null);

            public IReadonlyReactiveProperty<Vector2Int> CurrentParcel => currentParcel;
            public IReadonlyReactiveProperty<ISceneFacade?> CurrentScene => currentScene;
            public IReadOnlyCollection<ISceneFacade> Scenes { get; } = Array.Empty<ISceneFacade>();
            public IReadOnlyCollection<ISceneFacade> PortableExperiencesScenes { get; } = Array.Empty<ISceneFacade>();

            public void Add(ISceneFacade sceneFacade, IReadOnlyList<Vector2Int> parcels) { }

            public void AddNonRealScene(IReadOnlyList<Vector2Int> parcels) { }

            public void AddNonRealScene(Vector2Int parcel) { }

            public void AddPortableExperienceScene(ISceneFacade sceneFacade, string sceneUrn) { }

            public void RemoveNonRealScene(IReadOnlyList<Vector2Int> parcels) { }

            public void RemoveSceneFacade(IReadOnlyList<Vector2Int> parcels) { }

            public bool Contains(Vector2Int parcel) => false;

            public bool TryGetByParcel(Vector2Int parcel, out ISceneFacade sceneFacade)
            {
                sceneFacade = null!;
                return false;
            }

            public bool TryGetBySceneId(string sceneId, out ISceneFacade? sceneFacade)
            {
                sceneFacade = null;
                return false;
            }

            public bool TryGetPortableExperienceBySceneUrn(string sceneUrn, out ISceneFacade sceneFacade)
            {
                sceneFacade = null!;
                return false;
            }

            public void RemovePortableExperienceFacade(string sceneUrn) { }

            public void ClearScenes(bool clearPortableExperiences = false) { }

            public void SetCurrentScene(ISceneFacade? sceneFacade) { }

            public void UpdateCurrentParcel(Vector2Int newParcel) { }
        }

        /// <summary>Stub IRendererFeaturesCache for avatar-only bootstrapper (no outline/quality features).</summary>
        public class StubRendererFeaturesCache : IRendererFeaturesCache
        {
            public void Dispose() { }

            public T? GetRendererFeature<T>() where T : ScriptableRendererFeature => null;
        }

        /// <summary>Stub IUserBlockingCache for avatar-only bootstrapper (no blocking).</summary>
        public class StubUserBlockingCache : IUserBlockingCache
        {
            private static readonly ReadOnlyHashSet<string> EmptySet = new (new HashSet<string>());

            public ReadOnlyHashSet<string> BlockedUsers => EmptySet;
            public ReadOnlyHashSet<string> BlockedByUsers => EmptySet;
            public bool HideChatMessages { get; set; }
            public bool UserIsBlocked(string userId) => false;
        }

        /// <summary>IIpfsRealm that uses production DCL URLs so wearable/avatar loading can resolve and fetch from decentraland.org.</summary>
        public class ProductionDclIpfsRealm : IIpfsRealm
        {
            private static readonly URLDomain Lambdas = URLDomain.FromString("https://peer.decentraland.org/lambdas/");
            private static readonly URLDomain AbCdn = URLDomain.FromString("https://ab-cdn.decentraland.org/");
            private static readonly URLDomain Content = URLDomain.FromString("https://content.decentraland.org/");

            public URLDomain CatalystBaseUrl => Lambdas;
            public URLDomain ContentBaseUrl => Content;
            public URLDomain LambdasBaseUrl => Lambdas;
            public IReadOnlyList<string> SceneUrns => Array.Empty<string>();
            public URLDomain EntitiesActiveEndpoint => URLDomain.EMPTY;
            public URLDomain AssetBundleRegistry => AbCdn;

            public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null) =>
                throw new NotSupportedException();

            public string GetFileHash(byte[] file) => file.IpfsHashV1();
        }
    }
}
