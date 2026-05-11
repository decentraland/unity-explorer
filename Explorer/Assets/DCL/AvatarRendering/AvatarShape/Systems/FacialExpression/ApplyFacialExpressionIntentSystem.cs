using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Multiplayer.Movement;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Consumes <see cref="TriggerFacialExpressionIntent"/> on the local player and applies the
    ///     three atlas indices to <see cref="AvatarFaceComponent"/> (drives rendering) and
    ///     <see cref="LocalPlayerFacialExpressionComponent"/> (drives the network send). The system
    ///     never sees the expression table — callers (wheel controller, keyboard adapter) translate
    ///     slot → indices upstream so the data shape mirrors the rfc4 payload.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class ApplyFacialExpressionIntentSystem : BaseUnityLoopSystem
    {
        internal ApplyFacialExpressionIntentSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SetupNetworkExpressionComponentQuery(World);
            ApplyIntentQuery(World);
        }

        /// <summary>
        ///     Adds <see cref="LocalPlayerFacialExpressionComponent"/> to the player entity as soon as
        ///     <see cref="AvatarFaceComponent"/> is available. Runs at most once per player thanks to
        ///     the [None] filter.
        /// </summary>
        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceComponent))]
        [None(typeof(LocalPlayerFacialExpressionComponent), typeof(DeleteEntityIntention))]
        private void SetupNetworkExpressionComponent(in Entity entity)
        {
            World.Add(entity, new LocalPlayerFacialExpressionComponent());
        }

        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyIntent(
            in Entity entity,
            ref AvatarFaceComponent face,
            ref LocalPlayerFacialExpressionComponent network,
            ref TriggerFacialExpressionIntent intent)
        {
            face.EyebrowsExpressionIndex = intent.EyebrowsIndex;
            face.EyesExpressionIndex = intent.EyesIndex;
            face.MouthExpressionIndex = intent.MouthIndex;
            face.IsDirty = true;

            network.EyebrowsIndex = intent.EyebrowsIndex;
            network.EyesIndex = intent.EyesIndex;
            network.MouthIndex = intent.MouthIndex;

            World.Remove<TriggerFacialExpressionIntent>(entity);
        }
    }
}