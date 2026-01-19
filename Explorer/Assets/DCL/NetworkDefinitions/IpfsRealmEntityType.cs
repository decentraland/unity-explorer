namespace DCL.Ipfs
{
    public enum IpfsRealmEntityType
    {
        Scene,
        Profile,
        Wearable,
        Store,
        Emote,
        Outfits,
    }

    public static class IpfsRealmEntityTypeExtensions
    {
        public static string ToEntityString(this IpfsRealmEntityType type) =>
            type.ToString().ToLower();
    }
}
