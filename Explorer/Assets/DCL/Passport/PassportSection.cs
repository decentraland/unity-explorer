using System;

namespace DCL.Passport
{
    [Flags]
    public enum PassportSection
    {
        NONE,
        OVERVIEW = 1,
        BADGES = 1 << 1,
        PHOTOS = 2 << 1,
    }
}
