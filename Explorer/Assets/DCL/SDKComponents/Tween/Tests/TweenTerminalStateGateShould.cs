using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.Tween.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using Utility.Primitives;
using Entity = Arch.Core.Entity;
using Vector3 = Decentraland.Common.Vector3;
using Vector2 = Decentraland.Common.Vector2;

namespace DCL.SDKComponents.Tween.Tests
{
    /// <summary>
    /// Guards the terminal-state gate on the NON-DIRTY update path of the base tween system. Once a
    /// non-looping tween has reported TsCompleted, GetTweenerState (two virtual ITweener dispatches) is
    /// dead work: it can only re-return TsCompleted and the else-if(TsActive) body is skipped either way.
    /// The gate short-circuits it.
    /// </summary>
    [TestFixture]
    public class TweenTerminalStateGateShould : UnitySystemTestBase<TweenUpdaterSystem>
    {
        private const int FRAMES = 100;

        [SetUp]
        public void SetUp()
        {
            var sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            system = new TweenUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(), new TweenerPool(), sceneStateProvider);
        }

        [TearDown]
        public void TearDown() =>
            system?.Dispose();

        [Test]
        public void NotQueryCompletedTransformTweenerStateOnNonDirtyFrames()
        {
            ITweener tweener = Substitute.For<ITweener>();
            tweener.IsFinished().Returns(true); // completed, non-looping

            Entity entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity,
                new CRDTEntity(1),
                new PBTween { Duration = 500, IsDirty = false, Move = new Move { Start = Vec3(0, 0, 0), End = Vec3(1, 0, 0) } },
                new MaterialComponent { Result = DefaultMaterial.New() },
                new SDKTweenComponent
                {
                    IsDirty = false,
                    TweenStateStatus = TweenStateStatus.TsCompleted,
                    TweenMode = PBTween.ModeOneofCase.Move,
                    CustomTweener = tweener,
                });

            tweener.ClearReceivedCalls();

            for (int i = 0; i < FRAMES; i++)
                system!.Update(0.016f);

            tweener.DidNotReceive().IsFinished();
            tweener.DidNotReceive().IsPaused();
        }

        [Test]
        public void NotQueryCompletedTextureTweenerStateOnNonDirtyFrames()
        {
            ITweener tweener = Substitute.For<ITweener>();
            tweener.IsFinished().Returns(true);

            Entity entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity,
                new CRDTEntity(2),
                new PBTween
                {
                    Duration = 500, IsDirty = false,
                    TextureMove = new TextureMove { Start = Vec2(0, 0), End = Vec2(1, 0), MovementType = TextureMovementType.TmtOffset },
                },
                new MaterialComponent { Result = DefaultMaterial.New() },
                new SDKTweenComponent
                {
                    IsDirty = false,
                    TweenStateStatus = TweenStateStatus.TsCompleted,
                    TweenMode = PBTween.ModeOneofCase.TextureMove,
                    CustomTweener = tweener,
                });

            tweener.ClearReceivedCalls();

            for (int i = 0; i < FRAMES; i++)
                system!.Update(0.016f);

            tweener.DidNotReceive().IsFinished();
            tweener.DidNotReceive().IsPaused();
        }

        private static Vector3 Vec3(float x, float y, float z) =>
            new () { X = x, Y = y, Z = z };

        private static Vector2 Vec2(float x, float y) =>
            new () { X = x, Y = y };
    }

    /// <summary>
    /// Same terminal-state gate, applied to the sequence system's non-dirty branch
    /// (UpdateTweenSequenceStateIfChanged).
    /// </summary>
    [TestFixture]
    public class TweenSequenceTerminalStateGateShould : UnitySystemTestBase<TweenSequenceUpdaterSystem>
    {
        private const int FRAMES = 100;

        [SetUp]
        public void SetUp()
        {
            var sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);
            system = new TweenSequenceUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(), new TweenerPool(), sceneStateProvider);
        }

        [TearDown]
        public void TearDown() =>
            system?.Dispose();

        [Test]
        public void NotQueryCompletedSequenceTweenerStateOnNonDirtyFrames()
        {
            ITweener tweener = Substitute.For<ITweener>();
            tweener.IsFinished().Returns(true);

            Entity entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
            world.Add(entity,
                new CRDTEntity(1),
                new PBTween { Duration = 500, IsDirty = false, Move = new Move { Start = Vec3(0, 0, 0), End = Vec3(1, 0, 0) } },
                new PBTweenSequence { IsDirty = false },
                new SDKTweenSequenceComponent
                {
                    IsDirty = false,
                    HasTransformTweens = false,
                    TweenStateStatus = TweenStateStatus.TsCompleted,
                    SequenceTweener = tweener,
                });

            tweener.ClearReceivedCalls();

            for (int i = 0; i < FRAMES; i++)
                system!.Update(0.016f);

            tweener.DidNotReceive().IsFinished();
            tweener.DidNotReceive().IsPaused();
        }

        private static Vector3 Vec3(float x, float y, float z) =>
            new () { X = x, Y = y, Z = z };
    }
}
