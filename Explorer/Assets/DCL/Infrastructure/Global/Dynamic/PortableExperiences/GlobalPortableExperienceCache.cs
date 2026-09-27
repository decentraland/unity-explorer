using System;
using System.Collections.Generic;
using Utility.PortableExperiences;

namespace PortableExperiences.Controller
{
    /// <summary>
    ///     Per-session state for global Portable Experiences, the counterpart of SmartWearableCache and LocalPortableExperienceCache.
    /// </summary>
    public class GlobalPortableExperienceCache : IPortableExperiencesStatus
    {
        public HashSet<string> RunningPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        public HashSet<string> KilledPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        IReadOnlyCollection<string> IPortableExperiencesStatus.RunningPortableExperiences => RunningPortableExperiences;

        IReadOnlyCollection<string> IPortableExperiencesStatus.KilledPortableExperiences => KilledPortableExperiences;

        public void Clear()
        {
            RunningPortableExperiences.Clear();
            KilledPortableExperiences.Clear();
        }
    }
}
