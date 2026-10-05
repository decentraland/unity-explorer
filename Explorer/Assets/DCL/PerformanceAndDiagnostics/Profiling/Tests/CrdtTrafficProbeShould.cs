using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.Profiling.Tests
{
    public class CrdtTrafficProbeShould
    {
        private const int ENTITY = 512;
        private const int TRANSFORM = 1;
        private const int MATERIAL = 1017;

        private CrdtTrafficProbe probe = null!;
        private List<CrdtTrafficProbe.EntrySnapshot> entries = null!;

        [SetUp]
        public void SetUp()
        {
            probe = new CrdtTrafficProbe();
            entries = new List<CrdtTrafficProbe.EntrySnapshot>();
        }

        [Test]
        public void IgnoreTrafficWhileIdle()
        {
            // Act
            probe.RecordBatch();
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, TRANSFORM, CrdtTrafficOutcome.Applied, 10);
            CrdtTrafficProbe.Snapshot snapshot = probe.StopCapture(entries);

            // Assert
            Assert.That(probe.IsCapturing, Is.False);
            Assert.That(snapshot.Batches, Is.EqualTo(0));
            Assert.That(snapshot.FromScene.Total, Is.EqualTo(0));
            Assert.That(entries, Is.Empty);
        }

        [Test]
        public void AggregatePerDirectionEntityAndComponent()
        {
            // Arrange
            probe.TryStartCapture();

            // Act
            probe.RecordBatch();
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, TRANSFORM, CrdtTrafficOutcome.Applied, 10);
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, TRANSFORM, CrdtTrafficOutcome.RedundantIdenticalData, 10);
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, MATERIAL, CrdtTrafficOutcome.NoOpSameState, 40);
            probe.Record(CrdtTrafficDirection.ToScene, ENTITY, TRANSFORM, CrdtTrafficOutcome.Applied, 5);
            CrdtTrafficProbe.Snapshot snapshot = probe.StopCapture(entries);

            // Assert
            Assert.That(snapshot.Batches, Is.EqualTo(1));
            Assert.That(snapshot.FromScene.Total, Is.EqualTo(3));
            Assert.That(snapshot.FromScene.Wasted, Is.EqualTo(2));
            Assert.That(snapshot.FromSceneBytes, Is.EqualTo(60));
            Assert.That(snapshot.ToScene.Total, Is.EqualTo(1));
            Assert.That(snapshot.ToSceneBytes, Is.EqualTo(5));
            Assert.That(entries.Count, Is.EqualTo(3));

            CrdtTrafficProbe.EntrySnapshot transform = Find(CrdtTrafficDirection.FromScene, TRANSFORM);
            Assert.That(transform.Messages, Is.EqualTo(2));
            Assert.That(transform.Bytes, Is.EqualTo(20));
            Assert.That(transform.Counts[CrdtTrafficOutcome.Applied], Is.EqualTo(1));
            Assert.That(transform.Counts[CrdtTrafficOutcome.RedundantIdenticalData], Is.EqualTo(1));
            Assert.That(Find(CrdtTrafficDirection.ToScene, TRANSFORM).Messages, Is.EqualTo(1));
        }

        [Test]
        public void RefuseASecondCaptureWhileOneRuns()
        {
            // Arrange
            Assert.That(probe.TryStartCapture(), Is.True);

            // Act & Assert
            Assert.That(probe.TryStartCapture(), Is.False);
            Assert.That(probe.IsCapturing, Is.True);
        }

        [Test]
        public void ResetBetweenCaptures()
        {
            // Arrange
            probe.TryStartCapture();
            probe.RecordBatch();
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, TRANSFORM, CrdtTrafficOutcome.Applied, 10);
            probe.StopCapture(entries);
            entries.Clear();

            // Act
            probe.TryStartCapture();
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, MATERIAL, CrdtTrafficOutcome.Applied, 7);
            CrdtTrafficProbe.Snapshot snapshot = probe.StopCapture(entries);

            // Assert
            Assert.That(snapshot.Batches, Is.EqualTo(0));
            Assert.That(snapshot.FromScene.Total, Is.EqualTo(1));
            Assert.That(snapshot.FromSceneBytes, Is.EqualTo(7));
            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].ComponentId, Is.EqualTo(MATERIAL));
        }

        [Test]
        public void StopRecordingOnceStopped()
        {
            // Arrange
            probe.TryStartCapture();
            probe.StopCapture(entries);

            // Act
            probe.Record(CrdtTrafficDirection.FromScene, ENTITY, TRANSFORM, CrdtTrafficOutcome.Applied, 10);
            CrdtTrafficProbe.Snapshot snapshot = probe.StopCapture(entries);

            // Assert
            Assert.That(snapshot.FromScene.Total, Is.EqualTo(0));
            Assert.That(entries, Is.Empty);
        }

        private CrdtTrafficProbe.EntrySnapshot Find(CrdtTrafficDirection direction, int componentId)
        {
            foreach (CrdtTrafficProbe.EntrySnapshot entry in entries)
            {
                if (entry.Direction == direction && entry.ComponentId == componentId)
                    return entry;
            }

            Assert.Fail($"No entry for {direction} component {componentId}");
            return default(CrdtTrafficProbe.EntrySnapshot);
        }
    }
}
