using TMPro;
using UnityEngine;

namespace ECS.Profiling
{
    public class ProfilerView : MonoBehaviour
    {

        [SerializeField]
        public TMP_Text averageFrameRate;

        [SerializeField]
        public TMP_Text hiccupCounter;

    }
}
