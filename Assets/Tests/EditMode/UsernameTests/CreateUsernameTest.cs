using UnityEngine;
using NUnit.Framework;

public class CreateUsernameTest 
{private CreateUsername script;

    [SetUp] //Get validation function from Create Username script
    public void Setup()
    {
        GameObject obj = new GameObject();
        script = obj.AddComponent<CreateUsername>();
    }

    [Test] //Test header allows it to be tested properly by the software and get the results
    public void EmptyUsername_ReturnsFalse()
    {
        bool result = script.isValidUsername("", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Please enter a username", error); //Compares errror messages
    }

    [Test]
    public void ShortUsername_ReturnsFalse()
    {
        bool result = script.isValidUsername("ab", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Username must be 3+ characters", error);
    }

    [Test]
    public void LongUsernameWithLengthTwentyPlus_ReturnsFasle()
    {
        bool result = script.isValidUsername("abcdefghijklmnopqrstuv", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Username must be under 20 characters", error);
    }

    [Test]
    public void ValidUsername_ReturnsTrue()
    {
        bool result = script.isValidUsername("Akiria", out string error);

        Assert.IsTrue(result);
        Assert.AreEqual("", error);
    }

}
