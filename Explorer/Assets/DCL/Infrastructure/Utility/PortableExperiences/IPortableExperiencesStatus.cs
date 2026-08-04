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
}
