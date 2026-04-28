using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterCamera;
using DCL.Character.Components;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.MutePersistence;
using DCL.VoiceChat.Nearby.Systems;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NSubstitute;
using RichTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby
{
    /// <summary>
    ///     Profiler-attachable PlayMode testbed for the full Nearby audio ECS chain.
    ///     Spawns an isolated <see cref="World"/> with N fake avatars and drives
    ///     <see cref="NearbyAudioBindingSystem"/> → <see cref="NearbyAudioPositionSystem"/> →
    ///     <see cref="NearbyAudioCleanupSystem"/> every <see cref="MonoBehaviour.Update"/>,
    ///     plus an inline pass that destroys entities marked with <see cref="DeleteEntityIntention"/>
    ///     (the production <c>DestroyEntitiesSystem</c> is intentionally not in this rig).
    ///     <para>
    ///         Inspector-tweakable at runtime: target avatar count, listening-gate state,
    ///         per-system Update toggles. Context-menu actions drive the cleanup triggers
    ///         (clear all sids → mass cleanup; suppress/resume → bulk archetype-move teardown).
    ///     </para>
    ///     <para>
    ///         Sources are constructed through the real <see cref="NearbyAudioSourceFactory"/>
    ///         so configuration (mixer group, rolloff curve, spatial settings) matches runtime.
    ///         For audio-thread panning profiling, use <see cref="NearbyAudioPerformanceManualTest"/>
    ///         — that stand injects a fake <c>AudioStream</c> so <c>ApplySpatialPanning</c> runs
    ///         on the Audio Mixer Thread; this stand focuses on main-thread ECS-chain cost.
    ///     </para>
    /// </summary>
    public class NearbyAudioFullCycleManualTest : MonoBehaviour
    {
        private static readonly QueryDescription DELETE_INTENTION_QUERY =
            new QueryDescription().WithAll<DeleteEntityIntention>();

        private static readonly QueryDescription AUDIO_SOURCE_QUERY =
            new QueryDescription().WithAll<NearbyAudioSourceComponent>();

        private static readonly FieldInfo HEAD_ANCHOR_FIELD =
            typeof(AvatarBase).GetField("<HeadAnchorPoint>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;

        [Header("Population")]
        [Range(0, 200)]
        [SerializeField] private int targetAvatarCount = 25;
        [SerializeField] private float spreadRadius = 15f;

        [Header("Listening Gate")]
        [Tooltip("Inspector-driven target state. Translated to the matching state-model transition every Update.")]
        [SerializeField] private NearbyVoiceChatState forceState = NearbyVoiceChatState.IDLE;

        [Header("System Toggles (off = skip that stage in Update)")]
        [SerializeField] private bool runBinding = true;
        [SerializeField] private bool runPosition = true;
        [SerializeField] private bool runCleanup = true;

        [Header("HUD")]
        [SerializeField] private bool showOverlay = true;

        // ── World/system state ──────────────────────────────────────
        private World world;
        private FakeStreamRegistry registry;
        private Dictionary<StreamKey, Entity> bindings;
        private NearbyVoiceChatStateModel stateModel;
        private VoiceChatConfiguration configuration;
        private NearbyAudioSourceFactory sourceFactory;

        private NearbyAudioBindingSystem bindingSystem;
        private NearbyAudioPositionSystem positionSystem;
        private NearbyAudioCleanupSystem cleanupSystem;

        // ── Avatar bookkeeping ──────────────────────────────────────
        private readonly List<AvatarHandle> avatars = new (256);
        private readonly List<Entity> deleteScratch = new (32);
        private NearbyVoiceChatState lastSyncedState = NearbyVoiceChatState.IDLE;
        private int nextAvatarOrdinal;

        private struct AvatarHandle
        {
            public string Wallet;
            public Entity Entity;
            public GameObject AvatarGo;
        }

        private void Awake()
        {
            Camera camera = EnsureCameraAndListener();

            world = World.Create();

            // Camera entity defaults Mode = FirstPerson, so the position system skips PlayerComponent.CameraFocus.
            world.Create(new CameraComponent(camera));

            // Local listener anchor — parented under the testbed so it's auto-cleaned on destroy.
            var playerGo = new GameObject("Listener_LocalPlayer");
            playerGo.transform.SetParent(transform);
            playerGo.transform.localPosition = Vector3.zero;
            world.Create(new PlayerComponent(playerGo.transform));

            registry = new FakeStreamRegistry();
            bindings = new Dictionary<StreamKey, Entity>();
            stateModel = new NearbyVoiceChatStateModel(NearbyVoiceChatState.IDLE);
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            sourceFactory = new NearbyAudioSourceFactory(configuration);

            IUserBlockingCache userBlockingCache = Substitute.For<IUserBlockingCache>();
            var muteService = new NearbyMuteService(Substitute.For<INearbyMuteCache>(), Substitute.For<INearbyMuteRepository>());

            bindingSystem = new NearbyAudioBindingSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
            positionSystem = new NearbyAudioPositionSystem(world, muteService);
            positionSystem.Initialize();
            cleanupSystem = new NearbyAudioCleanupSystem(world, registry, bindings, userBlockingCache, stateModel, sourceFactory);
        }

        private void Update()
        {
            SyncListeningGate();
            SyncAvatarPopulation();

            if (runBinding) bindingSystem.Update(0);
            if (runPosition) positionSystem.Update(0);
            if (runCleanup)
            {
                cleanupSystem.Update(0);
                DestroyEntitiesMarkedForDeletion();
            }
        }

        private void OnDestroy()
        {
            // Same audio-thread reaping as the editor perf tests — Unity keeps invoking
            // OnAudioFilterRead on a foreign thread until LivekitAudioSource is fully destroyed.
            cleanupSystem?.Dispose();
            positionSystem?.Dispose();
            bindingSystem?.Dispose();

            foreach (LivekitAudioSource src in Object.FindObjectsByType<LivekitAudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (src == null) continue;
                src.Stop();
                src.Free();
                Object.DestroyImmediate(src.gameObject);
            }

            foreach (AvatarHandle h in avatars)
                if (h.AvatarGo != null) Object.DestroyImmediate(h.AvatarGo);
            avatars.Clear();

            stateModel?.Dispose();
            world?.Dispose();
            if (configuration != null) Object.DestroyImmediate(configuration);
        }

        // ── Listening-gate sync ─────────────────────────────────────

        private void SyncListeningGate()
        {
            if (forceState == lastSyncedState) return;

            switch (forceState)
            {
                case NearbyVoiceChatState.IDLE:
                    if (lastSyncedState == NearbyVoiceChatState.SUPPRESSED)
                        stateModel.Resume(SuppressionReason.CALL);
                    else if (lastSyncedState == NearbyVoiceChatState.DISABLED)
                        stateModel.Enable();
                    else if (lastSyncedState == NearbyVoiceChatState.OPEN_MIC)
                        stateModel.StopSpeaking();
                    break;
                case NearbyVoiceChatState.OPEN_MIC:
                    stateModel.StartSpeaking();
                    break;
                case NearbyVoiceChatState.SUPPRESSED:
                    stateModel.Suppress(SuppressionReason.CALL);
                    break;
                case NearbyVoiceChatState.DISABLED:
                    stateModel.Disable();
                    break;
            }

            lastSyncedState = forceState;
        }

        // ── Avatar population ───────────────────────────────────────

        private void SyncAvatarPopulation()
        {
            while (avatars.Count > targetAvatarCount)
                RemoveLastAvatar();

            while (avatars.Count < targetAvatarCount)
                AddAvatar();
        }

        private void AddAvatar()
        {
            string wallet = $"perf-wallet-{nextAvatarOrdinal++:D4}";

            var avatarGo = new GameObject($"Avatar_{wallet}");
            avatarGo.transform.SetParent(transform);
            avatarGo.transform.localPosition = Random.insideUnitSphere * spreadRadius;

            AvatarBase avatarBase = avatarGo.AddComponent<AvatarBase>();
            var headAnchorGo = new GameObject("HeadAnchor");
            headAnchorGo.transform.SetParent(avatarGo.transform, worldPositionStays: false);
            headAnchorGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            HEAD_ANCHOR_FIELD.SetValue(avatarBase, headAnchorGo.transform);

            Entity entity = world.Create(new Profile(wallet, wallet, new Avatar()), avatarBase);
            registry.Add(wallet, "sid");

            avatars.Add(new AvatarHandle { Wallet = wallet, Entity = entity, AvatarGo = avatarGo });
        }

        private void RemoveLastAvatar()
        {
            int idx = avatars.Count - 1;
            AvatarHandle h = avatars[idx];
            avatars.RemoveAt(idx);

            // Drive both Trigger #1 (avatar gone) and Trigger #2 (stream gone) so cleanup fires either way.
            registry.RemoveAll(h.Wallet);
            if (world.IsAlive(h.Entity)) world.Destroy(h.Entity);

            if (h.AvatarGo != null) Object.DestroyImmediate(h.AvatarGo);
        }

        private void DestroyEntitiesMarkedForDeletion()
        {
            deleteScratch.Clear();
            world.Query(in DELETE_INTENTION_QUERY, (Entity e) => deleteScratch.Add(e));

            foreach (Entity e in deleteScratch)
                if (world.IsAlive(e)) world.Destroy(e);
        }

        // ── Camera & listener ───────────────────────────────────────

        private Camera EnsureCameraAndListener()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = camGo.AddComponent<Camera>();
            }

            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();

            return camera;
        }

        // ── HUD overlay ─────────────────────────────────────────────

        private void OnGUI()
        {
            if (!showOverlay || world == null) return;

            int audioCount = world.CountEntities(in AUDIO_SOURCE_QUERY);
            int markedCount = world.CountEntities(in DELETE_INTENTION_QUERY);

            GUI.Label(new Rect(10, 10, 480, 20), $"Avatars: {avatars.Count}/{targetAvatarCount}");
            GUI.Label(new Rect(10, 30, 480, 20), $"Bindings: {bindings.Count}");
            GUI.Label(new Rect(10, 50, 480, 20), $"Audio entities: {audioCount} (marked for delete: {markedCount})");
            GUI.Label(new Rect(10, 70, 480, 20), $"State: {stateModel.State.Value} (Inspector target: {forceState})");
        }

        // ── Inspector context-menu actions ──────────────────────────

        [ContextMenu("Add 10 avatars")]
        private void Add10Avatars() => targetAvatarCount = Mathf.Min(200, targetAvatarCount + 10);

        [ContextMenu("Remove 10 avatars")]
        private void Remove10Avatars() => targetAvatarCount = Mathf.Max(0, targetAvatarCount - 10);

        [ContextMenu("Suppress (CALL)")]
        private void SuppressContext() => forceState = NearbyVoiceChatState.SUPPRESSED;

        [ContextMenu("Resume to IDLE")]
        private void ResumeContext() => forceState = NearbyVoiceChatState.IDLE;

        [ContextMenu("Force mass cleanup (clear all sids)")]
        private void ForceMassCleanup() => registry?.ClearAll();

        // ── Fake stream registry ────────────────────────────────────
        // Mirrors the binding test fake: Owned<AudioStream>(null) yields a Weak whose Resource.Has
        // is true, so the binding system actually instantiates LivekitAudioSource through the real
        // factory — we want the integration cost, not a stubbed-out short-circuit.
        private sealed class FakeStreamRegistry : INearbyAudioStreamRegistry
        {
            private readonly Dictionary<string, ConcurrentDictionary<string, byte>> sidsByIdentity = new ();
            private readonly Dictionary<StreamKey, Owned<AudioStream>> streamsByKey = new ();

            public void Add(string walletId, string sid)
            {
                if (!sidsByIdentity.TryGetValue(walletId, out var sids))
                {
                    sids = new ConcurrentDictionary<string, byte>();
                    sidsByIdentity[walletId] = sids;
                }

                sids.TryAdd(sid, 0);

                var key = new StreamKey(walletId, sid);
                if (!streamsByKey.ContainsKey(key))
                    streamsByKey[key] = new Owned<AudioStream>(null!);
            }

            public void RemoveAll(string walletId) =>
                sidsByIdentity.Remove(walletId);

            public void ClearAll() =>
                sidsByIdentity.Clear();

            public ConcurrentDictionary<string, byte>? GetAudioSids(string walletId) =>
                sidsByIdentity.TryGetValue(walletId, out var sids) ? sids : null;

            public Weak<AudioStream> GetActiveStream(StreamKey key) =>
                streamsByKey.TryGetValue(key, out Owned<AudioStream>? owned)
                    ? owned.Downgrade()
                    : Weak<AudioStream>.Null;

            public bool IsStreamGone(StreamKey key)
            {
                ConcurrentDictionary<string, byte>? sids = GetAudioSids(key.identity);
                return sids == null || !sids.ContainsKey(key.sid);
            }

            public void Dispose() { }
        }
    }

#if UNITY_EDITOR
    public static class NearbyAudioFullCycleManualTestMenu
    {
        [UnityEditor.MenuItem("Decentraland/Manual Tests/ Nearby Audio Full Cycle [Perf]")]
        private static void OpenTestbed()
        {
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play mode first.");
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var go = new GameObject("NearbyAudioFullCycleTestbed");
            go.AddComponent<NearbyAudioFullCycleManualTest>();

            UnityEditor.Selection.activeGameObject = go;
            Debug.Log("Nearby Audio Full Cycle Testbed ready — press Play.");
        }
    }
#endif
}
