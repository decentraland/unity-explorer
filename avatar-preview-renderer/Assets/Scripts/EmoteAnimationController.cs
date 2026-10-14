using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Loading;
using UnityEngine;

/// <summary>
/// What the emote playback reports. The values are the wire contract: the bridge sends them and
/// the jslib maps them back to names by value.
/// </summary>
public enum EmotePlaybackEvent : byte
{
    Play = 0,
    Playing = 1,
    Pause = 2,
    Loop = 3,
    End = 4,
}

public class EmoteAnimationController : MonoBehaviour
{
    // Public because AvatarLoader samples the same clip to build the item view's blended pose, and a
    // second literal would let the two drift apart silently.
    public const string IDLE_CLIP_NAME = "Idle";

    // How far before its end an audio time is held, since the source rejects the exact end.
    private const float AUDIO_END_MARGIN = 0.01f;

    // One state rather than flags, so every entry point reads and writes the same rule.
    private enum PlaybackState
    {
        // No clip of ours runs: nothing loaded, or the rig is back on its idle.
        Idle,
        Playing,
        // Speed zero at some position; a play resumes.
        Paused,
        // Speed zero on the first frame; a play starts over.
        Held,
        // The clip ran out and may still be fading; a play starts over.
        Ended,
    }

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animation avatarAnimation;

    public bool HasAudio => _emoteAudioClip != null;

    /// <summary>
    /// True when a play should resume rather than start over.
    /// </summary>
    public bool IsPaused => _state == PlaybackState.Paused;

    /// <summary>
    /// Every playback change, with the clip time in seconds it happened at. Playing is reported
    /// once per frame while the clip runs.
    /// </summary>
    public event Action<EmotePlaybackEvent, float> PlaybackChanged;

    private LoadedEmote? _loadedEmote;
    // The prop and, for a social emote, the other avatar: each plays its own clip under the emote's
    // state name, shown while the emote runs and hidden once it leaves.
    private readonly List<(GameObject root, Animation animation)> _followers = new();
    private AudioClip _emoteAudioClip;
    private PlaybackState _state = PlaybackState.Idle;

    // How many times the clip has wrapped, so a loop is reported once per wrap.
    private int _reportedLoops;

    // Where a seek put a clip at speed zero, paused or held: one moved to its end reads 0 again
    // once Unity stops it there.
    private float? _stillSeek;

    /// <summary>
    /// Plays <paramref name="loadedEmote"/> from the start. <paramref name="partner"/> is the other
    /// avatar of a social emote, playing <paramref name="partnerAnimation"/> in step with it.
    /// </summary>
    public void PlayEmote(LoadedEmote loadedEmote, [CanBeNull] GameObject partner = null,
        [CanBeNull] Animation partnerAnimation = null)
    {
        // The previous emote's state would otherwise stay on the rig, bound to a clip its importer
        // has disposed.
        ClearEmote();

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

        SetFollowers(partner, partnerAnimation);
        _emoteAudioClip = _loadedEmote.Value.Audio;

        ReplayEmote();
    }

    /// <summary>
    /// Swaps the other avatar of the loaded social emote for a rebuilt one, which takes over the
    /// playback as it is: shown only while the emote is on screen, at its time and speed.
    /// </summary>
    public void ReplacePartner(GameObject partner, [CanBeNull] Animation partnerAnimation)
    {
        if (!_loadedEmote.HasValue) return;

        SetFollowers(partner, partnerAnimation);

        // Built hidden, which is right once the emote left or before it starts.
        if (_state is PlaybackState.Idle or PlaybackState.Ended) return;

        partner.SetActive(true);
        if (partnerAnimation == null) return;

        var urn = _loadedEmote.Value.Entity.URN;
        var avatarState = avatarAnimation[urn];

        partnerAnimation.Play();
        var partnerState = partnerAnimation[urn];
        if (partnerState == null) return;

        // A clip at speed zero seeked to its end reads 0 once Unity stops it there, so its seek wins.
        partnerState.time = (_state is PlaybackState.Paused or PlaybackState.Held ? _stillSeek : null)
                            ?? GetCurrentEmoteTime();
        partnerState.speed = avatarState != null ? avatarState.speed : 1f;
        partnerAnimation.Sample();
    }

    private void SetFollowers([CanBeNull] GameObject partner, [CanBeNull] Animation partnerAnimation)
    {
        _followers.Clear();

        if (_loadedEmote.HasValue && _loadedEmote.Value.Prop != null)
            _followers.Add((_loadedEmote.Value.Prop, _loadedEmote.Value.PropAnim));

        if (partner != null) _followers.Add((partner, partnerAnimation));
    }

    public float GetEmoteLength()
    {
        if (!_loadedEmote.HasValue) return 0f;
        return _loadedEmote.Value.Clip.length;
    }

    /// <summary>
    /// True while the clip advances: a paused, held, or ended clip is not playing.
    /// </summary>
    public bool IsEmotePlaying()
    {
        if (!_loadedEmote.HasValue) return false;
        return _state == PlaybackState.Playing && avatarAnimation.IsPlaying(_loadedEmote.Value.Entity.URN);
    }

    public void ReplayEmote()
    {
        if (!_loadedEmote.HasValue) return;

        _state = PlaybackState.Playing;
        _stillSeek = null;
        _reportedLoops = 0;

        // A pause or a stop left the speeds at zero.
        var urn = _loadedEmote.Value.Entity.URN;
        SetSpeed(urn, 1f);
        RestartFollowers();

        // Crossfade
        avatarAnimation.Rewind(urn);
        avatarAnimation.Play(urn);

        if (_loadedEmote.Value.Clip.wrapMode != WrapMode.Loop)
        {
            avatarAnimation.CrossFadeQueued(IDLE_CLIP_NAME, 0.3f);
        }

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Play, 0f);
    }

    public void PauseEmote()
    {
        if (!_loadedEmote.HasValue || _state != PlaybackState.Playing) return;

        _state = PlaybackState.Paused;
        _stillSeek = null;
        SetSpeed(_loadedEmote.Value.Entity.URN, 0f);
        audioSource.Pause();

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Pause, GetCurrentEmoteTime());
    }

    public void ResumeEmote()
    {
        if (!_loadedEmote.HasValue || _state != PlaybackState.Paused) return;

        _state = PlaybackState.Playing;
        SetSpeed(_loadedEmote.Value.Entity.URN, 1f);
        audioSource.UnPause();

        var time = _stillSeek ?? GetCurrentEmoteTime();
        _stillSeek = null;
        RestartFinishedAudio(time);
        PlaybackChanged?.Invoke(EmotePlaybackEvent.Play, time);
    }

    /// <summary>
    /// Holds the emote on its first frame with its audio stopped; the next play starts it over.
    /// <see cref="ReturnToIdle"/> leaves the emote instead.
    /// </summary>
    public void StopEmote()
    {
        if (!_loadedEmote.HasValue) return;

        var wasRunning = _state is PlaybackState.Playing or PlaybackState.Paused;
        _state = PlaybackState.Held;
        _stillSeek = null;

        // StopAll, so an idle cross-fade already queued behind the clip's end is dropped too.
        var urn = _loadedEmote.Value.Entity.URN;
        avatarAnimation.Play(urn, PlayMode.StopAll);
        Seek(urn, 0f);

        // The prop and the other avatar too, on their first frame: a clip that ran out left them hidden.
        RestartFollowers();
        SetSpeed(urn, 0f);
        audioSource.Stop();

        // A clip that already ended or was held reported its End then.
        if (wasRunning) PlaybackChanged?.Invoke(EmotePlaybackEvent.End, 0f);
    }

    /// <summary>
    /// Leaves the emote for the rig's idle clip, with its prop hidden and its audio stopped.
    /// </summary>
    public void ReturnToIdle(bool withCrossFade)
    {
        if (!_loadedEmote.HasValue) return;

        var time = GetCurrentEmoteTime();
        var wasRunning = _state is PlaybackState.Playing or PlaybackState.Paused;
        _state = PlaybackState.Ended;

        audioSource.Stop();
        HideFollowers();

        if (withCrossFade)
        {
            avatarAnimation.CrossFade(IDLE_CLIP_NAME, 0.3f, PlayMode.StopAll);
        }
        else
        {
            avatarAnimation.Play(IDLE_CLIP_NAME, PlayMode.StopAll);
        }

        // A clip that already ended or was held reported its End then.
        if (wasRunning) PlaybackChanged?.Invoke(EmotePlaybackEvent.End, time);
    }

    /// <summary>
    /// Moves the clip to <paramref name="seconds"/>. A paused or held clip stays so; a clip that
    /// ended or never ran starts over first, with the idle queued behind it as a play does.
    /// </summary>
    public void GoToEmote(float seconds)
    {
        if (!_loadedEmote.HasValue) return;

        // A negative time would count as a wrap, and one past the end would skip the clip's end event.
        seconds = Mathf.Clamp(seconds, 0f, _loadedEmote.Value.Clip.length);

        var urn = _loadedEmote.Value.Entity.URN;

        if (_state is PlaybackState.Idle or PlaybackState.Ended)
        {
            ReplayEmote();

            // The seek skips the clip's start event, which is what starts the audio, so it starts here.
            if (seconds > 0f)
            {
                Seek(urn, seconds);
                RestartFinishedAudio(seconds);
                PlaybackChanged?.Invoke(EmotePlaybackEvent.Playing, seconds);
            }

            return;
        }

        Seek(urn, seconds);
        if (_state is PlaybackState.Paused or PlaybackState.Held) _stillSeek = seconds;

        // A held or paused clip reports no position: it is not playing.
        if (_state != PlaybackState.Playing) return;

        RestartFinishedAudio(seconds);
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

    /// <summary>
    /// Leaves the emote and forgets it. The state is removed by name, so the clip object need not
    /// still exist.
    /// </summary>
    public void ClearEmote()
    {
        if (!_loadedEmote.HasValue) return;

        ReturnToIdle(false);
        avatarAnimation.RemoveClip(_loadedEmote.Value.Entity.URN);
        _loadedEmote = null;
        _emoteAudioClip = null;
        _followers.Clear();
        audioSource.clip = null;
        _state = PlaybackState.Idle;
        _stillSeek = null;
    }

    /// <summary>
    /// Splits a clip time that may have run past the length, as a looping clip's does, into the
    /// number of wraps and the time within the clip.
    /// </summary>
    internal static (int loops, float clipTime) WrapClipTime(float time, float length)
    {
        if (length <= 0f) return (0, 0f);

        var loops = Mathf.FloorToInt(time / length);
        return (loops, time - loops * length);
    }

    private void Update()
    {
        if (!_loadedEmote.HasValue || _state != PlaybackState.Playing) return;

        // A rig that cannot play reports nothing, rather than an end that never came.
        if (!avatarAnimation.isActiveAndEnabled) return;

        var urn = _loadedEmote.Value.Entity.URN;

        if (!avatarAnimation.IsPlaying(urn))
        {
            // A seek past the clip's end event skips it, so a clip that ran out without it ends here.
            EmoteEnded();
            return;
        }

        var state = avatarAnimation[urn];
        if (state == null || state.length <= 0f) return;

        var loops = WrapClipTime(state.time, state.length).loops;

        if (loops > _reportedLoops)
        {
            _reportedLoops = loops;

            if (_loadedEmote.Value.Clip.wrapMode == WrapMode.Loop)
            {
                PlaybackChanged?.Invoke(EmotePlaybackEvent.Loop, 0f);
            }
        }

        PlaybackChanged?.Invoke(EmotePlaybackEvent.Playing, GetCurrentEmoteTime());
    }

    // Moves the clip and its prop, keeps the audio in step, and re-bases the loop count so the next
    // wraps are neither skipped nor invented.
    private void Seek(string urn, float seconds)
    {
        var state = avatarAnimation[urn];
        if (state != null)
        {
            state.time = seconds;
            _reportedLoops = WrapClipTime(seconds, state.length).loops;
        }

        avatarAnimation.Sample();

        foreach (var (_, animation) in _followers)
        {
            var followerState = animation != null ? animation[urn] : null;
            if (followerState != null) followerState.time = seconds;
        }

        // Even when paused, so a resume continues the audio from the right place; a time past the
        // audio's end stops it rather than leaving the source on its last margin.
        if (_emoteAudioClip != null)
        {
            audioSource.clip = _emoteAudioClip;
            if (seconds < _emoteAudioClip.length) audioSource.time = AudioTime(seconds);
            else audioSource.Stop();
        }
    }

    // The source plays the clip itself rather than a one-shot, so pausing, seeking and resuming act
    // on it.
    private void PlayAudioFrom(float seconds)
    {
        if (_emoteAudioClip == null) return;

        audioSource.clip = _emoteAudioClip;
        audioSource.time = AudioTime(seconds);
        audioSource.Play();
    }

    // A source that is not playing (its clip played out, or a seek skipped the moment it starts) stays
    // stopped when only its time moves, so it is started at the position, if that is inside the audio.
    private void RestartFinishedAudio(float seconds)
    {
        if (_emoteAudioClip == null || audioSource.isPlaying || seconds >= _emoteAudioClip.length) return;

        PlayAudioFrom(seconds);
    }

    private float AudioTime(float seconds) =>
        Mathf.Clamp(seconds, 0f, Mathf.Max(0f, _emoteAudioClip.length - AUDIO_END_MARGIN));

    private void SetSpeed(string urn, float speed)
    {
        var state = avatarAnimation[urn];
        if (state != null) state.speed = speed;

        foreach (var (_, animation) in _followers)
        {
            var followerState = animation != null ? animation[urn] : null;
            if (followerState != null) followerState.speed = speed;
        }
    }

    // Within the clip: a looping one wraps however many times it ran, any other holds at its end.
    private float GetCurrentEmoteTime()
    {
        if (!_loadedEmote.HasValue) return 0f;

        var state = avatarAnimation[_loadedEmote.Value.Entity.URN];
        if (state == null) return 0f;

        return _loadedEmote.Value.Clip.wrapMode == WrapMode.Loop
            ? WrapClipTime(state.time, state.length).clipTime
            : Mathf.Min(state.time, state.length);
    }

    // Shown first: an inactive object takes no animation call.
    private void RestartFollowers()
    {
        foreach (var (root, animation) in _followers)
        {
            if (root == null) continue;

            root.SetActive(true);

            if (animation == null) continue;

            animation.Rewind();
            animation.Sample();
            animation.Play();
        }
    }

    private void HideFollowers()
    {
        foreach (var (root, _) in _followers)
        {
            if (root != null) root.SetActive(false);
        }
    }

    [UsedImplicitly]
    private void EmoteStarted()
    {
        PlayAudioFrom(0f);
        RestartFollowers();
    }

    [UsedImplicitly]
    private void EmoteEnded()
    {
        if (!_loadedEmote.HasValue || _loadedEmote.Value.Clip.wrapMode == WrapMode.Loop) return;
        if (_state != PlaybackState.Playing) return;

        _state = PlaybackState.Ended;
        HideFollowers();
        PlaybackChanged?.Invoke(EmotePlaybackEvent.End, _loadedEmote.Value.Clip.length);
    }
}
