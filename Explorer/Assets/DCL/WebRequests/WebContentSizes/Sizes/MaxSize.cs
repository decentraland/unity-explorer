using System;

namespace DCL.WebRequests.WebContentSizes.Sizes
{
    [Serializable]
    public class MaxSize : IMaxSize
    {
        public ulong maxSizeInBytes;

        public ulong MaxSizeInBytes() =>
            maxSizeInBytes;
    }
}
