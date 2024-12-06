using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    //Data set during the build process, commented the Create Asset Menu as we only need one of these
    //[CreateAssetMenu(fileName = "BuildData", menuName = "DCL/BuildData", order = 0)]
    public class BuildData : ScriptableObject
    {
        [SerializeField] public string InstallSource = "";
    }
}
