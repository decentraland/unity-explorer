using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class ReceiverExtrapolation : MonoBehaviour
    {
        [SerializeField] private MessageBus messageBus;

        private Vector3 currentVelocity = Vector3.zero;

        // Class members
        private Vector3 P_0; // Position at the time of receiving the new package
        private Vector3 P_0_n; // Position from the new package
        private Vector3 v_0_n; // Velocity from the new package

        private float T_t; // Time since the new package was received
        private float T_hat; // Normalized time factor for interpolation

        private bool firstMessage = true;

        private void Awake()
        {
            messageBus.MessageSent += OnMessageReceived;
        }

        private void Update()
        {
            T_t += UnityEngine.Time.deltaTime;
            float T_hat = Mathf.Clamp(T_t / messageBus.PackageSentRate, 0f, 1f);

            Vector3 V_b = currentVelocity + (v_0_n - currentVelocity) * T_hat;

            Vector3 P_t = P_0 + (V_b * T_t);
            Vector3 P_t_n = P_0_n + (v_0_n * T_t);

            // Apply the interpolated position
            transform.position = P_t + ((P_t_n - P_t) * T_hat);

            if (T_hat >= 1f)
                currentVelocity = v_0_n;
        }

        private void OnMessageReceived(MessageMock newMessage)
        {
            P_0 = transform.position; // Current position at the time of the new package
            currentVelocity = v_0_n;

            P_0_n = newMessage.position;
            v_0_n = newMessage.velocity;

            T_t = 0f; // Reset time since the new package

            if (firstMessage)
            {
                transform.position = newMessage.position;
                currentVelocity = newMessage.velocity;

                T_t = 1f;
                firstMessage = false;
            }
        }
    }
}
