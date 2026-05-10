using UnityEngine;
using NUnit.Framework;
public class CreatePasswordTests 
{
    private CreateUsername script;

    [SetUp]
    public void Setup()
    {
        GameObject obj = new GameObject();
        script = obj.AddComponent<CreateUsername>();
    }

    [Test]
    public void EmptyPassword_ReturnsFalse()
    {
        bool result = script.isValidPassword("", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Please enter a password", error);
    }

    [Test]
    public void ShortPassword_ReturnsFalse()
    {
        bool result = script.isValidPassword("Kiri!2", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Password must be 8 characters or more", error);
    }

     [Test]
    public void PasswordWithoutUppercase_ReturnsFalse()
    {
        bool result = script.isValidPassword("password1!", out string error);

        Assert.IsFalse(result);
        Assert.AreEqual("Password needs an uppercase letter", error);
    }

    [Test]
    public void PasswordWithoutLowerCase_ReturnsFalse()
    {
        bool result = script.isValidPassword("PASSWORD1!", out string error);
            
        Assert.IsFalse(result);
        Assert.AreEqual("Password needs a lowercase letter", error);
    }

    [Test]
    public void PasswordWithoutNumber_ReturnsFalse()
    {
        bool result = script.isValidPassword("PasswordD!!", out string error);
            
        Assert.IsFalse(result);
        Assert.AreEqual("Password needs a number", error);
    }

    [Test]
    public void PasswordWithoutSpecialCharacter_ReturnsFalse()
    {
        bool result = script.isValidPassword("PasswordD11", out string error);
            
        Assert.IsFalse(result);
        Assert.AreEqual("Password needs a special character", error);
    }

    [Test]
    public void ValidPassword_ReturnsTrue()
    {
        bool result = script.isValidPassword("Password1!", out string error);

        Assert.IsTrue(result);
    }
}

