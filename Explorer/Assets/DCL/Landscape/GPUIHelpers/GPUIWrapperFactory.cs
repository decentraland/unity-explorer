namespace DCL.Landscape.GPUIHelpers
{
    public static class GPUIWrapperFactory
    {
        public static IGPUIWrapper CreateGPUIWrapper()
        {
#if GPUIPRO_PRESENT
            return new GPUIWrapper();
#else
            return FakeGPUIWrapper();
#endif
        }
    }
}