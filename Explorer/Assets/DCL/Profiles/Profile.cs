#nullable disable

using JetBrains.Annotations;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public class Profile
    {
        internal HashSet<string> blocked;
        internal List<string> interests;
        public string UserId { get; internal set; }
        public string Name { get; internal set; }
        public string UnclaimedName { get; internal set; }
        public bool HasClaimedName { get; internal set; }
        public string Description { get; internal set; }
        public int TutorialStep { get; internal set; }
        public string Email { get; internal set; }
        public int Version { get; internal set; }
        public Avatar Avatar { get; internal set; }

        /// <summary>
        ///     This flag can be moved elsewhere when the final flow is established
        /// </summary>
        public bool IsDirty { get; set; }

        [CanBeNull] public IReadOnlyCollection<string> Blocked => blocked;
        [CanBeNull] public IReadOnlyCollection<string> Interests => interests;

        internal Profile() { }

        public Profile(string userId, string name, Avatar avatar)
        {
            UserId = userId;
            Name = name;
            Avatar = avatar;
        }
    }
}
