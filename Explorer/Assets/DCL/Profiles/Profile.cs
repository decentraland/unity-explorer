using System.Collections.Generic;

namespace DCL.Profiles
{
    public class Profile
    {
        public string UserId { get; }
        public string Name { get; }
        public string UnclaimedName { get; }
        public bool HasClaimedName { get; }
        public string Description { get; }
        public int TutorialStep { get; }
        public string Email { get; }
        public int Version { get; }
        public Avatar Avatar { get; }
        public IReadOnlyCollection<string> Blocked { get; }
        public IReadOnlyCollection<string> Interests { get; }

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
