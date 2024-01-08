using System;

namespace DCL.Profiles
{
    public class ProfileParseException : Exception
    {
        public string ProfileId { get; }
        public int Version { get; }

        public ProfileParseException(string profileId, int version, string json, Exception innerException)
            : base($"Cannot parse profile: {profileId} - {version}\n{json}", innerException)
        {
            ProfileId = profileId;
            Version = version;
        }
    }
}
