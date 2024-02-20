using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class SenderMovement : MonoBehaviour
    {
        public float speed = 5.0f;
        private CharacterController controller;

        private void Start()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Move();
        }

        private void Move()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(horizontal, 0, vertical);
            controller.Move(movement * speed * Time.deltaTime);
        }

    }
}
