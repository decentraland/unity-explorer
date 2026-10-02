#if DCL_RENDER_SERVER
using UnityEngine;

public partial class EmoteAnimationController
{
    /// <summary>
    /// Holds the loaded emote, and its prop, frozen at <paramref name="seconds"/>. Every other state is
    /// stopped, including the queued Idle crossfade, so the pose is the emote's alone at full weight.
    /// </summary>
    public void PoseAt(float seconds)
    {
        if (!_loadedEmote.HasValue) return;

        var urn = _loadedEmote.Value.Entity.URN;

        audioSource.Stop();

        avatarAnimation.Play(urn, PlayMode.StopAll);
        var state = avatarAnimation[urn];
        state.time = seconds;
        state.speed = 0f;
        state.weight = 1f;
        avatarAnimation.Sample();

        var prop = _loadedEmote.Value.Prop;
        if (prop != null) prop.SetActive(true);

        if (_propAnimation != null)
        {
            _propAnimation.Play(PlayMode.StopAll);

            foreach (AnimationState propState in _propAnimation)
            {
                propState.time = seconds;
                propState.speed = 0f;
            }

            _propAnimation.Sample();
        }

        _paused = true;
    }
}
#endif
