using System;

namespace DCL.Profiling
{
    /// <summary>
    ///     Calculated in Nanoseconds (if not specified)
    /// </summary>
    public class FrameTimesRecorder
    {
        private const int MANUAL_SHIFT_THRESHOLD = 16; // Threshold for when to use Array.Copy vs manual shifting
        private ulong[] samples;
        private int capacity;

        public ulong TotalRecordedTime { get; private set; } // [ns]
        public int SamplesAmount { get; private set; }

        public float Avg => SamplesAmount == 0 ? 0 : TotalRecordedTime / (float)SamplesAmount;

        public ulong Min => samples[0];
        public ulong Max => SamplesAmount == 0 ? samples[0] : samples[SamplesAmount - 1];

        public FrameTimesRecorder(int capacity)
        {
            this.capacity = capacity;
            samples = new ulong[capacity];
            SamplesAmount = 0;
        }

        public void AddFrameTime(ulong frameTime)
        {
            TotalRecordedTime += frameTime;
            InsertSampleSorted(frameTime);
        }

        private void InsertSampleSorted(ulong frameTime)
        {
            if (SamplesAmount == capacity)
                GrowArray();

            int insertIndex = BinarySearchForInsertionPosition(frameTime);

            if (insertIndex < SamplesAmount)
            {
                // Array.Copy(samples, insertIndex, samples, insertIndex + 1, currentIndex - insertIndex);
                ShiftElementsRight(insertIndex, SamplesAmount - insertIndex);
            }

            samples[insertIndex] = frameTime;
            SamplesAmount++;
        }

        private void ShiftElementsRight(int startIndex, int count)
        {
            if (count <= 0) return;

            if (count <= MANUAL_SHIFT_THRESHOLD) // Use manual shifting for small moves
                for (int i = startIndex + count - 1; i >= startIndex; i--)
                    samples[i + 1] = samples[i];
            else // Use Array.Copy for larger moves
                Array.Copy(samples, startIndex, samples, startIndex + 1, count);
        }

        private void GrowArray()
        {
            int newCapacity = capacity * 2;
            var newArray = new ulong[newCapacity];

            Array.Copy(samples, newArray, SamplesAmount);

            samples = newArray;
            capacity = newCapacity;
        }

        private int BinarySearchForInsertionPosition(ulong value)
        {
            var left = 0;
            int right = SamplesAmount - 1;

            while (left <= right)
            {
                int mid = left + ((right - left) / 2);

                if (samples[mid] == value)
                    return mid;

                if (samples[mid] < value)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            return left;
        }

        public ulong Percentile(float value)
        {
            if (SamplesAmount == 0)
                return 0;

            var index = (int)Math.Ceiling(value / 100f * SamplesAmount);
            index = Math.Min(index, SamplesAmount) - 1;
            return samples[index];
        }

        public ReadOnlySpan<ulong> GetSortedSamples() =>
            new (samples, 0, SamplesAmount);

        public ReadOnlySpan<float> GetSortedSamplesMs()
        {
             const float NS_TO_MS = 1e-6f;

            var msValues = new float[SamplesAmount];
            for (var i = 0; i < SamplesAmount; i++)
                msValues[i] = samples[i] * NS_TO_MS;

            return msValues;
        }

        public void Clear()
        {
            SamplesAmount = 0;
            TotalRecordedTime = 0;
        }
    }
}
