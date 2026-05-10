using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using UnityEngine.UI;

public class SlotUI_Tests
{
    private GameObject slotObject;
    private SlotUI slotUI;
    private Image image;

    [SetUp]
    public void Setup()
    {
        // Create test GameObject with required components
        slotObject = new GameObject("TestSlot");
        image = slotObject.AddComponent<Image>();
        slotUI = slotObject.AddComponent<SlotUI>();

        if (slotUI.slotImage == null)
            slotUI.slotImage = image;
    }

    [TearDown]
    public void Teardown()
    {
        if (slotObject != null)
            Object.Destroy(slotObject);
    }

    [Test]
    // Test 1: Verify component requirements
    public void test_SlotUI_requires_image_component()
    {
        var requiresComponent = typeof(SlotUI).GetCustomAttributes(typeof(RequireComponent), true);
        Assert.IsNotNull(requiresComponent);
        Assert.IsTrue(requiresComponent.Length > 0);
    }

    [Test]
    // Test 2: SetPlayerColor for Player 1 works
    public void test_SetPlayerColor_sets_correct_colour_for_P1()
    {
        //Act
        slotUI.SetPlayerColor(1);

        //Assert
        Assert.AreEqual(slotUI.player1Color, slotUI.slotImage.color);
    }

    [Test]
    // Test 3: SetPlayerColor for Player 2 works
    public void test_SetPlayerColor_sets_correct_colour_for_P2()
    {
        //Act
        slotUI.SetPlayerColor(2);

        //Assert
        Assert.AreEqual(slotUI.player2Color, slotUI.slotImage.color);
    }

    [Test]
    // Test 4: SetPlayerColor when player value is 0 (empty) or invalid
    public void test_SetPlayerColor_sets_emptycolour_for_empty_or_invalid()
    {
        slotUI.SetPlayerColor(0);
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);

        slotUI.SetPlayerColor(3);
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);

        slotUI.SetPlayerColor(-1);
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);
    }


    [Test]
    // Test 5: Highlight for Player 1
    public void test_Highlight_for_P1_sets_correct_colour()
    {
        //Act
        slotUI.Highlight(1);

        //Assert
        Assert.AreEqual(slotUI.highlightplayer1Color, slotUI.slotImage.color);
    }

    [Test]
    // Test 6: Highlight for Player 2
    public void test_Highlight_for_P2_sets_correct_colour()
    {
        //Act
        slotUI.Highlight(2);

        //Assert
        Assert.AreEqual(slotUI.highlightplayer2Color, slotUI.slotImage.color);
    }

    [Test]
    // Test 7: Highlight for Empty/Invalid
    public void test_Highlight_for_InvalidPlayer_sets_empty()
    {
        slotUI.Highlight(0);
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);

        slotUI.Highlight(99);
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);
    }

    [Test]
    // Test 8: HighlightMill for Player 1 is correct
    public void test_HighlightMill_for_P1_sets_mill_colour()
    {
        //Act
        slotUI.HighlightMill(1);

        //Assert
        Assert.AreEqual(slotUI.millplayer1Color, slotUI.slotImage.color);
    }

    [Test]
    // Test 9: HighlightMill for Player 2 is correct
    public void test_HighlightMill_for_P2_sets_mill_colour()
    {
        //Act
        slotUI.HighlightMill(2);

        //Assert
        Assert.AreEqual(slotUI.millplayer2Color, slotUI.slotImage.color);
    }

    [Test]
    // Test 10: HighlightMill for Empty/Invalid
    public void test_HighlightMill_sets_empty_for_InvalidPlayer()
    {
        //Act
        slotUI.HighlightMill(0);
        
        //Assert
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);
    }

    [Test]
    // Test 11: ResetColor returns to empty colour
    public void test_ResetColor_sets_empty_colour()
    {
        //Setting colour to reset
        slotUI.SetPlayerColor(1);
        Assert.AreNotEqual(slotUI.emptyColor, slotUI.slotImage.color);

        //resetting colour
        slotUI.ResetColor();
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);
    }

    [Test]
    // Test 12: Verify all colour properties are correctly assigned
    public void test_SlotUI_all_colours_assigned_properly()
    {
        Assert.IsNotNull(slotUI.emptyColor);
        Assert.IsNotNull(slotUI.player1Color);
        Assert.IsNotNull(slotUI.player2Color);
        Assert.IsNotNull(slotUI.highlightplayer1Color);
        Assert.IsNotNull(slotUI.highlightplayer2Color);
        Assert.IsNotNull(slotUI.millplayer1Color);
        Assert.IsNotNull(slotUI.millplayer2Color);
    }

    [Test]
    //Test 13: Colour changes work sequentially
    public void test_SlotUI_colour_changes_work_sequentially()
    {
        slotUI.SetPlayerColor(1);
        Assert.AreEqual(slotUI.player1Color, slotUI.slotImage.color);

        slotUI.Highlight(1);
        Assert.AreEqual(slotUI.highlightplayer1Color, slotUI.slotImage.color);

        slotUI.HighlightMill(1);
        Assert.AreEqual(slotUI.millplayer1Color, slotUI.slotImage.color);

        slotUI.ResetColor();
        Assert.AreEqual(slotUI.emptyColor, slotUI.slotImage.color);
    }

    [Test]
    // Test 14: Verify colours persist when changing between players
    public void test_SlotUI_colours_persist_between_player_changes()
    {
        slotUI.SetPlayerColor(1);
        Assert.AreEqual(slotUI.player1Color, slotUI.slotImage.color);

        slotUI.SetPlayerColor(2);
        Assert.AreEqual(slotUI.player2Color, slotUI.slotImage.color);

        slotUI.HighlightMill(1);
        Assert.AreEqual(slotUI.millplayer1Color, slotUI.slotImage.color);

        slotUI.Highlight(2);
        Assert.AreEqual(slotUI.highlightplayer2Color, slotUI.slotImage.color);
    }

}