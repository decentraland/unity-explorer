using UnityEditor;
using UnityEngine;
#if URP
using UnityEngine.Rendering.Universal;
#endif

namespace StylizedGrass
{
    public class StylizedGrassEditor : Editor
    {
        [MenuItem("GameObject/Effects/Grass Bender")]
        public static void CreateGrassBender()
        {
            GrassBender gb = new GameObject().AddComponent<GrassBender>();
            gb.gameObject.name = "Grass Bender";

            Selection.activeGameObject = gb.gameObject;
            EditorApplication.ExecuteMenuItem("GameObject/Move To View");
        }

        #region Context menus

        public static void AddGrassBender(GameObject gameObject)
        {
            if (!gameObject.GetComponent<GrassBender>())
            {
                GrassBender bender = gameObject.AddComponent<GrassBender>();
                bender.OnEnable();
            }
        }
        
        [MenuItem("CONTEXT/MeshFilter/Convert to grass bender")]
        public static void ConvertMeshToBender(MenuCommand cmd)
        {
            MeshFilter mf = (MeshFilter)cmd.context;
            AddGrassBender(mf.gameObject);
        }

        [MenuItem("CONTEXT/TrailRenderer/Convert to grass bender")]
        public static void ConvertTrailToBender(MenuCommand cmd)
        {
            TrailRenderer t = (TrailRenderer)cmd.context;
            AddGrassBender(t.gameObject);
        }

        [MenuItem("CONTEXT/ParticleSystem/Convert to grass bender")]
        public static void ConvertParticleToBender(MenuCommand cmd)
        {
            ParticleSystem ps = (ParticleSystem)cmd.context;
            AddGrassBender(ps.gameObject);
        }
        
        [MenuItem("CONTEXT/LineRenderer/Convert to grass bender")]
        public static void ConvertLineToBender(MenuCommand cmd)
        {
            LineRenderer line = (LineRenderer)cmd.context;
            AddGrassBender(line.gameObject);
        }
        #endregion
    }
}