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
        private readonly DCLInput dclInput;
        private SingleInstanceEntity inputMap;

        internal ApplyInputMapsSystem(World world, DCLInput dclInput) : base(world)
        {
            this.dclInput = dclInput;
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
                        case InputMapComponent.Kind.Camera:
                            SetActionMapEnabled(isActive, dclInput.Camera);
                            break;
                        case InputMapComponent.Kind.FreeCamera:
                            SetActionMapEnabled(isActive, dclInput.FreeCamera);
                            break;
                        case InputMapComponent.Kind.Player:
                            SetActionMapEnabled(isActive, dclInput.Player);
                            break;
                        case InputMapComponent.Kind.EmoteWheel:
                            SetActionMapEnabled(isActive, dclInput.EmoteWheel);
                            break;
                        case InputMapComponent.Kind.Emotes:
                            SetActionMapEnabled(isActive, dclInput.Emotes);
                            break;
                    }
                }
            }
        }

        private static void SetActionMapEnabled(bool enabled, InputActionMap map)
        {
            if (enabled)
                map.Enable();
            else
                map.Disable();
        }
    }
}
