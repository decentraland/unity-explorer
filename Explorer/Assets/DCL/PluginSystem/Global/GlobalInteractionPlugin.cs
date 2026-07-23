using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Input;
using DCL.Interaction.HoverCanvas;
using DCL.Interaction.HoverCanvas.Systems;
using DCL.Interaction.HoverCanvas.UI;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Utility;
using DCL.Utilities;
using ECS.SceneLifeCycle;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using ProcessPointerEventsSystem = DCL.Interaction.Systems.ProcessPointerEventsSystem;
using ProcessOtherAvatarsInteractionSystem = DCL.Interaction.Systems.ProcessOtherAvatarsInteractionSystem;
using PlayerOriginatedProximitySystem = DCL.Interaction.Systems.PlayerOriginatedProximitySystem;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;

namespace DCL.PluginSystem.Global
{
    public class GlobalInteractionPlugin : IDCLGlobalPlugin<GlobalInteractionPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly GlobalInputEvents globalInputEvents;
        private readonly IEventSystem eventSystem;
        private readonly IScenesCache scenesCache;
        private readonly IMVCManager mvcManager;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private readonly ObjectProxy<Entity> cameraEntityProxy;

        private HoverCanvas hoverCanvas = null!;
        private Settings settings = null!;

        public GlobalInteractionPlugin(
            IAssetsProvisioner assetsProvisioner,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            GlobalInputEvents globalInputEvents,
            IEventSystem eventSystem,
            IScenesCache scenesCache,
            IMVCManager mvcManager,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            ObjectProxy<Entity> cameraEntityProxy)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.globalInputEvents = globalInputEvents;
            this.eventSystem = eventSystem;
            this.scenesCache = scenesCache;
            this.mvcManager = mvcManager;
            this.menusAccessFacade = menusAccessFacade;
            this.cameraEntityProxy = cameraEntityProxy;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Settings pluginSettings, CancellationToken ct)
        {
            settings = pluginSettings;

            hoverCanvas = (await assetsProvisioner.ProvideInstanceAsync(pluginSettings.hoverCanvasSettings.HoverUIDocument, ct: ct)).Value.rootVisualElement.Q<HoverCanvas>();
            hoverCanvas.Initialize();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var playerInteractionEntity = new PlayerInteractionEntity(
                builder.World.Create(
                    new PlayerOriginRaycastResultForSceneEntities(),
                    new PlayerOriginRaycastResultForGlobalEntities(),
                    new HoverStateComponent(),
                    new HoverFeedbackComponent(hoverCanvas.TooltipsCount),
                    new ProximityResultForSceneEntities(),
                    new SyntheticPointerInput()),
                builder.World, arguments.PlayerEntity);

            PlayerOriginatedRaycastSystem.InjectToWorld(ref builder, entityCollidersGlobalCache, playerInteractionEntity, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE);
            PlayerOriginatedProximitySystem.InjectToWorld(ref builder, entityCollidersGlobalCache, scenesCache, playerInteractionEntity);

            DCLInput.PlayerActions playerInput = DCLInput.Instance.Player;

            // TODO How to add FORWARD/BACKWARD/LEFT/RIGHT properly?
            var actionsMap = new Dictionary<InputAction, UnityEngine.InputSystem.InputAction>
            {
                { InputAction.IaPointer, playerInput.Pointer },
                { InputAction.IaPrimary, playerInput.Primary },
                { InputAction.IaSecondary, playerInput.Secondary },
                { InputAction.IaJump, playerInput.Jump },
                { InputAction.IaForward, playerInput.ActionForward },
                { InputAction.IaBackward, playerInput.ActionBackward },
                { InputAction.IaRight, playerInput.ActionRight },
                { InputAction.IaLeft, playerInput.ActionLeft },
                { InputAction.IaAction3, playerInput.ActionButton3 },
                { InputAction.IaAction4, playerInput.ActionButton4 },
                { InputAction.IaAction5, playerInput.ActionButton5 },
                { InputAction.IaAction6, playerInput.ActionButton6 },
                { InputAction.IaAny, playerInput.Any },
                { InputAction.IaWalk, playerInput.Walk },
                { InputAction.IaModifier, playerInput.Sprint },
            };

            ProcessPointerEventsSystem.InjectToWorld(ref builder, actionsMap, entityCollidersGlobalCache, eventSystem);
            ProcessOtherAvatarsInteractionSystem.InjectToWorld(ref builder, eventSystem, menusAccessFacade, mvcManager, cameraEntityProxy);
            ShowHoverFeedbackSystem.InjectToWorld(ref builder, hoverCanvas, settings.hoverCanvasSettings.InputButtons);
            PrepareGlobalInputEventsSystem.InjectToWorld(ref builder, globalInputEvents, actionsMap);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(GlobalInteractionPlugin))]
            [field: Space]
            [field: SerializeField] internal HoverCanvasSettings hoverCanvasSettings { get; private set; }
        }
    }
}
