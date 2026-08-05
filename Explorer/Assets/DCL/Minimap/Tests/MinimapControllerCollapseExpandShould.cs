using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using DCL.Clipboard;
using DCL.Donations;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Minimap.Settings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.PlacesAPIService;
using DCL.RealmNavigation;
using DCL.ResourcesUnloading;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using MVC;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap.Tests
{
    /// <summary>
    ///     Regression test for unity-explorer#9470 (minimap-hidden-ui-lock / QA "Black market | Hiding
    ///     mini map blocks the UI of the market"): collapsing the minimap must stop the (now invisible)
    ///     FullBackground CanvasGroup and the ExpandBgImage backdrop from eating pointer input meant for
    ///     scene UI underneath, and re-expanding must restore normal blocking.
    ///
    ///     MinimapController/MinimapView live in the DCL.UI.Hud assembly, which grants no
    ///     InternalsVisibleTo to DCL.EditMode.Tests, so this deliberately never touches any `internal`
    ///     member of either type. Instead it loads the real Minimap.prefab (resolving all of MinimapView's
    ///     ~25 serialized-field wiring for free) and reaches the Collapse/Expand buttons and the
    ///     FullBackground/ExpandBgImage graphics via public Transform.Find/GetComponent hierarchy lookups
    ///     - the same hierarchy documented in report.md - then fires their public Button.onClick, the
    ///     exact entry point OnViewInstantiated() wires to the private CollapseMinimap()/ExpandMinimap().
    /// </summary>
    public class MinimapControllerCollapseExpandShould
    {
        private const string MINIMAP_PREFAB_PATH = "Assets/DCL/Minimap/Assets/Minimap.prefab";

        private GameObject viewGameObject = null!;
        private MinimapController controller = null!;
        private MinimapContextMenuSettings minimapContextMenuSettings = null!;
        private World world = null!;

        private CanvasGroup fullBackgroundCanvasGroup = null!;
        private Image expandBgImage = null!;
        private Button collapseButton = null!;
        private Button expandButton = null!;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MINIMAP_PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load {MINIMAP_PREFAB_PATH} - has the Minimap prefab moved?");

            viewGameObject = Object.Instantiate(prefab!);
            MinimapView view = viewGameObject.GetComponent<MinimapView>();
            Assert.IsNotNull(view, "Minimap.prefab root has no MinimapView component - prefab wiring changed?");

            // Hierarchy per report.md step 5/6 and review.md finding 1/5 (re-verified against the
            // pin's Minimap.prefab): Minimap/Container/{FullBackground, ExpandBgImage, InfoSection/
            // {Collapse, ExpandButton}}. Reached via public Transform API only - no `internal` access.
            Transform container = viewGameObject.transform.Find("Container")!;
            Assert.IsNotNull(container, "Minimap.prefab hierarchy changed: Container not found under the root");

            fullBackgroundCanvasGroup = container.Find("FullBackground")!.GetComponent<CanvasGroup>();
            expandBgImage = container.Find("ExpandBgImage")!.GetComponent<Image>();
            collapseButton = container.Find("InfoSection/Collapse")!.GetComponent<Button>();
            expandButton = container.Find("InfoSection/ExpandButton")!.GetComponent<Button>();

            Assert.IsNotNull(fullBackgroundCanvasGroup, "Container/FullBackground has no CanvasGroup - prefab wiring changed?");
            Assert.IsNotNull(expandBgImage, "Container/ExpandBgImage has no Image - prefab wiring changed?");
            Assert.IsNotNull(collapseButton, "Container/InfoSection/Collapse has no Button - prefab wiring changed?");
            Assert.IsNotNull(expandButton, "Container/InfoSection/ExpandButton has no Button - prefab wiring changed?");

            world = World.Create();
            Entity playerEntity = world.Create();

            minimapContextMenuSettings = ScriptableObject.CreateInstance<MinimapContextMenuSettings>();

            IScenesCache scenesCache = Substitute.For<IScenesCache>();
            ITeleportController teleportController = Substitute.For<ITeleportController>();
            ICacheCleaner cacheCleaner = Substitute.For<ICacheCleaner>();

            // Concrete (non-interface) constructor dependency; never exercised by Collapse/Expand -
            // just needs to exist. ECSReloadScene's own dependencies are equally inert here.
            var reloadSceneCommand = new ReloadSceneChatCommand(
                new ECSReloadScene(scenesCache, world, playerEntity, false, cacheCleaner),
                world,
                playerEntity,
                scenesCache,
                teleportController,
                false);

            IDonationsService donationsService = Substitute.For<IDonationsService>();
            donationsService.DonationsEnabledCurrentScene.Returns(
                new ReactiveProperty<(bool enabled, string? creatorAddress, Vector2Int? baseParcel)>((false, null, null)));

            controller = new MinimapController(
                view,
                Substitute.For<IMapRenderer>(),
                Substitute.For<IMVCManager>(),
                Substitute.For<IPlacesAPIService>(),
                new IRealmData.Fake(), // RealmType defaults to GenesisCity, matching the repro parcel (3,-35)
                Substitute.For<IRealmNavigator>(),
                scenesCache,
                new MapPathEventBus(),
                Substitute.For<ISceneRestrictionBusController>(),
                Vector2Int.zero,
                Substitute.For<ISystemClipboard>(),
                Substitute.For<IDecentralandUrlsSource>(),
                Substitute.For<IChatMessagesBus>(),
                reloadSceneCommand,
                Substitute.For<IRoomHub>(),
                Substitute.For<ILoadingStatus>(),
                false, // includeBannedUsersFromScene: keeps IRoomHub.SceneRoom() untouched by ctor/Dispose
                new HomePlaceEventBus(),
                minimapContextMenuSettings,
                donationsService);

            // Same seam production DI (MinimapPlugin.InitializeAsync -> IMVCManager.RegisterController
            // -> eventual Show) uses to bring the view up. LaunchViewLifeCycleAsync sets the protected
            // viewInstance and calls OnViewInstantiated() - which wires collapseButton/expandButton's
            // onClick to the private CollapseMinimap()/ExpandMinimap() - synchronously, before
            // suspending forever on WaitForCloseIntentAsync (MinimapController overrides it with
            // UniTask.Never: it never signals a close intent by design). So this call never completes,
            // but everything before that first genuine await point already ran by the time control
            // returns here (UniTask/async-method semantics: the synchronous prefix up to the first
            // *actually incomplete* awaited task runs inline on the caller's thread).
            var ordering = new CanvasOrdering(CanvasOrdering.SortingLayer.Persistent, 0);
            UniTask launchTask = controller.LaunchViewLifeCycleAsync(ordering, new ControllerNoData(), CancellationToken.None);

            // Surface any exception thrown during that synchronous prefix instead of silently
            // swallowing it; a genuinely pending task (the expected outcome) is left alone.
            if (launchTask.Status != UniTaskStatus.Pending)
                launchTask.GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();

            if (minimapContextMenuSettings != null)
                Object.DestroyImmediate(minimapContextMenuSettings);

            if (viewGameObject != null)
                Object.DestroyImmediate(viewGameObject);

            world?.Dispose();
        }

        [Test]
        public void StopBlockingSceneUiClicksWhenCollapsedAndRestoreOnExpand()
        {
            // Precondition: while expanded, the map square legitimately blocks input (current, correct
            // behavior - unaffected by this bug/fix).
            Assert.IsTrue(fullBackgroundCanvasGroup.blocksRaycasts, "Precondition: FullBackground should block raycasts while expanded");
            Assert.IsTrue(fullBackgroundCanvasGroup.interactable, "Precondition: FullBackground should be interactable while expanded");
            Assert.IsTrue(expandBgImage.raycastTarget, "Precondition: ExpandBgImage should be a raycast target while expanded");

            // Act - collapse via the same UI entry point the fold arrow uses (Container/InfoSection/
            // Collapse's onClick, wired by OnViewInstantiated() to the private CollapseMinimap()).
            collapseButton.onClick.Invoke();

            // Assert - unity-explorer#9470: hiding the minimap must stop the (now invisible)
            // FullBackground subtree and the ExpandBgImage backdrop from swallowing clicks meant for
            // scene UI underneath (e.g. the Black-market buttons at 3,-35).
            //
            // FAILS at pin 80ee7584b413f5e65cf75bc8a2a51057b241b649: CollapseMinimap() only animates
            // alpha and toggles a couple of GameObjects - it never touches blocksRaycasts/interactable/
            // raycastTarget, so all three of these read back true/true/true instead.
            // PASSES with potential-fix.patch applied.
            Assert.IsFalse(fullBackgroundCanvasGroup.blocksRaycasts, "FullBackground.blocksRaycasts must be false while collapsed (left true on the pin, eating scene UI clicks)");
            Assert.IsFalse(fullBackgroundCanvasGroup.interactable, "FullBackground.interactable must be false while collapsed");
            Assert.IsFalse(expandBgImage.raycastTarget, "ExpandBgImage.raycastTarget must be false while collapsed (review.md finding 1: the second, independent click-eater outside the CanvasGroup)");

            // Act - expand via the same UI entry point the unfold arrow uses (Container/InfoSection/
            // ExpandButton's onClick, wired to the private ExpandMinimap()).
            expandButton.onClick.Invoke();

            // Assert - re-expanding must restore normal input blocking.
            Assert.IsTrue(fullBackgroundCanvasGroup.blocksRaycasts, "FullBackground.blocksRaycasts must be restored to true on expand");
            Assert.IsTrue(fullBackgroundCanvasGroup.interactable, "FullBackground.interactable must be restored to true on expand");
            Assert.IsTrue(expandBgImage.raycastTarget, "ExpandBgImage.raycastTarget must be restored to true on expand");
        }
    }
}
