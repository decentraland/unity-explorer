using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.SyntheticInput.Tests
{
    public class UiDiscoveryShould
    {
        private GameObject eventSystemGo = null!;
        private GameObject canvasGo = null!;
        private UiDiscovery discovery = null!;

        [SetUp]
        public void SetUp()
        {
            eventSystemGo = new GameObject("test-event-system");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();

            canvasGo = new GameObject("TestCanvasRoot(Clone)");
            canvasGo.AddComponent<Canvas>();

            discovery = new UiDiscovery(eventSystem);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvasGo);
            Object.DestroyImmediate(eventSystemGo);
        }

        private GameObject AddChild(Transform parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent);
            return child;
        }

        [Test]
        public void RoundTripAPathWithSameNamedSiblings()
        {
            GameObject panel = AddChild(canvasGo.transform, "Panel");
            AddChild(panel.transform, "Item");
            GameObject second = AddChild(panel.transform, "Item");
            AddChild(panel.transform, "Other");

            string path = discovery.PathOf(second.transform);
            Assert.That(path, Is.EqualTo("TestCanvasRoot/Panel/Item[1]"), "the Clone suffix is stripped and the sibling ordinal appended");

            Assert.That(discovery.TryResolve(UiElementAddress.UguiPath(path), out GameObject? resolved, out string? failure), Is.True, failure);
            Assert.That(resolved, Is.EqualTo(second));
        }

        [Test]
        public void ExplainWhichSegmentOfAPathIsMissing()
        {
            AddChild(canvasGo.transform, "Panel");

            Assert.That(discovery.TryResolve(UiElementAddress.UguiPath("TestCanvasRoot/Panel/Gone"), out _, out string? failure), Is.False);
            Assert.That(failure, Does.Contain("'Gone' not found under"));
        }

        [Test]
        public void RejectAStaleInstanceId()
        {
            Assert.That(discovery.TryResolve(UiElementAddress.UguiInstance(123456), out _, out string? failure), Is.False);
            Assert.That(failure, Does.Contain("re-run ui_list"));
        }

        [Test]
        public void ListAnInteractableButtonAndResolveItsListedId()
        {
            GameObject buttonGo = AddChild(canvasGo.transform, "MyButton");
            buttonGo.AddComponent<Image>();
            buttonGo.AddComponent<Button>();

            JArray listed = discovery.ListInteractable(checkOcclusion: false);

            Assert.That(listed.Count, Is.EqualTo(1));
            Assert.That(listed[0]!["kind"]!.Value<string>(), Is.EqualTo("button"));
            Assert.That(listed[0]!["path"]!.Value<string>(), Is.EqualTo("TestCanvasRoot/MyButton"));

            ulong listedId = listed[0]!["id"]!.Value<ulong>();
            Assert.That(discovery.TryResolve(UiElementAddress.UguiInstance(listedId), out GameObject? resolved, out _), Is.True);
            Assert.That(resolved, Is.EqualTo(buttonGo));
        }
    }
}
