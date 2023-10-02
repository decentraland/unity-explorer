using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Interaction.HoverCanvas;
using DCL.Interaction.HoverCanvas.Systems;
using DCL.Interaction.HoverCanvas.UI;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Utility;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using Utility.UIToolkit;

namespace DCL.PluginSystem.Global
{
    public class GlobalInteractionPlugin : IDCLGlobalPlugin<GlobalInteractionPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly UIDocument canvas;

        private readonly DCLInput dclInput;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly GlobalInputEvents globalInputEvents;

        private HoverCanvas hoverCanvas;
        private Settings settings;

        public GlobalInteractionPlugin(DCLInput dclInput,
            UIDocument canvas,
            IAssetsProvisioner assetsProvisioner,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            GlobalInputEvents globalInputEvents)
        {
            this.dclInput = dclInput;
            this.canvas = canvas;
            this.assetsProvisioner = assetsProvisioner;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.globalInputEvents = globalInputEvents;
        }

        public async UniTask Initialize(Settings settings, CancellationToken ct)
        {
            this.settings = settings;

            hoverCanvas =
                (await assetsProvisioner.ProvideMainAsset(settings.hoverCanvasSettings.HoverCanvasAsset, ct: ct))
               .Value.InstantiateForElement<HoverCanvas>();

            hoverCanvas.Initialize();
            hoverCanvas.SetDisplayed(false);
            canvas.rootVisualElement.Add(hoverCanvas);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var playerInteractionEntity = new PlayerInteractionEntity(
                builder.World.Create(new PlayerOriginRaycastResult(), new HoverStateComponent(), new HoverFeedbackComponent(hoverCanvas.TooltipsCount)),
                builder.World);

            PlayerOriginatedRaycastSystem.InjectToWorld(ref builder, dclInput.Camera.Point, entityCollidersGlobalCache,
                playerInteractionEntity, 100f);

            DCLInput.PlayerActions playerInput = dclInput.Player;

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
            };

            ProcessPointerEventsSystem.InjectToWorld(ref builder, actionsMap, entityCollidersGlobalCache);
            ShowHoverFeedbackSystem.InjectToWorld(ref builder, hoverCanvas, settings.hoverCanvasSettings.InputButtons);
            PrepareGlobalInputEventsSystem.InjectToWorld(ref builder, globalInputEvents, actionsMap);
        }

        public void Dispose() { }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(GlobalInteractionPlugin))]
            [field: Space]
            [field: SerializeField] internal HoverCanvasSettings hoverCanvasSettings { get; private set; }
        }
    }
}
