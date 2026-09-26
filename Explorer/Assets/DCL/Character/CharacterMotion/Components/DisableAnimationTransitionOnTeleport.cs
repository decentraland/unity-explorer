namespace DCL.Character.CharacterMotion.Components
{
    public struct DisableAnimationTransitionOnTeleport
    {
        public readonly int ExpireFrame;

        public DisableAnimationTransitionOnTeleport(int expireFrame)
        {
            this.ExpireFrame = expireFrame;
        }
    }
}
