namespace DCL.Profiles.Self
{
    public interface IProfilePropagation
    {
        void PropagateIfNewVersion(Profile profile);

        public class Dummy : IProfilePropagation
        {
            public void PropagateIfNewVersion(Profile profile) { }
        }
    }
}
