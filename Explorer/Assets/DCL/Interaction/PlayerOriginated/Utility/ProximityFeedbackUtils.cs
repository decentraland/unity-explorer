using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;

namespace DCL.Interaction.PlayerOriginated.Utility
{
    public static class ProximityFeedbackUtils
    {
        public static void TryIssueProximityLeaveEventForPreviousEntity(
            in ProximityResultForSceneEntities proximityResultForSceneEntities,
            in GlobalColliderSceneEntityInfo previousSceneEntityInfo
        )
        {
            World world = previousSceneEntityInfo.EcsExecutor.World;

            // Entity died or PointerEvents component was removed, nothing to do
            if (!world.IsAlive(previousSceneEntityInfo.ColliderSceneEntityInfo.EntityReference) ||
                !world.TryGet(previousSceneEntityInfo.ColliderSceneEntityInfo.EntityReference, out PBPointerEvents? pbPointerEvents))
                return;

            for (var i = 0; i < pbPointerEvents!.PointerEvents.Count; i++)
            {
                PBPointerEvents.Types.Entry pointerEvent = pbPointerEvents.PointerEvents[i];
                PBPointerEvents.Types.Info info = pointerEvent.EventInfo;

                if (!InteractionInputUtils.IsQualifiedByDistance(proximityResultForSceneEntities, info)) continue;

                pbPointerEvents.AppendPointerEventResultsIntent.TryAppendEnterOrLeaveInput(PointerEventType.PetProximityLeave, pointerEvent, i);
            }
        }
    }
}
