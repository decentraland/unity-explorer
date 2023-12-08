using System.Collections.Generic;
using UnityEngine;

namespace DCL.Profiles
{
    public class Emote
    {
        public int Slot { get; set; }
        public string Urn { get; set; }
    }

    public class Eyes
    {
        public Color Color { get; set; }
    }

    public class Hair
    {
        public Color Color { get; set; }
    }

    public class Skin
    {
        public Color Color { get; set; }
    }

    public class Snapshot
    {
        public string Face256 { get; set; }
        public string Body { get; set; }
    }

    public class Avatar
    {
        public string BodyShape { get; set; }
        public HashSet<string> Wearables { get; set; }
        public HashSet<string> ForceRender { get; set; }
        public Dictionary<string, Emote> Emotes { get; set; }
        public Snapshot Snapshot { get; set; }
        public Eyes Eyes { get; set; }
        public Hair Hair { get; set; }
        public Skin Skin { get; set; }
    }

    public class Profile
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string UnclaimedName { get; set; }
        public bool HasClaimedName { get; set; }
        public string Description { get; set; }
        public int TutorialStep { get; set; }
        public string Email { get; set; }
        public string EthAddress { get; set; }
        public int Version { get; set; }
        public Avatar Avatar { get; set; }
        public HashSet<string> Blocked { get; set; }
        public List<string> Interests { get; set; }
    }
}
