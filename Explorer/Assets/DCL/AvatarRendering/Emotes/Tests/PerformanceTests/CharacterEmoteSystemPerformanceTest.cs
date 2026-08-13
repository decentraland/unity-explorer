using Arch.Core;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes.Play;
using DCL.DebugUtilities;
using DCL.Multiplayer.Emotes;
using ECS.SceneLifeCycle;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.AvatarRendering.Emotes.Tests.PerformanceTests
{
    /// <summary>
    /// <see cref="CharacterEmoteSystem"/>'s <c>UpdateEmoteTags</c> query must poll the native Mecanim
    /// animator state (<c>IAvatarView.GetAnimatorCurrentStateTag</c>) only for avatars that are
    /// actually emoting — not for every entity carrying a <see cref="CharacterEmoteComponent"/>. The
    /// guard early-outs when
    /// <c>CurrentEmoteReference == null &amp;&amp; CurrentAnimationTag == 0</c>.
    /// <para>
    /// Metric: the number of <c>GetAnimatorCurrentStateTag</c> calls during a single
    /// <c>system.Update</c> over a crowd of <c>n</c> avatars, <c>emoting</c> of which hold a live
    /// (legacy) emote reference. Pass criterion: poll count == <c>emoting</c>. A <c>Measure.Method</c>
    /// also records the guarded full-Update cost over the crowd for the timing dimension.
    /// </para>
    /// <para>
    /// A single shared, counting <see cref="IAvatarView"/> substitute is used across every entity: the
    /// query invokes it exactly once per entity that reaches the native poll, so the running total is
    /// the poll count. <c>IsLegacyAnimationPlaying</c> returns true so the emoting fixtures stay
    /// "playing" and are not stopped by <c>CancelEmotes</c> earlier in the same Update — keeping their
    /// live reference so <c>UpdateEmoteTags</c> must poll them.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class CharacterEmoteSystemPerformanceTest : UnitySystemTestBase<CharacterEmoteSystem>
    {
        private GameObject audioSourcePrefab = null!;
        private GameObject poolRootContainer = null!;
        private EmoteReferences legacyEmote = null!;
        private IAvatarView avatarView = null!;
        private int animatorTagPolls;

        [SetUp]
        public void SetUp()
        {
            // EmotePlayer's ctor resolves its pool parent via GameObject.Find("ROOT_POOL_CONTAINER").
            // The bare EditMode scene has no such object, so the null-forgiving `!` in production
            // would let a runtime NRE through — create the expected scene object before constructing it.
            poolRootContainer = new GameObject("ROOT_POOL_CONTAINER");

            audioSourcePrefab = new GameObject("EmoteAudioSource");
            AudioSource audioSource = audioSourcePrefab.AddComponent<AudioSource>();
            var emotePlayer = new EmotePlayer(audioSource, ScriptableObject.CreateInstance<EmoteMaskCatalog>(), legacyAnimationsEnabled: true);

            system = new CharacterEmoteSystem(world, Substitute.For<IEmoteStorage>(), Substitute.For<IEmotesMessageBus>(),
                emotePlayer, Substitute.For<IDebugContainerBuilder>(), localSceneDevelopment: false, new ScenesCache());

            avatarView = Substitute.For<IAvatarView>();
            avatarView.IsLegacyAnimationPlaying.Returns(true);
            // UpdateEmoteTags polls the int-layer overload (GetAnimatorCurrentStateTag(BASE_LAYER_INDEX),
            // BASE_LAYER_INDEX is a const int) — not the string-layer one. NSubstitute keys returns per
            // overload, so the counter must be registered on the int overload or the real polls go
            // uncounted and every case reads 0.
            avatarView.GetAnimatorCurrentStateTag(Arg.Any<int>()).Returns(_ =>
            {
                animatorTagPolls++;
                return 0;
            });

            legacyEmote = new GameObject(nameof(EmoteReferences)).AddComponent<EmoteReferences>();
            legacyEmote.Initialize(null, null, null, null, 0, legacy: true);
        }

        protected override void OnTearDown()
        {
            if (legacyEmote != null) Object.DestroyImmediate(legacyEmote.gameObject);
            if (audioSourcePrefab != null) Object.DestroyImmediate(audioSourcePrefab);
            if (poolRootContainer != null) Object.DestroyImmediate(poolRootContainer);
        }

        private void PopulateCrowd(int n, int emoting)
        {
            for (int i = 0; i < n; i++)
            {
                var emoteComponent = new CharacterEmoteComponent();

                if (i < emoting)
                    emoteComponent.CurrentEmoteReference = legacyEmote;

                world.Create(emoteComponent, avatarView);
            }
        }

        [Test]
        [Performance]
        [TestCase(100, 0)]
        [TestCase(100, 5)]
        public void UpdateEmoteTags_PollsOnlyEmotingAvatars(int n, int emoting)
        {
            PopulateCrowd(n, emoting);

            animatorTagPolls = 0;
            system!.Update(0f);

            Measure.Custom(new SampleGroup("Animator.GetStateTag.Calls", SampleUnit.Undefined), animatorTagPolls);

            Assert.AreEqual(emoting, animatorTagPolls,
                $"UpdateEmoteTags polled the Mecanim animator {animatorTagPolls} times over {n} avatars " +
                $"({emoting} emoting); the guard must limit native polls to emoting avatars.");

            Measure.Method(() => system!.Update(0f))
                   .WarmupCount(5)
                   .MeasurementCount(30)
                   .GC()
                   .Run();
        }
    }
}
