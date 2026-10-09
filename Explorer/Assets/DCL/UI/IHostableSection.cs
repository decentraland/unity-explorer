using UnityEngine;

namespace DCL.UI
{
    /// <summary>
    ///     A section whose single view can be moved under another host, such as a modal frame, and back to the slot it was authored in.
    /// </summary>
    public interface IHostableSection : ISection
    {
        /// <summary>The parent the view currently sits under.</summary>
        RectTransform? CurrentHost { get; }

        /// <summary>True while the view sits in the slot it was authored in.</summary>
        bool IsAtHome { get; }

        void AttachTo(RectTransform host);

        void AttachToHome();
    }
}
