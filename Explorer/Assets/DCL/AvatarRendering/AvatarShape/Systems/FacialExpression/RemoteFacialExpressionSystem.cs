using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.FacialExpression;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Drains <see cref="IFacialExpressionMessageBus"/> intentions and applies them to the matching
    ///     remote avatar's <see cref="AvatarFaceComponent"/>. When the participant entity isn't
    ///     known yet (avatar still loading), the intention is requeued for the next frame.
    ///     Per ADR-317 face state is last-write-wins; no timestamp/order check.
    /// </summary>
    /// <remarks>
    ///     TODO/WARNING — pending architect review. This system bypasses ECS and resolves the target
    ///     entity by walletId via <see cref="IReadOnlyEntityParticipantTable"/> instead of running a
    ///     query. Same pattern as <c>RemoteEmotesSystem</c>; performant but at odds with the
    ///     "systems are the sole entity manipulation entry point via queries" principle. Validate the
    ///     approach (or rework as a query-driven system) before merging.
    /// </remarks>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public class RemoteFacialExpressionSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IFacialExpressionMessageBus bus;
        private readonly List<RemoteFacialExpressionIntention> drained = new ();

        internal RemoteFacialExpressionSystem(World world, IReadOnlyEntityParticipantTable entityParticipantTable, IFacialExpressionMessageBus bus) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.bus = bus;
        }

        protected override void Update(float t)
        {
            bus.Drain(drained);

            if (drained.Count == 0)
                return;

            foreach (RemoteFacialExpressionIntention intention in drained)
            {
                if (!entityParticipantTable.TryGet(intention.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
                {
                    bus.SaveForRetry(intention);
                    continue;
                }

                ref AvatarFaceComponent face = ref World.TryGetRef<AvatarFaceComponent>(entry.Entity, out bool exists);

                if (!exists)
                {
                    bus.SaveForRetry(intention);
                    continue;
                }

                face.EyebrowsExpressionIndex = intention.EyebrowsIndex;
                face.EyesExpressionIndex = intention.EyesIndex;
                face.MouthExpressionIndex = intention.MouthIndex;
                face.IsDirty = true;
            }

            drained.Clear();
        }
    }
}
