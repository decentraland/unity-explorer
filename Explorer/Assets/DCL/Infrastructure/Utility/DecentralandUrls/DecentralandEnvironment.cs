namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public enum DecentralandEnvironment
    {
        Org = 0,
        Zone = 1,

        /// <summary>
        ///     A deployment reachable under a base domain other than <c>decentraland.*</c>, selected with the
        ///     <c>--base-domain</c> app arg. New values must be appended: the enum is serialized by index on
        ///     <c>MainSceneLoader</c>.
        /// </summary>
        Custom = 2,
    }
}
