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
    ///     Regression coverage for #9502: the refcounted input block acquired in <c>OnBeforeViewShow</c>
    ///     leaked forever when the loading screen closed on a cancelled token or mid-load.
    /// </summary>
    public class SceneLoadingScreenControllerInputBlockShould
    {
        private const string VIEW_PREFAB_PATH = "Assets/DCL/SceneLoadingScreens/Assets/SceneLoadingScreen.prefab";
        private const string AUDIO_MIXER_PATH = "Assets/DCL/Audio/Prefabs/GeneralAudioMixer.mixer";

        private static readonly InputMapComponent.Kind ALL_KINDS = AllKinds();
        private static readonly InputMapComponent.Kind BLOCKED_BY_LOADING_SCREEN = BlockUserInputMask();

        // The view's detached UpdateLocalizedTextAsync().Forget() can log a localization failure several
        // Editor ticks later; each test pumps this many frames so the log fires while suppression is active.
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
            // Late fire-and-forget localization logs can land between tests, so suppression spans the whole fixture
            originalIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LogAssert.ignoreFailingMessages = originalIgnoreFailingMessages;
        }

        // Pumps bounded Editor frames so pending fire-and-forget continuations resolve, and may log,
        // before the test returns; the log may fire 0..N times here.
        private static async UniTask FlushDeferredViewLogsAsync()
        {
            for (var i = 0; i < FLUSH_FRAME_COUNT; i++)
                await UniTask.Yield();
        }

        [SetUp]
        public void SetUp()
        {
            // The controller's ctor reads DCLPlayerPrefs, whose static backing field is never populated in
            // EditMode; inject an in-memory implementation via reflection (same pattern as ChatReactionRecentsServiceShould)
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

            // Reset the static field so later tests don't see a stale in-memory prefs instance
            var dclPrefsField = typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static);
            dclPrefsField!.SetValue(null, null);
        }

        [Test]
        public async Task ReleaseInputBlockWhenCloseIntentIsCancelledAsync()
        {
            // The framework resets LogAssert state at test start, so the flag must be raised inside the test body
            LogAssert.ignoreFailingMessages = true;

            SceneLoadingScreenController controller = CreateController(EmptyTipsProvider());

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // With the outer token already cancelled, the fade-out (the unpatched code's only release) never runs
            await controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), CompletedParams(), cts.Token);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS & ~BLOCKED_BY_LOADING_SCREEN),
                "input should be blocked right after showing the loading screen");

            // Mirrors MVCManager.ShowOverlayAsync's finally, which always calls HideViewAsync once the view has shown
            await ((IController)controller).HideViewAsync(CancellationToken.None);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS),
                "BLOCK_USER_INPUT must be released when the loading screen closes on a cancelled token - " +
                "unpatched, this leaks +1 on the refcount forever (#9502)");

            // Let the fire-and-forget localized-text refreshes settle before TearDown destroys viewInstance
            await FlushDeferredViewLogsAsync();
        }

        [Test]
        public async Task ReleaseInputBlockWhenClosedWhileSceneIsStillLoadingAsync()
        {
            // The framework resets LogAssert state at test start, so the flag must be raised inside the test body
            LogAssert.ignoreFailingMessages = true;

            SceneLoadingScreenController controller = CreateController(EmptyTipsProvider());

            // A never-completing load report keeps WaitForCloseIntentAsync suspended, so the fade-out never runs.
            // Deliberately not awaited: HideViewAsync races the still-suspended lifecycle task, as in production.
            AsyncLoadProcessReport pendingReport = AsyncLoadProcessReport.Create(CancellationToken.None);
            UniTask launch = controller.LaunchViewLifeCycleAsync(new CanvasOrdering(CanvasOrdering.SortingLayer.Overlay, 0), new SceneLoadingScreenController.Params(pendingReport), CancellationToken.None);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS & ~BLOCKED_BY_LOADING_SCREEN),
                "input should be blocked right after showing the loading screen");

            await ((IController)controller).HideViewAsync(CancellationToken.None);

            Assert.That(ActiveKinds(), Is.EqualTo(ALL_KINDS),
                "BLOCK_USER_INPUT must be released even when the loading screen closes while the scene " +
                "is still loading - unpatched, this leaks +1 on the refcount forever (#9502)");

            launch.Forget();

            // Let the fire-and-forget localized-text refreshes settle before TearDown destroys viewInstance
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
