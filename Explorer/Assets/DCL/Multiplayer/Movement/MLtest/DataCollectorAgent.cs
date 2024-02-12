using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MLtest
{
    public class DataCollectorAgent : Agent
    {
        [SerializeField] private GameObject target;

        public override void CollectObservations(VectorSensor sensor)
        {
            // Добавление позиции агента
            sensor.AddObservation(target.transform.position);

            // // Добавление скорости агента, предполагая, что у агента есть компонент Rigidbody
            // Rigidbody rb = GetComponent<Rigidbody>();
            // sensor.AddObservation(rb.velocity);

            // Добавление ввода с клавиатуры
            sensor.AddObservation(UnityEngine.Input.GetKey(KeyCode.W) ? 1 : 0);
            sensor.AddObservation(UnityEngine.Input.GetKey(KeyCode.A) ? 1 : 0);
            sensor.AddObservation(UnityEngine.Input.GetKey(KeyCode.S) ? 1 : 0);
            sensor.AddObservation(UnityEngine.Input.GetKey(KeyCode.D) ? 1 : 0);
            sensor.AddObservation(UnityEngine.Input.GetKey(KeyCode.Space) ? 1 : 0);
        }
    }
}
