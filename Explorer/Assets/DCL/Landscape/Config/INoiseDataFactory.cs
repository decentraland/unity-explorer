namespace DCL.Landscape.Config
{
    public interface INoiseDataFactory
    {
        public INoiseGenerator GetGenerator(uint baseSeed);
    }
}
