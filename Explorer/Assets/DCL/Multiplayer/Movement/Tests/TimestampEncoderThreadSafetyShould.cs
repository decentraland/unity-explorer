using DCL.Multiplayer.Movement.Settings;
using NUnit.Framework;
using UnityEditor;

namespace DCL.Multiplayer.Movement.Tests
{
    /// <summary>
    ///     <c>TimestampEncoder.Decompress</c> is a read-modify-write over <c>lastOriginalTimestamp</c>/
    ///     <c>timestampOffset</c> with no synchronization: the same code decodes to a different timestamp
    ///     depending on the encoder's prior wraparound history, and a single instance driven by more than one
    ///     message stream produces cross-contaminated output. It is not safe to share one
    ///     <c>TimestampEncoder</c> across concurrent callers — each caller needs its own instance, or callers
    ///     must be serialized onto one thread. See <c>LiveKitMovementMessageBus</c>, whose island and scene
    ///     compressed subscriptions are delivered from independent LiveKit room threads.
    /// </summary>
    [TestFixture]
    [Category("Performance")]
    public class TimestampEncoderThreadSafetyShould
    {
                private static MessageEncodingSettings settings = null!;

        private static MessageEncodingSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:MessageEncodingSettings");
                    settings = AssetDatabase.LoadAssetAtPath<MessageEncodingSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }

                return settings;
            }
        }

        private static double[] SerializedReference(int[] codes)
        {
            var enc = new TimestampEncoder(Settings);
            var outputs = new double[codes.Length];
            for (var i = 0; i < codes.Length; i++)
                outputs[i] = enc.Decompress(codes[i]);
            return outputs;
        }

        /// <summary>
        ///     Decompress is stateful: the SAME code decodes to a DIFFERENT timestamp depending on the encoder's
        ///     prior wraparound history. A pure/stateless decoder could safely be shared across threads; this one
        ///     cannot.
        /// </summary>
        [Test]
        public void DecompressIsOrderDependent_NotAPureFunction()
        {
            var enc = new TimestampEncoder(Settings);
            int steps = 1 << Settings.TIMESTAMP_BITS;

            int lowCode = steps / 10;
            int highCode = (steps * 9) / 10;

            double fresh = new TimestampEncoder(Settings).Decompress(lowCode);

            enc.Decompress(highCode);
            double afterAdvance = enc.Decompress(lowCode);

            Assert.AreNotEqual(fresh, afterAdvance,
                "TimestampEncoder.Decompress is order-dependent (mutable offset state) — it cannot be shared across concurrent threads.");
            Assert.Greater(afterAdvance, fresh,
                "The advanced encoder must apply a buffer-wraparound offset the fresh one did not.");
        }

        /// <summary>
        ///     Two message streams (island + scene) driven through ONE shared encoder cross-contaminate: the
        ///     shared encoder's per-stream outputs diverge from what a dedicated per-stream encoder produces.
        ///     Deterministic (single-threaded interleave), so the divergence does not depend on OS scheduling.
        /// </summary>
        [Test]
        public void SharedEncoderCrossContaminatesRooms_DedicatedEncoderDoesNot()
        {
            int steps = 1 << Settings.TIMESTAMP_BITS;
            const int N = 4096;

            // Decompress only trips its wraparound offset when a decode jumps BACKWARD by more than 75% of
            // the ring (WRAPAROUND_THRESHOLD). Pinning the streams 7/8 of a ring apart puts every low island
            // decode just after a high scene decode 7/8 above it — a >75% backward jump — so the shared
            // encoder trips a spurious wraparound on each island message and its outputs run away from the
            // dedicated reference, which sees only the low island codes creeping up and never trips.
            int gap = (steps * 7) / 8;

            var islandCodes = new int[N];
            var sceneCodes = new int[N];
            for (var i = 0; i < N; i++)
            {
                islandCodes[i] = i % steps;
                sceneCodes[i] = (i + gap) % steps;
            }

            double[] islandDedicated = SerializedReference(islandCodes);

            var shared = new TimestampEncoder(Settings);
            var islandFromShared = new double[N];
            for (var i = 0; i < N; i++)
            {
                islandFromShared[i] = shared.Decompress(islandCodes[i]);
                shared.Decompress(sceneCodes[i]);
            }

            var diverged = false;
            for (var i = 0; i < N; i++)
                if (islandDedicated[i] != islandFromShared[i]) { diverged = true; break; }

            Assert.IsTrue(diverged,
                "A shared TimestampEncoder driven by two interleaved streams must diverge from the dedicated-per-stream decode — proving shared mutable state.");
        }
    }
}
