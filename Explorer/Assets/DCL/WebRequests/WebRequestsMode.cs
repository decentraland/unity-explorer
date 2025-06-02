using Best.HTTP;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    public enum WebRequestsMode : byte
    {
        /// <summary>
        ///     Default <see cref="UnityWebRequest" /> will be used
        /// </summary>
        UNITY = 0,

        /// <summary>
        ///     <see cref="HTTPRequest" /> will be used
        /// </summary>
        HTTP2 = 1,

        /// <summary>
        ///     <see cref="YetAnotherWebRequest" /> will be used.
        /// </summary>
        YET_ANOTHER = 2,
    }
}
