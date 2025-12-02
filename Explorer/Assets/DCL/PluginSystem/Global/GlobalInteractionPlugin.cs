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
using DCL.SocialEmotes.UI;
using DCL.Utilities;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using ProcessPointerEventsSystem = DCL.Interaction.Systems.ProcessPointerEventsSystem;
using ProcessOtherAvatarsInteractionSystem = DCL.Interaction.Systems.ProcessOtherAvatarsInteractionSystem;

namespace DCL.PluginSystem.Global
{
    public class GlobalInteractionPlugin : IDCLGlobalPlugin<GlobalInteractionPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly GlobalInputEvents globalInputEvents;
        private readonly IEventSystem eventSystem;
        private readonly IMVCManagerMenusAccessFacade menusAccessFacade;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly Entity playerEntity;

        private HoverCanvas hoverCanvas;
        private Settings settings;
        private Material hoverMaterial;
        private Material hoverOorMaterial;

        public GlobalInteractionPlugin(
            IAssetsProvisioner assetsProvisioner,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            GlobalInputEvents globalInputEvents,
            IEventSystem eventSystem,
            IMVCManagerMenusAccessFacade menusAccessFacade,
            IWeb3IdentityCache identityCache,
            ObjectProxy<Entity> cameraEntityProxy,
            Entity playerEntity)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.globalInputEvents = globalInputEvents;
            this.eventSystem = eventSystem;
            this.menusAccessFacade = menusAccessFacade;
            this.identityCache = identityCache;
            this.cameraEntityProxy = cameraEntityProxy;
            this.playerEntity = playerEntity;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            this.settings = settings;

            hoverCanvas = (await assetsProvisioner.ProvideInstanceAsync(settings.hoverCanvasSettings.HoverUIDocument, ct: ct)).Value.rootVisualElement.Q<HoverCanvas>();
            hoverCanvas.Initialize();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var playerInteractionEntity = new PlayerInteractionEntity(
                builder.World.Create(new PlayerOriginRaycastResultForSceneEntities(), new PlayerOriginRaycastResultForGlobalEntities(), new HoverStateComponent(), new HoverFeedbackComponent(hoverCanvas.TooltipsCount)),
                builder.World);

            PlayerOriginatedRaycastSystem.InjectToWorld(ref builder, DCLInput.Instance.Camera.Point, entityCollidersGlobalCache,
                playerInteractionEntity, 100f);

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
            ProcessOtherAvatarsInteractionSystem.InjectToWorld(ref builder, eventSystem, menusAccessFacade, identityCache, cameraEntityProxy, playerEntity, settings.socialEmoteOutcomesContextMenuSettings);
            ShowHoverFeedbackSystem.InjectToWorld(ref builder, hoverCanvas, settings.hoverCanvasSettings.InputButtons);
            PrepareGlobalInputEventsSystem.InjectToWorld(ref builder, globalInputEvents, actionsMap);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(GlobalInteractionPlugin))]
            [field: Space]
            [field: SerializeField] internal HoverCanvasSettings hoverCanvasSettings { get; private set; }
            [field: SerializeField] internal SocialEmoteOutcomesContextMenuSettings socialEmoteOutcomesContextMenuSettings { get; private set; }
        }
    }
}
