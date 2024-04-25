using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace Utility
{
    /// <summary>
    ///     Helper to calculate parcels in a ring around a center parcel
    ///     <para>Uses Unity jobs and Burst to parallelize calculations, thus requires native collections and Mathematics primitives</para>
    /// </summary>
    public class ParcelMathJobifiedHelper : IDisposable
    {
        /// <summary>
        /// Beyond that value calculations will be too slow and too much memory will be required
        /// </summary>
        public const int RADIUS_HARD_LIMIT = 100;

        private CalculateRingJob calculateRingJob;
        private JobHandle jobHandle;

        /// <summary>
        ///     Flattened rings, starting from the inner
        /// </summary>
        private NativeArray<ParcelInfo> rings;

        public bool JobStarted { get; private set; }

        public ref NativeArray<ParcelInfo> LastSplit => ref rings;

        private void EnsureRingsArraySize(int maxRadius)
        {
            int ringsArraySize = GetRingsArraySize(maxRadius);

            if (rings.Length != ringsArraySize)
            {
                rings.Dispose();
                rings = new NativeArray<ParcelInfo>(ringsArraySize, Allocator.Persistent);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRingsArraySize(int maxRadius)
        {
            int d = (maxRadius * 2) + 1;
            return d * d;
        }

        /// <summary>
        ///     Schedule a job so it can be completed later
        /// </summary>
        public void StartParcelsRingSplit(int2 centerParcel, int maxRadius, NativeHashSet<int2> processedParcels)
        {
            Assert.IsTrue(jobHandle.IsCompleted, "Can't start several jobs at the same time");

            EnsureRingsArraySize(maxRadius);

            // We can move inner circle to this thread to not schedule them, e.g. < 4
            rings[0] = new ParcelInfo
                { AlreadyProcessed = processedParcels.Contains(centerParcel), Parcel = centerParcel };

            calculateRingJob = new CalculateRingJob
            {
                Center = centerParcel,
                Rings = rings,
                ProcessedParcels = processedParcels,
            };

            jobHandle = calculateRingJob.Schedule(maxRadius, 2);
            JobStarted = true;
        }

        public ref readonly NativeArray<ParcelInfo> FinishParcelsRingSplit()
        {
            jobHandle.Complete();
            JobStarted = false;
            return ref rings;
        }

        public NativeSlice<ParcelInfo> GetRing(int radius) =>
            radius == 0
                ? rings.Slice(0, 1)
                : rings.Slice((4 * (radius - 1) * radius) + 1, 8 * radius);

        /// <summary>
        ///     Graceful complete if the job started
        /// </summary>
        public void Complete()
        {
            if (JobStarted)
                jobHandle.Complete();

            JobStarted = false;
        }

        public struct ParcelInfo
        {
            public int2 Parcel;

            /// <summary>
            ///     Whether this parcel was already retrieved,
            ///     we calculate it in parallel jobs
            /// </summary>
            public bool AlreadyProcessed;

            public float RingSqrDistance;
        }

        [BurstCompile]
        public struct CalculateRingJob : IJobParallelFor
        {
            public int2 Center;

            [NativeDisableParallelForRestriction]
            [WriteOnly]
            public NativeArray<ParcelInfo> Rings;

            [ReadOnly]
            public NativeHashSet<int2> ProcessedParcels;

            public void Execute(int ringLevel)
            {
                // Find start index
                int index = (4 * ringLevel * (ringLevel + 1)) + 1; // + 1 stands for 0 radius

                ringLevel++;
                float ringSqrDistance = ringLevel * ParcelMathHelper.PARCEL_SIZE * (ringLevel * ParcelMathHelper.PARCEL_SIZE);

                for (int i = -ringLevel; i <= ringLevel; i++)
                {
                    int minX = Center.x - ringLevel;
                    int maxX = Center.x + ringLevel;

                    if (i == -ringLevel || i == ringLevel)
                    {
                        for (int j = minX; j <= maxX; j++)
                        {
                            var parcel = new int2(j, Center.y + i);

                            Rings[index] = new ParcelInfo
                            {
                                AlreadyProcessed = ProcessedParcels.Contains(parcel), Parcel = parcel,
                                RingSqrDistance = ringSqrDistance,
                            };

                            index++;
                        }
                    }
                    else
                    {
                        var parcel = new int2(minX, Center.y + i);

                        Rings[index] = new ParcelInfo
                        {
                            AlreadyProcessed = ProcessedParcels.Contains(parcel), Parcel = parcel,
                            RingSqrDistance = ringSqrDistance,
                        };

                        index++;
                        parcel = new int2(maxX, Center.y + i);

                        Rings[index] = new ParcelInfo
                        {
                            AlreadyProcessed = ProcessedParcels.Contains(parcel), Parcel = parcel,
                            RingSqrDistance = ringSqrDistance,
                        };

                        index++;
                    }
                }
            }
        }

        public void Dispose()
        {
            rings.Dispose();
        }
    }
}
