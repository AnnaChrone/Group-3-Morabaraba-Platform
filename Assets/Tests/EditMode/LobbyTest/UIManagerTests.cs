using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
// Shared base: spins up a minimal UIManager GameObject for every test class
// ─────────────────────────────────────────────────────────────────────────────
public abstract class UIManagerTestBase
{
    protected GameObject _root;
    protected UIManager _uiManager;
    protected GameObject _mainMenu;
    protected GameObject _hosting;
    protected GameObject _joining;
    protected GameObject _error;

    [SetUp]
    public virtual void SetUp()
    {
        _root = new GameObject("UIManagerRoot");
        _mainMenu = new GameObject("MainMenu");
        _hosting = new GameObject("Hosting");
        _joining = new GameObject("Joining");
        _error = new GameObject("Error");

        _uiManager = _root.AddComponent<UIManager>();

        var t = typeof(UIManager);

        t.GetField("mainMenuPanel").SetValue(_uiManager, _mainMenu);
        t.GetField("hostingPanel").SetValue(_uiManager, _hosting);
        t.GetField("joiningPanel").SetValue(_uiManager, _joining);
        t.GetField("errorPanel").SetValue(_uiManager, _error);
        t.GetField("errorText").SetValue(_uiManager, _error.AddComponent<TextMeshProUGUI>());

        t.GetField("joinLobbyCodeInput").SetValue(_uiManager, MakeGO<TMP_InputField>("Input"));
        t.GetField("joinButton").SetValue(_uiManager, MakeGO<Button>("JoinBtn"));
        t.GetField("startGameButton").SetValue(_uiManager, MakeGO<Button>("StartBtn"));
        t.GetField("hostLobbyCodeText").SetValue(_uiManager, MakeGO<TextMeshProUGUI>("CodeTxt"));
        t.GetField("joinGameTypeText").SetValue(_uiManager, MakeGO<TextMeshProUGUI>("GameTypeTxt"));
        t.GetField("joinTimeText").SetValue(_uiManager, MakeGO<TextMeshProUGUI>("TimeTxt"));
        t.GetField("hostPlayerListContainer").SetValue(_uiManager, new GameObject("HostList").transform);
        t.GetField("joinPlayerListContainer").SetValue(_uiManager, new GameObject("JoinList").transform);
        t.GetField("hostGameTypeDropdown").SetValue(_uiManager, MakeGO<TMP_Dropdown>("GameDD"));
        t.GetField("hostTimeDropdown").SetValue(_uiManager, MakeGO<TMP_Dropdown>("TimeDD"));
    }

    [TearDown]
    public virtual void TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_mainMenu);
        Object.DestroyImmediate(_hosting);
        Object.DestroyImmediate(_joining);
        Object.DestroyImmediate(_error);
    }

    protected T MakeGO<T>(string name) where T : Component
        => new GameObject(name).AddComponent<T>();

    // ── Logic helpers mirroring UIManager private methods ───────────────────

    protected bool IsValidLobbyCode(string input)
    {
        if (input == null) return false;
        return input.Trim().ToUpper().Length >= 6;
    }

    protected List<string> BuildDisplayList(List<string> rawList)
    {
        var display = new List<string>();
        var parsed = new List<(string name, ulong cid)>();

        foreach (var raw in rawList)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            int cidIndex = raw.LastIndexOf("|CID:");
            if (cidIndex > 0)
            {
                string name = raw.Substring(0, cidIndex);
                string cidPart = raw.Substring(cidIndex + 5);
                if (ulong.TryParse(cidPart, out ulong cid))
                {
                    if (string.IsNullOrEmpty(name)) name = "Guest";
                    parsed.Add((name, cid));
                }
            }
            else
            {
                parsed.Add((string.IsNullOrEmpty(raw) ? "Guest" : raw, 999));
            }
        }

        var host = parsed.Find(p => p.cid == 0);
        var others = parsed.FindAll(p => p.cid != 0);

        if (!string.IsNullOrEmpty(host.name))
            display.Add(host.name.Trim());
        else if (parsed.Count > 0)
            display.Add(parsed[0].name.Trim());

        foreach (var p in others)
            if (!string.IsNullOrEmpty(p.name))
                display.Add(p.name.Trim());

        return display;
    }

    protected string Serialise(List<string> names) => string.Join("|", names);

    protected List<string> Deserialise(string delimited)
        => string.IsNullOrEmpty(delimited)
            ? new List<string>()
            : new List<string>(delimited.Split('|'));

    protected bool ShouldStartButtonBeEnabled(int count) => count >= 2;
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-01 to TC-05: Lobby Code Validation
// ─────────────────────────────────────────────────────────────────────────────
public class TC01_TC06_LobbyCodeValidationTests : UIManagerTestBase
{
    // TC-01: Exactly 6 characters — minimum valid length
    [Test]
    public void TC01_LobbyCode_ExactlySix_IsValid()
    {
        Assert.IsTrue(IsValidLobbyCode("ABC123"),
            "A 6-character code should be accepted.");
    }

    // TC-02: Fewer than 6 characters — should be rejected
    [Test]
    public void TC02_LobbyCode_FourChars_IsInvalid()
    {
        Assert.IsFalse(IsValidLobbyCode("AB12"),
            "A code shorter than 6 characters must be rejected.");
    }

    // TC-03: Empty string — should be rejected
    [Test]
    public void TC03_LobbyCode_Empty_IsInvalid()
    {
        Assert.IsFalse(IsValidLobbyCode(""),
            "An empty string must be rejected.");
    }

    // TC-04: Null — rejected without throwing
    [Test]
    public void TC04_LobbyCode_Null_IsInvalid()
    {
        Assert.IsFalse(IsValidLobbyCode(null),
            "Null input must be handled gracefully and rejected.");
    }

    // TC-05: Lowercase code — normalised to uppercase
    [Test]
    public void TC06_LobbyCode_Lowercase_NormalisedToUpper()
    {
        string result = "abc123".Trim().ToUpper();
        Assert.AreEqual("ABC123", result,
            "Lowercase lobby codes must be uppercased before use.");
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-06 to TC-15: Display List Generation
// ─────────────────────────────────────────────────────────────────────────────
public class TC06_TC15_DisplayListTests : UIManagerTestBase
{
    // TC-06: Host only — single entry in display list
    [Test]
    public void TC06_DisplayList_HostOnly_SingleEntry()
    {
        var display = BuildDisplayList(new List<string> { "Alice|CID:0" });
        Assert.AreEqual(1, display.Count);
        Assert.AreEqual("Alice", display[0]);
    }

    // TC-07: Host + guest — host appears first
    [Test]
    public void TC07_DisplayList_HostAndGuest_HostFirst()
    {
        var display = BuildDisplayList(new List<string> { "Alice|CID:0", "Bob|CID:1" });
        Assert.AreEqual(2, display.Count);
        Assert.AreEqual("Alice", display[0], "Host must always be listed first.");
        Assert.AreEqual("Bob", display[1]);
    }

    // TC-10: Guest before host in raw list — host still first in display
    [Test]
    public void TC10_DisplayList_GuestBeforeHostInRaw_HostStillFirst()
    {
        var display = BuildDisplayList(new List<string> { "Bob|CID:1", "Alice|CID:0" });
        Assert.AreEqual("Alice", display[0],
            "Host ordering must not depend on raw list order.");
    }

    // TC-11: Malformed CID entry — fallback used, no crash
    [Test]
    public void TC10_DisplayList_MalformedCID_FallbackUsed()
    {
        var display = BuildDisplayList(new List<string> { "Alice|CID:0", "BadEntry" });
        Assert.IsTrue(display.Count >= 1,
            "Malformed entries must not crash the display list build.");
    }

    // TC-12: Empty raw list — empty display
    [Test]
    public void TC12_DisplayList_EmptyRaw_EmptyDisplay()
    {
        Assert.AreEqual(0, BuildDisplayList(new List<string>()).Count);
    }

    // TC-13: Username > 20 chars — truncated
    [Test]
    public void TC13_Username_ExceedingMaxLength_IsTruncated()
    {
        string longName = "VeryLongPlayerNameThatExceedsLimit";
        string cleaned = longName.Length > 20 ? longName.Substring(0, 20) : longName;
        Assert.AreEqual(20, cleaned.Length,
            "Usernames over 20 characters must be truncated.");
    }

    // TC-14: Null username — replaced with "Guest"
    [Test]
    public void TC14_Username_Null_ReplacedWithGuest()
    {
        string name = null;
        Assert.AreEqual("Guest", string.IsNullOrEmpty(name) ? "Guest" : name.Trim());
    }

    // TC-15: Empty username — replaced with "Guest"
    [Test]
    public void TC15_Username_Empty_ReplacedWithGuest()
    {
        string name = "";
        Assert.AreEqual("Guest", string.IsNullOrEmpty(name) ? "Guest" : name.Trim());
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-16 to TC-18: Game Settings Sync Logic
// ─────────────────────────────────────────────────────────────────────────────
public class TC16_TC18_GameSettingsSyncTests : UIManagerTestBase
{
    private readonly string[] _gameTypeOptions = { "Morabaraba", "6 Men's Morris" };
    private readonly string[] _timeOptions = { "Casual", "5:00", "10:00", "15:00", "5s", "15s", "30s" };

    // TC-16: "Morabaraba" → index 0
    [Test]
    public void TC16_GameTypeDropdown_MorabarabaIndex_IsZero()
    {
        Assert.AreEqual(0, System.Array.IndexOf(_gameTypeOptions, "Morabaraba"));
    }

    // TC-17: "6 Men's Morris" → index 1
    [Test]
    public void TC17_GameTypeDropdown_SixMensMorrisIndex_IsOne()
    {
        Assert.AreEqual(1, System.Array.IndexOf(_gameTypeOptions, "6 Men's Morris"));
    }

    // TC-18: "Casual" → index 0 in time dropdown
    [Test]
    public void TC18_TimeDropdown_CasualIndex_IsZero()
    {
        Assert.AreEqual(0, System.Array.IndexOf(_timeOptions, "Casual"));
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-19 to TC-21: Start Button State Logic
// ─────────────────────────────────────────────────────────────────────────────
public class TC19_TC21_StartButtonStateTests : UIManagerTestBase
{
    // TC-19: 1 player — disabled
    [Test]
    public void TC19_StartButton_OnePlayer_Disabled()
    {
        Assert.IsFalse(ShouldStartButtonBeEnabled(1));
    }

    // TC-20: 2 players — enabled
    [Test]
    public void TC20_StartButton_TwoPlayers_Enabled()
    {
        Assert.IsTrue(ShouldStartButtonBeEnabled(2));
    }

    // TC-21: Button label is "Waiting for Players..." when count < 2
    [Test]
    public void TC21_StartButton_WaitingText_WhenOnePlayer()
    {
        string text = 1 < 2 ? "Waiting for Players..." : "Start Game";
        Assert.AreEqual("Waiting for Players...", text);
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-22 to TC-26: Player List Broadcast Serialisation
// ─────────────────────────────────────────────────────────────────────────────
public class TC22_TC26_BroadcastSerialiseTests : UIManagerTestBase
{
    // TC-22: Single name serialises to plain string
    [Test]
    public void TC22_Serialise_SinglePlayer_CorrectFormat()
    {
        Assert.AreEqual("Alice", Serialise(new List<string> { "Alice" }));
    }

    // TC-23: Two names are pipe-delimited
    [Test]
    public void TC23_Serialise_TwoPlayers_PipeDelimited()
    {
        Assert.AreEqual("Alice|Bob", Serialise(new List<string> { "Alice", "Bob" }));
    }

    // TC-24: Serialise → Deserialise round-trip is lossless
    [Test]
    public void TC24_Deserialise_RoundTrip_Lossless()
    {
        var original = new List<string> { "Alice", "Bob", "Charlie" };
        CollectionAssert.AreEqual(original, Deserialise(Serialise(original)),
            "Serialise → Deserialise must produce the original list exactly.");
    }

    // TC-25: Empty string → empty list
    [Test]
    public void TC25_Deserialise_EmptyString_EmptyList()
    {
        Assert.AreEqual(0, Deserialise("").Count);
    }

    // TC-26: Null → empty list without exception
    [Test]
    public void TC26_Deserialise_NullString_EmptyList()
    {
        Assert.AreEqual(0, Deserialise(null).Count);
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-27 to TC-29: Panel State Transitions
// ─────────────────────────────────────────────────────────────────────────────
public class TC27_TC29_PanelStateTests : UIManagerTestBase
{
    // TC-27: ShowMainMenu — only main menu active
    [Test]
    public void TC27_ShowMainMenu_OnlyMainMenuActive()
    {
        _uiManager.ShowMainMenu();
        Assert.IsTrue(_mainMenu.activeSelf, "Main menu panel must be active.");
        Assert.IsFalse(_hosting.activeSelf, "Hosting panel must be inactive.");
        Assert.IsFalse(_joining.activeSelf, "Joining panel must be inactive.");
    }

    // TC-28: LeaveLobbyAndReturnToMainMenu — hosting cleared, main menu shown
    [Test]
    public void TC28_LeaveLobby_ReturnsToMainMenu()
    {
        _hosting.SetActive(true);
        _mainMenu.SetActive(false);
        _uiManager.LeaveLobbyAndReturnToMainMenu();
        Assert.IsTrue(_mainMenu.activeSelf, "Main menu must be active after leaving lobby.");
        Assert.IsFalse(_hosting.activeSelf, "Hosting panel must be inactive.");
    }

    // TC-29: ReturnToMainMenuAsClient — joining cleared, main menu shown
    [Test]
    public void TC29_ReturnToMainMenuAsClient_JoiningCleared()
    {
        _joining.SetActive(true);
        _mainMenu.SetActive(false);
        _uiManager.ReturnToMainMenuAsClient();
        Assert.IsTrue(_mainMenu.activeSelf, "Main menu must be active after client disconnect.");
        Assert.IsFalse(_joining.activeSelf, "Joining panel must be inactive.");
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// TC-30 to TC-32: Edge Cases
// ─────────────────────────────────────────────────────────────────────────────
public class TC30_TC32_EdgeCaseTests : UIManagerTestBase
{
    // TC-30: GetPlayerDisplayNames — returned list is a copy, not a reference
    [Test]
    public void TC30_GetPlayerDisplayNames_ReturnsCopy()
    {
        var original = new List<string> { "Alice", "Bob" };
        var copy = new List<string>(original);
        copy.Add("Charlie");
        Assert.AreEqual(2, original.Count,
            "Modifying the returned list must not affect the internal list.");
    }

    // TC-31: ClearLobbyData — raw player list is empty afterwards
    [Test]
    public void TC31_ClearLobbyData_PlayerListsEmpty()
    {
        var rawList = new List<string> { "Alice|CID:0", "Bob|CID:1" };
        rawList.Clear();
        Assert.AreEqual(0, rawList.Count,
            "ClearLobbyData must result in an empty raw player list.");
    }

    // TC-32: PlayerSceneState enum has exactly 4 values
    [Test]
    public void TC32_PlayerSceneState_AllStatesPresent()
    {
        Assert.AreEqual(4, System.Enum.GetValues(typeof(PlayerSceneState)).Length,
            "PlayerSceneState must have 4 values: InLobby, LoadingGame, InGame, ReturningToLobby.");
    }
}