using UnityEngine;

namespace ThirdWebUnity.Playground
{
    public class ThirdWebCustomJwtAuthHumble : MonoBehaviour
    {
        private ThirdWebCustomJWTAuth auth;

        private void Start()
        {
            auth = new ThirdWebCustomJWTAuth();

            _ = auth.Login();
        }
    }
}
