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

        [Theory]
        [InlineData("Portrait_Head Villager", "headvillager")]
        [InlineData("CharacterPortrait_HeadVillager", "headvillager")]
        [InlineData("Portrait_Collegian_Smile", "collegian")]
        [InlineData("CharacterPortrait_Kid", "kid")]
        public void TheCharacterOfAPictureOrABookEntry(string name, string key)
        {
            Assert.Equal(key, PortraitFaces.CharacterKey(name));
        }

        [Fact]
        public void SameCharacterAcrossFacesAndTheBook()
        {
            Assert.True(PortraitFaces.SameCharacter("Portrait_Collegian_Smile", "Portrait_Collegian_Surprised"));
            Assert.True(PortraitFaces.SameCharacter("CharacterPortrait_Kid", "Portrait_Kid"));
            Assert.False(PortraitFaces.SameCharacter("Portrait_Kid", "Portrait_Somsee"));
            Assert.Equal("Street Racer", PortraitFaces.SpacedWords("StreetRacer"));
            Assert.Equal("Collegian", PortraitFaces.SpacedWords("Collegian"));
            Assert.True(PortraitFaces.HasFace(new[] { "face_smile", "face_shake" }));
            Assert.False(PortraitFaces.HasFace(new[] { "face_shake" }));
        }

        [Fact]
        public void APortraitIsNamedByWhoSpeaksWithItMostOften()
        {
            var names = new PortraitNames();
            Assert.Null(names.NameOf("collegian"));
            // The college student's lines with his picture lit; one of the player's over it.
            names.LearnLine("College Student", "collegian");
            names.LearnLine("College Student", "collegian");
            names.LearnLine("You", "collegian");
            names.LearnLine("College Student", "collegian");
            Assert.Equal("College Student", names.NameOf("collegian"));
            names.Clear();
            Assert.Null(names.NameOf("collegian"));
        }

        [Fact]
        public void AConversationsOwnNameForACharacterComesFirst()
        {
            // Everywhere he is "Uncle Kanit"; at the opening ceremony "Brother Kanit".
            var all = new PortraitNames();
            all.LearnLine("Uncle Kanit", "kanit");
            all.LearnLine("Uncle Kanit", "kanit");
            all.LearnLine("Brother Kanit", "kanit");
            var here = new PortraitNames();
            here.LearnLine("Brother Kanit", "kanit");
            Assert.Equal("Uncle Kanit", all.NameOf("kanit"));
            Assert.Equal("Brother Kanit", here.NameOf("kanit", all.IsGeneric));
            Assert.True(here.HasHeard("kanit", "Brother Kanit"));
            Assert.False(here.HasHeard("kanit", "Uncle Kanit"));
        }

        [Fact]
        public void ThePlayerAndTheNarratorAreNeverACharactersName()
        {
            var names = new PortraitNames();
            // The narrator mostly speaks with no picture lit; once over the gamer's picture.
            names.LearnLine("Narration", null);
            names.LearnLine("Narration", null);
            names.LearnLine("Narration", null);
            names.LearnLine("Narration", "gamer");
            // The player mostly speaks while the others are dimmed.
            names.LearnLine("You", null);
            names.LearnLine("You", null);
            names.LearnLine("You", "kid");
            names.LearnLine("Naughty Kid", "kid");
            Assert.True(names.IsGeneric("Narration"));
            Assert.True(names.IsGeneric("You"));
            Assert.False(names.IsGeneric("Naughty Kid"));
            Assert.Null(names.NameOf("gamer"));
            Assert.Equal("Naughty Kid", names.NameOf("kid"));
        }

        [Fact]
        public void EveryTextItCanSayExistsInEveryLanguage()
        {
            string[] keys = { "face_smile", "face_surprised", "face_calm", "face_better_glasses", "face_usual", "face_changed", "story_picture", "face_of", "set_pictures", "face_shake", "set_shaking", "face_appears", "character_number" };
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
