using DCL.VoiceChat.Nearby.Audio;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using NUnit.Framework;
using RichTypes;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.VoiceChat.Nearby.Tests
{
    /// <summary>
    /// Documents <see cref="NearbyAudioSourceFactory"/> contract under the pool-backed implementation (A2):
    ///
    /// - <c>Create</c> returns a fully enabled, playing <see cref="LivekitAudioSource"/> parented under <c>sourcesRoot</c>.
    /// - <c>Dispose</c> drives the source into the inert POOLED state (GameObject inactive, both components disabled,
    ///   stream cleared, mute=true / volume=0, parent under POOL_CONTAINER) and recycles it for the next <c>Create</c>.
    /// - <c>DisposeRoot</c> destroys all pooled instances + the sources-root subtree.
    /// </summary>
    public class NearbyAudioSourceFactoryShould
    {
        private const string WALLET_A = "wallet-alice";
        private const string WALLET_B = "wallet-bob";
        private const string SID_1 = "sid-1";
        private const string SID_2 = "sid-2";

        private VoiceChatConfiguration configuration = null!;
        private NearbyAudioSourceFactory factory = null!;
        private readonly List<LivekitAudioSource> seenSources = new (8);

        [SetUp]
        public void SetUp()
        {
            configuration = ScriptableObject.CreateInstance<VoiceChatConfiguration>();
            factory = new NearbyAudioSourceFactory(configuration);
            seenSources.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy any straggler that escaped the factory's lifecycle — pooled sources keep their
            // GameObjects alive after Dispose and would carry over into the next test, where Unity
            // could still invoke OnAudioFilterRead on the audio thread.
            foreach (LivekitAudioSource src in Object.FindObjectsByType<LivekitAudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (src == null) continue;
                src.Stop();
                src.Free();
                Object.DestroyImmediate(src.gameObject);
            }

            if (configuration != null) Object.DestroyImmediate(configuration);
        }

        // ── §12.1 — pool-backed factory contract ────────────────────

        [Test]
        public void CreatesFirstSourceWhenPoolEmpty()
        {
            // First Create on an empty pool triggers the creation handler; the resulting source must be
            // alive, active, and parented under sourcesRoot — A1's spawn-disabled hand-off relies on the
            // factory returning an enabled instance that BindingSystem then disables.
            LivekitAudioSource source = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            seenSources.Add(source);

            Assert.That(source, Is.Not.Null);
            Assert.That(source.gameObject.activeSelf, Is.True, "freshly acquired source must be active");
            Assert.That(source.transform.IsChildOf(factory.sourcesRoot), Is.True,
                "live source belongs under sourcesRoot subtree (pool path leaves it under POOL_CONTAINER, legacy path under sourcesRoot direct)");
        }

        [Test]
        public void ReusesPooledSourceAfterDispose()
        {
            // Tracer bullet for the pool: Create → Dispose → Create returns the same reference.
            // Demonstrates the factory recycles the GO+AudioSource+LivekitAudioSource triple instead of
            // round-tripping Object.Instantiate on every audible-range crossing.
            LivekitAudioSource first = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            factory.Dispose(first);

            LivekitAudioSource second = factory.Create(new StreamKey(WALLET_B, SID_1), Weak<AudioStream>.Null);
            seenSources.Add(second);

            Assert.That(second, Is.SameAs(first), "pool must hand back the just-released instance");
        }

        [Test]
        public void PoolStateInvariantsWhenDisposed()
        {
            // §6 inert-state invariants — once Dispose runs, Unity must route neither audio-thread
            // (OnAudioFilterRead) nor main-thread (OnAudioConfigurationChanged) callbacks to the source.
            LivekitAudioSource source = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            AudioSource audioSource = source.AudioSource;

            factory.Dispose(source);

            Assert.That(source.gameObject.activeSelf, Is.False, "GameObject must be inactive in pool");
            Assert.That(source.enabled, Is.False, "LivekitAudioSource must be disabled in pool (drops audio config subscription)");
            Assert.That(audioSource.enabled, Is.False, "AudioSource must be disabled in pool");
            Assert.That(audioSource.isPlaying, Is.False);
            Assert.That(audioSource.mute, Is.True);
            Assert.That(audioSource.volume, Is.EqualTo(0f));
            Assert.That(source.transform.parent, Is.Not.Null);
            Assert.That(source.transform.parent, Is.EqualTo(factory.sourcesRoot),
                "pooled source must sit under the factory's single hierarchy root (the pool's renamed container)");

            // Keep alive across teardown — pool owns it now.
            seenSources.Add(source);
        }

        [Test]
        public void LiveStateInvariantsWhenAcquired()
        {
            // §7 live-state invariants — immediately post-Create (pre-A1-spawn-disable) the source must
            // be wired up for playback so PositionSystem can rectify state on the very next tick.
            LivekitAudioSource source = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            seenSources.Add(source);

            AudioSource audioSource = source.AudioSource;

            Assert.That(source.enabled, Is.True);
            Assert.That(audioSource.enabled, Is.True);
            Assert.That(audioSource.isPlaying, Is.True);
            Assert.That(audioSource.mute, Is.True, "PositionSystem unmutes after first sync — initial state is muted");
            Assert.That(source.transform.IsChildOf(factory.sourcesRoot), Is.True,
                "live source must sit under sourcesRoot subtree — pool path keeps it under POOL_CONTAINER to avoid SetParent");
            Assert.That(source.gameObject.name, Does.StartWith("LivekitSource_"));
        }

        [Test]
        public void AppliesSettingsOnceAtCreationNotPerAcquire()
        {
            // §5.2 regression guard — once-per-instance settings (mixer group, rolloff, spatial) are
            // applied in the creation handler, not on every acquire. If a future refactor moves them
            // back into Create, this test catches it: a value mutated externally must survive a recycle.
            LivekitAudioSource first = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            AudioSource audioSource = first.AudioSource;

            const float MUTATED_SPREAD = 42f;
            audioSource.spread = MUTATED_SPREAD;

            factory.Dispose(first);

            LivekitAudioSource second = factory.Create(new StreamKey(WALLET_B, SID_1), Weak<AudioStream>.Null);
            seenSources.Add(second);

            Assert.That(second, Is.SameAs(first), "precondition — same pooled instance");
            Assert.That(second.AudioSource.spread, Is.EqualTo(MUTATED_SPREAD),
                "settings must not be re-applied on every acquire — Apply3dAudioSettings would reset spread to 0");
        }

        [Test]
        public void DisposeDoesNotDoubleReleaseWhenCalledTwice()
        {
            // CleanupSystem teardown can revisit an already-disposed source: TearDownMarkedAudioEntities
            // re-fires across frames until DestroyEntitiesSystem catches up (it's [ThrottlingEnabled]),
            // and OnDispose's DisposeAllAudioSources iterates components without filtering
            // DeleteEntityIntention. With PoolConstants.CHECK_COLLECTIONS=false the underlying pool
            // would silently push a duplicate onto its stack, so a later Create() would alias the same
            // LivekitAudioSource across two entities. Dispose must be idempotent at the factory layer.
            LivekitAudioSource source = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);

            Assert.DoesNotThrow(() => factory.Dispose(source));
            Assert.That(factory.poolCountInactive, Is.EqualTo(1), "first Dispose returns the source to the pool");
            Assert.That(factory.liveInstanceCount, Is.EqualTo(0), "first Dispose decrements the live counter");

            Assert.DoesNotThrow(() => factory.Dispose(source), "second Dispose must short-circuit");
            Assert.That(factory.poolCountInactive, Is.EqualTo(1),
                "second Dispose must NOT push a duplicate onto the pool stack — that's the aliasing bug");
            Assert.That(factory.liveInstanceCount, Is.EqualTo(0), "second Dispose must not double-decrement");

            seenSources.Add(source);
        }

        [Test]
        public void DisposeRootClearsPoolAndDestroysRoot()
        {
            // §5.6 — after DisposeRoot the entire feature footprint (live + pooled) is gone.
            // Pool counters must zero out and sourcesRoot must report Unity-fake-null.
            LivekitAudioSource a = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            LivekitAudioSource b = factory.Create(new StreamKey(WALLET_A, SID_2), Weak<AudioStream>.Null);
            LivekitAudioSource c = factory.Create(new StreamKey(WALLET_B, SID_1), Weak<AudioStream>.Null);

            factory.Dispose(a);
            factory.Dispose(b);
            factory.Dispose(c);

            Transform sourcesRootBefore = factory.sourcesRoot;
            factory.DisposeRoot();

            Assert.That(factory.poolCountInactive, Is.EqualTo(0), "pool must drain on DisposeRoot");
            Assert.That(sourcesRootBefore == null, Is.True, "sourcesRoot must be Unity-fake-null after destroy");
        }

        [Test]
        public void MultipleConcurrentLiveSourcesDoNotShareState()
        {
            // Two avatars in audible range simultaneously must produce two distinct instances with
            // their own per-owner names. The pool widens lazily — second Create on an empty pool
            // triggers a second creationHandler invocation rather than aliasing the first instance.
            LivekitAudioSource a = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            LivekitAudioSource b = factory.Create(new StreamKey(WALLET_B, SID_1), Weak<AudioStream>.Null);
            seenSources.Add(a);
            seenSources.Add(b);

            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(a.gameObject.name, Does.Contain(WALLET_A));
            Assert.That(b.gameObject.name, Does.Contain(WALLET_B));
        }

        [Test]
        public void OverflowFallsBackToLegacyAndDestroysOnDispose()
        {
            // Beyond MAX_LIVE_INSTANCES the factory peels off into the legacy path so the resident
            // GO+AudioSource set can't grow without bound. Overflow instances are destroyed on
            // Dispose instead of returning to the pool — proven by liveInstanceCount staying at the
            // cap (overflow Create doesn't increment it) and the GameObject being Unity-fake-null
            // after Dispose (legacy DisposeLegacy ⇒ SafeDestroyGameObject, not pool.Release).
            int cap = NearbyAudioSourceFactory.MAX_LIVE_INSTANCES;
            var poolManaged = new List<LivekitAudioSource>(cap);

            for (var i = 0; i < cap; i++)
                poolManaged.Add(factory.Create(new StreamKey(WALLET_A, $"sid-{i}"), Weak<AudioStream>.Null));

            Assert.That(factory.liveInstanceCount, Is.EqualTo(cap),
                "all sub-cap Creates must take the pool path and increment liveInstanceCount");

            LivekitAudioSource overflow = factory.Create(new StreamKey(WALLET_B, "overflow"), Weak<AudioStream>.Null);

            Assert.That(factory.liveInstanceCount, Is.EqualTo(cap),
                "overflow Create must NOT increment liveInstanceCount — it goes through the legacy path");
            Assert.That(overflow.gameObject.activeSelf, Is.True, "overflow legacy source must still be live");

            GameObject overflowGo = overflow.gameObject;
            factory.Dispose(overflow);

            Assert.That(overflowGo == null, Is.True,
                "overflow source must be destroyed on Dispose, not returned to the pool");
            Assert.That(factory.poolCountInactive, Is.EqualTo(0),
                "overflow path bypasses pool.Release — no inactive entries should accrue from the overflow Dispose");
            Assert.That(factory.liveInstanceCount, Is.EqualTo(cap),
                "Dispose of an overflow (legacy-tracked) source must not touch the pool's liveInstanceCount");

            // Pool-managed sources still need cleanup for the next test — TearDown's FindObjectsByType
            // sweep covers them, but stash them so this test's intent is explicit.
            seenSources.AddRange(poolManaged);
        }

        [Test]
        public void AudioConfigSubscriptionDroppedInPool()
        {
            // §6 unsubscription invariant — the audio config subscription is owned by
            // LivekitAudioSource.OnEnable/OnDisable, so the only observable indicator from outside is
            // that LivekitAudioSource.enabled is false in POOLED state. That precondition is what
            // guarantees Unity has invoked OnDisable, which has unsubscribed the event handler.
            LivekitAudioSource source = factory.Create(new StreamKey(WALLET_A, SID_1), Weak<AudioStream>.Null);
            factory.Dispose(source);
            seenSources.Add(source);

            Assert.That(source.enabled, Is.False,
                "LivekitAudioSource.enabled must be false in pool — OnDisable is the unsubscription point");
        }
    }
}
