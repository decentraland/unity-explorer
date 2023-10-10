using Diagnostics.ReportsHandling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    /// <summary>
    ///     Provides API to reuse regions of the fixed compute buffer
    /// </summary>
    public class FixedComputeBufferHandler : IEqualityComparer<FixedComputeBufferHandler.Slice>
    {
        public readonly struct Slice
        {
            public readonly int StartIndex;
            public readonly int Length;

            public Slice(int startIndex, int length)
            {
                StartIndex = startIndex;
                Length = length;
            }
        }

        /// <summary>
        ///     The number of free regions after which the fragmentation will be executed
        /// </summary>
        private const int DEFRAGMENTATION_THRESHOLD = 20;

        public readonly ComputeBuffer Buffer;

        private readonly List<Slice> freeRegions;
        private readonly HashSet<Slice> rentedRegions;

        private readonly Dictionary<int, int> rebindingMap;

        private readonly List<Slice> rentedRegionsSortedTemp;

        public FixedComputeBufferHandler(int elementsCount, int stride)
        {
            Buffer = new ComputeBuffer(elementsCount, stride);

            freeRegions = new List<Slice>(DEFRAGMENTATION_THRESHOLD) { new (0, elementsCount) };
            rentedRegions = new HashSet<Slice>(100, this);
            rentedRegionsSortedTemp = new List<Slice>(100);

            rebindingMap = new Dictionary<int, int>(DEFRAGMENTATION_THRESHOLD);
        }

        internal Slice Rent(int length)
        {
            // Search the freeRegions list for the first region that's larger than or equal to N.
            // Update the start pointer of that region by N bytes (or remove the region from freeRegions if it's exactly N bytes).
            // Add the rented region to the usedRegions list.

            // As we maintain a fixed number of free regions, the complexity is O(1), not (N)
            for (var i = 0; i < freeRegions.Count; i++)
            {
                Slice freeRegion = freeRegions[i];

                if (freeRegion.Length >= length)
                {
                    var rentedRegion = new Slice(freeRegion.StartIndex, length);
                    freeRegion = new Slice(freeRegion.StartIndex + length, freeRegion.Length - length);

                    if (freeRegion.Length == 0)
                        freeRegions.RemoveAt(i);
                    else
                        freeRegions[i] = freeRegion;

                    rentedRegions.Add(rentedRegion);
                    return rentedRegion;
                }
            }

            throw new OverflowException("Capacity of Fixed Buffer is exceeded");
        }

        public void Release(Slice slice)
        {
            // When releasing a region:
            // Remove the region from the usedRegions list.
            // Merge the region with any adjacent free regions in the freeRegions list.
            // The freeRegions list should always be kept sorted. If you merge regions, make sure the result is still sorted.

            if (!rentedRegions.Remove(slice))
            {
                ReportHub.LogError(ReportCategory.AVATAR, "Trying to release a slice that was not rented");
                return;
            }

            // Find the position for the slice in the list
            for (var i = 0; i < freeRegions.Count; i++)
            {
                Slice freeRegion = freeRegions[i];

                if (slice.StartIndex < freeRegion.StartIndex)
                {
                    freeRegions.Insert(i, slice);

                    // Try to merge with the previous region
                    MergePreviousSlice(slice, ref i);

                    // Try to merge with the next region
                    MergeNextSlice(slice, i);

                    break;
                }
            }
        }

        private void MergeNextSlice(Slice slice, int i)
        {
            if (i >= freeRegions.Count - 1) return;
            Slice nextRegion = freeRegions[i + 1];

            if (slice.StartIndex + slice.Length == nextRegion.StartIndex)
            {
                freeRegions[i] = new Slice(slice.StartIndex, slice.Length + nextRegion.Length);
                freeRegions.RemoveAt(i + 1);
            }
        }

        private void MergePreviousSlice(Slice slice, ref int i)
        {
            if (i <= 0) return;
            Slice previousRegion = freeRegions[i - 1];

            if (previousRegion.StartIndex + previousRegion.Length == slice.StartIndex)
            {
                freeRegions[i - 1] = new Slice(previousRegion.StartIndex, previousRegion.Length + slice.Length);
                freeRegions.RemoveAt(i);
                i--;
            }
        }

        /// <summary>
        ///     Returns temporary dictionary that should be consumed straight away
        /// </summary>
        /// <returns>Empty Dictionary if no defragmentation was performed</returns>
        public IReadOnlyDictionary<int, int> TryMakeDefragmentation()
        {
            rebindingMap.Clear();

            // When the number of fragmented free regions crosses a certain limit.

            if (freeRegions.Count >= DEFRAGMENTATION_THRESHOLD)
            {
                // Set a pointer writePointer to the start of the buffer.
                // For each region in usedRegions:
                // If the start of region is not equal to writePointer, it means there's a gap (fragmentation) before this region.
                // Log the current position of the region and where it needs to move in the rebindingMap.
                // Update stored region in rentedRegions with the new position.
                // Advance the writePointer by the size of the region.

                var writePointer = 0;

                // Sort in a separate collection before processing
                rentedRegionsSortedTemp.Clear();
                rentedRegionsSortedTemp.AddRange(rentedRegions);

                rentedRegionsSortedTemp.Sort(static (a, b) => a.StartIndex.CompareTo(b.StartIndex));

                foreach (Slice region in rentedRegionsSortedTemp)
                {
                    // If it's not the same, this means there's a gap (due to fragmentation) between the writePointer and the region.
                    if (region.StartIndex != writePointer)
                    {
                        // If it's not the same, this means there's a gap (due to fragmentation) between the writePointer and the region.
                        rebindingMap[region.StartIndex] = writePointer;

                        // Update the stored region
                        rentedRegions.Remove(region);
                        rentedRegions.Add(new Slice(writePointer, region.Length));

                        continue;
                    }

                    // If it's the same as writePointer, this region is already in the right place.
                    writePointer += region.Length;
                }
            }

            return rebindingMap;
        }

        bool IEqualityComparer<Slice>.Equals(Slice x, Slice y) =>
            x.StartIndex == y.StartIndex && x.Length == y.Length;

        int IEqualityComparer<Slice>.GetHashCode(Slice obj) =>
            HashCode.Combine(obj.StartIndex, obj.Length);
    }
}
