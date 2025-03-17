using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Bunches
{
    public interface IBunch<out T> : IDisposable
    {
        IReadOnlyCollection<T> Collection();

        bool Available();
    }
}
