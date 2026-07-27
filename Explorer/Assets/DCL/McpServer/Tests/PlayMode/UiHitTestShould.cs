#if MCP_TEST_AUTOMATION
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DCL.McpServer.Tests.PlayMode
{
    /// <summary>
    ///     Guards the hit-test behind <c>click_ui</c> / <c>hover_ui</c>: an interaction must be refused when something
    ///     covers the target, because a suite that reports success on an occluded control goes green on a UI a real
    ///     user cannot operate. That is the property the whole client-UI surface rests on, so it is asserted against a
    ///     real Canvas and a real <see cref="EventSystem" /> raycast rather than mocked.
    ///     <para>
    ///         PlayMode, because <see cref="EventSystem.RaycastAll" /> needs a live scene with laid-out RectTransforms.
    ///         The tools are driven through <see cref="UiAutomation" /> directly rather than over HTTP: the transport is
    ///         already covered by <c>McpHttpServerShould</c>, and binding a port would make this flaky on CI agents.
    ///     </para>
    /// </summary>
    public class UiHitTestShould
    {
        private const string TARGET = "HitTestTarget";
        private const string OTHER = "OtherTarget";
        private const string MODAL = "Modal";

        private GameObject eventSystem = null!;
        private GameObject canvas = null!;
        private Recorder targetRecorder = null!;

        [SetUp]
        public void SetUp()
        {
            // Only the EventSystem component is needed: RaycastAll does not go through an input module, and adding the
            // legacy StandaloneInputModule throws outright because the project runs the Input System package.
            eventSystem = new GameObject(nameof(EventSystem), typeof(EventSystem));

            canvas = new GameObject("HitTestCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            targetRecorder = AddRaycastTarget(TARGET, 400f, Vector2.zero).AddComponent<Recorder>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(eventSystem);
        }

        [UnityTest]
        public IEnumerator HoverAClearControlAndRefuseAnOccludedOne()
        {
            yield return null;

            Assert.That(UiAutomation.TryHover(TARGET, out JObject clear), Is.True);
            AssertReached(clear, "hovered", TARGET);
            Assert.That(targetRecorder.Enters, Is.EqualTo(1), "the pointer-enter never reached the control");

            Cover();
            yield return null;

            Assert.That(UiAutomation.TryHover(TARGET, out JObject blocked), Is.True);
            AssertRefused(blocked, "hovered");
            Assert.That(targetRecorder.Enters, Is.EqualTo(1), "an occluded control must not receive the pointer-enter");
        }

        [UnityTest]
        public IEnumerator ClickAClearControlAndRefuseAnOccludedOne()
        {
            yield return null;

            Assert.That(UiAutomation.TryClick(TARGET, out JObject clear), Is.True);
            AssertReached(clear, "clicked", TARGET);
            Assert.That(targetRecorder.Clicks, Is.EqualTo(1), "the click never reached the control");

            Cover();
            yield return null;

            Assert.That(UiAutomation.TryClick(TARGET, out JObject blocked), Is.True);
            AssertRefused(blocked, "clicked");
            Assert.That(targetRecorder.Clicks, Is.EqualTo(1), "an occluded control must not receive the click");
        }

        [UnityTest]
        public IEnumerator ExitTheElementItWasHoveringBefore()
        {
            // Placed clear of the target: overlapping it would make the first hover a (correct) occlusion refusal,
            // and the exit assertion below would then be proving nothing.
            AddRaycastTarget(OTHER, 100f, new Vector2(300f, 0f));
            yield return null;

            Assert.That(UiAutomation.TryHover(TARGET, out JObject first), Is.True);
            Assert.That(first["hovered"]!.Value<bool>(), Is.True, "the first hover must land, or the exit below proves nothing");

            // Without this the test still passes on the direct-dispatch fallback, where no hit-test ran at all.
            Assert.That(first["dispatch"]!.Value<string>(), Is.EqualTo("raycast"));
            Assert.That(targetRecorder.Exits, Is.EqualTo(0));

            // Moving to another element must release the first, or hover states pile up across a suite.
            Assert.That(UiAutomation.TryHover(OTHER, out JObject moved), Is.True);
            Assert.That(moved["hovered"]!.Value<bool>(), Is.True);
            Assert.That(targetRecorder.Exits, Is.EqualTo(1), "the previously hovered control never received its pointer-exit");
        }

        private static void AssertReached(JObject result, string field, string expectedTopHit)
        {
            Assert.That(result["dispatch"]!.Value<string>(), Is.EqualTo("raycast"));
            Assert.That(result["topHit"]!.Value<string>(), Is.EqualTo(expectedTopHit));
            Assert.That(result[field]!.Value<bool>(), Is.True);
        }

        private static void AssertRefused(JObject result, string field)
        {
            Assert.That(result[field]!.Value<bool>(), Is.False, $"{field} must be false when the control is covered");
            Assert.That(result["dispatch"]!.Value<string>(), Is.EqualTo("blocked"));
            Assert.That(result["topHit"]!.Value<string>(), Is.EqualTo(MODAL), "the result must name what covered the control");
            Assert.That(result["reason"]!.Value<string>(), Does.Contain(MODAL));
        }

        /// <summary>Drops a raycastable graphic over the whole canvas, so the target sits behind it.</summary>
        private void Cover()
        {
            AddRaycastTarget(MODAL, 800f, Vector2.zero).transform.SetAsLastSibling();
        }

        private GameObject AddRaycastTarget(string name, float size, Vector2 position)
        {
            var element = new GameObject(name, typeof(Image));
            element.transform.SetParent(canvas.transform, false);

            var rect = element.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            element.GetComponent<Image>().raycastTarget = true;

            return element;
        }

        /// <summary>Counts the pointer events that actually arrive, so a result field cannot claim more than happened.</summary>
        private class Recorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            public int Enters { get; private set; }
            public int Exits { get; private set; }
            public int Clicks { get; private set; }

            public void OnPointerEnter(PointerEventData eventData) => Enters++;

            public void OnPointerExit(PointerEventData eventData) => Exits++;

            public void OnPointerClick(PointerEventData eventData) => Clicks++;
        }
    }
}
#endif
