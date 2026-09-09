using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Systems.UICanvasInformation;
using ECS.Groups;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    /// <summary>
    ///     Pins the contract of PBUiCanvasInformation.DevicePixelRatio: it reports the scene panel's own
    ///     physical-pixels-per-UI-point density, republished whenever that density or the viewport moves.
    /// </summary>
    public class UICanvasInformationSystemShould : UnitySystemTestBase<UICanvasInformationSystem>
    {
        /// <summary>Panel scale the system is expected to report verbatim; deliberately not the unattached-panel fallback of 1.</summary>
        private const float SCENE_PANEL_SCALE = 2.75f;

        /// <summary>Panel scale applied mid-test to move the ratio and trip the dirty check.</summary>
        private const float RESCALED_SCENE_PANEL_SCALE = 1.5f;

        private const float RATIO_TOLERANCE = 0.001f;

        private readonly List<PBUiCanvasInformation> publishedComponents = new ();

        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private GameObject canvasGameObject = null!;
        private PanelSettings panelSettings = null!;
        private UIDocument canvas = null!;

        [SetUp]
        public void SetUp()
        {
            publishedComponents.Clear();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            // PutMessage receives a delegate that fills a rented message; run it to observe what would be written
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
                    publishedComponents.Add(component);
                });

            canvasGameObject = new GameObject(nameof(UICanvasInformationSystemShould) + "_Canvas");
            canvas = canvasGameObject.AddComponent<UIDocument>();
            AttachNewPanelSettings(SCENE_PANEL_SCALE);

            Assert.That(canvas.rootVisualElement?.panel, Is.Not.Null,
                "The scene UIDocument must be attached to a live panel, otherwise the system reports its unattached fallback instead of the panel ratio.");

            var builder = new ArchSystemsWorldBuilder<World>(world);

            // The custom group must be injected first or InjectToWorld throws GroupNotFoundException
            builder.InjectCustomGroup(new SyncedInitializationSystemGroup(Substitute.For<ISceneStateProvider>()));
            system = UICanvasInformationSystem.InjectToWorld(ref builder, ecsToCRDTWriter, canvas);

            world.Create(new SceneRootComponent());
        }

        /// <summary>Attaching a fresh PanelSettings applies the scale immediately; mutating scale on a live panel only applies during the player loop, which never runs in these synchronous tests.</summary>
        private void AttachNewPanelSettings(float scale)
        {
            PanelSettings previous = panelSettings;

            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize; // mirrors the shipped DCLScenePanelSettings.asset (m_ScaleMode: 0)
            panelSettings.scale = scale;
            canvas.panelSettings = panelSettings;

            if (previous != null)
                UnityEngine.Object.DestroyImmediate(previous);
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
            system.Initialize();

            Assert.That(publishedComponents, Is.Not.Empty, "Initialize() must publish a PBUiCanvasInformation via PutMessage.");
            Assert.That(publishedComponents[0].DevicePixelRatio, Is.EqualTo(SCENE_PANEL_SCALE).Within(RATIO_TOLERANCE));
        }

        [Test]
        public void NotRepublishWhenViewportAndPixelsPerPointAreUnchanged()
        {
            system.Initialize();
            system.Update(0);

            int publishedAfterFirstUpdate = publishedComponents.Count;
            system.Update(0);

            Assert.That(publishedComponents.Count, Is.EqualTo(publishedAfterFirstUpdate), "A tick that changes neither the viewport nor the panel ratio must not republish.");
        }

        [Test]
        public void RepublishWhenScenePanelPixelsPerPointChanges()
        {
            system.Initialize();
            system.Update(0);

            int publishedAfterFirstUpdate = publishedComponents.Count;
            AttachNewPanelSettings(RESCALED_SCENE_PANEL_SCALE);
            system.Update(0);

            Assert.That(publishedComponents.Count, Is.EqualTo(publishedAfterFirstUpdate + 1));
            Assert.That(publishedComponents[^1].DevicePixelRatio, Is.EqualTo(RESCALED_SCENE_PANEL_SCALE).Within(RATIO_TOLERANCE));
        }
    }
}
