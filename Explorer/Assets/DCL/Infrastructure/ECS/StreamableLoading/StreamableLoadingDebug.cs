namespace ECS.StreamableLoading
{
    public static class StreamableLoadingDebug
    {
        public static readonly bool ENABLED =
#if DEVELOPMENT_BUILD || DEBUG || UNITY_EDITOR || ENABLE_PROFILER
            true;
#else
            false;
#endif
    }
}
