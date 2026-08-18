using System;

namespace PortableExperiences.Controller
{
    /// <summary>
    ///     The user refused the permissions a Portable Experience requires, so the spawn was aborted.
    /// </summary>
    public class PortableExperienceAuthorizationDeniedException : Exception
    {
        public PortableExperienceAuthorizationDeniedException(string message) : base(message) { }
    }
}
