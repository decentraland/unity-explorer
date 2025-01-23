namespace ECS.StreamableLoading.Cache.Disk.CleanUp
{
    public interface IDiskCleanUp
    {
        void CleanUpIfNeeded();

        void NotifyUsed(string fileName);

        class None : IDiskCleanUp
        {
            public static readonly None INSTANCE = new ();

            private None() { }

            public void CleanUpIfNeeded()
            {
                // ignored
            }

            public void NotifyUsed(string fileName)
            {
                // ignored
            }
        }
    }
}
