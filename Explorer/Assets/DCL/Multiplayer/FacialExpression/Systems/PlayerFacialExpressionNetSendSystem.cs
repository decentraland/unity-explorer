using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using ECS.Abstract;

namespace DCL.Multiplayer.FacialExpression.Systems
{
    /// <summary>
    ///     Edge-triggered facial expression send. Reads <see cref="LocalPlayerFacialExpressionComponent"/>
    ///     on the local player entity, compares against the last-sent indices cached in this system,
    ///     and emits a <c>FacialExpression</c> message only when something changed (per ADR-317).
    ///     Component is written by AvatarShape's expression input system (slot binding, debug menu, etc.).
    /// </summary>
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class PlayerFacialExpressionNetSendSystem : BaseUnityLoopSystem
    {
        // Sentinel "never sent" — first frame the component exists, indices won't match these and a send fires.
        private const byte UNSENT = byte.MaxValue;

        private readonly IFacialExpressionMessageBus bus;

        private byte lastEyebrows = UNSENT;
        private byte lastEyes = UNSENT;
        private byte lastMouth = UNSENT;

        internal PlayerFacialExpressionNetSendSystem(World world, IFacialExpressionMessageBus bus) : base(world)
        {
            this.bus = bus;
        }

        protected override void Update(float t)
        {
            SendIfChangedQuery(World);
        }

        [Query]
        private void SendIfChanged(in LocalPlayerFacialExpressionComponent face)
        {
            if (face.EyebrowsIndex == lastEyebrows
                && face.EyesIndex == lastEyes
                && face.MouthIndex == lastMouth)
                return;

            lastEyebrows = face.EyebrowsIndex;
            lastEyes = face.EyesIndex;
            lastMouth = face.MouthIndex;

            bus.Send(face.EyebrowsIndex, face.EyesIndex, face.MouthIndex);
        }
    }
}