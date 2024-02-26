namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    public class MainPlayerAvatarBaseProxy
    {
        public AvatarBase AvatarBase { get; private set; }

        public bool Configured { get; private set; }

        public void SetAvatarBase(AvatarBase newAvatarBase)
        {
            AvatarBase = newAvatarBase;
            Configured = true;
        }
    }
}
