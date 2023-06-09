using Arch.Core;
using Arch.SystemGroups.Metadata;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using System;
using System.Runtime.CompilerServices;

namespace ECS.Feedback.Systems
{
    public class WritePBPointerEventsSystem : BaseUnityLoopSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<CRDTEntity, PBPointerEventsResult>();
        private WriteFeedbackToCrdt writeFeedbackToCrdt;

        public WritePBPointerEventsSystem(World world, IECSToCRDTWriter componentWriter) : base(world)
        {
            writeFeedbackToCrdt = new WriteFeedbackToCrdt(world, componentWriter);
        }

        protected override void Update(float _)
        {
            World.InlineEntityQuery<WriteFeedbackToCrdt, CRDTEntity, PBPointerEventsResult>(in queryDescription, ref writeFeedbackToCrdt);
        }

        private readonly struct WriteFeedbackToCrdt : IForEachWithEntity<CRDTEntity, PBPointerEventsResult>
        {
            private readonly IECSToCRDTWriter writer;
            private readonly World world;

            public WriteFeedbackToCrdt(World world, IECSToCRDTWriter writer)
            {
                this.writer = writer;
                this.world = world;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Update(in Entity entity, ref CRDTEntity crdtEntity, ref PBPointerEventsResult pbResult)
            {
                writer.AppendMessage(crdtEntity, pbResult);
                world.Remove(entity, typeof(PBPointerEventsResult));
            }
        }

        protected override AttributesInfoBase GetMetadataInternal() =>
            throw new NotImplementedException();
    }
}
