using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Applies facial expression indices received over the network to remote avatar entities.
    ///     The expression is carried inside every <see cref="NetworkMovementMessage"/> (encoded in headSyncData),
    ///     so remote players automatically receive the sender's current expression with each movement packet.
    ///     When a player joins the room the first movement message they receive already contains the expression,
    ///     satisfying the "late-join" requirement without a separate handshake.
    ///     Runs after <see cref="RemotePlayersMovementSystem"/> so <see cref="RemotePlayerMovementComponent.PastMessage"/>
    ///     is up to date when this system reads it.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(RemotePlayersMovementSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemoteFacialExpressionSystem : BaseUnityLoopSystem
    {
        internal RemoteFacialExpressionSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ApplyRemoteFacialExpressionQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void ApplyRemoteFacialExpression(
            ref RemotePlayerMovementComponent remoteMovement,
            ref AvatarFaceExpressionComponent faceExpression)
        {
            if (!remoteMovement.Initialized)
                return;

            NetworkMovementMessage past = remoteMovement.PastMessage;

            if (faceExpression.EyebrowsExpressionIndex == past.eyebrowsExpressionIndex
                && faceExpression.EyesExpressionIndex == past.eyesExpressionIndex
                && faceExpression.MouthExpressionIndex == past.mouthExpressionIndex)
                return;

            faceExpression.EyebrowsExpressionIndex = past.eyebrowsExpressionIndex;
            faceExpression.EyesExpressionIndex = past.eyesExpressionIndex;
            faceExpression.MouthExpressionIndex = past.mouthExpressionIndex;
            faceExpression.IsDirty = true;
        }
    }
}
