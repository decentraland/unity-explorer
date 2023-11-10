using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     This interface is used as a constraint for generics and should not be referenced directly
    /// </summary>
    internal interface ITypedWebRequest
    {
        UnityWebRequest UnityWebRequest { get; }
    }
}
