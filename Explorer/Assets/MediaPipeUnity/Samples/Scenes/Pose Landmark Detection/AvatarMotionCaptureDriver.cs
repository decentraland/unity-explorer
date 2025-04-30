using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

namespace MediaPipeUnity.Samples.Scenes.Pose_Landmark_Detection
{
    public class AvatarMotionCaptureDriver : MonoBehaviour
    {
        PoseLandmarkerRunner runner;
        PoseLandmarkerRunner Runner => runner ??= FindObjectOfType<PoseLandmarkerRunner>();

        public GameObject[] points = new GameObject[33];
        private GameObject parent;

        [Space]
        public Transform[] avatarPoints;

        private void Awake()
        {
            parent = new GameObject();
            parent.transform.SetParent(transform);
            parent.transform.localPosition = new Vector3(0, 1, 0);
            parent.transform.localEulerAngles = new Vector3(0, 180, 0);

            for (int i = 0; i < 33; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "P" + i;
                sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                sphere.transform.SetParent(parent.transform);

                points[i] = sphere;
            }
        }

        private void Update()
        {
            if (Runner == null) return;

            for (var j = 11; j < 21; j++)
            {
                points[j].transform.localPosition = Runner.resultPos[j];
            }

            for (int i = 0; i < avatarPoints.Length; i++)
            {
                var aPoint = avatarPoints[i];

                if (aPoint != null)
                    aPoint.position = points[i].transform.position;
            }
        }
    }
}
