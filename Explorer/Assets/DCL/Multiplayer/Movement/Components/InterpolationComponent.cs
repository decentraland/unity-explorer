using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        public readonly Transform Transform;
        public bool Enabled;

        public bool IsBlend;

        public FullMovementMessage Start;
        public FullMovementMessage End;

        public float Time;
        public float TotalDuration;
        public float SlowDownFactor;

        private IMultiplayerMovementSettings settings;

        public InterpolationComponent(Transform transform)
        {
            Transform = transform;
            settings = null;

            Enabled = false;

            Start = null;
            End = null;
            Time = 0f;
            TotalDuration = 0f;

            IsBlend = false;
            SlowDownFactor = 1f;
        }

        public (FullMovementMessage message, float restTimeDelta) Update(float deltaTime)
        {
            var remainedDeltaTime = 0f;

            Time += deltaTime / SlowDownFactor;

            UpdateRotation();

            if (Time >= TotalDuration)
            {
                remainedDeltaTime = (Time - TotalDuration)*SlowDownFactor;
                Time = TotalDuration;
                UpdateEndRotation();
            }

            Transform.position = DoTransition(Start, End, Time, TotalDuration, IsBlend);

            return Time == TotalDuration ? (Disable(), remainedDeltaTime) : (null, 0);
        }

        private void UpdateEndRotation()
        {
            // future position
            Vector3 flattenedDiff = End.velocity;
            flattenedDiff.y = 0;

            if (flattenedDiff != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(flattenedDiff, Vector3.up);
                Transform.rotation = lookRotation;
            }
        }

        private void UpdateStartRotation()
        {
            // future position
            Vector3 flattenedDiff = Start.velocity;
            flattenedDiff.y = 0;

            if (flattenedDiff != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(flattenedDiff, Vector3.up);
                Transform.rotation = lookRotation;
            }
        }

        private void UpdateRotation()
        {
            // future position
            Vector3 flattenedDiff = DoTransition(Start, End, Time + 0.1f, TotalDuration, IsBlend) - Transform.position;
            flattenedDiff.y = 0;

            if (flattenedDiff != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(flattenedDiff, Vector3.up);
                Transform.rotation = lookRotation;
            }
        }

        private void Enable(int inboxMessages)
        {
            Time = 0f;
            SlowDownFactor = 1f;
            TotalDuration = End.timestamp - Start.timestamp;

            Transform.position = DoTransition(Start, End, Time, TotalDuration, IsBlend);
            UpdateStartRotation();

            if (IsBlend)
            {
                float positionDiff = Vector3.Distance(Start.position, End.position);
                float speed = positionDiff / TotalDuration;

                if (speed > settings.MaxBlendSpeed)
                {
                    float desiredDuration = positionDiff / settings.MaxBlendSpeed;
                    SlowDownFactor = desiredDuration / TotalDuration;
                }
            }
            else
            {
                float correctionTime = (settings.SpeedUpFactor + inboxMessages) * UnityEngine.Time.smoothDeltaTime;
                TotalDuration = Mathf.Max(TotalDuration - correctionTime, TotalDuration / 4f);
            }

            Enabled = true;
        }

        public void Run(FullMovementMessage from, FullMovementMessage to, int inboxMessages, IMultiplayerMovementSettings settings, bool isBlend = false)
        {
            this.settings = settings;

            if (from?.timestamp >= to.timestamp) return;

            Start = from;
            End = to;

            IsBlend = isBlend;

            Enable(inboxMessages);
        }

        private FullMovementMessage Disable()
        {
            Enabled = false;
            return End;
        }

        private Vector3 DoTransition(FullMovementMessage start, FullMovementMessage end, float time, float totalDuration, bool isBlend)
        {
            return (isBlend ? settings.BlendType : settings.InterpolationType) switch
                   {
                       InterpolationType.Linear => InterpolationSpline.Linear(start, end, time, totalDuration),
                       InterpolationType.PositionBlending => InterpolationSpline.ProjectivePositionBlending(start, end, time, totalDuration),
                       InterpolationType.VelocityBlending => InterpolationSpline.ProjectiveVelocityBlending(start, end, time, totalDuration),
                       InterpolationType.Bezier => InterpolationSpline.Bezier(start, end, time, totalDuration),
                       InterpolationType.Hermite => InterpolationSpline.Hermite(start, end, time, totalDuration),
                       InterpolationType.MonotoneYHermite => InterpolationSpline.MonotoneYHermite(start, end, time, totalDuration),
                       InterpolationType.FullMonotonicHermite => InterpolationSpline.FullMonotonicHermite(start, end, time, totalDuration),
                       _ => InterpolationSpline.Linear(start, end, time, totalDuration),
                   };
        }
    }
}
