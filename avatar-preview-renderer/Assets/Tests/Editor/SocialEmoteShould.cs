using Data;
using NUnit.Framework;

namespace Tests
{
    public class SocialEmoteShould
    {
        // The clips of a published social emote, Rock Paper Scissors, in file order.
        private static readonly string[] CLIP_NAMES =
        {
            "RPS_Outcome1_Avatar", "RPS_Outcome2_Avatar", "RPS_Outcome3_Avatar", "RPS_Start",
            "RPS_Outcome1_AvatarOther", "RPS_Outcome2_AvatarOther", "RPS_Outcome3_AvatarOther",
            "RPS_Outcome1_Prop", "RPS_Outcome2_Prop", "RPS_Outcome3_Prop", "RPS_Start_Prop",
        };

        [Test]
        public void SelectTheClipsOfTheChosenOutcome()
        {
            // Arrange
            var animation = SocialEmoteAnimation.FromJson(
                "{\"title\":\"Tie\",\"loop\":false,\"audio\":\"tie.mp3\",\"Armature\":{\"animation\":\"RPS_Outcome3_Avatar\"},"
                + "\"Armature_Other\":{\"animation\":\"RPS_Outcome3_AvatarOther\"},\"Armature_Prop\":{\"animation\":\"RPS_Outcome3_Prop\"}}");

            // Act
            var (avatar, other, prop) = animation.SelectClips(CLIP_NAMES);

            // Assert
            Assert.AreEqual(2, avatar);
            Assert.AreEqual(6, other);
            Assert.AreEqual(9, prop);
        }

        [Test]
        public void KeepTheAvatarOffTheOtherAvatarsClipWhenMatchingByPrefix()
        {
            // Arrange: the avatar's prefix also starts the other avatar's clip.
            var animation = new SocialEmoteAnimation
            {
                Armature = new EmoteClip { animation = "Hug" },
                Armature_Other = new EmoteClip { animation = "Hug_Other" },
            };

            // Act
            var (avatar, other, prop) = animation.SelectClips(new[] { "Hug_Other_Clip", "Hug_Avatar" });

            // Assert
            Assert.AreEqual(1, avatar);
            Assert.AreEqual(0, other);
            Assert.AreEqual(-1, prop);
        }

        [Test]
        public void PlayTheStartAnimationAloneWithoutAnOtherAvatar()
        {
            // Arrange
            var animation = new SocialEmoteAnimation
            {
                Armature = new EmoteClip { animation = "RPS_Start" },
                Armature_Prop = new EmoteClip { animation = "RPS_Start_Prop" },
            };

            // Act
            var (avatar, other, prop) = animation.SelectClips(CLIP_NAMES);

            // Assert
            Assert.AreEqual(3, avatar);
            Assert.AreEqual(-1, other);
            Assert.AreEqual(10, prop);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"title\":\"no clips\"}")]
        public void RejectAnOptionThatIsNoSocialEmoteAnimation(string json)
        {
            // Act
            var animation = SocialEmoteAnimation.FromJson(json);

            // Assert
            Assert.IsNull(animation);
        }

        [Test]
        public void GiveAnEmoteWithoutOutcomesNoAnimationEvenWithAnOption()
        {
            // Arrange
            var chosen = new SocialEmoteAnimation { Armature = new EmoteClip { animation = "RPS_Outcome3_Avatar" } };

            // Act
            var animation = SocialEmoteAnimation.Resolve(null, chosen, 0);

            // Assert
            Assert.IsNull(animation);
        }

        [Test]
        public void PreferTheChosenAnimationOverARandomOutcome()
        {
            // Arrange
            var outcomes = new[] { new SocialEmoteAnimation(), new SocialEmoteAnimation() };
            var chosen = new SocialEmoteAnimation();

            // Act
            var animation = SocialEmoteAnimation.Resolve(outcomes, chosen, 1);

            // Assert
            Assert.AreSame(chosen, animation);
        }

        [Test]
        public void PlayTheRandomOutcomeWithoutAChoice()
        {
            // Arrange
            var outcomes = new[] { new SocialEmoteAnimation(), new SocialEmoteAnimation() };

            // Act
            var animation = SocialEmoteAnimation.Resolve(outcomes, null, 1);

            // Assert
            Assert.AreSame(outcomes[1], animation);
        }

        [Test]
        public void ReadAnOutcomeAsAnAnimation()
        {
            // Arrange
            var outcome = new OutcomeGroup
            {
                title = "Hug",
                loop = true,
                audio = "hug.mp3",
                clips = new OutcomeClips
                {
                    Armature = new EmoteClip { animation = "Friends_Outcome1_Avatar" },
                    Armature_Other = new EmoteClip { animation = "Friends_Outcome1_AvatarOther" },
                },
            };

            // Act
            var animation = SocialEmoteAnimation.FromOutcome(outcome);

            // Assert
            Assert.AreEqual("Hug", animation.title);
            Assert.IsTrue(animation.loop);
            Assert.AreEqual("hug.mp3", animation.audio);
            Assert.AreEqual("Friends_Outcome1_Avatar", animation.AvatarAnimation);
            Assert.AreEqual("Friends_Outcome1_AvatarOther", animation.OtherAnimation);
            Assert.IsNull(animation.PropAnimation);
        }
    }
}
