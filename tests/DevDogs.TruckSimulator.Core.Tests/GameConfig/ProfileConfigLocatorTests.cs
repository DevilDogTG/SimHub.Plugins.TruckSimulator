using DevDogs.TruckSimulator.Core.GameConfig;
using DevDogs.TruckSimulator.Core.Telemetry;

namespace DevDogs.TruckSimulator.Core.Tests.GameConfig;

public class ProfileConfigLocatorTests
{
    private const string Documents = @"C:\Users\me\Documents";
    private const string Steam = @"C:\Steam";
    private const string Tuay = "54756179";
    private const string DevilDogTG = "446576696C446F675447";

    private static readonly DateTime _t0 = new(2026, 11, 20, 21, 0, 0, DateTimeKind.Utc);

    private static readonly GameFolders _ets2 = GameFolders.For(TruckGame.Ets2, Documents, Steam)!;

    private static string Cloud(
        string account,
        string folder,
        int appId = 227300) => Path.Combine(Steam, "userdata", account, appId.ToString(), "remote", "profiles", folder, "config.cfg");

    private static string Local(string folder) =>
        Path.Combine(Documents, "Euro Truck Simulator 2", "profiles", folder, "config.cfg");

    [Theory]
    [InlineData("Tuay", "54756179")]
    [InlineData("DevilDogTG", "446576696C446F675447")]
    [InlineData("Łukasz", "C581756B61737A")]
    public void FolderName_IsUtf8Hex(
        string name,
        string expected) => ProfileConfigLocator.FolderName(name).Should().Be(expected);

    [Fact]
    public void For_KnowsBothGames()
    {
        GameFolders.For(TruckGame.Ets2, Documents, Steam).Should().Be(
            new GameFolders(Path.Combine(Documents, "Euro Truck Simulator 2"), Steam, 227300));
        GameFolders.For(TruckGame.Ats, Documents, Steam).Should().Be(
            new GameFolders(Path.Combine(Documents, "American Truck Simulator"), Steam, 270880));
        GameFolders.For(TruckGame.Unknown, Documents, Steam).Should().BeNull();
    }

    [Fact]
    public void GameLog_IsInDocumentsFolder() =>
        _ets2.GameLog.Should().Be(Path.Combine(Documents, "Euro Truck Simulator 2", "game.log.txt"));

    [Fact]
    public void Find_CloudProfile_PicksItsFolderNotTheNewestOther()
    {
        var fs = new FakeFileSystem()
            .File(Cloud("1", Tuay), _t0)
            .File(Cloud("1", DevilDogTG), _t0.AddHours(1));

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.SteamCloud), fs)
            .Should().Be(Cloud("1", Tuay));
    }

    [Fact]
    public void Find_FolderNameInLowerCase_StillMatches()
    {
        var lowerCase = DevilDogTG.ToLowerInvariant();
        var fs = new FakeFileSystem().File(Cloud("1", lowerCase), _t0);

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("DevilDogTG", ProfileStorage.SteamCloud), fs)
            .Should().Be(Cloud("1", lowerCase));
    }

    [Fact]
    public void Find_SameProfileUnderTwoSteamAccounts_PicksNewest()
    {
        var fs = new FakeFileSystem()
            .File(Cloud("111", Tuay), _t0)
            .File(Cloud("222", Tuay), _t0.AddMinutes(5));

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.SteamCloud), fs)
            .Should().Be(Cloud("222", Tuay));
    }

    [Fact]
    public void Find_CloudProfile_IgnoresOtherGamesAppId()
    {
        var fs = new FakeFileSystem().File(Cloud("1", Tuay, appId: 270880), _t0);

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.SteamCloud), fs).Should().BeNull();
    }

    [Fact]
    public void Find_LocalProfile_LooksOnlyInDocuments()
    {
        var fs = new FakeFileSystem()
            .File(Local(Tuay), _t0)
            .File(Cloud("1", Tuay), _t0.AddHours(1));

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.Local), fs).Should().Be(Local(Tuay));
    }

    [Fact]
    public void Find_UnknownStorage_LooksInBoth()
    {
        var fs = new FakeFileSystem()
            .File(Local(Tuay), _t0)
            .File(Cloud("1", Tuay), _t0.AddHours(1));

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.Unknown), fs).Should().Be(Cloud("1", Tuay));
    }

    [Fact]
    public void Find_NoProfile_PicksNewestConfigOfAll()
    {
        var fs = new FakeFileSystem()
            .File(Cloud("1", Tuay), _t0)
            .File(Local(DevilDogTG), _t0.AddMinutes(1))
            .File(Cloud("1", DevilDogTG), _t0.AddMinutes(2));

        ProfileConfigLocator.Find(_ets2, null, fs).Should().Be(Cloud("1", DevilDogTG));
    }

    [Fact]
    public void Find_ProfileFolderWithoutConfig_ReturnsNull()
    {
        var fs = new FakeFileSystem().Directory(Path.GetDirectoryName(Cloud("1", Tuay))!);

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.SteamCloud), fs).Should().BeNull();
    }

    [Fact]
    public void Find_NeverReadsSteamProfilesOrBackups()
    {
        var gameFolder = Path.Combine(Documents, "Euro Truck Simulator 2");
        var fs = new FakeFileSystem()
            .File(Path.Combine(gameFolder, "steam_profiles", Tuay, "config.cfg"), _t0)
            .File(Path.Combine(gameFolder, "steam_profiles(1.62.3.3s).bak", Tuay, "config.cfg"), _t0);

        ProfileConfigLocator.Find(_ets2, new ActiveProfile("Tuay", ProfileStorage.Unknown), fs).Should().BeNull();
        ProfileConfigLocator.Find(_ets2, null, fs).Should().BeNull();
    }

    [Fact]
    public void Find_NoSteam_StillFindsLocalProfile()
    {
        var folders = GameFolders.For(TruckGame.Ets2, Documents, steamRoot: null)!;
        var fs = new FakeFileSystem().File(Local(Tuay), _t0);

        ProfileConfigLocator.Find(folders, new ActiveProfile("Tuay", ProfileStorage.Unknown), fs).Should().Be(Local(Tuay));
    }

    /// <summary>
    /// An in-memory file tree: folders are implied by the files added.
    /// </summary>
    private sealed class FakeFileSystem : IFileSystemProbe
    {
        private readonly Dictionary<string, DateTime> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

        public FakeFileSystem File(
            string path,
            DateTime written)
        {
            _files[path] = written;
            return Directory(Path.GetDirectoryName(path)!);
        }

        public FakeFileSystem Directory(string path)
        {
            for (var dir = path; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                _directories.Add(dir);
            }

            return this;
        }

        public IEnumerable<string> GetDirectories(string path) =>
            _directories.Where(d => string.Equals(Path.GetDirectoryName(d), path, StringComparison.OrdinalIgnoreCase));

        public DateTime? GetLastWriteTimeUtc(string path) => _files.TryGetValue(path, out var t) ? t : null;
    }
}
