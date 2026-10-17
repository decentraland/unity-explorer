using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Data only storage of references to the skybox elements
    ///     that are assigned from the scene
    /// </summary>
    public class SkyBoxSceneData : MonoBehaviour
    {
        [field: SerializeField]
        public Light DirectionalLight { get; private set; }
    }
}
