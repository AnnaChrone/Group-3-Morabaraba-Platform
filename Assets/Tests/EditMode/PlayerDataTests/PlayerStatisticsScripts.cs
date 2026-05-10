using UnityEngine;
using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;

public class PlayerStatisticsScripts 
{
  private PlayerData playerData;

    [SetUp]
    public void Setup()
    {
        GameObject obj = new GameObject();
        playerData = obj.AddComponent<PlayerData>();
    }

    [Test]
    public void SetWinsToValue()
    {
        playerData.setWins(5);

        Assert.AreEqual(5, playerData.wins);
    }

    [Test]
    public void SetLossToValue()
    {
        playerData.setLoss(3);

        Assert.AreEqual(3, playerData.losses);
    }

    [Test]
    public void SetDrawToValue()
    {
        playerData.setDraw(2);

        Assert.AreEqual(2, playerData.draw);
    }

}
