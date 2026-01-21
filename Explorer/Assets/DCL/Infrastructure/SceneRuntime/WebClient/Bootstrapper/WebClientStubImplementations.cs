using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.JsModulesImplementation.Communications;
using Cysharp.Threading.Tasks;
using DCL.Clipboard;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.Web3;
using DCL.WebRequests.Analytics;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using Global.Dynamic;
using MVC;
using PortableExperiences.Controller;
using SceneRunner.Mapping;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SceneRuntime.WebClient.Bootstrapper
{
    /// <summary>
    /// Stub implementations for WebGL scene bootstrapper dependencies.
    /// These provide minimal no-op implementations of interfaces that aren't needed
    /// for basic scene rendering in WebGL.
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
            public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
                UniTask.FromResult(new EthApiResponse {});

            public void Dispose() { }
        }

        public class StubMVCManager : IMVCManager
        {
            public event Action<IController>? OnViewShowed;
            public event Action<IController>? OnViewClosed;

            public UniTask ShowAsync<TView, TInputData>(ShowCommand<TView, TInputData> command, CancellationToken ct = default) where TView : IView
            {
                return UniTask.CompletedTask;
            }

            public void RegisterController<TView, TInputData>(IController<TView, TInputData> controller) where TView : IView { }

            public void SetAllViewsCanvasActive(bool isActive) { }

            public void SetAllViewsCanvasActive(IController except, bool isActive) { }

            public void Dispose() { }
        }

        public class StubDecentralandUrlsSource : IDecentralandUrlsSource
        {
            public string Url(DecentralandUrl decentralandUrl) => string.Empty;
            public string GetHostnameForFeatureFlag() => string.Empty;
        }

        public class StubPortableExperiencesController : IPortableExperiencesController
        {
            public event Action<string>? PortableExperienceLoaded;
            public event Action<string>? PortableExperienceUnloaded;
            public Dictionary<string, Entity> PortableExperienceEntities { get; } = new();
            public GlobalWorld GlobalWorld { get; set; } = null!;

            public bool CanKillPortableExperience(string id) => false;
            public UniTask<IPortableExperiencesController.SpawnResponse> CreatePortableExperienceByEnsAsync(ENS ens, CancellationToken ct, bool isGlobalPortableExperience = false, bool force = false)
            {
                return UniTask.FromResult(new IPortableExperiencesController.SpawnResponse());
            }
            public IPortableExperiencesController.ExitResponse UnloadPortableExperienceById(string id) => new IPortableExperiencesController.ExitResponse { status = false };
            public List<IPortableExperiencesController.SpawnResponse> GetAllPortableExperiences() => new List<IPortableExperiencesController.SpawnResponse>();
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
            public string GetText() => string.Empty;

            public void Set(string text)
            {
            }

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
            public IDictionary<Type, Func<IRequestMetric>> GetTrackedMetrics() => new Dictionary<Type, Func<IRequestMetric>>();
            public IReadOnlyList<IRequestMetric>? GetMetric(Type requestType) => null;
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
            public bool IsEnabled(ReportHandler handler) => true;
            public bool CategoryIsEnabled(string category, LogType logType) => true;
            public ICategorySeverityMatrix GetMatrix(ReportHandler handler) => new StubCategorySeverityMatrix();
        }

        public class StubCategorySeverityMatrix : ICategorySeverityMatrix
        {
            public bool IsEnabled(string category, LogType severity) => true;
        }

        public class StubSceneMapping : ISceneMapping
        {
            public World? GetWorld(string sceneName) => null;
            public World? GetWorld(Vector2Int coordinates) => null;
            public void Register(string sceneName, IReadOnlyList<Vector2Int> coordinates, World world) { }
        }

        public class StubReleasablePerformanceBudget : IReleasablePerformanceBudget
        {
            public bool TrySpendBudget() => true; // Always allow
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
    }
}
