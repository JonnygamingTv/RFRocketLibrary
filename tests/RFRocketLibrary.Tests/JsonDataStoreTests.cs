using System;
using System.IO;
using System.Threading.Tasks;
using RFRocketLibrary.Storages;
using Xunit;

namespace RFRocketLibrary.Tests
{
    public class JsonDataStoreTests : IDisposable
    {
        private readonly string _dir;

        public JsonDataStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rfrl-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        private class SampleData
        {
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        [Fact]
        public async Task SaveThenLoad_RoundTrips()
        {
            var store = new JsonDataStore<SampleData>(_dir, "data.json");

            Assert.True(await store.SaveAsync(new SampleData {Name = "alpha", Count = 7}));
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal("alpha", loaded!.Name);
            Assert.Equal(7, loaded.Count);
        }

        [Fact]
        public void Save_ReplacesPreviousContent()
        {
            var store = new JsonDataStore<SampleData>(_dir, "data.json");

            Assert.True(store.Save(new SampleData {Name = "one", Count = 1}));
            Assert.True(store.Save(new SampleData {Name = "two", Count = 2}));

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal("two", loaded!.Name);
        }

        [Fact]
        public void Save_LeavesNoTempFileBehind()
        {
            var store = new JsonDataStore<SampleData>(_dir, "data.json");

            Assert.True(store.Save(new SampleData {Name = "x", Count = 1}));

            Assert.False(File.Exists(Path.Combine(_dir, "data.json.tmp")));
            Assert.True(File.Exists(Path.Combine(_dir, "data.json")));
        }

        [Fact]
        public void Load_CorruptedFile_ReturnsNullInsteadOfThrowing()
        {
            var path = Path.Combine(_dir, "corrupt.json");
            File.WriteAllText(path, "{ this is not valid json");

            var store = new JsonDataStore<SampleData>(_dir, "corrupt.json");

            var loaded = store.Load();
            Assert.Null(loaded);
        }

        [Fact]
        public void Load_MissingFile_ReturnsNull()
        {
            var store = new JsonDataStore<SampleData>(_dir, "missing.json");
            Assert.Null(store.Load());
        }
    }
}
