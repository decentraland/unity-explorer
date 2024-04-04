using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public class PrepareGlobalInputEventsSystem : BaseUnityLoopSystem
    {
        private readonly GlobalInputEvents globalInputEvents;
        private readonly IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap;

        internal PrepareGlobalInputEventsSystem(World world,
            GlobalInputEvents globalInputEvents,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap) : base(world)
        {
            this.globalInputEvents = globalInputEvents;
            this.sdkInputActionsMap = sdkInputActionsMap;
        }

        protected override void Update(float t)
        {
            globalInputEvents.Clear();

            foreach (KeyValuePair<InputAction, UnityEngine.InputSystem.InputAction> pair in sdkInputActionsMap)
            {
                if (pair.Value.WasPressedThisFrame())
                    globalInputEvents.Add(new IGlobalInputEvents.Entry(pair.Key, PointerEventType.PetDown));

                if (pair.Value.WasReleasedThisFrame())
                    globalInputEvents.Add(new IGlobalInputEvents.Entry(pair.Key, PointerEventType.PetUp));
            }
        }
    }
}
