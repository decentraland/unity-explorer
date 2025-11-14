using UnityEngine;

namespace ThirdWebUnity.Playground
{
    public class ThirdWebCustomJwtAuthHumble : MonoBehaviour
    {
        public string email = "popuzin@gmail.com";
        public string password = "secret123";

        private ThirdWebCustomJWTAuth auth;

        private void Start() =>
            auth = new ThirdWebCustomJWTAuth();

        [ContextMenu(nameof(Login))]
        private void Login() =>
            _ = auth.Login(email, password);

        [ContextMenu(nameof(Register))]
        private void Register() =>
            _ = ThirdWebCustomJWTAuth.Register(email, password);

        [ContextMenu(nameof(CheckConfirmed))]
        private void CheckConfirmed() =>
            _ = auth.CheckConfirmed(email);
    }
}
