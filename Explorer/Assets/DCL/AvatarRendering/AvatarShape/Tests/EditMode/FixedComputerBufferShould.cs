using DCL.AvatarRendering.AvatarShape.ComputeShader;
using JetBrains.Annotations;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    public class FixedComputerBufferShould
    {
        private FixedComputeBufferHandler bufferHandler;


        public void Setup()
        {
            bufferHandler = new FixedComputeBufferHandler(1000, 4, 4);
        }


        public void TearDown()
        {
            bufferHandler.Dispose();
        }


        public void Rent([Values(1, 10, 100, 500, 999)] int length)
        {
            FixedComputeBufferHandler.Slice slice = bufferHandler.Rent(length);
            Assert.AreEqual(length, slice.Length);
            Assert.That(bufferHandler.RentedRegions, Contains.Item(new FixedComputeBufferHandler.Slice(0, length)));
        }


        [Sequential]
        public void ThrowIfCapacityExceeded([Values(0, 100, 800)] int firstIteration, [Values(1001, 905, 231)] int secondIteration)
        {
            if (firstIteration != 0)
                bufferHandler.Rent(firstIteration);

            Assert.That(() => bufferHandler.Rent(secondIteration), Throws.TypeOf<OverflowException>());
        }


        public void PassTestScenarios([ValueSource(nameof(CreateTestScenarios))] TestScenario[] testScenarios)
        {
            for (var index = 0; index < testScenarios.Length; index++)
            {
                TestScenario testScenario = testScenarios[index];
                testScenario.Execute(bufferHandler, index);
                testScenario.AssertStep(bufferHandler);
            }
        }

        private static TestScenario[][] CreateTestScenarios()
        {
            return new[]
            {
                new TestScenario[]
                {
                    new RentScenario(100),
                    new RentScenario(200),
                    new RentScenario(50),
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(0, 100)),
                    new RentScenario(55, handler =>
                    {
                        CollectionAssert.AreEqual(new FixedComputeBufferHandler.Slice[] { new (0, 55), new (100, 200), new (300, 50) }, handler.RentedRegions);
                        CollectionAssert.AreEqual(new FixedComputeBufferHandler.Slice[] { new (55, 45), new (350, 650) }, handler.FreeRegions);
                    }),
                },
                new TestScenario[]
                {
                    new RentScenario(50),
                    new RentScenario(100),
                    new RentScenario(200),
                    new RentScenario(40),
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(50, 100)),
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(150, 200), handler =>
                    {
                        CollectionAssert.AreEqual(new FixedComputeBufferHandler.Slice[] { new (0, 50), new (350, 40) }, handler.RentedRegions);
                        CollectionAssert.AreEqual(new FixedComputeBufferHandler.Slice[] { new (50, 300), new (390, 610) }, handler.FreeRegions);
                    }), // Should Merge Regions
                },
                new TestScenario[]
                {
                    new RentScenario(1000),
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(0, 1000), handler =>
                    {
                        Assert.That(handler.RentedRegions.Count, Is.EqualTo(0));
                        CollectionAssert.AreEqual(new FixedComputeBufferHandler.Slice[] { new (0, 1000) }, handler.FreeRegions);
                    }),
                },

                // Defragmentation Threshold = 4 (3 freed region + 1 default)
                new TestScenario[]
                {
                    new RentScenario(10), // released
                    new RentScenario(20),
                    new RentScenario(30), // released
                    new RentScenario(40),
                    new RentScenario(50),
                    new RentScenario(60), // released
                    new RentScenario(70),
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(0, 10)),
                    new DefragmentationScenario(slices => Assert.That(slices.Count, Is.EqualTo(0))), // no defragmentation
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(30, 30)),
                    new DefragmentationScenario(slices => Assert.That(slices.Count, Is.EqualTo(0))), // no defragmentation
                    new ReleaseScenario(new FixedComputeBufferHandler.Slice(150, 60)),
                    new DefragmentationScenario(map =>
                    {
                        // Defragmentation should appear as there are 3 fragments
                        CollectionAssert.AreEquivalent(new Dictionary<int, FixedComputeBufferHandler.Slice>
                        {
                            { 10, new FixedComputeBufferHandler.Slice(0, 20) },
                            { 60, new FixedComputeBufferHandler.Slice(20, 40) },
                            { 100, new FixedComputeBufferHandler.Slice(60, 50) },
                            { 210, new FixedComputeBufferHandler.Slice(110, 70) },
                        }, map);
                    }, handler =>
                    {
                        // Assert Rented Regions
                        CollectionAssert.AreEquivalent(new FixedComputeBufferHandler.Slice[]
                        {
                            new (0, 20),
                            new (20, 40),
                            new (60, 50),
                            new (110, 70),
                        }, handler.RentedRegions);

                        // Assert Free Regions
                        CollectionAssert.AreEqual(new FixedComputeBufferHandler.Slice[]
                        {
                            new (180, 820),
                        }, handler.FreeRegions);
                    }),
                },
            };
        }

        public abstract class TestScenario
        {
            [CanBeNull] private readonly Action<FixedComputeBufferHandler> assertCumulative;

            protected TestScenario(Action<FixedComputeBufferHandler> assertCumulative = null)
            {
                this.assertCumulative = assertCumulative;
            }

            public abstract void Execute(FixedComputeBufferHandler fixedComputeBufferHandler, int iterationNumber);

            public void AssertStep(FixedComputeBufferHandler fixedComputeBufferHandler)
            {
                assertCumulative?.Invoke(fixedComputeBufferHandler);
            }
        }

        public class RentScenario : TestScenario
        {
            private readonly int length;

            public RentScenario(int length, Action<FixedComputeBufferHandler> assertCumulative = null) : base(assertCumulative)
            {
                this.length = length;
            }

            public override void Execute(FixedComputeBufferHandler fixedComputeBufferHandler, int iterationNumber)
            {
                FixedComputeBufferHandler.Slice slice = fixedComputeBufferHandler.Rent(length);
                Assert.That(slice.Length, Is.EqualTo(length), $"{nameof(RentScenario)} #{iterationNumber}");
            }
        }

        public class ReleaseScenario : TestScenario
        {
            private readonly FixedComputeBufferHandler.Slice slice;

            public ReleaseScenario(FixedComputeBufferHandler.Slice slice, Action<FixedComputeBufferHandler> assertCumulative = null) : base(assertCumulative)
            {
                this.slice = slice;
            }

            public override void Execute(FixedComputeBufferHandler fixedComputeBufferHandler, int iterationNumber)
            {
                fixedComputeBufferHandler.Release(slice);
                Assert.That(fixedComputeBufferHandler.RentedRegions, Does.Not.Contains(slice), $"{nameof(ReleaseScenario)} #{iterationNumber}");
            }
        }

        public class DefragmentationScenario : TestScenario
        {
            private readonly Action<IReadOnlyDictionary<int, FixedComputeBufferHandler.Slice>> assertDefragmentationMap;

            public DefragmentationScenario(
                Action<IReadOnlyDictionary<int, FixedComputeBufferHandler.Slice>> assertDefragmentationMap,
                Action<FixedComputeBufferHandler> assertCumulative = null) : base(assertCumulative)
            {
                this.assertDefragmentationMap = assertDefragmentationMap;
            }

            public override void Execute(FixedComputeBufferHandler fixedComputeBufferHandler, int iterationNumber)
            {
                IReadOnlyDictionary<int, FixedComputeBufferHandler.Slice> map = fixedComputeBufferHandler.TryMakeDefragmentation();
                assertDefragmentationMap(map);
            }
        }
    }
}
