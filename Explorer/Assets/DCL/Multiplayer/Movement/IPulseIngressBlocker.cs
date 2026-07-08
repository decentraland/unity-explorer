namespace DCL.Multiplayer.Movement
{
    /// <summary>
    ///     Seam so teleport operations can block ingress without referencing the concrete bus, whose assembly DCL.RealmNavigation cannot depend on.
    /// </summary>
    public interface IPulseIngressBlocker
    {
        /// <summary>
        ///     Drops ingress and purges known peers until the realm change's outcome is announced.
        /// </summary>
        void BlockIngressUntilTeleportBroadcast();
    }
}
