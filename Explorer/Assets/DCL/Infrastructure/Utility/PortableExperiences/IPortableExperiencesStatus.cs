using System;
using System.Collections.Generic;

namespace Utility.PortableExperiences
{
    /// <summary>
    ///     Read-only view over a Portable Experience status tracker; decouples consumers from the assembly the trackers live in.
    /// </summary>
    public interface IPortableExperiencesStatus
    {
        IReadOnlyCollection<string> RunningPortableExperiences { get; }

        IReadOnlyCollection<string> KilledPortableExperiences { get; }
    }

    public interface ILocalPortableExperiencesStatus : IPortableExperiencesStatus
    {
        IReadOnlyCollection<string> AuthorizedPortableExperiences { get; }
    }

    /// <summary>
    ///     Lifecycle surface of the Portable Experiences controller; decouples consumers from the assembly the controller lives in.
    /// </summary>
    public interface IPortableExperiencesLifecycle
    {
        event Action<string>? PortableExperienceLoaded;

        event Action<string>? PortableExperienceUnloaded;

        bool CanKillPortableExperience(string id);

        /// <summary>
        ///     Unloads the Portable Experience and records it as killed, blocking automatic restarts for the session.
        /// </summary>
        void KillPortableExperience(string id);

        /// <summary>
        ///     Unloads the Portable Experience without the killed marker, so it can be started again.
        /// </summary>
        void UnloadPortableExperience(string id);
    }
}
