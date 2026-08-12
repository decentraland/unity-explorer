using DCL.Chat.ChatMessages;
using DCL.Chat.ChatReactions.Configs;
using DCL.Chat.ChatViewModels;
using DCL.Chat.History;
using DCL.FeatureFlags;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DCL.Tests.PlayMode.PerformanceTests
{
    /// <summary>
    /// Verifies RefreshItem rebinds exactly one visible item (via the ItemBindCount delta) and leaves the scroll
    /// position untouched, and that it no-ops for an off-screen index instead of rebinding every shown item.
    /// </summary>
    [Category("Performance")]
    public class ChatFeedRefreshItemPerformanceTest
    {
#if UNITY_EDITOR
        private const string PREFAB_PATH = "Assets/DCL/Chat/Assets/ChatMessageFeedView.prefab";

        private Canvas canvas = null!;
        private ChatMessageFeedView view = null!;
        private readonly List<ChatMessageViewModel> viewModels = new ();

        [SetUp]
        public void SetUp()
        {
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool>(),
                variants = new Dictionary<string, FeatureFlagVariantDto>(),
            }));

            // Building the view models routes through ChatMessage -> OfficialWalletsHelper.Instance, which throws until
            // initialized. Its constructor reads FeatureFlagsConfiguration.Instance, so init it after that.
            OfficialWalletsHelper.Reset();
            OfficialWalletsHelper.Initialize(new OfficialWalletsHelper());

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
            Assert.IsNotNull(prefab, $"Could not load chat feed prefab from {PREFAB_PATH}");

            var canvasGo = new GameObject("test-canvas", typeof(Canvas));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject instance = Object.Instantiate(prefab, canvas.transform);
            view = instance.GetComponentInChildren<ChatMessageFeedView>(true);
            Assert.IsNotNull(view, "ChatMessageFeedView not found under prefab");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ChatMessageViewModel vm in viewModels)
                ChatMessageViewModel.POOL.Release(vm);
            viewModels.Clear();

            if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
            OfficialWalletsHelper.Reset();
            FeatureFlagsConfiguration.Reset();
        }

        [UnityTest]
        [Performance]
        public IEnumerator RefreshItem_RebindsSingleVisibleItemOnly()
        {
            for (int i = 0; i < 40; i++)
            {
                ChatMessageViewModel vm = ChatMessageViewModel.POOL.Get();
                vm.Message = ChatMessage.NewFromSystem($"system message {i}");
                vm.IsSeparator = false;
                vm.PendingToAnimate = false;
                viewModels.Add(vm);
            }

            view.SetReactionsEnabled(false);
            view.SetReactionsConfig(ScriptableObject.CreateInstance<ChatReactionsAtlasConfig>(), "0x0",
                ScriptableObject.CreateInstance<ChatReactionsMessageConfig>());
            view.Initialize(viewModels);
            view.ReconstructScrollView(true);

            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            if (view.ItemBindCount == 0)
                Assert.Ignore("LoopListView2 produced no shown items in the headless layout — cannot exercise the single-item path.");

            // IsItemVisible is viewport-position based: an item just outside the viewport can still be instantiated as a
            // buffer item, which RefreshItem WOULD legitimately rebind. RefreshItem only no-ops for indices outside the
            // loop list's instantiated range, so resolve a genuinely not-shown index using the same predicate the
            // optimization keys on (GetShownItemByItemIndex == null), reached via reflection on the private loopList.
            object loopList = typeof(ChatMessageFeedView)
                             .GetField("loopList", BindingFlags.NonPublic | BindingFlags.Instance)!
                             .GetValue(view)!;
            MethodInfo getShownItem = loopList.GetType().GetMethod("GetShownItemByItemIndex", new[] { typeof(int) })!;

            // ModelToViewIndex adds 1 for the top padding item; mirror it here.
            bool IsItemShown(int modelIndex) => getShownItem.Invoke(loopList, new object[] { modelIndex + 1 }) != null;

            int visibleIndex = -1, offscreenIndex = -1;
            for (int i = 0; i < viewModels.Count; i++)
            {
                if (view.IsItemVisible(i)) { if (visibleIndex < 0) visibleIndex = i; }
                else if (offscreenIndex < 0 && !IsItemShown(i)) offscreenIndex = i;
            }

            if (visibleIndex < 0)
                Assert.Ignore("No visible item resolved in the headless layout.");

            Vector2 posBefore = view.ContentAnchoredPosition;
            int before = view.ItemBindCount;
            view.RefreshItem(visibleIndex);
            Assert.AreEqual(1, view.ItemBindCount - before, "RefreshItem must rebind exactly one visible item");

            Assert.AreEqual(posBefore.x, view.ContentAnchoredPosition.x, 0.01f, "content x moved during RefreshItem");
            Assert.AreEqual(posBefore.y, view.ContentAnchoredPosition.y, 0.01f, "content y moved during RefreshItem");

            if (offscreenIndex >= 0)
            {
                int beforeOff = view.ItemBindCount;
                view.RefreshItem(offscreenIndex);
                Assert.AreEqual(0, view.ItemBindCount - beforeOff, "RefreshItem must not rebind an off-screen item");
            }

            Measure.Method(() => view.RefreshItem(visibleIndex)).WarmupCount(5).MeasurementCount(20).Run();
        }
#endif
    }
}
