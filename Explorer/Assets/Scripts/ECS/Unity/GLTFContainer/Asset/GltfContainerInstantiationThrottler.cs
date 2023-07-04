namespace ECS.Unity.GLTFContainer.Asset
{
    public class GltfContainerInstantiationThrottler : IGltfContainerInstantiationThrottler
    {
        private readonly int allowedInstantiationsPerFrame;

        private int currentCount;

        public GltfContainerInstantiationThrottler(int allowedInstantiationsPerFrame)
        {
            this.allowedInstantiationsPerFrame = allowedInstantiationsPerFrame;
        }

        public bool Acquire(int count = 1)
        {
            if (currentCount + count > allowedInstantiationsPerFrame)
                return false;

            currentCount += count;
            return true;
        }

        public void Reset()
        {
            currentCount = 0;
        }
    }
}
