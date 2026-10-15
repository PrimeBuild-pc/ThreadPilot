namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class GameBoostProcessListsTests
    {
        [Theory]
        [InlineData("Cyberpunk2077", @"D:\SteamLibrary\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe", true)]
        [InlineData("FortniteClient-Win64-Shipping", @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe", true)]
        [InlineData("Forza", @"C:\XboxGames\Forza Horizon 5\Content\ForzaHorizon5.exe", true)]
        [InlineData("UnityCrashHandler64", @"D:\SteamLibrary\steamapps\common\Game\UnityCrashHandler64.exe", false)]
        [InlineData("EasyAntiCheat_EOS_Setup", @"D:\SteamLibrary\steamapps\common\Game\EasyAntiCheat\EasyAntiCheat_EOS_Setup.exe", false)]
        [InlineData("RiotClientServices", @"C:\Riot Games\Riot Client\RiotClientServices.exe", false)]
        [InlineData("GameLauncher", @"D:\SteamLibrary\steamapps\common\Game\GameLauncher.exe", false)]
        [InlineData("steam", @"C:\Program Files (x86)\Steam\steam.exe", false)]
        [InlineData("chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", false)]
        public void IsGame_FromLibraryPath(string name, string path, bool expected) =>
            Assert.Equal(expected, GameBoostProcessLists.IsGame(name, path, manualGames: null, excludedGames: null));

        [Fact]
        public void IsGame_ManualEntryByNameOrPath()
        {
            const string path = @"C:\Program Files (x86)\World of Warcraft\_retail_\Wow.exe";

            Assert.True(GameBoostProcessLists.IsGame("Wow", path, ["wow.exe"], null));
            Assert.True(GameBoostProcessLists.IsGame("Wow", path, [path], null));
            Assert.False(GameBoostProcessLists.IsGame("Wow", path, [@"D:\Other\Wow.exe"], null));
        }

        [Fact]
        public void IsGame_ExclusionWinsOverLibraryAndManual()
        {
            const string path = @"D:\SteamLibrary\steamapps\common\Game\Game.exe";

            Assert.False(GameBoostProcessLists.IsGame("Game", path, ["Game"], ["Game.exe"]));
        }

        [Fact]
        public void IsListedBackground_IsolationAddsOnlyTheExtraList()
        {
            var userList = GameBoostProcessLists.DefaultBackgroundProcesses.ToList();

            Assert.True(GameBoostProcessLists.IsListedBackground("chrome", null, GameBoostMode.Automatic, userList));
            Assert.False(GameBoostProcessLists.IsListedBackground("Spotify", null, GameBoostMode.Automatic, userList));
            Assert.True(GameBoostProcessLists.IsListedBackground("Spotify", null, GameBoostMode.Isolation, userList));
            Assert.False(GameBoostProcessLists.IsListedBackground("Discord", null, GameBoostMode.Isolation, userList));
            Assert.False(GameBoostProcessLists.IsListedBackground("obs64", null, GameBoostMode.Isolation, userList));
        }

        [Fact]
        public void IsListedBackground_UserAddedEntry()
        {
            Assert.True(GameBoostProcessLists.IsListedBackground("MyTool", null, GameBoostMode.Automatic, ["MyTool.exe"]));
            Assert.True(GameBoostProcessLists.IsListedBackground("MyTool", @"C:\Tools\MyTool.exe", GameBoostMode.Automatic, [@"C:\Tools\MyTool.exe"]));
            Assert.False(GameBoostProcessLists.IsListedBackground("MyTool", @"D:\Other\MyTool.exe", GameBoostMode.Automatic, [@"C:\Tools\MyTool.exe"]));
        }
    }
}
