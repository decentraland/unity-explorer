using System.Collections.Generic;

namespace DCL.Profiles
{
    public class DefaultProfileCache : IProfileCache
    {
        private readonly Dictionary<string, Profile> profiles = new ();

        public Profile? Get(string id) =>
            profiles.ContainsKey(id) ? profiles[id] : null;

        public void Set(string id, Profile profile) =>
            profiles[id] = profile;

        public void Remove(string id) =>
            profiles.Remove(id);
    }
}
