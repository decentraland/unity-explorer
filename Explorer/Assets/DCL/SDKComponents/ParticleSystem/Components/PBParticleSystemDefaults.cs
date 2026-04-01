using DCL.ECSComponents;

namespace DCL.SDKComponents.ParticleSystem.Components
{
    public static class PBParticleSystemDefaults
    {
        public static bool GetActive(this PBParticleSystem self) =>
            !self.HasActive || self.Active;

        public static float GetRate(this PBParticleSystem self) =>
            self.HasRate ? self.Rate : 10f;

        public static uint GetMaxParticles(this PBParticleSystem self) =>
            self.HasMaxParticles ? self.MaxParticles : 1000;

        public static float GetLifetime(this PBParticleSystem self) =>
            self.HasLifetime ? self.Lifetime : 5f;

        public static float GetGravity(this PBParticleSystem self) =>
            self.HasGravity ? self.Gravity : 0f;

        public static bool GetLoop(this PBParticleSystem self) =>
            !self.HasLoop || self.Loop;

        public static bool GetPrewarm(this PBParticleSystem self) =>
            self.HasPrewarm && self.Prewarm;

        public static bool GetFaceTravelDirection(this PBParticleSystem self) =>
            self.HasFaceTravelDirection && self.FaceTravelDirection;

        public static PBParticleSystem.Types.BlendMode GetBlendMode(this PBParticleSystem self) =>
            self.HasBlendMode ? self.BlendMode : PBParticleSystem.Types.BlendMode.PsbAlpha;

        public static bool GetBillboard(this PBParticleSystem self) =>
            !self.HasBillboard || self.Billboard;

        public static PBParticleSystem.Types.SimulationSpace GetSimulationSpace(this PBParticleSystem self) =>
            self.HasSimulationSpace ? self.SimulationSpace : PBParticleSystem.Types.SimulationSpace.PssLocal;

        public static PBParticleSystem.Types.PlaybackState GetPlaybackState(this PBParticleSystem self) =>
            self.HasPlaybackState ? self.PlaybackState : PBParticleSystem.Types.PlaybackState.PsPlaying;

        // --- Shape sub-types ---

        public static float GetRadius(this PBParticleSystem.Types.Sphere self) =>
            self.HasRadius ? self.Radius : 1f;

        public static float GetAngle(this PBParticleSystem.Types.Cone self) =>
            self.HasAngle ? self.Angle : 25f;

        public static float GetRadius(this PBParticleSystem.Types.Cone self) =>
            self.HasRadius ? self.Radius : 1f;

        // --- LimitVelocity ---

        public static float GetDampen(this PBParticleSystem.Types.LimitVelocity self) =>
            self.HasDampen ? self.Dampen : 1f;

        // --- SpriteSheet ---

        public static float GetFramesPerSecond(this PBParticleSystem.Types.SpriteSheetAnimation self) =>
            self.HasFramesPerSecond ? self.FramesPerSecond : 30f;

        // --- Burst ---

        public static int GetCycles(this PBParticleSystem.Types.Burst self) =>
            self.HasCycles ? self.Cycles : 1;

        public static float GetInterval(this PBParticleSystem.Types.Burst self) =>
            self.HasInterval ? self.Interval : 0.01f;

        public static float GetProbability(this PBParticleSystem.Types.Burst self) =>
            self.HasProbability ? self.Probability : 1f;
    }
}
