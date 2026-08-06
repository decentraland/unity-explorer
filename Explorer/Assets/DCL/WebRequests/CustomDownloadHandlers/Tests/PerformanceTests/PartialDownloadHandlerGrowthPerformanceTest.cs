using NUnit.Framework;
using System;
using System.Buffers;
using Unity.PerformanceTesting;

namespace DCL.WebRequests.CustomDownloadHandlers.Tests.PerformanceTests
{
    /// <summary>
    /// Falsifies fix #18: <see cref="PartialDownloadHandler"/> must pre-size its accumulation
    /// buffer from the (partial-response) Content-Length header and grow geometrically otherwise,
    /// instead of the old arithmetic <c>Rent(len + dataLength) + full Array.Copy</c> on every
    /// ReceiveData callback (an O(N^2) rent+memcpy churn per 1MB range).
    ///
    /// UnityWebRequest delivers a 1MB range in many small callbacks (~64KB each => ~16 callbacks).
    /// A <see cref="CountingArrayPool"/> records every Rent/Return so we can assert the allocator
    /// behaviour directly:
    ///   * Content-Length present  -> EXACTLY 1 Rent, 0 regrow copies (falsified if Rent scales with callbacks).
    ///   * Content-Length absent    -> geometric fallback caps Rents at ~log2(callbacks), never one-per-callback.
    ///   * Rent count == Return count in both cases (exactly-once ownership preserved).
    ///   * Reconstructed bytes == concatenated input (guards the `bufferPointer` vs `PartialData.Length` copy length).
    ///
    /// The test drives the protected DownloadHandlerScript callbacks directly through
    /// <see cref="TestablePartialDownloadHandler"/>, so no live network is needed.
    /// </summary>
    [Category("Performance")]
    public class PartialDownloadHandlerGrowthPerformanceTest
    {
        private const int SLICE = 64 * 1024;               // typical ReceiveData chunk
        private const int CALLBACKS = 16;                  // 16 * 64KB == 1MB == PartialDownloadingRange.CHUNK_SIZE
        private const int TOTAL = SLICE * CALLBACKS;

        private const int WARMUP = 3;
        private const int MEASUREMENTS = 20;

        /// <summary>Deterministic per-slice payloads so the reconstructed buffer can be verified byte-for-byte.</summary>
        private static byte[][] BuildSlices()
        {
            var slices = new byte[CALLBACKS][];
            for (int c = 0; c < CALLBACKS; c++)
            {
                var slice = new byte[SLICE];
                for (int i = 0; i < SLICE; i++)
                    slice[i] = (byte)((c * 31 + i) & 0xFF);
                slices[c] = slice;
            }
            return slices;
        }

        private static void AssertReconstructed(TestablePartialDownloadHandler handler, byte[][] slices)
        {
            Assert.That(handler.DownloadedSize, Is.EqualTo(TOTAL), "all fed bytes must be accumulated");
            Assert.That(handler.PartialData, Is.Not.Null);
            Assert.That(handler.PartialData!.Length, Is.GreaterThanOrEqualTo(TOTAL), "buffer must hold every byte");

            int p = 0;
            for (int c = 0; c < CALLBACKS; c++)
            for (int i = 0; i < SLICE; i++, p++)
                if (handler.PartialData[p] != slices[c][i])
                    Assert.Fail($"byte {p} mismatch (chunk {c}, offset {i}): got {handler.PartialData[p]}, expected {slices[c][i]}");
        }

        // ───── Correctness + allocator-shape falsifiers ─────────────────────────────

        [Test, Performance]
        public void ContentLengthPresent_SingleRent_NoRegrow_BytesIntact()
        {
            byte[][] slices = BuildSlices();

            var pool = new CountingArrayPool();
            var handler = new TestablePartialDownloadHandler(new byte[SLICE], pool);

            handler.CallReceiveContentLengthHeader((ulong)TOTAL);
            foreach (byte[] s in slices)
                Assert.That(handler.CallReceiveData(s, s.Length), Is.True);

            // Headline falsifier: pre-sizing collapses the whole range to a single rent.
            Assert.That(pool.RentCalls, Is.EqualTo(1),
                "Content-Length present must pre-size in exactly one Rent (0 regrow copies)");
            AssertReconstructed(handler, slices);

            // Consumer (PartialDownloadOp -> PartialDownloadSystemBase.finally) returns the final buffer exactly once.
            pool.Return(handler.PartialData!, true);
            Assert.That(pool.ReturnCalls, Is.EqualTo(pool.RentCalls), "Rent/Return must balance exactly-once");

            // Perf sample of the metric of interest: rent calls per 1MB range.
            var rentGroup = new SampleGroup("PartialDownload.RentCalls.ContentLength", SampleUnit.Undefined);
            for (int m = 0; m < WARMUP + MEASUREMENTS; m++)
            {
                var p = new CountingArrayPool();
                var h = new TestablePartialDownloadHandler(new byte[SLICE], p);
                h.CallReceiveContentLengthHeader((ulong)TOTAL);
                foreach (byte[] s in slices) h.CallReceiveData(s, s.Length);
                if (m >= WARMUP) Measure.Custom(rentGroup, p.RentCalls);
                p.Return(h.PartialData!, true);
            }
        }

        [Test, Performance]
        public void ContentLengthAbsent_GeometricGrowth_NeverOneRentPerCallback()
        {
            byte[][] slices = BuildSlices();

            var pool = new CountingArrayPool();
            var handler = new TestablePartialDownloadHandler(new byte[SLICE], pool);

            // No ReceiveContentLengthHeader call -> exercises the geometric fallback in ReceiveData.
            foreach (byte[] s in slices)
                Assert.That(handler.CallReceiveData(s, s.Length), Is.True);

            // Old arithmetic path rented once per callback (== CALLBACKS). Geometric growth is ~log2(N).
            Assert.That(pool.RentCalls, Is.LessThan(CALLBACKS),
                "geometric growth must not rent once-per-callback");
            Assert.That(pool.RentCalls, Is.LessThanOrEqualTo(6),
                "geometric growth from a 64KB seed over 1MB caps at ~log2(16)+1 rents");
            AssertReconstructed(handler, slices);

            // Every regrow returns the old buffer; consumer returns the final one -> perfectly balanced.
            pool.Return(handler.PartialData!, true);
            Assert.That(pool.ReturnCalls, Is.EqualTo(pool.RentCalls), "Rent/Return must balance exactly-once");
        }

        // ───── Wall-clock: full 1MB fill, Content-Length present vs absent ──────────

        [Test, Performance]
        public void Fill1MB_ContentLengthPresent_Time()
        {
            byte[][] slices = BuildSlices();

            Measure.Method(() =>
                    {
                        var pool = ArrayPool<byte>.Shared;
                        var h = new TestablePartialDownloadHandler(new byte[SLICE], pool);
                        h.CallReceiveContentLengthHeader((ulong)TOTAL);
                        foreach (byte[] s in slices) h.CallReceiveData(s, s.Length);
                        pool.Return(h.PartialData!, true);
                    })
               .WarmupCount(WARMUP).MeasurementCount(MEASUREMENTS).GC().Run();
        }

        // ───── Test doubles ─────────────────────────────────────────────────────────

        /// <summary>Exposes the protected DownloadHandlerScript callbacks so a test can feed them directly.</summary>
        private sealed class TestablePartialDownloadHandler : PartialDownloadHandler
        {
            public TestablePartialDownloadHandler(byte[] preallocatedBuffer, ArrayPool<byte> buffersPool)
                : base(preallocatedBuffer, buffersPool) { }

            public void CallReceiveContentLengthHeader(ulong contentLength) => ReceiveContentLengthHeader(contentLength);

            public bool CallReceiveData(byte[] receivedData, int dataLength) => ReceiveData(receivedData, dataLength);
        }

        /// <summary>Delegates to <see cref="ArrayPool{T}.Shared"/> while counting Rent/Return calls.</summary>
        private sealed class CountingArrayPool : ArrayPool<byte>
        {
            private readonly ArrayPool<byte> inner = Shared;

            public int RentCalls { get; private set; }
            public int ReturnCalls { get; private set; }

            public override byte[] Rent(int minimumLength)
            {
                RentCalls++;
                return inner.Rent(minimumLength);
            }

            public override void Return(byte[] array, bool clearArray = false)
            {
                ReturnCalls++;
                inner.Return(array, clearArray);
            }
        }
    }
}
