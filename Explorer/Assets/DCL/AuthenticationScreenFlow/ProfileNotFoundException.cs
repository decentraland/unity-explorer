using DCL.Profiles.Self;
using System;

namespace DCL.AuthenticationScreenFlow
{
    public class ProfileNotFoundException : Exception { }

    /// <summary>A failed own-profile read; the cause is reported where the fetch failed.</summary>
    public class ProfileFetchFailedException : Exception
    {
        public ProfileFetchFailedException(ProfileReadError error) : base($"Profile fetch failed: {error}")
        {
        }
    }

    public class NotAllowedUserException : Exception
    {
        public NotAllowedUserException(string message) : base(message)
        {
        }
    }
}
