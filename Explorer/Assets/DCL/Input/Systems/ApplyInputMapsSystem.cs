using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Input.Component;
using ECS.Abstract;
using UnityEngine.InputSystem;
using Utility;

namespace DCL.Input.Systems
{
    /// <summary>
    ///     Controls activity of input action maps based on <see cref="InputMapComponent" />
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(InputGroup))]
    public partial class ApplyInputMapsSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity inputMap;

        internal ApplyInputMapsSystem(World world) : base(world)
        {
        }

        public override void Initialize()
        {
            inputMap = World.CacheInputMap();
        }

        protected override void Update(float t)
        {
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);

            if (inputMapComponent.IsDirty)
            {
                inputMapComponent.IsDirty = false;

                for (var i = 0; i < InputMapComponent.VALUES.Count; i++)
                {
                    InputMapComponent.Kind value = InputMapComponent.VALUES[i];
                    bool isActive = EnumUtils.HasFlag(inputMapComponent.Active, value);

                    switch (value)
                    {
                        case InputMapComponent.Kind.CAMERA:
                            SetActionMapEnabled(isActive, DCLInput.Instance.Camera);
                            break;
                        case InputMapComponent.Kind.FREE_CAMERA:
                            SetActionMapEnabled(isActive, DCLInput.Instance.FreeCamera);
                            break;
                        case InputMapComponent.Kind.PLAYER:
                            SetActionMapEnabled(isActive, DCLInput.Instance.Player);
                            break;
                        case InputMapComponent.Kind.EMOTE_WHEEL:
                            SetActionMapEnabled(isActive, DCLInput.Instance.EmoteWheel);
                            break;
                        case InputMapComponent.Kind.EMOTES:
                            SetActionMapEnabled(isActive, DCLInput.Instance.Emotes);
                            break;
                        case InputMapComponent.Kind.SHORTCUTS:
                            SetActionMapEnabled(isActive, DCLInput.Instance.Shortcuts);
                            break;
                        case InputMapComponent.Kind.IN_WORLD_CAMERA:
                            SetActionMapEnabled(isActive, DCLInput.Instance.InWorldCamera);
                            break;
                    }
                }
            }
        }

        private static void SetActionMapEnabled(bool enabled, InputActionMap map)
        {
            if (map.enabled == enabled) return;

            if (enabled) map.Enable();
            else map.Disable();
        }
    }
}
