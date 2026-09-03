using System.Collections.Generic;
using Granny.Core;
using NUnit.Framework;

namespace Granny.Tests
{
    /// <summary>
    /// Saving is what makes a five-day run survive quitting the game, so a
    /// silently unreadable file is the kind of bug that only shows up after
    /// someone has lost four days of progress.
    /// </summary>
    public class SaveSystemTests
    {
        [SetUp]
        public void SetUp() => SaveSystem.Delete();

        [TearDown]
        public void TearDown()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            SaveSystem.Delete();
        }

        static SaveData MakeRun() => new()
        {
            difficultyName = "Hard",
            day = 3,
            heldItemId = "hammer",
            pocketItemIds = new List<string> { "key.front", string.Empty, "battery" },
            clearedLockIds = new List<string> { "Plank" },
            searchedContainerIds = new List<string> { "Dresser_0", "Dresser_4" },
        };

        [Test]
        public void NoSaveExistsToBeginWith()
        {
            Assert.IsFalse(SaveSystem.HasSave);
            Assert.IsNull(SaveSystem.Load());
        }

        [Test]
        public void ARunSurvivesARoundTrip()
        {
            SaveSystem.Save(MakeRun());

            Assert.IsTrue(SaveSystem.HasSave);
            var loaded = SaveSystem.Load();

            Assert.IsNotNull(loaded);
            Assert.AreEqual("Hard", loaded.difficultyName);
            Assert.AreEqual(3, loaded.day);
            Assert.AreEqual("hammer", loaded.heldItemId);
            CollectionAssert.AreEqual(new[] { "key.front", string.Empty, "battery" }, loaded.pocketItemIds);
            CollectionAssert.AreEqual(new[] { "Plank" }, loaded.clearedLockIds);
            CollectionAssert.AreEqual(new[] { "Dresser_0", "Dresser_4" }, loaded.searchedContainerIds);
        }

        [Test]
        public void SavingTwiceKeepsOnlyTheNewerRun()
        {
            SaveSystem.Save(MakeRun());

            var later = MakeRun();
            later.day = 5;
            SaveSystem.Save(later);

            Assert.AreEqual(5, SaveSystem.Load().day);
        }

        [Test]
        public void AFileFromAnOlderBuildIsRejectedRatherThanMisread()
        {
            var stale = MakeRun();
            stale.version = SaveData.CurrentVersion - 1;

            SaveSystem.Save(stale);

            // Save stamps the current version on write, so the rejection has to be
            // provoked by editing the file itself.
            var path = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "run.json");
            var json = System.IO.File.ReadAllText(path)
                .Replace($"\"version\": {SaveData.CurrentVersion}", "\"version\": 0");
            System.IO.File.WriteAllText(path, json);

            Assert.IsNull(SaveSystem.Load(), "An incompatible save must be refused, not partially read.");
        }

        [Test]
        public void CorruptJsonDoesNotThrow()
        {
            // Loading a mangled file logs an error by design, and the test runner
            // treats a logged error as a failure, so it has to be waived up front.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            var path = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "run.json");
            System.IO.File.WriteAllText(path, "{ this is not json");

            SaveData loaded = null;
            Assert.DoesNotThrow(() => loaded = SaveSystem.Load());
            Assert.IsNull(loaded);
        }

        [Test]
        public void SavingNullIsIgnored()
        {
            Assert.DoesNotThrow(() => SaveSystem.Save(null));
            Assert.IsFalse(SaveSystem.HasSave);
        }

        [Test]
        public void DeleteRemovesTheRun()
        {
            SaveSystem.Save(MakeRun());
            Assert.IsTrue(SaveSystem.HasSave);

            SaveSystem.Delete();

            Assert.IsFalse(SaveSystem.HasSave);
            Assert.IsNull(SaveSystem.Load());
        }
    }
}
