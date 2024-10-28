namespace DCL.Navmap
{
    public interface ISearchHistory
    {
        void Add(string search);

        string[] Get();
    }
}
