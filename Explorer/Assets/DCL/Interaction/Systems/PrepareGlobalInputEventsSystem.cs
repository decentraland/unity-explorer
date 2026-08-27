using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using ECS.Abstract;
using System.Collections.Generic;
using ProcessPointerEventsSystem = DCL.Interaction.Systems.ProcessPointerEventsSystem;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    /// <summary>
    ///     Collects the input actions pressed or released this frame into the buffer every scene's
    ///     WritePointerEventResultsSystem broadcasts to its root entity. Synthetic button edges posted by an
    ///     automation driver (<see cref="SyntheticPointerInput" />) are appended after the real ones, so a
    ///     synthetic press fans out to the scene exactly like a real key; the system is pinned between the
    ///     raycast and the pointer-events processing because the latter consumes the synthetic post.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerOriginatedRaycastSystem))]
    [UpdateBefore(typeof(ProcessPointerEventsSystem))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class PrepareGlobalInputEventsSystem : BaseUnityLoopSystem
    {
        private readonly GlobalInputEvents globalInputEvents;
        private readonly IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap;
        private readonly PlayerInteractionEntity playerInteractionEntity;

        internal PrepareGlobalInputEventsSystem(World world,
            GlobalInputEvents globalInputEvents,
            IReadOnlyDictionary<InputAction, UnityEngine.InputSystem.InputAction> sdkInputActionsMap,
            PlayerInteractionEntity playerInteractionEntity) : base(world)
        {
            this.globalInputEvents = globalInputEvents;
            this.sdkInputActionsMap = sdkInputActionsMap;
            this.playerInteractionEntity = playerInteractionEntity;
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

            AppendSyntheticEntries();
        }

        /// <summary>
        ///     A synthetic edge of an action the player really pressed or released this same frame is skipped:
        ///     the real loop above already added it, and the scene must not observe the event twice.
        /// </summary>
        private void AppendSyntheticEntries()
        {
            SyntheticPointerInput synthetic = playerInteractionEntity.SyntheticPointerInput;

            if (!synthetic.IsPostedThisFrame)
                return;

            if (synthetic.PressButton is { } pressed && !WasReallyPressedThisFrame(pressed))
                globalInputEvents.Add(new IGlobalInputEvents.Entry(pressed, PointerEventType.PetDown));

            if (synthetic.ReleaseButton is { } released && !WasReallyReleasedThisFrame(released))
                globalInputEvents.Add(new IGlobalInputEvents.Entry(released, PointerEventType.PetUp));
        }

        private bool WasReallyPressedThisFrame(InputAction action) =>
            sdkInputActionsMap.TryGetValue(action, out UnityEngine.InputSystem.InputAction? unityAction) && unityAction!.WasPressedThisFrame();

        private bool WasReallyReleasedThisFrame(InputAction action) =>
            sdkInputActionsMap.TryGetValue(action, out UnityEngine.InputSystem.InputAction? unityAction) && unityAction!.WasReleasedThisFrame();
    }
}
