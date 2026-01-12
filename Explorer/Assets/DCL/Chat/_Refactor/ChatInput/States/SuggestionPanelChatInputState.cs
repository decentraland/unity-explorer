using DCL.Audio;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Emoji;
using DCL.Profiles;
using DCL.UI.CustomInputField;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SuggestionPanel;
using MVC;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Pool;

namespace DCL.Chat.ChatInput
{
    public class SuggestionPanelChatInputState : IndependentMVCState, IDisposable
    {
        private readonly ChatInputView chatInputView;
        private readonly EmojiMapping emojiMapping;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly GetParticipantProfilesCommand getParticipantProfilesCommand;
        private static readonly Regex EMOJI_PATTERN_REGEX = new (@"(?<!https?:)(:\w{2,10})", RegexOptions.Compiled);
        private static readonly Regex PROFILE_PATTERN_REGEX = new (@"(?:^|\s)@([A-Za-z0-9]{1,15})(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex PRE_MATCH_PATTERN_REGEX = new (@"(?<=^|\s)([@:]\S+)$", RegexOptions.Compiled);

        private readonly InputSuggestionPanelController suggestionPanelController;
        private readonly CustomInputField inputField;
        private readonly ChatClickDetectionHandler clickDetectionHandler;

        private readonly Dictionary<string, ProfileInputSuggestionData> profileSuggestionsDictionary = new ();
        private readonly Dictionary<string, EmojiInputSuggestionData> emojiSuggestionsDictionary;

        private readonly List<Profile.CompactInfo> participantProfiles = new (100);

        private int wordMatchIndex;
        private Match lastMatch = Match.Empty;

        public SuggestionPanelChatInputState(ChatInputView chatInputView, EmojiMapping emojiMapping, ProfileRepositoryWrapper profileRepositoryWrapper, GetParticipantProfilesCommand getParticipantProfilesCommand)
        {
            this.chatInputView = chatInputView;
            this.emojiMapping = emojiMapping;
            this.profileRepositoryWrapper = profileRepositoryWrapper;
            this.getParticipantProfilesCommand = getParticipantProfilesCommand;

            suggestionPanelController = new InputSuggestionPanelController(chatInputView.suggestionPanel);
            clickDetectionHandler = new ChatClickDetectionHandler(chatInputView.suggestionPanel.transform);
            clickDetectionHandler.OnClickOutside += Deactivate;
            clickDetectionHandler.Pause();

            inputField = chatInputView.inputField;

            emojiSuggestionsDictionary = new Dictionary<string, EmojiInputSuggestionData>(emojiMapping.NameMapping.Count);

            foreach (KeyValuePair<string, EmojiData> pair in emojiMapping.NameMapping)
                emojiSuggestionsDictionary.Add(pair.Key, new EmojiInputSuggestionData(pair.Value.EmojiCode, pair.Value.EmojiName));
        }

        public void Dispose()
        {
            suggestionPanelController.Dispose();
            clickDetectionHandler.Dispose();
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

                if (lastMatch.Success) return true;

                //If we don't find any emoji pattern only then we look for username patterns

                UpdateProfileNameMap();
                lastMatch = suggestionPanelController.HandleSuggestionsSearch(wordMatch.Value, PROFILE_PATTERN_REGEX, InputSuggestionType.PROFILE, profileSuggestionsDictionary);

                if (lastMatch.Success) return true;
            }

            lastMatch = Match.Empty;
            return false;
        }

        internal void ReplaceSuggestionInText(string suggestion)
        {
            //Assert.IsTrue(lastMatch.Success);

            if (lastMatch.Success)
            {
                if (!inputField.IsWithinCharacterLimit(suggestion.Length - lastMatch.Groups[1].Length)) return;

                UIAudioEventsBus.Instance.SendPlayAudioEvent(chatInputView.emojiContainer.addEmojiAudio);
                int replaceAmount = lastMatch.Groups[1].Length;
                int replaceAt = wordMatchIndex + lastMatch.Groups[1].Index;

                inputField.ReplaceTextAtPosition(replaceAt, replaceAmount, suggestion);
                chatInputView.RefreshCharacterCount();
            }

            // TODO It's here because the input field needs to be focused again after losing focus
            chatInputView.SelectInputField();
        }

        protected override void Activate()
        {
            suggestionPanelController.SetPanelVisibility(true);
            inputField.UpAndDownArrowsEnabled = false;
            clickDetectionHandler.Resume();
        }

        protected override void Deactivate()
        {
            suggestionPanelController.SetPanelVisibility(false);
            inputField.UpAndDownArrowsEnabled = true;
            lastMatch = Match.Empty;
            clickDetectionHandler.Pause();
        }

        private void UpdateProfileNameMap()
        {
            getParticipantProfilesCommand.Execute(participantProfiles);

            List<KeyValuePair<string, ProfileInputSuggestionData>>? profileSuggestions = ListPool<KeyValuePair<string, ProfileInputSuggestionData>>.Get();
            profileSuggestions.AddRange(profileSuggestionsDictionary);

            for (int index = 0; index < profileSuggestions.Count; index++)
            {
                KeyValuePair<string, ProfileInputSuggestionData> suggestion = profileSuggestions[index];
                bool isThereProfileForSuggestion = participantProfiles.FindIndex(profile => profile.UserId == suggestion.Value.GetId()) > -1;

                if (!isThereProfileForSuggestion)
                    profileSuggestionsDictionary.Remove(suggestion.Key);
            }

            profileSuggestions.Clear();
            ListPool<KeyValuePair<string, ProfileInputSuggestionData>>.Release(profileSuggestions);

            //We add or update the remaining participants
            foreach (Profile.CompactInfo profile in participantProfiles)
            {
                if (profileSuggestionsDictionary.TryGetValue(profile.DisplayName, out ProfileInputSuggestionData profileSuggestionData))
                {
                    if (!profileSuggestionData.ProfileData.Equals(profile))
                        profileSuggestionsDictionary[profile.DisplayName] = new ProfileInputSuggestionData(profile, profileRepositoryWrapper);
                }
                else profileSuggestionsDictionary.TryAdd(profile.DisplayName, new ProfileInputSuggestionData(profile, profileRepositoryWrapper));
            }
        }
    }
}
