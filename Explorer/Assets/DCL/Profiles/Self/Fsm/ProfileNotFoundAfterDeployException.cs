using System;

namespace DCL.Profiles.Self
{
    public class ProfileNotFoundAfterDeployException : Exception
    {
        public ProfileNotFoundAfterDeployException(UserId address, int version)
            : base($"Profile v{version} of {address.Value} was not found on the catalyst after the deploy") { }
    }
}
