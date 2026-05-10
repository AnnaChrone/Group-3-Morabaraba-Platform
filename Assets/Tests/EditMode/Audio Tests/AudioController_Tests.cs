using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;

public class AudioControllerTests
{
    private GameObject audioControllerObject;
    private AudioController audioController;
    private AudioSource testAudioSource;
    private List<string> debugLogs;
    private List<LogType> logTypes;

    [SetUp]
    public void Setup()
    {
        // Setup log capture
        debugLogs = new List<string>();
        logTypes = new List<LogType>();
        Application.logMessageReceived += HandleLog;

        // Create test GameObject with required components
        audioControllerObject = new GameObject("TestAudioController");
        testAudioSource = audioControllerObject.AddComponent<AudioSource>();
        audioController = audioControllerObject.AddComponent<AudioController>();

        // Setup AudioController references
        audioController.UIAudioPlayer = testAudioSource;

        // Create dummy AudioClips for testing
        audioController.SelectSound = AudioClip.Create("TestSelect", 44100, 1, 44100, false);
        audioController.PlaceSound = AudioClip.Create("TestPlace", 44100, 1, 44100, false);
        audioController.MoveSound = AudioClip.Create("TestMove", 44100, 1, 44100, false);
        audioController.FormMillSound = AudioClip.Create("TestFormMill", 44100, 1, 44100, false);
        audioController.BreakMillSound = AudioClip.Create("TestBreakMill", 44100, 1, 44100, false);
        audioController.CaptureSound = AudioClip.Create("TestCapture", 44100, 1, 44100, false);
        audioController.WinSound = AudioClip.Create("TestWin", 44100, 1, 44100, false);
        audioController.LossSound = AudioClip.Create("TestLoss", 44100, 1, 44100, false);
        audioController.DrawSound = AudioClip.Create("TestDraw", 44100, 1, 44100, false);
        audioController.FlyingSound = AudioClip.Create("TestFly", 44100, 1, 44100, false);
        audioController.RewindSound = AudioClip.Create("TestRewind", 44100, 1, 44100, false);
        audioController.InvalidMoveSound = AudioClip.Create("TestInvalid", 44100, 1, 44100, false);
    }

    [TearDown]
    public void Teardown()
    {
        // Clean up log capture
        Application.logMessageReceived -= HandleLog;

        // Clean up after each test
        if (audioControllerObject != null)
            GameObject.Destroy(audioControllerObject);
    }

    //helper functions for test cases
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        debugLogs.Add(logString);
        logTypes.Add(type);
    }

    private void ClearLogs()
    {
        debugLogs.Clear();
        logTypes.Clear();
    }

    private bool HasLogContaining(string text)
    {
        return debugLogs.Exists(log => log.Contains(text));
    }

    private bool HasWarningContaining(string text)
    {
        for (int i = 0; i < debugLogs.Count; i++)
        {
            if (logTypes[i] == LogType.Warning && debugLogs[i].Contains(text))
                return true;
        }
        return false;
    }

    [Test]
    // Test 1: Verify Singleton Instance is created
    public void test_AudioController_singleton_instance_creates()
    {
        //Assign
        var testObject = new GameObject("TestSingleton");
        var controller = testObject.AddComponent<AudioController>();

        //Act
        controller.InitializeForTesting();

        // Verify the singleton instance is set
        Assert.IsNotNull(AudioController.Instance);

        // Use ReferenceEquals or compare the objects directly
        Assert.IsTrue(ReferenceEquals(controller, AudioController.Instance));

        // Alternative: compare the gameObject
        Assert.AreEqual(controller.gameObject, AudioController.Instance.gameObject);

        GameObject.Destroy(testObject);
    }

    [Test]
    // Test 2: Case name for "select" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_select()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("select"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("SELECT"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Select"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("SeLeCt"));
    }

    [Test]
    // Test 4: Case name for "place" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_place()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("place"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("PLACE"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Place"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("pLaCe"));
    }

    [Test]
    // Test 5: Case name for "move" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_move()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("move"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("MOVE"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Move"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("mOvE"));
    }

    [Test]
    // Test 6: Case name for "formmill" sound is not 
    public void test_PlayAudio_is_case_insensitive_formmill()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("formmill"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("FORMMILL"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("FormMill"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("fOrMmIlL"));
    }

    [Test]
    // Test 7: Case name for "breakmill" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_breakmill()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("breakmill"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("BREAKMILL"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("BreakMill"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("bReAkMiLl"));
    }

    [Test]
    // Test 8: Case name for "capture" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_capture()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("capture"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("CAPTURE"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Capture"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("cApTuRe"));
    }

    [Test]
    // Test 9: Case name for "win" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_win()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("win"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("WIN"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Win"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("wIn"));
    }

    [Test]
    // Test 10: Case name for "loss" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_losss()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("loss"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("LOSS"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Loss"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("lOsS"));
    }

    [Test]
    // Test 11: Case name for "draw" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_draw()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("draw"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("DRAW"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Draw"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("dRaW"));
    }

    [Test]
    // Test 12: Case name for "rewind" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_rewind()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("rewind"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("REWIND"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Rewind"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("rEwInD"));
    }

    [Test]
    // Test 13: Case name for "fly" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_fly()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("fly"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("FLY"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Fly"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("fLy"));
    }

    [Test]
    // Test 14: Case name for "invalid" sound is not sensitive
    public void test_PlayAudio_is_case_insensitive_invalid()
    {
        Assert.DoesNotThrow(() => audioController.PlayAudio("invalid"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("INVALID"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("Invalid"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("iNvAlId"));
    }

    [Test]
    // Test 15: Unknown audio clip name shows warning
    public void test_PlayAudio_shows_warning_when_clip_unknown()
    {
        //Assign
        ClearLogs();

        //Act
        audioController.PlayAudio("UnknownClip");
        
        //Assert
        Assert.IsTrue(HasWarningContaining("Unknown audio request: UnknownClip"));

        //Assign
        ClearLogs();

        //Act
        audioController.PlayAudio("nonexistent");
        
        //Assert
        Assert.IsTrue(HasWarningContaining("Unknown audio request: nonexistent"));
    }

    [Test]
    // Test 16: Empty string handling
    public void test_PlayAudio_shows_warning_with_empty_string()
    {
        //Assign
        ClearLogs();

        //Act
        audioController.PlayAudio("");

        //Assert
        Assert.IsTrue(HasWarningContaining("Audio clip name is null or empty"));
    }

    [Test]
    // Test 17: Null string handling
    public void test_PlayAudio_shows_warning_with_null_string()
    {
        //Assign
        ClearLogs();

        //Act
        audioController.PlayAudio(null);

        //Assert
        Assert.IsTrue(HasWarningContaining("Audio clip name is null or empty"));
    }

    [Test]
    // Test 18: Verify all audio clips are assigned
    public void test_AudioController_has_all_clips_assigned()
    {
        Assert.IsNotNull(audioController.SelectSound);
        Assert.IsNotNull(audioController.PlaceSound);
        Assert.IsNotNull(audioController.MoveSound);
        Assert.IsNotNull(audioController.FormMillSound);
        Assert.IsNotNull(audioController.BreakMillSound);
        Assert.IsNotNull(audioController.CaptureSound);
        Assert.IsNotNull(audioController.WinSound);
        Assert.IsNotNull(audioController.LossSound);
        Assert.IsNotNull(audioController.DrawSound);
        Assert.IsNotNull(audioController.FlyingSound);
        Assert.IsNotNull(audioController.RewindSound);
        Assert.IsNotNull(audioController.InvalidMoveSound);
    }

    [Test]
    // Test 19: Play audio with null AudioClip wont throw
    public void test_PlayAudio_NullAudioClip_DoesNotThrow()
    {
        //Assign
        audioController.WinSound = null;

        //Assert
        Assert.DoesNotThrow(() => audioController.PlayAudio("Win"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("WIN"));
        Assert.DoesNotThrow(() => audioController.PlayAudio("win"));
    }

    [Test]
    // Test 20: Sequential audio requests work
    public void test_PlayAudio_sequential_audio_requests_work()
    {
        Assert.DoesNotThrow(() =>
        {
            audioController.PlayAudio("Select");
            audioController.PlayAudio("PLACE");
            audioController.PlayAudio("move");
            audioController.PlayAudio("FORMMILL");
            audioController.PlayAudio("capture");
            audioController.PlayAudio("WIN");
        });
    }

    [Test]
    // Test 21: Stress test with mixed case rapid calls
    public void test_PlayAudio_stress_test_with_rapid_calls()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                audioController.PlayAudio("SeLeCt");
                audioController.PlayAudio("pLaCe");
                audioController.PlayAudio("MoVe");
                audioController.PlayAudio("fLy");
            }
        });
    }
}