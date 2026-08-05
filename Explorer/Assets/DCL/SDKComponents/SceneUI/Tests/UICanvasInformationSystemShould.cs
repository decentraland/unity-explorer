using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Systems.UICanvasInformation;
using ECS.Groups;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    // Regression coverage for the dpr-vh-vw-positionunit bug: UICanvasInformationSystem used to
    // publish DevicePixelRatio = Screen.mainWindowDisplayInfo.width / Screen.width (the
    // monitor-native-width / window-client-width ratio). That quantity is not a device pixel
    // ratio for unity-explorer's scene UI - the scene panel (DCLScenePanelSettings) is
    // ConstantPixelSize @ scale 1, so 1 UI-Toolkit point == 1 framebuffer pixel regardless of
    // which monitor hosts the window or its native resolution. Scenes derive vh/vw (and, since
    // js-sdk-toolchain #1433, every virtual-screen-scaled px value and scaleFontSize) from this
    // field via `points = (dim / 100) * (scale / devicePixelRatio)`, so the bogus ratio silently
    // mis-sizes `vw`/`vh` elements and live-resizes them when the window crosses monitors.
    //
    // The fix plumbs the scene UIDocument into the system and reports the panel's own
    // `scaledPixelsPerPoint` (1.0f fallback while unattached) instead of the monitor/window
    // ratio - see report.md "## Patch" and potential-fix.patch in
    // bugreports-late-jul/dpr-vh-vw-positionunit/.
    public class UICanvasInformationSystemShould : UnitySystemTestBase<UICanvasInformationSystem>
    {
        // Deliberately NOT 1.0 (the system's own unattached-panel fallback) and NOT a common OS
        // DPI preset (100/125/150/175/200/225/250/300/350%): this isolates the injected-UIDocument
        // seam from the OLD formula's monitorWidth/windowWidth ratio. Screen and
        // Screen.mainWindowDisplayInfo are static Unity APIs with no injectable abstraction - the
        // ROBUST patch variant is testable only because it additionally plumbs a UIDocument, which
        // IS injectable. The OLD formula is a ratio of two real screen-pixel widths in the test
        // runner's own window/monitor; landing on exactly 2.75 by coincidence is not realistic.
        private const float SCENE_PANEL_SCALE = 2.75f;

        private IECSToCRDTWriter ecsToCRDTWriter;
        private GameObject canvasGameObject;
        private PanelSettings panelSettings;
        private UIDocument canvas;
        private PBUiCanvasInformation? capturedComponent;

        [SetUp]
        public void SetUp()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            capturedComponent = null;

            // UICanvasInformationSystem.WriteToCRDT() rents the message from the writer and
            // populates it via a static delegate - capture that delegate and invoke it against a
            // fresh instance to observe what would have been written, exactly as the real
            // IECSToCRDTWriter implementation would.
            ecsToCRDTWriter
               .When(x => x.PutMessage(
                    Arg.Any<Action<PBUiCanvasInformation, UICanvasInformationSystem>>(),
                    Arg.Any<CRDTEntity>(),
                    Arg.Any<UICanvasInformationSystem>()))
               .Do(callInfo =>
                {
                    var prepareMessage = callInfo.Arg<Action<PBUiCanvasInformation, UICanvasInformationSystem>>();
                    var data = callInfo.Arg<UICanvasInformationSystem>();
                    var component = new PBUiCanvasInformation();
                    prepareMessage(component, data);
                    capturedComponent = component;
                });

            // ConstantPixelSize mirrors the shipped DCLScenePanelSettings.asset (m_ScaleMode: 0)
            // but with a scale the OLD Screen-ratio formula cannot plausibly reproduce.
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = SCENE_PANEL_SCALE;

            canvasGameObject = new GameObject(nameof(UICanvasInformationSystemShould) + "_Canvas");
            canvas = canvasGameObject.AddComponent<UIDocument>();
            canvas.panelSettings = panelSettings;

            var builder = new ArchSystemsWorldBuilder<World>(world);

            // UICanvasInformationSystem is [UpdateInGroup(typeof(SyncedInitializationSystemGroup))] -
            // a CUSTOM group, so it must be injected into the builder before the system is, exactly as
            // production does in ECSWorldFactory.cs (InjectCustomGroup(new SyncedInitializationSystemGroup(...))).
            // Otherwise InjectToWorld throws Arch.SystemGroups.GroupNotFoundException in SetUp.
            builder.InjectCustomGroup(new SyncedInitializationSystemGroup(Substitute.For<ISceneStateProvider>()));
            system = UICanvasInformationSystem.InjectToWorld(ref builder, ecsToCRDTWriter, canvas);
        }

        protected override void OnTearDown()
        {
            if (canvasGameObject != null)
                UnityEngine.Object.DestroyImmediate(canvasGameObject);

            if (panelSettings != null)
                UnityEngine.Object.DestroyImmediate(panelSettings);
        }

        [Test]
        public void ReportScenePanelPixelsPerPointAsDevicePixelRatio()
        {
            // Arrange-time invariant: the seam only exercises the fixed code path once the
            // UIDocument is actually attached to a live panel.
            Assert.That(canvas.rootVisualElement, Is.Not.Null);
            Assert.That(canvas.rootVisualElement.panel, Is.Not.Null);

            // Act - Initialize() publishes unconditionally (bypasses the dirty-check).
            system.Initialize();

            // Assert
            Assert.That(capturedComponent, Is.Not.Null, "UICanvasInformationSystem.Initialize() must publish a PBUiCanvasInformation via PutMessage.");

            // Pre-fix: DevicePixelRatio = Screen.mainWindowDisplayInfo.width / Screen.width (the
            // monitor/window ratio) - unrelated to SCENE_PANEL_SCALE, so this fails.
            // Post-fix: DevicePixelRatio = canvas.rootVisualElement.scaledPixelsPerPoint, i.e.
            // exactly the configured panel scale, so this passes.
            Assert.That(capturedComponent!.DevicePixelRatio, Is.EqualTo(SCENE_PANEL_SCALE).Within(0.001f));
        }
    }
}
