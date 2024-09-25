using DCL.Diagnostics;
using System;
using Unity.Profiling;
using Utility.Multithreading;

namespace ECS.StreamableLoading
{
    public abstract class StreamableRefCountData<TAsset> : IStreamableRefCountData where TAsset: class
    {
        public readonly struct RefAcquisition : IDisposable
        {
            private readonly StreamableRefCountData<TAsset> refCountData;

            public RefAcquisition(StreamableRefCountData<TAsset> refCountData)
            {
                this.refCountData = refCountData;
                refCountData.AddReference();
            }

            public void Dispose()
            {
                refCountData.Dereference();
            }
        }

        private readonly string reportCategory;

        internal int referenceCount { get; private set; }

        public TAsset Asset { get; }

        public long LastUsedFrame { get; private set; }

        protected abstract ref ProfilerCounterValue<int> totalCount { get; }

        protected abstract ref ProfilerCounterValue<int> referencedCount { get; }

        protected StreamableRefCountData(TAsset asset, string reportCategory = ReportCategory.STREAMABLE_LOADING)
        {
            this.reportCategory = reportCategory;
            Asset = asset;
        }

        /// <summary>
        ///     Dispose is forced when the whole cache is being disposed of
        /// </summary>
        public void Dispose(bool force)
        {
            if (!force && !CanBeDisposed()) return;

            DestroyObject();

            if (referenceCount > 0)
                referencedCount.Value--;

            totalCount.Value--;
        }

        public void Dispose()
        {
            Dispose(false);
        }

        protected abstract void DestroyObject();

        /// <summary>
        ///     Needed for non-ECS code to properly handle reference counting
        /// </summary>
        /// <returns></returns>
        public RefAcquisition AcquireRef() =>
            new (this);

        public void AddReference()
        {
            if (referenceCount == 0)
                referencedCount.Value++;

            referenceCount++;

            LastUsedFrame = MultithreadingUtility.FrameCount;
        }

        public void Dereference()
        {
            referenceCount--;

            if (referenceCount < 0)
                ReportHub.LogError(reportCategory, $"Reference count of {typeof(TAsset).Name} should never be negative!");

            LastUsedFrame = MultithreadingUtility.FrameCount;

            if (referenceCount == 0) referencedCount.Value--;
        }

        public bool CanBeDisposed() =>
            referenceCount <= 0;

        public static implicit operator TAsset?(StreamableRefCountData<TAsset>? refCountData) =>
            refCountData?.Asset;
    }
}
