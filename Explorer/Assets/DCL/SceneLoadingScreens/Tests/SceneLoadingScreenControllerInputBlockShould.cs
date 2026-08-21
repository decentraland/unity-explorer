using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.Input;
using DCL.Input.Component;
using DCL.Prefs;
using DCL.Utilities;
using ECS.Abstract;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.SceneLoadingScreens.Tests
{
    /// <summary>
    ///     Regression coverage for #9502: camera and avatar input stayed permanently blocked after the
    ///     scene loading screen closed on a cancelled outer token (teleport superseded/aborted mid-load,
    ///     e.g. spawning into a World via a coordinate link, or a long Alt-Tab stalling the fade).
    ///     <see cref="InputMapComponent.BlockInput" />/<see cref="InputMapComponent.UnblockInput" /> are
    ///     refcounted with no external audit or reset, so an acquire in <c>OnBeforeViewShow</c> that is
    ///     never matched by a release on an abnormal close leaks the block forever - every subsequent
    ///     "unblock" (e.g. a chat blur) only brings the counter from 2 back to 1, not to 0.
    ///     See bugreports-early-aug/camera-avatar-locked-after-paste-alttab/{report.md,review.md}.
    /// </summary>
    public class SceneLoadingScreenControllerInputBlockShould
    {
        private const string VIEW_PREFAB_PATH = "Assets/DCL/SceneLoadingScreens/Assets/SceneLoadingScreen.prefab";
        private const string AUDIO_MIXER_PATH = "Assets/DCL/Audio/Prefabs/GeneralAudioMixer.mixer";

        private static readonly InputMapComponent.Kind ALL_KINDS = AllKinds();
        private static readonly InputMapComponent.Kind BLOCKED_BY_LOADING_SCREEN = BlockUserInputMask();

        // SceneLoadingScreenController.UpdateLocalizedTextAsync() is fired via .Forget() from both
        // OnViewInstantiated and OnViewShow - a detached UniTaskVoid this test never has a handle to
        // await. In a bare EditMode harness (no active localization catalog) its AsyncOperationHandle
        // resolves to Failed a few Editor ticks later and logs "cannot load localized text" - unrelated
        // to the input-block bug under test. Nothing else in the process pumps the Editor loop once a
        // test's own awaits are done, so without an explicit flush that continuation can resolve
        // arbitrarily far in the future (another fixture entirely) - past any ignoreFailingMessages
        // window scoped only to [SetUp]/[TearDown] or even [OneTimeSetUp]/[OneTimeTearDown]. The fix is
        // two-part: suppress for the whole fixture AND explicitly pump bounded frames at the tail of
        // each test (FlushDeferredViewLogsAsync) so the log - if it fires at all - fires HERE, while
        // suppression is still active, instead of leaking into an unrelated later test.
        private const int FLUSH_FRAME_COUNT = 30;

        private World? world;
        private SingleInstanceEntity inputMapEntity;
        private IInputBlock? inputBlock;
        private SceneLoadingScreenView? viewInstance;
        private AudioMixerVolumesController? audioMixerVolumesController;
        private bool originalIgnoreFailingMessages;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // The view's fire-and-forget localized-text refresh is unrelated to the input-block bug
            // under test and can log in a bare Editor test context (no active localization init) -
            // including from late async continuations that land between tests, so the suppression
            // must span the whole fixture, not a single test.
            originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LogAssert.ignoreFailingMessages = originalIgnoreFailingMessages;
        }

        // Pumps bounded Editor frames so any pending fire-and-forget continuation (see the comment
        // above FLUSH_FRAME_COUNT) gets a chance to actually resolve and log before this test returns,
        // rather than resolving later while some unrelated test/fixture is running. Tolerant by design:
        // the message may fire 0..N times here (or not at all) - LogAssert.ignoreFailingMessages is what
        // makes that acceptable, not an expectation that it must occur.
        private static async UniTask FlushDeferredViewLogsAsync()
        {
            for (var i = 0; i < FLUSH_FRAME_COUNT; i++)
                await UniTask.Yield();
        }

        [SetUp]
        public void SetUp()
        {
            // SceneLoadingScreenController's ctor eagerly constructs a PersistentSetting<int>, which reads
            // DCLPlayerPrefs.GetInt off the static dclPrefs backing field - never populated in a bare EditMode
            // test process (RuntimeInitializeOnLoadMethod only fires in Play/Runtime). Inject an in-memory
            // implementation via reflection, the established pattern for this exact gap (see
            // ChatReactionRecentsServiceShould/HomeMarkerControllerShould).
            var dclPrefsField = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);
            dclPrefsField!.SetValue(null, new InMemoryDCLPlayerPrefs());

            world = World.Create();
            world.Create(new InputMapComponent(ALL_KINDS));
            inputMapEntity = world.CacheInputMap();
            inputBlock = new ECSInputBlock(world);

            var viewPrefab = AssetDatabase.LoadAssetAtPath<SceneLoadingScreenView>(VIEW_PREFAB_PATH);
            Assert.IsNotNull(viewPrefab, $"Could not load the real loading-screen prefab from {VIEW_PREFAB_PATH}");
            viewInstance = Object.Instantiate(viewPrefab);

            var audioMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AUDIO_MIXER_PATH);
            Assert.IsNotNull(audioMixer, $"Could not load the real audio mixer from {AUDIO_MIXER_PATH}");
            audioMixerVolumesController = new AudioMixerVolumesController(audioMixer);
        }

        [TearDown]
        public void TearDown()
        {
            if (viewInstance != null)
                Object.DestroyImmediate(viewInstance.gameObject);

            world!.Dispose();

            // Reset the static field so later tests in the same run aren't left with a stale in-memory
            // prefs instance (mirrors the reset half of the same established pattern).
            var dclPrefsField = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);
            dclPrefsField!.SetValue(null, null);
        }

        [Test]
        public async Task ReleaseInputBlockWhenCloseIntentIsCancelledAsync()
        {
            // The framework resets LogAssert state at test start, wiping any fixture/SetUp-scoped
            // suppression - the flag must be raised inside the test body itself.
            LogAssert.ignoreFailingMessages = true;

            SceneLoadingScreenController controller = CreateController(EmptyTipsProvider());

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // OnBeforeViewShow blocks input unconditionally on every show. The outer token is already
            // cancelled, so WaitForCloseIntentAsync's "if (!ct.IsCancellationRequested) await FadeOutAsync(ct)"
            // guard is never entered and the only release the unpatched code has (the last statement of
            // FadeOutAsync) never runs - this is leak path 1 from report.md ("outer token cancelled").
            await controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), CompletedParams(), cts.Token);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS & ~BLOCKED_BY_LOADING_SCREEN),
                "input should be blocked right after showing the loading screen");

            // The MVC teardown (MVCManager.ShowOverlayAsync's finally) always calls HideViewAsync once the
            // view has started showing, regardless of whether WaitForCloseIntentAsync threw, was cancelled,
            // or returned normally - so OnViewClose is guaranteed to run here exactly as it does in production.
            await ((IController)controller).HideViewAsync(CancellationToken.None);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS),
                "BLOCK_USER_INPUT must be released when the loading screen closes on a cancelled token - " +
                "unpatched, this leaks +1 on the refcount forever (#9502)");

            // Let the OnViewInstantiated/OnViewShow localized-text refreshes settle before TearDown
            // destroys viewInstance, so any log they produce fires inside this test, not later.
            await FlushDeferredViewLogsAsync();
        }

        [Test]
        public async Task ReleaseInputBlockWhenClosedWhileSceneIsStillLoadingAsync()
        {
            // The framework resets LogAssert state at test start, wiping any fixture/SetUp-scoped
            // suppression - the flag must be raised inside the test body itself.
            LogAssert.ignoreFailingMessages = true;

            SceneLoadingScreenController controller = CreateController(EmptyTipsProvider());

            // A load report that never completes keeps WaitForCloseIntentAsync suspended waiting on it,
            // so the fade-out (the unpatched code's only release) never runs. Deliberately not awaited:
            // this matches the real MVCManager.ShowOverlayAsync race where the teardown's finally calls
            // HideViewAsync while the orphaned lifecycle task is still suspended (teleport superseded
            // mid-load - leak path 2 from report.md).
            AsyncLoadProcessReport pendingReport = AsyncLoadProcessReport.Create(CancellationToken.None);
            UniTask launch = controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new SceneLoadingScreenController.Params(pendingReport), CancellationToken.None);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS & ~BLOCKED_BY_LOADING_SCREEN),
                "input should be blocked right after showing the loading screen");

            await ((IController)controller).HideViewAsync(CancellationToken.None);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS),
                "BLOCK_USER_INPUT must be released even when the loading screen closes while the scene " +
                "is still loading - unpatched, this leaks +1 on the refcount forever (#9502)");

            launch.Forget();

            // Let the OnViewInstantiated/OnViewShow localized-text refreshes settle before TearDown
            // destroys viewInstance, so any log they produce fires inside this test, not later.
            await FlushDeferredViewLogsAsync();
        }

        private static ISceneTipsProvider EmptyTipsProvider()
        {
            ISceneTipsProvider tipsProvider = Substitute.For<ISceneTipsProvider>();
            tipsProvider.Get().Returns(new SceneTips(TimeSpan.Zero, false, new List<SceneTips.Tip>()));
            return tipsProvider;
        }

        private SceneLoadingScreenController CreateController(ISceneTipsProvider tipsProvider) =>
            new (() => viewInstance!, tipsProvider, TimeSpan.Zero, audioMixerVolumesController!, inputBlock!, Substitute.For<IMVCManager>());

        private static SceneLoadingScreenController.Params CompletedParams()
        {
            AsyncLoadProcessReport report = AsyncLoadProcessReport.Create(CancellationToken.None);
            report.SetProgress(1f);
            return new SceneLoadingScreenController.Params(report);
        }

        private InputMapComponent.Kind ActiveKinds() =>
            inputMapEntity.GetInputMapComponent(world!).Active;

        private static InputMapComponent.Kind AllKinds()
        {
            InputMapComponent.Kind all = InputMapComponent.Kind.None;

            foreach (InputMapComponent.Kind kind in InputMapComponent.VALUES)
                all |= kind;

            return all;
        }

        private static InputMapComponent.Kind BlockUserInputMask()
        {
            InputMapComponent.Kind mask = InputMapComponent.Kind.None;

            foreach (InputMapComponent.Kind kind in InputMapComponent.BLOCK_USER_INPUT)
                mask |= kind;

            return mask;
        }
    }
}
