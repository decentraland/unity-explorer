using System;

namespace DCL.WebRequests.WebContentSizes.Sizes.Lazy
{
    public class LazyMaxSize : ILazyMaxSize
    {
        private IMaxSize? maxSize;

        public ulong MaxSizeInBytes()
        {
            if (maxSize == null)
                throw new InvalidOperationException("Initialize MaxSize first");

            return maxSize.MaxSizeInBytes();
        }

        public void Initialize(IMaxSize maxSize)
        {
            this.maxSize = maxSize;
        }
    }
}
