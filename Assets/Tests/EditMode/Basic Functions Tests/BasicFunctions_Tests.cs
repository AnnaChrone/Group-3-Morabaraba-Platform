using NUnit.Framework;
using UnityEngine;

public class BasicFunctions_Tests
{
    private GameObject obj;
    private BasicFunctions script;
    private GameObject closeTarget;

    [SetUp]
    public void Setup()
    {
        obj = new GameObject("BasicFunctions");
        script = obj.AddComponent<BasicFunctions>();

        closeTarget = new GameObject("CloseTarget");
        closeTarget.SetActive(false);

        script.CloseTarget = closeTarget;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(obj);
        Object.DestroyImmediate(closeTarget);
    }

    //UI toggle test cases

    [Test]
    public void test_OnClose_activates_object_on_first_call()
    {
        script.onClose();

        Assert.IsTrue(closeTarget.activeSelf);
    }

    [Test]
    public void test_OnClose_deactivates_object_on_second_call()
    {
        script.onClose();
        script.onClose();

        Assert.IsFalse(closeTarget.activeSelf);
    }

    [Test]
    public void test_OnClose_flips_correctly_between_multiple_toggles()
    {
        for (int i = 0; i < 5; i++)
        {
            script.onClose();
        }

        Assert.IsTrue(closeTarget.activeSelf); // odd number of toggles
    }

    //Null safety test cases

    [Test]
    public void test_OnClose_doesnt_crash_when_closeTarget_is_null()
    {
        script.CloseTarget = null;

        Assert.DoesNotThrow(() => script.onClose());
    }

    //Quit game test cases

    [Test]
    public void test_QuitGame_does_not_crash()
    {
        Assert.DoesNotThrow(() => script.QuitGame());
    }
}