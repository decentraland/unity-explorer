using System.Collections.Generic;

namespace DCL.Profiles
{
    public class Profile
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string UnclaimedName { get; set; }
        public bool HasClaimedName { get; set; }
        public string Description { get; set; }
        public int TutorialStep { get; set; }
        public string Email { get; set; }
        public int Version { get; set; }
        public Avatar Avatar { get; set; }
        public HashSet<string> Blocked { get; set; }
        public List<string> Interests { get; set; }
    }
}
