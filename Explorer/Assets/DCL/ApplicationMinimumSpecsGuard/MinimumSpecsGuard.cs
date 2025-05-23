using UnityEngine.Device;

namespace DCL.ApplicationMinimumSpecsGuard
{

    public class MinimumSpecsGuard
    {
        private const int MINIMUM_RAM = 1024 * 16;
        private const int MINIMUM_VRAM = 1024 * 6;

        public bool HasMinimumSpecs()
        {
            if (!SystemInfo.supportsComputeShaders)
                return false;

            if (SystemInfo.systemMemorySize < MINIMUM_RAM)
                return false;

            if (SystemInfo.graphicsMemorySize < MINIMUM_VRAM)
                return false;

            return true;
        }
    }
}
