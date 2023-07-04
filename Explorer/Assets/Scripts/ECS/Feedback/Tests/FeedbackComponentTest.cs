using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Feedback.Systems;
using NSubstitute;
using NUnit.Framework;

[TestFixture]
public class FeedbackComponentShould
{
    [Test]
    public void CallPutMessageOnNewResult()
    {
        var world = World.Create();

        Entity baseEntity = world.Create(new CRDTEntity());

        IECSToCRDTWriter writer = Substitute.For<IECSToCRDTWriter>();
        var system = new WritePBPointerEventsSystem(world, writer);

        for (var i = 0; i < 100; i++)
        {
            world.Add(baseEntity, new PBPointerEventsResult());
            system.Update(0);
        }

        writer.Received(100).AppendMessage(Arg.Any<CRDTEntity>(), Arg.Any<PBPointerEventsResult>());
    }
}
