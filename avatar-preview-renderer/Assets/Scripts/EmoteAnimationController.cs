using System;
using JetBrains.Annotations;
using Loading;
using UnityEngine;

public class EmoteAnimationController : MonoBehaviour
{
    private const string IDLE_CLIP_NAME = "Idle";

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animation avatarAnimation;

    public bool HasAudio => _emoteAudioClip != null;
    public bool IsPaused => _paused;
    public event Action EmoteAnimationEnded;

    private LoadedEmote? _loadedEmote;
    private Animation _propAnimation;
    private AudioClip _emoteAudioClip;
    private bool _paused;

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

    public bool IsEmotePlaying()
    {
        if (!_loadedEmote.HasValue) return false;
        return avatarAnimation.IsPlaying(_loadedEmote.Value.Entity.URN);
    }

    public void ReplayEmote()
    {
        if (!_loadedEmote.HasValue) return;

        _paused = false;

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
    }

    public void StopEmote(bool withCrossFade = true)
    {
        if (!_loadedEmote.HasValue) return;

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
    }

    public void EnableSound()
    {
        if (audioSource != null) audioSource.volume = 1f;
    }

    public void DisableSound()
    {
        if (audioSource != null) audioSource.volume = 0f;
    }

    private float GetCurrentEmoteTime()
    {
        if (!_loadedEmote.HasValue) return 0f;
        var state = avatarAnimation[_loadedEmote.Value.Entity.URN];
        return state?.time ?? 0f;
    }

    public void ClearEmote()
    {
        if (!_loadedEmote.HasValue) return;

        StopEmote(false);
        avatarAnimation.RemoveClip(_loadedEmote.Value.Clip);
        _loadedEmote = null;
        _emoteAudioClip = null;
        _propAnimation = null;
        audioSource.clip = null;
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
        }
    }
}