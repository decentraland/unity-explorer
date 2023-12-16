using System.Collections.Generic;

namespace DCL.Profiles
{
    public class Profile
    {
        public string UserId { get; internal set; }
        public string Name { get; internal set; }
        public string UnclaimedName { get; internal set; }
        public bool HasClaimedName { get; internal set; }
        public string Description { get; internal set; }
        public int TutorialStep { get; internal set; }
        public string Email { get; internal set; }
        public int Version { get; internal set; }
        public Avatar Avatar { get; internal set; }
        public IReadOnlyCollection<string>? Blocked { get; internal set; }
        public IReadOnlyCollection<string>? Interests { get; internal set; }

        internal Profile() { }

        public Profile(string userId, string name, string unclaimedName, bool hasClaimedName, string description,
            int tutorialStep, string email, int version, Avatar avatar, IReadOnlyCollection<string> blocked,
            IReadOnlyCollection<string> interests)
        {
            UserId = userId;
            Name = name;
            UnclaimedName = unclaimedName;
            HasClaimedName = hasClaimedName;
            Description = description;
            TutorialStep = tutorialStep;
            Email = email;
            Version = version;
            Avatar = avatar;
            Blocked = blocked;
            Interests = interests;
        }
    }
}
