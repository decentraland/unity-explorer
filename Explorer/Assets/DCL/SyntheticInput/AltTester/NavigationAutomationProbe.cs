#if ALTTESTER
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.RealmNavigation;
using DCL.Utility.Types;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.SyntheticInput.AltTester
{
    /// <summary>
    ///     <para>
    ///         AltTester front-end for moving the session between realms and parcels: tests call these via
    ///         <c>AltDriver.CallStaticMethod</c> (assembly <c>DCL.SyntheticInput</c> — this assembly name is a wire
    ///         contract) so a fixture can put the client in the world and parcel it needs instead of depending on
    ///         the launch arguments alone. The environment (org/zone) is launch-only and is reported, not changed.
    ///     </para>
    ///     <para>
    ///         Realm changes and teleports go through <see cref="IRealmNavigator" /> — the same path the
    ///         <c>/goto</c> chat command takes, loading screen included. They are multi-frame, so the API is
    ///         start/poll like <see cref="WorldAutomationProbe" />: a Start* method returns an operation id and
    ///         <see cref="PollJson" /> reports <c>{"done":false}</c> until the payload is ready. Nothing here throws
    ///         towards the test.
    ///     </para>
    /// </summary>
    public static class NavigationAutomationProbe
    {
        private const string WORLD_SUFFIX = ".dcl.eth";
        private const int ARRIVAL_POLL_INTERVAL_MS = 500;
        private const float MIN_TIMEOUT_SEC = 5f;
        private const float MAX_TIMEOUT_SEC = 300f;
        private const float DEFAULT_TIMEOUT_SEC = 120f;

        private static Session? session;

        /// <summary>Written once by DynamicWorldContainer when the automation session starts (the static-latch probe pattern).</summary>
        public static void Install(IRealmNavigator realmNavigator, IRealmData realmData, IDecentralandUrlsSource urlsSource,
            IScenesCache scenesCache, IReadOnlyLoadingStatus loadingStatus, DecentralandEnvironment environment) =>
            session = new Session(realmNavigator, realmData, urlsSource, scenesCache, loadingStatus, environment);

        public static bool IsReady() =>
            session != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

        /// <summary>
        ///     Where the client is right now:
        ///     <c>{"ok":true,"environment":"zone","realmName":"...","hostname":"...","realmKind":"World","currentParcel":{"x":0,"y":0},"loadingScreenOn":false,"scene":{...}|null}</c>.
        ///     environment is the lower-cased <see cref="DecentralandEnvironment" /> the client was launched with.
        /// </summary>
        public static string GetStatusJson()
        {
            if (!TryGetSession(out Session ready, out string failedPayload))
                return failedPayload;

            IRealmData realmData = ready.RealmData;

            var payload = new JObject
            {
                ["ok"] = true,
                ["environment"] = ready.Environment.ToString().ToLowerInvariant(),
                ["realmConfigured"] = realmData.Configured,
                ["realmName"] = realmData.Configured ? realmData.RealmName : string.Empty,
                ["hostname"] = realmData.Configured ? realmData.Hostname : string.Empty,
                ["realmKind"] = realmData.RealmType.Value.ToString(),
                ["currentParcel"] = ParcelJson(ready.ScenesCache.CurrentParcel.Value),
                ["loadingScreenOn"] = ready.LoadingStatus.IsLoadingScreenOn(),
                ["scene"] = SceneJson(ready.ScenesCache.CurrentScene.Value),
            };

            return payload.ToString();
        }

        /// <summary>
        ///     Changes realm to a world and lands on a parcel of it, then waits until the scene there is ready.
        ///     <paramref name="world" /> is a world name (<c>sdk7testscenes</c>, <c>sdk7testscenes.dcl.eth</c>, any
        ///     ENS name) resolved against this environment's world server — the same resolution the <c>/goto</c>
        ///     chat command applies. Already being on that world turns this into a parcel teleport.
        /// </summary>
        public static int StartGoToWorld(string world, int parcelX, int parcelY, float timeoutSec)
        {
            if (!TryGetSession(out Session ready, out string failedPayload))
                return AltOperationRegistry.Start(UniTask.FromResult(failedPayload));

            if (string.IsNullOrWhiteSpace(world))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload("world must not be empty")));

            return AltOperationRegistry.Start(GoToWorldAsync(ready, world.Trim(), new Vector2Int(parcelX, parcelY), ClampTimeout(timeoutSec)));
        }

        /// <summary>Teleports to a parcel of the current realm (world or Genesis City alike) and waits until the scene there is ready.</summary>
        public static int StartTeleport(int parcelX, int parcelY, float timeoutSec)
        {
            if (!TryGetSession(out Session ready, out string failedPayload))
                return AltOperationRegistry.Start(UniTask.FromResult(failedPayload));

            return AltOperationRegistry.Start(TeleportAsync(ready, new Vector2Int(parcelX, parcelY), ClampTimeout(timeoutSec)));
        }

        private static async UniTask<string> GoToWorldAsync(Session ready, string world, Vector2Int parcel, float timeoutSec)
        {
            URLDomain realmUrl = ResolveWorldUrl(ready.UrlsSource, world);

            if (ready.RealmNavigator.IsAlreadyOnRealm(realmUrl))
                return await TeleportAsync(ready, parcel, timeoutSec);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSec;

            EnumResult<ChangeRealmError> result;

            try { result = await ready.RealmNavigator.TryChangeRealmAsync(realmUrl, cts.Token, parcel, isWorld: true); }
            catch (OperationCanceledException) { return AltOperationRegistry.ErrorPayload($"changing realm to '{realmUrl}' did not complete within {timeoutSec}s"); }

            if (result.Error is { } error)
                return AltOperationRegistry.ErrorPayload(FailureText("realm change failed", error.State.ToString(), error.Message));

            return await WaitForArrivalAsync(ready, parcel, deadline);
        }

        private static async UniTask<string> TeleportAsync(Session ready, Vector2Int parcel, float timeoutSec)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSec;

            EnumResult<TaskError> result;

            // isLocal keeps the teleport inside the current realm; false would route it to Genesis City.
            try { result = await ready.RealmNavigator.TeleportToParcelAsync(parcel, cts.Token, isLocal: true); }
            catch (OperationCanceledException) { return AltOperationRegistry.ErrorPayload($"teleport to ({parcel.x},{parcel.y}) did not complete within {timeoutSec}s"); }

            if (result.Error is { } error)
                return AltOperationRegistry.ErrorPayload(FailureText("teleport failed", error.State.ToString(), error.Message));

            return await WaitForArrivalAsync(ready, parcel, deadline);
        }

        /// <summary>
        ///     Polls until the player stands on the target parcel with the loading screen down and the scene there
        ///     ready (or absent), mirroring the MCP teleport tool's arrival check.
        /// </summary>
        private static async UniTask<string> WaitForArrivalAsync(Session ready, Vector2Int parcel, float deadline)
        {
            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Delay(ARRIVAL_POLL_INTERVAL_MS);

                ISceneFacade? scene = ready.ScenesCache.CurrentScene.Value;
                bool arrived = ready.ScenesCache.CurrentParcel.Value == parcel || (scene?.Contains(parcel) ?? false);

                if (!arrived || ready.LoadingStatus.IsLoadingScreenOn())
                    continue;

                if (scene == null)
                    return ArrivalPayload(ready, parcel, ok: true, "no scene is deployed at this parcel");

                if (scene.SceneStateProvider.IsNotRunningState())
                    return ArrivalPayload(ready, parcel, ok: false, $"scene '{scene.Info.Name}' is not running: {scene.SceneStateProvider.State.Value()}");

                if (scene.IsSceneReady())
                    return ArrivalPayload(ready, parcel, ok: true, null);
            }

            return ArrivalPayload(ready, parcel, ok: false, $"did not reach a ready scene at ({parcel.x},{parcel.y}) before the timeout");
        }

        private static string ArrivalPayload(Session ready, Vector2Int parcel, bool ok, string? note)
        {
            var payload = new JObject
            {
                ["ok"] = ok,
                ["targetParcel"] = ParcelJson(parcel),
                ["realmName"] = ready.RealmData.Configured ? ready.RealmData.RealmName : string.Empty,
                ["currentParcel"] = ParcelJson(ready.ScenesCache.CurrentParcel.Value),
                ["scene"] = SceneJson(ready.ScenesCache.CurrentScene.Value),
            };

            if (note != null)
                payload[ok ? "info" : "error"] = note;

            return payload.ToString();
        }

        /// <summary>Short names get the .dcl.eth suffix, full ENS names are kept, then both resolve under this environment's world server.</summary>
        private static URLDomain ResolveWorldUrl(IDecentralandUrlsSource urlsSource, string world)
        {
            string worldName = world;

            if (!worldName.IsEns() && !worldName.EndsWith(WORLD_SUFFIX, StringComparison.OrdinalIgnoreCase))
                worldName += WORLD_SUFFIX;

            URLAddress worldAddress = URLDomain.FromString(urlsSource.Url(DecentralandUrl.WorldServer)).Append(URLPath.FromString(worldName));
            return URLDomain.FromString(worldAddress.Value);
        }

        private static bool TryGetSession(out Session ready, out string failedPayload)
        {
            if (session != null)
            {
                ready = session;
                failedPayload = string.Empty;
                return true;
            }

            ready = null!;
            failedPayload = AltOperationRegistry.ErrorPayload("the navigation probe is not installed (launch with --alttester or --mcp)");
            return false;
        }

        private static string FailureText(string what, string state, string message) =>
            string.IsNullOrEmpty(message) ? $"{what}: {state}" : $"{what}: {state} ({message})";

        private static float ClampTimeout(float timeoutSec) =>
            timeoutSec <= 0f ? DEFAULT_TIMEOUT_SEC : Mathf.Clamp(timeoutSec, MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

        private static JObject ParcelJson(Vector2Int parcel) =>
            new () { ["x"] = parcel.x, ["y"] = parcel.y };

        private static JToken SceneJson(ISceneFacade? scene) =>
            scene == null
                ? JValue.CreateNull()
                : new JObject
                {
                    ["name"] = scene.Info.Name,
                    ["baseParcel"] = ParcelJson(scene.Info.BaseParcel),
                    ["state"] = scene.SceneStateProvider.State.Value().ToString(),
                    ["ready"] = scene.IsSceneReady(),
                };

        private sealed class Session
        {
            public readonly IRealmNavigator RealmNavigator;
            public readonly IRealmData RealmData;
            public readonly IDecentralandUrlsSource UrlsSource;
            public readonly IScenesCache ScenesCache;
            public readonly IReadOnlyLoadingStatus LoadingStatus;
            public readonly DecentralandEnvironment Environment;

            public Session(IRealmNavigator realmNavigator, IRealmData realmData, IDecentralandUrlsSource urlsSource,
                IScenesCache scenesCache, IReadOnlyLoadingStatus loadingStatus, DecentralandEnvironment environment)
            {
                RealmNavigator = realmNavigator;
                RealmData = realmData;
                UrlsSource = urlsSource;
                ScenesCache = scenesCache;
                LoadingStatus = loadingStatus;
                Environment = environment;
            }
        }
    }
}
#endif
