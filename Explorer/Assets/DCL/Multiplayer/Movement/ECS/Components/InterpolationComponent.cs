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

        public MessageMock Start;
        public MessageMock End;

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

        public MessageMock Update(float deltaTime)
        {
            time += deltaTime / slowDownFactor;

            if (time > totalDuration)
                return Disable();

            Transform.position = DoTransition(Start, End, time, totalDuration, IsBlend);
            UpdateRotation();

            return null;
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

            // if (IsBlend)
            // {
            //     float positionDiff = Vector3.Distance(start.position, end.position);
            //     float speed = positionDiff / totalDuration;
            //
            //     if (speed > settings.MaxBlendSpeed)
            //     {
            //         float desiredDuration = positionDiff / settings.MaxBlendSpeed;
            //         slowDownFactor = desiredDuration / totalDuration;
            //     }
            // }
            // else
            // {
            //     float correctionTime = (settings.SpeedUpFactor + inboxMessages) * Time.smoothDeltaTime;
            //     totalDuration = Mathf.Max(totalDuration - correctionTime, totalDuration / 4f);
            // }

            Enabled = true;
        }

        public void Run(MessageMock from, MessageMock to, int inboxMessages, IMultiplayerSpatialStateSettings settings, bool isBlend = false)
        {
            this.settings = settings;

            if (from?.timestamp >= to.timestamp) return;

            Start = from;
            End = to;

            IsBlend = isBlend;

            if(Start != null) Start.position = Transform.position;
            Enable(inboxMessages);
        }

        private MessageMock Disable()
        {
            // Transform.position = end.position;
            End.position = Transform.position;
            Enabled = false;

            return End;
        }

        private Vector3 DoTransition(MessageMock start, MessageMock end, float time, float totalDuration, bool isBlend)
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
