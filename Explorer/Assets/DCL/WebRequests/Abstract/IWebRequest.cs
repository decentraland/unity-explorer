using Best.HTTP;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     This abstraction allows to switch easily between <see cref="UnityWebRequest" /> and <see cref="HTTPRequest" />
    /// </summary>
    public partial interface IWebRequest : IDisposable
    {
        /// <summary>
        ///     Either <see cref="UnityWebRequest" /> or <see cref="HTTPRequest" />
        /// </summary>
        internal object nativeRequest { get; }
    }
}
