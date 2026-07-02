using UnityEngine;

namespace DCL.Passport.Modules.Creations
{
    public class CreationsDetails_PassportModuleView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject MainContainer { get; private set; }

        [field: SerializeField]
        public GameObject MainLoadingSpinner { get; private set; }

        [field: SerializeField]
        public GameObject NoCreationsLabel { get; private set; }

    }
}
