using System;

namespace DCL.AuthenticationScreenFlow
{
    /// <summary>The catalyst read of the own profile failed.</summary>
    public class ProfileFetchFailedException : Exception
    {
        public ProfileFetchFailedException() : base("Own profile fetch failed") { }
    }
}
