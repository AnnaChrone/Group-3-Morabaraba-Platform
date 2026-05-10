using UnityEngine;
using NUnit.Framework;

public class CreateUsernamesTest 
{
    private CreateUsername script;

    [SetUp] //Get validation from create username script
    public void Setup()
    {
        GameObject obj = new GameObject();
        script = obj.AddComponent<CreateUsername>();
    }
    //Test cases with specific input - should all pass
    [Test]
    public void EmptyUsername_ReturnsFalse()
    {
        bool result = script.isValidUsername("", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Please enter a username", error);
    }

    [Test]
    public void ShortUsername_ReturnsFalse()
    {
        bool result = script.isValidUsername("ab", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Username must be 3+ characters", error);
    }

    [Test]
    public void UsernameOverTwentyLength_ReturnsFalse()
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

    [Test]
    public void PasswordWithoutUppercase_ReturnsFalse()
    {
        bool result = script.isValidPassword("password1!", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Password needs an uppercase letter", error);
    }

    [Test]
    public void ValidPassword_ReturnsTrue()
    {
        bool result = script.isValidPassword("Password1!", out string error);

        Assert.IsTrue(result);
    }
}
