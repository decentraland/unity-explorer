namespace DCL.Profiles
{
    public interface IProfileCache
    {
        Profile? Get(string id);

        void Set(string id, Profile profile);
    }
}
