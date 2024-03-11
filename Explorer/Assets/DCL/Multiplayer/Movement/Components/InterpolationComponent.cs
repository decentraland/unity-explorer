using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        public bool Enabled;
        public bool IsBlend;

        public FullMovementMessage Start;
        public FullMovementMessage End;

        public float Time;
        public float TotalDuration;
        public float SlowDownFactor;

        public void Restart(FullMovementMessage from, FullMovementMessage to, float maxBlendSpeed, float speedUpFactor, int inboxMessages, bool isBlend = false)
        {
            IsBlend = isBlend;

            Start = from;
            End = to;

            Time = 0f;
            SlowDownFactor = 1f;
            TotalDuration = End.timestamp - Start.timestamp;

            if (IsBlend)
            {
                float positionDiff = Vector3.Distance(Start.position, End.position);
                float speed = positionDiff / TotalDuration;

                if (speed > maxBlendSpeed)
                {
                    float desiredDuration = positionDiff / maxBlendSpeed;
                    SlowDownFactor = desiredDuration / TotalDuration;
                }
            }
            else
            {
                float correctionTime = (speedUpFactor + inboxMessages) * UnityEngine.Time.smoothDeltaTime;
                TotalDuration = Mathf.Max(TotalDuration - correctionTime, TotalDuration / 4f);
            }

            Enabled = true;
        }
    }
}
