namespace DCL.Multiplayer.Movement.ECS
{
    public class Interpolation
    {
        // public static void Execute(ref CharacterTransform transComp, ref InterpolationComponent ext, float deltaTime, RemotePlayerExtrapolationSettings settings)
        // {
        //
        // }
        // public (FullMovementMessage message, float restTimeDelta) Update(float deltaTime)
        // {
        //     var remainedDeltaTime = 0f;
        //
        //     time += deltaTime / slowDownFactor;
        //
        //     UpdateRotation();
        //
        //     if (time >= totalDuration)
        //     {
        //         remainedDeltaTime = (time - totalDuration)*slowDownFactor;
        //         time = totalDuration;
        //         UpdateEndRotation();
        //     }
        //
        //     Transform.position = DoTransition(Start, End, time, totalDuration, IsBlend);
        //
        //     return time == totalDuration ? (Disable(), remainedDeltaTime) : (null, 0);
        // }
    }
}
