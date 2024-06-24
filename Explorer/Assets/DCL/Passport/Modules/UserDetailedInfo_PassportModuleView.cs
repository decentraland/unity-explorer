using TMPro;
using UnityEngine;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public RectTransform MainContainer { get; private set; }

        [field: SerializeField]
        public TMP_Text Description { get; private set; }
    }
}
