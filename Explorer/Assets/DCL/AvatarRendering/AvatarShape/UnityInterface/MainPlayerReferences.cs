using DCL.Character.Components;

namespace DCL.AvatarRendering.AvatarShape.UnityInterface
{
    public class MainPlayerAvatarBase
    {
        public AvatarBase AvatarBase { get; private set; }

        public bool Configured { get; private set; }

        public void SetAvatarBase(AvatarBase newAvatarBase)
        {
            AvatarBase = newAvatarBase;
            Configured = true;
        }
    }

    public class MainPlayerTransform
    {
        public CharacterTransform Transform { get; private set; }

        public bool Configured { get; private set; }

        public void SetTransform(CharacterTransform newTransform)
        {
            Transform = newTransform;
            Configured = true;
        }
    }

    public class MainPlayerReferences
    {
        public MainPlayerTransform MainPlayerTransform = new ();
        public MainPlayerAvatarBase MainPlayerAvatarBase = new ();
    }
}
