using System;
using JetBrains.Annotations;
using Loading;
using UnityEngine;

/// <summary>
/// What the emote playback reports, named as the Babylon preview's emote events.
/// </summary>
public enum EmotePlaybackEvent
{
    Play,
    Playing,
    Pause,
    Loop,
    End,
}

public class EmoteAnimationController : MonoBehaviour
{
    // Public because AvatarLoader samples the same clip to build the item view's blended pose, and a
    // second literal would let the two drift apart silently.
    public const string IDLE_CLIP_NAME = "Idle";

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animation avatarAnimation;

    public bool HasAudio => _emoteAudioClip != null;
    public bool IsPaused => _paused;
    public event Action EmoteAnimationEnded;

    /// <summary>
    /// Every playback change, with the clip time in seconds it happened at. Playing is reported
    /// once per frame while the clip runs.
    /// </summary>
    public event Action<EmotePlaybackEvent, float> PlaybackChanged;

    private LoadedEmote? _loadedEmote;
    private Animation _propAnimation;
    private AudioClip _emoteAudioClip;
    private bool _paused;

    // How many times the clip has wrapped, so a loop is reported once per wrap.
    private int _reportedLoops;

    public void PlayEmote(LoadedEmote loadedEmote)
    {
        _loadedEmote = loadedEmote;

        avatarAnimation.AddClip(_loadedEmote.Value.Clip, _loadedEmote.Value.Entity.URN);

        // Add prop / audio trigger
        _loadedEmote.Value.Clip.events = Array.Empty<AnimationEvent>();

        var clip = avatarAnimation.GetClip(_loadedEmote.Value.Entity.URN);
        clip.AddEvent(new AnimationEvent
        {
            time = 0,
            functionName = "EmoteStarted"
        });
        clip.AddEvent(new AnimationEvent
        {
            time = clip.length - 0.1f,
            functionName = "EmoteEnded"
        });

        _propAnimation = _loadedEmote.Value.PropAnim;
        _emoteAudioClip = _loadedEmote.Value.Audio;

        ReplayEmote();
    }

    public float GetEmoteLength()
    {
        if (!_loadedEmote.HasValue) return 0f;
        return _loadedEmote.Value.Clip.length;
    }

    /// <summary>
    /// True while the clip advances: a paused or held clip is not playing, as the Babylon preview
    /// answers it.
    /// </summary>
    public bool IsEmotePlaying()
    {
        if (!_loadedEmote.HasValue) return false;
        return !_paused && avatarAnimation.IsPlaying(_loadedEmote.Value.Entity.URN);
    }

    public void ReplayEmote()
    {
        if (!_loadedEmote.HasValue) return;

        _paused = false;
        _reportedLoops = 0;

        // Prop
        if (_propAnimation != null)
        {
            _propAnimation.Rewind();
            _propAnimation.Sample();
            _propAnimation.Play();
        }

        _loadedEmote.Value.Prop?.SetActive(true);

        // Crossfade
        avatarAnimation.Rewind(_loadedEmote.Value.Entity.URN);
        avatarAnimation.Play(_loadedEmote.Value.Entity.URN);

        if (_loadedEmote.Value.Clip.wrapMode != WrapMode.Loop)
        {
            avatarAnimation.CrossFadeQueued(IDLE_CLIP_NAME, 0.3f);
        }

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Play, 0f);
    }

    public void PauseEmote()
    {
        if (!_loadedEmote.HasValue) return;
        if (!avatarAnimation.IsPlaying(_loadedEmote.Value.Entity.URN)) return;

        _paused = true;

        // Pause by setting speed to 0
        var state = avatarAnimation[_loadedEmote.Value.Entity.URN];
        if (state != null) state.speed = 0f;

        if (_propAnimation != null)
        {
            var propState = _propAnimation[_loadedEmote.Value.Entity.URN];
            if (propState != null) propState.speed = 0f;
        }

        audioSource.Pause();

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Pause, GetCurrentEmoteTime());
    }

    public void ResumeEmote()
    {
        if (!_loadedEmote.HasValue || !_paused) return;

        _paused = false;

        var state = avatarAnimation[_loadedEmote.Value.Entity.URN];
        if (state != null) state.speed = 1f;

        if (_propAnimation != null)
        {
            var propState = _propAnimation[_loadedEmote.Value.Entity.URN];
            if (propState != null) propState.speed = 1f;
        }

        audioSource.UnPause();

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Play, GetCurrentEmoteTime());
    }

    /// <summary>
    /// Holds the emote on its first frame with its audio stopped, as the Babylon preview's stop
    /// does, so the next play starts it over. <see cref="ReturnToIdle"/> leaves the emote instead.
    /// </summary>
    public void StopEmote()
    {
        if (!_loadedEmote.HasValue) return;

        GoToEmote(0f);
        _paused = true;

        var urn = _loadedEmote.Value.Entity.URN;
        var state = avatarAnimation[urn];
        if (state != null) state.speed = 0f;

        if (_propAnimation != null)
        {
            var propState = _propAnimation[urn];
            if (propState != null) propState.speed = 0f;
        }

        audioSource.Stop();

        PlaybackChanged?.Invoke(EmotePlaybackEvent.End, 0f);
    }

    /// <summary>
    /// Leaves the emote for the rig's idle clip, with its prop hidden and its audio stopped.
    /// </summary>
    public void ReturnToIdle(bool withCrossFade)
    {
        if (!_loadedEmote.HasValue) return;

        var time = GetCurrentEmoteTime();
        _paused = false;

        audioSource.Stop();
        if (_propAnimation != null)
        {
            _loadedEmote.Value.Prop?.SetActive(false);
        }

        if (withCrossFade)
        {
            avatarAnimation.CrossFade(IDLE_CLIP_NAME, 0.3f, PlayMode.StopAll);
        }
        else
        {
            avatarAnimation.Play(IDLE_CLIP_NAME, PlayMode.StopAll);
        }

        PlaybackChanged?.Invoke(EmotePlaybackEvent.End, time);
    }

    public void GoToEmote(float seconds)
    {
        if (!_loadedEmote.HasValue) return;

        var urn = _loadedEmote.Value.Entity.URN;
        var wasPaused = _paused;

        // Ensure the animation is playing so we can seek it
        if (!avatarAnimation.IsPlaying(urn))
        {
            avatarAnimation.Play(urn);
        }

        var state = avatarAnimation[urn];
        if (state != null)
        {
            state.time = seconds;
            // If it was paused (or we want to just seek), keep it paused
            if (wasPaused)
            {
                state.speed = 0f;
            }
        }

        avatarAnimation.Sample();

        if (_propAnimation != null)
        {
            var propState = _propAnimation[urn];
            if (propState != null)
            {
                propState.time = seconds;
                if (wasPaused) propState.speed = 0f;
            }
        }

        // Sync audio position (even when paused so UnPause resumes from the correct position)
        if (_emoteAudioClip != null)
        {
            audioSource.time = Mathf.Clamp(seconds, 0f, _emoteAudioClip.length);
        }

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Playing, seconds);
    }

    public void EnableSound()
    {
        if (audioSource != null) audioSource.volume = 1f;
    }

    public void DisableSound()
    {
        if (audioSource != null) audioSource.volume = 0f;
    }

    public void ClearEmote()
    {
        if (!_loadedEmote.HasValue) return;

        ReturnToIdle(false);
        avatarAnimation.RemoveClip(_loadedEmote.Value.Clip);
        _loadedEmote = null;
        _emoteAudioClip = null;
        _propAnimation = null;
        audioSource.clip = null;
    }

    private void Update()
    {
        if (!_loadedEmote.HasValue || _paused) return;

        var urn = _loadedEmote.Value.Entity.URN;
        if (!avatarAnimation.IsPlaying(urn)) return;

        var state = avatarAnimation[urn];
        if (state == null || state.length <= 0f) return;

        // A looping clip's time keeps growing past its length.
        var loops = Mathf.FloorToInt(state.time / state.length);

        if (loops > _reportedLoops)
        {
            _reportedLoops = loops;

            if (_loadedEmote.Value.Clip.wrapMode == WrapMode.Loop)
            {
                PlaybackChanged?.Invoke(EmotePlaybackEvent.Loop, 0f);
            }
        }

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Playing, state.time - loops * state.length);
    }

    // Within the clip, however many times a looping one has wrapped.
    private float GetCurrentEmoteTime()
    {
        if (!_loadedEmote.HasValue) return 0f;

        var state = avatarAnimation[_loadedEmote.Value.Entity.URN];
        if (state == null || state.length <= 0f) return 0f;

        return state.time % state.length;
    }

    [UsedImplicitly]
    private void EmoteStarted()
    {
        if (_emoteAudioClip != null)
        {
            audioSource.PlayOneShot(_emoteAudioClip);
        }

        if (_propAnimation != null)
        {
            _propAnimation.Rewind();
            _propAnimation.Sample();
            _propAnimation.Play();
        }
    }

    [UsedImplicitly]
    private void EmoteEnded()
    {
        if (_loadedEmote.HasValue && _loadedEmote.Value.Clip.wrapMode != WrapMode.Loop)
        {
            EmoteAnimationEnded?.Invoke();
            _loadedEmote.Value.Prop?.SetActive(false);
            PlaybackChanged?.Invoke(EmotePlaybackEvent.End, _loadedEmote.Value.Clip.length);
        }
    }
}
