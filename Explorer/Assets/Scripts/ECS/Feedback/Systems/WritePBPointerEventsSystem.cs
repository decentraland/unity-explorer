using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECS7;
using ECS.Abstract;
using System.Runtime.CompilerServices;

public class WritePBPointerEventsSystem : BaseUnityLoopSystem
{
    private readonly QueryDescription queryDescription = new QueryDescription().WithAll<CRDTEntity, PointerEventsResult>();
    private WriteFeedbackToCrdt writeFeedbackToCrdt;

    public WritePBPointerEventsSystem(World world, IECSToCRDTWriter componentWriter) : base(world)
    {
        writeFeedbackToCrdt = new WriteFeedbackToCrdt(world, componentWriter);
    }

    protected override void Update(float _)
    {
        World.InlineEntityQuery<WriteFeedbackToCrdt, CRDTEntity, PointerEventsResult>(in queryDescription, ref writeFeedbackToCrdt);
    }

    private readonly struct WriteFeedbackToCrdt : IForEachWithEntity<CRDTEntity, PointerEventsResult>
    {
        private readonly IECSToCRDTWriter writer;
        private readonly World world;

        public WriteFeedbackToCrdt(World world, IECSToCRDTWriter writer)
        {
            this.writer = writer;
            this.world = world;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(in Entity entity, ref CRDTEntity crdtEntity, ref PointerEventsResult feedback)
        {
            writer.AppendMessage(crdtEntity, ComponentID.POINTER_EVENTS_RESULT, feedback.pbResult);
            world.Remove(entity, typeof(PointerEventsResult));
        }
    }
}
