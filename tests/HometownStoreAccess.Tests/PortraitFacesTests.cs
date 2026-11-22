using HometownStoreAccess.Core;
using Xunit;

namespace HometownStoreAccess.Tests
{
    /// <summary>Faces and story pictures in conversations, from the game's picture names (1.4.5).</summary>
    public class PortraitFacesTests
    {
        [Theory]
        [InlineData("Portrait_Collegian_Smile", "Collegian", "Smile")]
        [InlineData("Portrait_Otaku_Better_ ThinGlasse", "Otaku", "Better_ ThinGlasse")]
        [InlineData("Portrait_Kid", "Kid", "")]
        [InlineData("Portrait_Head Villager", "Head Villager", "")]
        [InlineData("Portrait _Placeholder", "Placeholder", "")]
        public void SplitsTheCharacterFromItsVariant(string picture, string character, string variant)
        {
            PortraitFaces.Split(picture, out string c, out string v);
            Assert.Equal(character, c);
            Assert.Equal(variant, v);
        }

        [Fact]
        public void TheSameCharacterWithAnotherPictureIsANewFace()
        {
            // The demo's own changes: the college student, and the otaku's makeover and back.
            Assert.Equal("face_surprised", PortraitFaces.ChangeKey("Portrait_Collegian_Smile", "Portrait_Collegian_Surprised"));
            Assert.Equal("face_smile", PortraitFaces.ChangeKey("Portrait_Collegian_Surprised", "Portrait_Collegian_Smile"));
            Assert.Equal("face_better_glasses", PortraitFaces.ChangeKey("Portrait_Otaku", "Portrait_Otaku_Better_ ThinGlasse"));
            Assert.Equal("face_usual", PortraitFaces.ChangeKey("Portrait_Otaku_Better_ ThinGlasse", "Portrait_Otaku"));
            // A variant the mod has no words for.
            Assert.Equal("face_changed", PortraitFaces.ChangeKey("Portrait_Monk", "Portrait_Monk_Angry"));
        }

        [Fact]
        public void APictureThatShowsAFaceSaysItWhenItAppears()
        {
            Assert.Equal("face_smile", PortraitFaces.ChangeKey(null, "Portrait_Collegian_Smile"));
            // Another character in the same place, with a face.
            Assert.Equal("face_smile", PortraitFaces.ChangeKey("Portrait_Student", "Portrait_Collegian_Smile"));
        }

        [Fact]
        public void PlainPicturesAndNoChangeSayNothing()
        {
            Assert.Null(PortraitFaces.ChangeKey(null, "Portrait_Kid"));
            Assert.Null(PortraitFaces.ChangeKey("Portrait_Kid", "Portrait_Somsee"));
            Assert.Null(PortraitFaces.ChangeKey("Portrait_Collegian_Smile", "Portrait_Collegian_Smile"));
            Assert.Null(PortraitFaces.ChangeKey("Portrait_Kid", null));
        }

        [Fact]
        public void AStoryPictureIsSaidWhenItAppearsOrChanges()
        {
            Assert.Equal("story_picture", PortraitFaces.StoryPictureKey(null, "CG_Festival"));
            Assert.Equal("story_picture", PortraitFaces.StoryPictureKey("CG_Festival", "CG_Night"));
            Assert.Null(PortraitFaces.StoryPictureKey("CG_Festival", "CG_Festival"));
            Assert.Null(PortraitFaces.StoryPictureKey("CG_Festival", null));
        }

        [Fact]
        public void AShakingPictureIsSaidAfterItsFaceEachByItsOwnSetting()
        {
            // The otaku's makeover line: his new look, and the picture shakes.
            Assert.Equal(new[] { "face_better_glasses", "face_shake" }, PortraitFaces.NoteKeys("Portrait_Otaku", "Portrait_Otaku_Better_ ThinGlasse", true, true, true));
            Assert.Equal(new[] { "face_shake" }, PortraitFaces.NoteKeys("Portrait_Otaku", "Portrait_Otaku_Better_ ThinGlasse", true, false, true));
            Assert.Equal(new[] { "face_better_glasses" }, PortraitFaces.NoteKeys("Portrait_Otaku", "Portrait_Otaku_Better_ ThinGlasse", true, true, false));
            // A plain picture that shakes; one that does not; none there.
            Assert.Equal(new[] { "face_shake" }, PortraitFaces.NoteKeys("Portrait_Kid", "Portrait_Kid", true, true, true));
            Assert.Empty(PortraitFaces.NoteKeys("Portrait_Kid", "Portrait_Kid", false, true, true));
            Assert.Empty(PortraitFaces.NoteKeys("Portrait_Kid", null, true, true, true));
        }

        [Fact]
        public void APortraitIsNamedByWhoSpeaksWithItMostOften()
        {
            var names = new PortraitNames();
            Assert.Null(names.NameOf("Collegian"));
            // The college student's lines, and one line of the player's over his undimmed picture.
            names.Learn("Collegian", "College Student");
            names.Learn("Collegian", "College Student");
            names.Learn("Collegian", "You");
            names.Learn("Collegian", "College Student");
            Assert.Equal("College Student", names.NameOf("Collegian"));
            Assert.Equal("College Student", names.NameOf("collegian"));
            names.Clear();
            Assert.Null(names.NameOf("Collegian"));
        }

        [Fact]
        public void EveryTextItCanSayExistsInEveryLanguage()
        {
            string[] keys = { "face_smile", "face_surprised", "face_calm", "face_better_glasses", "face_usual", "face_changed", "story_picture", "face_of", "set_pictures", "face_shake", "set_shaking" };
            foreach (string lang in Loc.SupportedLanguages)
            {
                Loc.SetLanguage(lang);
                try
                {
                    foreach (string k in keys) Assert.NotEqual(k, Loc.Get(k, "A", "B"));
                }
                finally
                {
                    Loc.SetLanguage("en");
                }
            }
        }
    }
}
