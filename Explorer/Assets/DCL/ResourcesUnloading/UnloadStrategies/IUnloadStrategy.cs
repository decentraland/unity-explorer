namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public interface IUnloadStrategy
    {
        void TryUnload(ICacheCleaner cacheCleaner);

    }
}