using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class EventScheduleElementView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text DateLabel { get; private set; }

        [field: SerializeField]
        public Button AddToCalendarButton { get; private set; }
    }
}
