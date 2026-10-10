using System;

namespace DCL.Passport
{
    [Flags]
    public enum PassportSection
    {
        None,
        Overview = 1 << 0,
        Badges = 1 << 1,
        Photos = 1 << 2,
        Creations = 1 << 3,
    }
}
