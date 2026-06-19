namespace DCL.Profiles.Self
{
    public interface IProfilePropagation
    {
        void Propagate(Profile profile);

        public class Dummy : IProfilePropagation
        {
            public void Propagate(Profile profile) { }
        }
    }
}
