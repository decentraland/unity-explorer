using Arch.Core;

namespace DCL.Utilities
{
    public class WorldProxy
    {
        public World? World { get; private set; }

        public void SetWorld(World newWorld)
        {
            World = newWorld;
        }
    }
}
