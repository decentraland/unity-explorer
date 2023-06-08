using ECS.StreamableLoading.Components.Common;
using UnityEngine.Networking;

namespace ECS.StreamableLoading
{
    internal delegate UnityWebRequest CreateDelegate<TIntent>(in TIntent intention) where TIntent: struct, ILoadingIntention;
}
