using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using System.Collections.Generic;
using ProcessPointerEventsSystem = DCL.Interaction.Systems.ProcessPointerEventsSystem;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;

namespace DCL.Interaction.PlayerOriginated.Systems
{
    /// <summary>
    ///     Collects the input actions pressed or released this frame into the buffer every scene's
    ///     WritePointerEventResultsSystem broadcasts to its root entity. Synthetic button edges posted by an
    ///     automation driver (<see cref="SyntheticPointerInput" />) are appended after the real ones, so a synthetic
    ///     press fans out exactly like a real key. Pinned between the raycast and the pointer-events processing,
    ///     because the latter consumes the synthetic post.
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

        /// <summary>The scene root is the receiver a null entity names in the post's delivery rule.</summary>
        private void AppendSyntheticEntries()
        {
            playerInteractionEntity.SyntheticPointerInput.DeliverableEdgesFor(null, null, sdkInputActionsMap,
                out InputAction? press, out InputAction? release);

            if (press is { } pressed)
                globalInputEvents.Add(new IGlobalInputEvents.Entry(pressed, PointerEventType.PetDown));

            if (release is { } released)
                globalInputEvents.Add(new IGlobalInputEvents.Entry(released, PointerEventType.PetUp));
        }
    }
}
