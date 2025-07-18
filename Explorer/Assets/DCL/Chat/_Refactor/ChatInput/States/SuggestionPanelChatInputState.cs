using DCL.Audio;
using DCL.Emoji;
using DCL.Profiles;
using DCL.UI.CustomInputField;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Assertions;
using UnityEngine.Pool;

namespace DCL.Chat
{
    public class SuggestionPanelChatInputState : IndependentMVCState<ChatInputStateContext>, IDisposable
    {
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,10})", RegexOptions.Compiled);
        private static readonly Regex PROFILE_PATTERN_REGEX = new (@"(?:^|\s)@([A-Za-z0-9]{1,15})(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex PRE_MATCH_PATTERN_REGEX = new (@"(?<=^|\s)([@:]\S+)$", RegexOptions.Compiled);

        private readonly InputSuggestionPanelController suggestionPanelController;
        private readonly CustomInputField inputField;
        private readonly ChatClickDetectionService clickDetection;

        private readonly Dictionary<string, ProfileInputSuggestionData> profileSuggestionsDictionary = new ();
        private readonly Dictionary<string, EmojiInputSuggestionData> emojiSuggestionsDictionary;

        private readonly List<Profile> participantProfiles = new (100);

        private int wordMatchIndex;
        private Match lastMatch = Match.Empty;

        public SuggestionPanelChatInputState(ChatInputStateContext context) : base(context)
        {
            suggestionPanelController = new InputSuggestionPanelController(context.ChatInputView.suggestionPanel);
            clickDetection = new ChatClickDetectionService(context.ChatInputView.suggestionPanel.transform);
            inputField = context.ChatInputView.inputField;

            emojiSuggestionsDictionary = new Dictionary<string, EmojiInputSuggestionData>(context.EmojiNameMapping.Count);

            foreach (KeyValuePair<string, EmojiData> pair in context.EmojiNameMapping)
                emojiSuggestionsDictionary.Add(pair.Key, new EmojiInputSuggestionData(pair.Value.EmojiCode, pair.Value.EmojiName));
        }

        internal bool TryFindMatch(string inputText)
        {
            //With this we are detecting only the last word (where the current caret position is) and checking for matches there.
            //This regex already pre-matches the starting patterns for both Emoji ":" and Profile "@" patterns, and only sends the match further to validate other specific conditions
            //This is needed because otherwise we wouldn't know which word in the whole text we are trying to match, and if there were several potential matches
            //it would always capture the first one instead of the current one.
            Match wordMatch = PRE_MATCH_PATTERN_REGEX.Match(inputText, 0, inputField.stringPosition);

            if (wordMatch.Success)
            {
                wordMatchIndex = wordMatch.Index;
                lastMatch = suggestionPanelController.HandleSuggestionsSearch(wordMatch.Value, EMOJI_PATTERN_REGEX, InputSuggestionType.EMOJIS, emojiSuggestionsDictionary);

                //If we don't find any emoji pattern only then we look for username patterns
                if (!lastMatch.Success)
                {
                    UpdateProfileNameMap();
                    lastMatch = suggestionPanelController.HandleSuggestionsSearch(wordMatch.Value, PROFILE_PATTERN_REGEX, InputSuggestionType.PROFILE, profileSuggestionsDictionary);
                }

                return true;
            }

            lastMatch = Match.Empty;
            return false;
        }

        internal void ReplaceSuggestionInText(string suggestion)
        {
            Assert.IsTrue(lastMatch.Success);

            if (!inputField.IsWithinCharacterLimit(suggestion.Length - lastMatch.Groups[1].Length)) return;

            UIAudioEventsBus.Instance.SendPlayAudioEvent(context.ChatInputView.addEmojiAudio);
            int replaceAmount = lastMatch.Groups[1].Length;
            int replaceAt = wordMatchIndex + lastMatch.Groups[1].Index;

            inputField.ReplaceTextAtPosition(replaceAt, replaceAmount, suggestion);

            // TODO It's here because the input field needs to be focused again after losing focus
            context.ChatInputView.SetActiveTyping();
        }

        protected override void Activate(ControllerNoData input)
        {
            suggestionPanelController.SetPanelVisibility(true);
            inputField.UpAndDownArrowsEnabled = false;
            clickDetection.OnClickOutside += Deactivate;
        }

        protected override void Deactivate()
        {
            suggestionPanelController.SetPanelVisibility(false);
            inputField.UpAndDownArrowsEnabled = true;
            clickDetection.OnClickOutside -= Deactivate;
            lastMatch = Match.Empty;
        }

        private void UpdateProfileNameMap()
        {
            context.GetParticipantProfilesCommand.Execute(participantProfiles);

            List<KeyValuePair<string, ProfileInputSuggestionData>>? profileSuggestions = ListPool<KeyValuePair<string, ProfileInputSuggestionData>>.Get();
            profileSuggestions.AddRange(profileSuggestionsDictionary);

            for (var index = 0; index < profileSuggestions.Count; index++)
            {
                KeyValuePair<string, ProfileInputSuggestionData> suggestion = profileSuggestions[index];
                bool isThereProfileForSuggestion = participantProfiles.FindIndex(profile => profile.UserId == suggestion.Value.GetId()) > -1;

                if (!isThereProfileForSuggestion)
                    profileSuggestionsDictionary.Remove(suggestion.Key);
            }

            profileSuggestions.Clear();
            ListPool<KeyValuePair<string, ProfileInputSuggestionData>>.Release(profileSuggestions);

            //We add or update the remaining participants
            foreach (Profile? profile in participantProfiles)
            {
                if (profile != null)
                {
                    if (profileSuggestionsDictionary.TryGetValue(profile.DisplayName, out ProfileInputSuggestionData profileSuggestionData))
                    {
                        if (profileSuggestionData.ProfileData != profile)
                            profileSuggestionsDictionary[profile.DisplayName] = new ProfileInputSuggestionData(profile, context.ProfileRepositoryWrapper);
                    }
                    else { profileSuggestionsDictionary.TryAdd(profile.DisplayName, new ProfileInputSuggestionData(profile, context.ProfileRepositoryWrapper)); }
                }
            }
        }

        public void Dispose()
        {
            suggestionPanelController.Dispose();
        }
    }
}
