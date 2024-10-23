using System;
using System.Text;
using UnityEngine;

namespace DCL.Profiling
{
    /// <summary>
    ///     Calculated in Nanoseconds (if not specified)
    /// </summary>
    /// Can be further optimized by taking into account ulong
    public class FrameTimesRecorder
    {
        private readonly StringBuilder stringBuilder = new ();

        private int capacity;
        private ulong[] samples;

        private ulong totalRecordedTime;

        public int SamplesAmount { get; private set; }
        public float Avg => SamplesAmount == 0 ? 0 : totalRecordedTime / (float)SamplesAmount;
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
            totalRecordedTime += frameTime;
            InsertSampleSorted(frameTime);
        }

        private void InsertSampleSorted(ulong frameTime)
        {
            if (SamplesAmount == capacity)
                GrowArray();

            int insertIndex = BinarySearchForInsertionPosition(frameTime);

            if (insertIndex < SamplesAmount)
                Array.Copy(samples, insertIndex, samples, insertIndex + 1, SamplesAmount - insertIndex);

            samples[insertIndex] = frameTime;
            SamplesAmount++;
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

        public string GetSamplesArrayAsString()
        {
            const float NS_TO_MS = 1e-6f;

            stringBuilder.Clear();
            stringBuilder.Append("[");

            for (var i = 0; i < SamplesAmount; i++)
            {
                stringBuilder.Append(Mathf.Round(samples[i] * NS_TO_MS));

                if (i < SamplesAmount - 1)
                    stringBuilder.Append(",");
            }

            stringBuilder.Append("]");

            return stringBuilder.ToString();
        }

        public void Clear()
        {
            SamplesAmount = 0;
            totalRecordedTime = 0;
        }
    }
}
