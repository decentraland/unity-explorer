using UnityEngine.Networking;

namespace DCL.WebRequests
{
    internal interface ITypedWebRequest
    {
        UnityWebRequest UnityWebRequest { get; }
    }
}
