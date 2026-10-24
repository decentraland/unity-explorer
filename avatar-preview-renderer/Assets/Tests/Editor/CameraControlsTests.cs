using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Preview.Tests
{
    public class CameraControlsTests
    {
        private Scene testScene;
        private Component bridge;
        private Transform cameraTransform;

        [SetUp]
        public void SetUp()
        {
            testScene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Additive);
            bridge = FindBridge();
            var controller = SerializedReference(bridge, "previewController");
            var cameraController = SerializedReference(controller, "previewCameraController");
            cameraTransform = SerializedReference(cameraController, "marketplaceAvatarCamera").transform;
            Call(cameraController, "Awake");
            Call(bridge, "SetMode", "marketplace");
        }

        [TearDown]
        public void TearDown()
        {
            if (testScene.IsValid()) EditorSceneManager.CloseScene(testScene, true);
        }

        [Test]
        public void ShouldMoveAndRepeatCameraAnglesThroughTheWebBridge()
        {
            var initialPosition = cameraTransform.position;
            Call(bridge, "SetCameraPosition", "1.5707963,-0.5235988,0");
            var requestedPosition = cameraTransform.position;
            var requestedRotation = cameraTransform.rotation;
            Assert.That(Vector3.Distance(initialPosition, requestedPosition), Is.GreaterThan(1f));

            Call(bridge, "SetCameraPosition", "-1.5707963,0.5235988,0");
            Call(bridge, "SetCameraPosition", "1.5707963,-0.5235988,0");
            Assert.That(Vector3.Distance(requestedPosition, cameraTransform.position), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(requestedRotation, cameraTransform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void ShouldSetAnAbsoluteTargetAndReverseZoomInAnyLocale()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Call(bridge, "SetOffset", "0,0,0");
                var initialPosition = cameraTransform.position;
                Call(bridge, "SetOffset", "0.25,0,0");
                var pannedPosition = cameraTransform.position;
                Assert.That(Vector3.Distance(initialPosition, pannedPosition), Is.EqualTo(0.25f).Within(0.00001f));

                Call(bridge, "SetOffset", "0.25,0,0");
                Assert.That(cameraTransform.position, Is.EqualTo(pannedPosition));
                var target = new Vector3(0.25f, 0f, 0f);
                var initialDistance = Vector3.Distance(pannedPosition, target);
                Call(bridge, "SetZoom", "0.5");
                Assert.That(Vector3.Distance(cameraTransform.position, target), Is.EqualTo(initialDistance - 0.5f).Within(0.00001f));
                Call(bridge, "SetZoom", "-0.5");
                Assert.That(Vector3.Distance(pannedPosition, cameraTransform.position), Is.LessThan(0.00001f));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        private static void Call(Component target, string method, params object[] arguments)
        {
            var entry = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException($"Missing web bridge method: {method}");
            entry.Invoke(target, arguments);
        }

        private Component FindBridge()
        {
            foreach (var root in testScene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Name == "JSBridge") return component;

            throw new InvalidOperationException("The preview scene must contain JSBridge.");
        }

        // Resolve scene references without depending on the predefined Assembly-CSharp.
        private static Component SerializedReference(Component owner, string property)
        {
            using var serialized = new SerializedObject(owner);
            if (serialized.FindProperty(property).objectReferenceValue is not Component component)
                throw new InvalidOperationException($"Missing scene reference: {property}");
            return component;
        }
    }
}
