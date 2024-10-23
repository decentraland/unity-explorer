using System;

namespace DCL.Profiling
{
    public class ProfilingReportRecorder
    {
        private float[] samples;
        private int currentIndex;
        private int capacity;

        // Threshold for when to use Array.Copy vs manual shifting
        private const int MANUAL_SHIFT_THRESHOLD = 16;

        public ProfilingReportRecorder(int initialCapacity = 1024)
        {
            capacity = initialCapacity;
            samples = new float[initialCapacity];
            currentIndex = 0;
        }

        public void AddFrameTime(float frameTime)
        {
            if (currentIndex == capacity)
                GrowArray();

            int insertIndex = BinarySearchForInsertionPosition(frameTime);

            if (insertIndex < currentIndex)
            {
                // Array.Copy(samples, insertIndex, samples, insertIndex + 1, currentIndex - insertIndex);
                ShiftElementsRight(insertIndex, currentIndex - insertIndex);
            }

            samples[insertIndex] = frameTime;
            currentIndex++;
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
            var newArray = new float[newCapacity];

            Array.Copy(samples, newArray, currentIndex);

            samples = newArray;
            capacity = newCapacity;
        }

        private int BinarySearchForInsertionPosition(float value)
        {
            var left = 0;
            int right = currentIndex - 1;

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

        public ReadOnlySpan<float> GetSortedTimes() =>
            new (samples, 0, currentIndex);

        public void Clear() =>
            currentIndex = 0;
    }
}
