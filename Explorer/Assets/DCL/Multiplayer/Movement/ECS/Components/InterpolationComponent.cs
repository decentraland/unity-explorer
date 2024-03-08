using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    public struct InterpolationComponent
    {
        public readonly Transform Transform;
        public bool Enabled;

        public bool IsBlend;

        public FullMovementMessage Start;
        public FullMovementMessage End;

        private float time;
        private float totalDuration;
        private float slowDownFactor;

        private IMultiplayerSpatialStateSettings settings;

        public InterpolationComponent(Transform transform)
        {
            Transform = transform;
            settings = null;

            Enabled = false;

            Start = null;
            End = null;
            time = 0f;
            totalDuration = 0f;

            IsBlend = false;
            slowDownFactor = 1f;
        }

        public (FullMovementMessage message, float restTimeDelta) Update(float deltaTime)
        {
            var remainedDeltaTime = 0f;

            time += deltaTime / slowDownFactor;

            if (time >= totalDuration)
            {
                remainedDeltaTime = (time - totalDuration)*slowDownFactor;
                time = totalDuration;
            }

            Transform.position = DoTransition(Start, End, time, totalDuration, IsBlend);
            UpdateRotation();

            return time == totalDuration ? (Disable(), remainedDeltaTime) : (null, 0);
        }

        private void UpdateRotation()
        {
            Vector3 flattenedDiff = End.position - Transform.position;
            flattenedDiff.y = 0;

            if (flattenedDiff != Vector3.zero)
            {
                var lookRotation = Quaternion.LookRotation(flattenedDiff, Vector3.up);
                Transform.rotation = lookRotation;
            }
        }

        private void Enable(int inboxMessages)
        {
            time = 0f;
            slowDownFactor = 1f;
            totalDuration = End.timestamp - Start.timestamp;

            Transform.position = DoTransition(Start, End, time, totalDuration, IsBlend);

            if (IsBlend)
            {
                float positionDiff = Vector3.Distance(Start.position, End.position);
                float speed = positionDiff / totalDuration;

                if (speed > settings.MaxBlendSpeed)
                {
                    float desiredDuration = positionDiff / settings.MaxBlendSpeed;
                    slowDownFactor = desiredDuration / totalDuration;
                }
            }
            else
            {
                float correctionTime = (settings.SpeedUpFactor + inboxMessages) * Time.smoothDeltaTime;
                totalDuration = Mathf.Max(totalDuration - correctionTime, totalDuration / 4f);
            }

            Enabled = true;
        }

        public void Run(FullMovementMessage from, FullMovementMessage to, int inboxMessages, IMultiplayerSpatialStateSettings settings, bool isBlend = false)
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
                       InterpolationType.Linear => Interpolate.Linear(start, end, time, totalDuration),
                       InterpolationType.PositionBlending => Interpolate.ProjectivePositionBlending(start, end, time, totalDuration),
                       InterpolationType.VelocityBlending => Interpolate.ProjectiveVelocityBlending(start, end, time, totalDuration),
                       InterpolationType.Bezier => Interpolate.Bezier(start, end, time, totalDuration),
                       InterpolationType.Hermite => Interpolate.Hermite(start, end, time, totalDuration),
                       InterpolationType.MonotoneYHermite => Interpolate.MonotoneYHermite(start, end, time, totalDuration),
                       InterpolationType.FullMonotonicHermite => Interpolate.FullMonotonicHermite(start, end, time, totalDuration),
                       _ => Interpolate.Linear(start, end, time, totalDuration),
                   };
        }
    }
}
