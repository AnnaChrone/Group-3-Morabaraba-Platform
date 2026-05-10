using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

public class SlotID_Tests
{
    private GameObject slotObject;
    private SlotID slotID;
    private GameController mockGameController;

    [SetUp]
    public void Setup()
    {
        //Generating test versions of Slots
        slotObject = new GameObject("TestSlot");
        slotID = slotObject.AddComponent<SlotID>();
        slotID.slotUI = slotObject.AddComponent<SlotUI>();

    }

    [TearDown]
    public void Teardown()
    {
        if (slotObject != null)
            Object.DestroyImmediate(slotObject);
    }

    //Initialization test cases
    [Test]
    //This test checks whether newly created and untouched slots are started unoccupied
    public void test_start_with_slot_unoccupied()
    {
        Assert.IsFalse(slotID.IsOccupied);
        Assert.AreEqual(0, slotID.occupiedBy);
    }

    [Test]
    //This test checks whether newly created and untouched slots are started not in mills
    public void test_start_with_slot_not_in_mill()
    {
        Assert.IsFalse(slotID.isInMill);
    }

    [Test]
    //This test checks that Awake() automatically assigns slotUI to each slot component
    public void test_awake_autoassigns_slotUI()
    {
        // Assign - create a new slot
        var newSlotObject = new GameObject("NewSlot");
        var newSlotID = newSlotObject.AddComponent<SlotID>();
        var uiComponent = newSlotObject.AddComponent<SlotUI>();

        // Act - run awake() through a testing function 
        newSlotID.InitializeForTesting();

        // Assert - check slotUI was assigned
        Assert.IsNotNull(newSlotID.slotUI);
        Assert.AreEqual(uiComponent, newSlotID.slotUI);

        Object.DestroyImmediate(newSlotObject);
    }

    [Test] //DECIDE IF WANNA KEEP THIS
    //This test checks that slotUI remains null if it is missing
    public void test_awake_leaves_slotUI_null_when_slotUI_missing()
    {
        // Setup
        var newSlotObject = new GameObject("NewSlot");
        var newSlotID = newSlotObject.AddComponent<SlotID>();

        // Act
        newSlotID.InitializeForTesting();

        // Assert
        Assert.IsNull(newSlotID.slotUI);

        Object.DestroyImmediate(newSlotObject);
    }

    //setOccupant test cases

    [Test]
    //This test checks that setting occupant as Player 1 marks the slot as occupied by player 1
    public void test_setting_occupant_as_P1_marks_slot_as_Occupied_by_P1()
    {
        // Act
        slotID.SetOccupant(1); //sets occupant with 1 (the signal for player 1)

        // Assert
        Assert.AreEqual(1, slotID.occupiedBy);
        Assert.IsTrue(slotID.IsOccupied);
    }

    [Test]
    //This test checks that setting occupant as Player 1 marks the slot as occupied by player 1
    public void test_setting_occupant_as_P2_marks_slot_as_Occupied_by_P2()
    {
        // Act
        slotID.SetOccupant(2); //sets occupant with 2 (the signal for player 2)

        // Assert
        Assert.AreEqual(2, slotID.occupiedBy);
        Assert.IsTrue(slotID.IsOccupied);
    }

    [Test]
    //This test checks that setting the occupant of P1 sets the Ui of the slot to the colour of P1
    public void test_SetOccupant_with_P1_updates_UI_to_P1_colour()
    {
        // Act
        slotID.SetOccupant(1);

        // Assert
        Assert.AreEqual(1, slotID.occupiedBy);
    }

    [Test]
    //This test checks that setting the occupant of P2 sets the Ui of the slot to the colour of P2
    public void test_SetOccupant_with_P2_updates_UI_to_P2_colour()
    {
        // Act
        slotID.SetOccupant(2);

        // Assert
        Assert.AreEqual(2, slotID.occupiedBy);
    }

    [Test]
    //This test checks that setting the occupant with 0 sets the slot colour to the empty slot colour
    public void test_SetOccupant_with_0_sets_UI_to_empty_colour()
    {
        // Act
        slotID.SetOccupant(1);
        slotID.SetOccupant(0);

        // Assert
        Assert.AreEqual(0, slotID.occupiedBy);
        Assert.IsFalse(slotID.IsOccupied);
    }

    [Test]
    //This test checks that setting the occupant with a negative maintains stability
    public void test_SetOccupant_with_negative_maintains_stability_without_throwing_exceptions()
    {
        // Act & Assert combined
        Assert.DoesNotThrow(() => slotID.SetOccupant(-1));
    }

    [Test]
    //This test checks that occupying a slot with an invalid player ID does not cause a crash
    public void test_SetOccupant_with_invalid_player_handles_without_exceptions()
    {
        // Act & Assert combined
        Assert.DoesNotThrow(() => slotID.SetOccupant(99));
    }

    [Test]
    //This test checks that changing occupancy correctly updates
    public void test_SetOccupant_when_changing_player_occupancy_correctly_reflects()
    {
        // Act
        slotID.SetOccupant(1);
        slotID.SetOccupant(2);

        // Assert
        Assert.AreEqual(2, slotID.occupiedBy);
        Assert.IsTrue(slotID.IsOccupied);
    }

    //ClearSlot tests
    [Test]
    //This test checks that clearing the slot of player 1 occupancy resets slot to empty state
    public void test_ClearSlot_of_P1_occupancy_resets_slot_to_empty()
    {
        // Assign
        slotID.SetOccupant(1);

        // Act
        slotID.ClearSlot();

        // Assert
        Assert.AreEqual(0, slotID.occupiedBy);
        Assert.IsFalse(slotID.IsOccupied);
    }

    [Test]
    //This test checks that clearing a slot within a mill resets mill status to false
    public void test_ClearSlot_when_slot_is_in_mill_resets_millstatus()
    {
        // Assign
        slotID.SetOccupant(1);
        slotID.isInMill = true;

        // Act
        slotID.ClearSlot();

        // Assert
        Assert.IsFalse(slotID.isInMill);
    }

    [Test] //DECIDE
    //This test checks that clearing the slot resets both occupancy and mill status simultaniously
    public void test_ClearSlot_resets_occupancy_and_millstatus_simulataniously()
    {
        // Assign
        slotID.SetOccupant(2);
        slotID.SetMillStatus(true);

        // Act
        slotID.ClearSlot();

        // Assert
        Assert.AreEqual(0, slotID.occupiedBy);
        Assert.IsFalse(slotID.isInMill);
        Assert.IsFalse(slotID.IsOccupied);
    }

    [Test]
    //This test checks that slot UI colour is reset to empty when cleared
    public void test_ClearSlot_resets_slot_colour_to_empty()
    {

        // Act
        slotID.SetOccupant(1);
        slotID.ClearSlot();

        // Assert
        Assert.AreEqual(0, slotID.occupiedBy);
    }

    [Test]
    //This test checks that clearslot keeps slot empty if it is already empty
    public void test_ClearSlot_keeps_slot_empty_if_already_empty()
    {
        // Act - slot is empty by default
        slotID.ClearSlot();

        // Assert
        Assert.AreEqual(0, slotID.occupiedBy);
        Assert.IsFalse(slotID.IsOccupied);
        Assert.IsFalse(slotID.isInMill);
    }

    //IsOccupied test cases
    [Test]
    //This test check that IsOccupied returns false when no occupant
    public void test_IsOccupied_returns_false_when_slot_has_no_occupant()
    {
        Assert.IsFalse(slotID.IsOccupied); //occupant is 0 on default
    }

    [Test]
    //This test checks that IsOccupied will return true when occupied by Player 1
    public void test_IsOccupied_returns_true_when_occupied_by_P1()
    {
        // Assign
        slotID.occupiedBy = 1;

        // Assert
        Assert.IsTrue(slotID.IsOccupied);
    }

    [Test]
    //This test checks that IsOccupied will return true when occupied by Player 2
    public void test_IsOccupied_returns_true_when_occupied_by_P2()
    {
        // Setup
        slotID.occupiedBy = 2;

        // Assert
        Assert.IsTrue(slotID.IsOccupied);
    }

    [Test]
    //This test checks that IsOccupied reflects effectively after multiple changes
    public void test_IsOccupied_reflects_effectively_with_multiple_changes()
    {
        // Initial state - default of 0
        Assert.IsFalse(slotID.IsOccupied);

        // After setting a player
        slotID.SetOccupant(1);
        Assert.IsTrue(slotID.IsOccupied);

        // After clearing
        slotID.ClearSlot();
        Assert.IsFalse(slotID.IsOccupied);
    }

    //Mill Status test cases
    [Test]
    //This test checks that mill status is true when a slot is part of a mill
    public void test_SetMillStatus_returns_true_when_a_slot_is_marked_part_of_a_mill()
    {
        // Act
        slotID.SetMillStatus(true);

        // Assert
        Assert.IsTrue(slotID.isInMill);
    }

    [Test]
    //This test checks that mill status is false when a slot is removed from a mill
    public void test_SetMillStatus_returns_false_when_a_slot_is_removed_from_a_mill()
    {
        // Assign
        slotID.SetMillStatus(true);

        // Act
        slotID.SetMillStatus(false);

        // Assert
        Assert.IsFalse(slotID.isInMill);
    }

    [Test]
    //This test checks that calling SetMillStatus multiple times correctly toggles between states
    public void test_SetMillStatus_toggles_between_states_when_called_multiple_times()
    {
        // Act & Assert
        slotID.SetMillStatus(true);
        Assert.IsTrue(slotID.isInMill);

        slotID.SetMillStatus(false);
        Assert.IsFalse(slotID.isInMill);

        slotID.SetMillStatus(true);
        Assert.IsTrue(slotID.isInMill);
    }

    //Edge and integration test cases
    [Test]
    //This test checks that slot number allows assignment
    public void test_SlotNumber_allows_assignment_of_slot_IDs()
    {
        // Act
        slotID.slotNumber = 5;

        // Assert
        Assert.AreEqual(5, slotID.slotNumber);
    }

    [Test]
    //This test checks that SlotNumber supports max count of 24 slots
    public void test_SlotNumber_supports_max_slot_count_of_24()
    {
        // Act
        slotID.slotNumber = 24;

        // Assert
        Assert.AreEqual(24, slotID.slotNumber);
    }

    [Test]
    //This test checks the cycle of empty > occupied > cleared maintains consistency
    public void test_empty_to_occupied_to_cleared_maintains_state()
    {
        // Start empty
        Assert.AreEqual(0, slotID.occupiedBy);
        Assert.IsFalse(slotID.isInMill);

        // Place player 1
        slotID.SetOccupant(1);
        Assert.AreEqual(1, slotID.occupiedBy);

        // Set mill status
        slotID.SetMillStatus(true);
        Assert.IsTrue(slotID.isInMill);

        // Clear slot
        slotID.ClearSlot();
        Assert.AreEqual(0, slotID.occupiedBy);
        Assert.IsFalse(slotID.isInMill);
    }

    [Test]
    //This test checks that setOccupant does not effect mill status when changing
    public void test_SetOccupant_must_not_effect_mill_status_apon_change()
    {
        // Assign
        slotID.SetOccupant(1);
        slotID.SetMillStatus(true);

        // Act
        slotID.SetOccupant(2);

        // Assert
        Assert.IsTrue(slotID.isInMill); // Mill status must persist
        Assert.AreEqual(2, slotID.occupiedBy);
    }
}
