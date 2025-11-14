using System;

namespace DCL.Profiles
{
    public class ProfileParseException : Exception
    {
        public string ProfileId { get; }

        public ProfileParseException(string profileId, string json, Exception innerException)
            : base($"Cannot parse profile: {profileId}\n{json}", innerException)
        {
            ProfileId = profileId;
        }
    }
}
