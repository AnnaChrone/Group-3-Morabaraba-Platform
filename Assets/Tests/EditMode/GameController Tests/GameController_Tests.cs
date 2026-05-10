using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;

public class GameControllerTests
{
    private GameObject gameObject;
    private GameController gameController;
    private FieldInfo allSlotsField;

    [SetUp]
    public void Setup()
    {
        gameObject = new GameObject("TestGameController");
        gameController = gameObject.AddComponent<GameController>();

        allSlotsField = typeof(GameController).GetField("allSlots",BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        allSlotsField.SetValue(gameController, new SlotID[0]);

        // Disable MonoBehaviour functioning
        gameController.enabled = false;

        gameController.HardcodeGameData();

        //Mock slots
        List<SlotID> slots = new List<SlotID>();

        for (int i = 1; i <= 24; i++)
        {
            GameObject slotObj = new GameObject($"Slot_{i}");

            SlotID slot = slotObj.AddComponent<SlotID>();
            slot.slotNumber = i;

            slots.Add(slot);
        }

        // Assign slots correctly
        allSlotsField.SetValue(gameController, slots.ToArray());
        SetNetworkVariable("Player1PiecesOnBoard", 9);
        SetNetworkVariable("Player2PiecesOnBoard", 9);
        InitializeNetworkVariables();
    }

    //Helper functions
    private void InitializeNetworkVariables()
    {
        InitializeSlotStates();
        SetNetworkVariable("CurrentPlayer", 1);
        SetNetworkVariable("PlacementCounter", 0);
        SetNetworkVariable("Player1PiecesOnBoard", 0);
        SetNetworkVariable("Player2PiecesOnBoard", 0);
        SetNetworkVariable("CurrentPhase", GamePhase.Placing);
        SetNetworkVariable("GameEnded", false);
        SetNetworkVariable("Player1Rewinds", 3);
        SetNetworkVariable("Player2Rewinds", 3);
    }

    private void InitializeSlotStates()
    {
        List<string> states = new List<string>();

        for (int i = 1; i <= 24; i++)
        {
            states.Add($"{i}:0");
        }

        string slotStatesString = string.Join(",", states);

        FieldInfo slotStatesField = typeof(GameController).GetField("SlotStates",BindingFlags.Public | BindingFlags.Instance);
        object networkVar = slotStatesField.GetValue(gameController);
        PropertyInfo valueProp = slotStatesField.FieldType.GetProperty("Value");
        FixedString4096Bytes fixedString =new FixedString4096Bytes(slotStatesString);
        valueProp.SetValue(networkVar, fixedString);
    }

    private void SetNetworkVariable(string fieldName, object value)
    {
        FieldInfo field = typeof(GameController).GetField(fieldName,BindingFlags.Public | BindingFlags.Instance);

        if (field == null)
            return;

        object networkVar = field.GetValue(gameController);
        PropertyInfo valueProp = field.FieldType.GetProperty("Value");
        valueProp?.SetValue(networkVar, value);
    }

    [TearDown]
    public void Teardown()
    {
        SlotID[] slots = (SlotID[])allSlotsField.GetValue(gameController);

        if (slots != null)
        {
            foreach (SlotID slot in slots)
            {
                if (slot != null && slot.gameObject != null)
                {
                    GameObject.DestroyImmediate(slot.gameObject);
                }
            }
        }

        if (gameObject != null)
        {
            GameObject.DestroyImmediate(gameObject);
        }
    }

    //Get/Set slot owner test cases

    [Test]
    public void test_SetSlotOwner_correct_owner_is_set_to_valid_slot()
    {
        gameController.SetSlotOwner(1, 1);
        int owner = gameController.GetSlotOwner(1);

        Assert.AreEqual(1, owner);
    }

    [Test]
    public void test_GetSlotOwner_Empty_slot_returns_zero()
    {
        int owner = gameController.GetSlotOwner(99);

        Assert.AreEqual(0, owner);
    }

    [Test]
    public void test_SetSlotOwner_OverwritesExistingOwner()
    {
        gameController.SetSlotOwner(1, 1);
        gameController.SetSlotOwner(1, 2);
        int owner = gameController.GetSlotOwner(1);

        Assert.AreEqual(2, owner);
    }

    //Adjacency test cases
    [Test]
    public void test_IsAdjacent_returns_true_for_connected_slots()
    {
        bool isAdjacent = gameController.IsAdjacent(gameController.GetSlotByNumber(1),gameController.GetSlotByNumber(2));

        Assert.IsTrue(isAdjacent);
    }

    [Test]
    public void test_IsAdjacent_returns_false_for_unconnected_slots()
    {
        bool isAdjacent = gameController.IsAdjacent(gameController.GetSlotByNumber(1),gameController.GetSlotByNumber(10));

        Assert.IsFalse(isAdjacent);
    }

    //Valid move test cases

    [Test]
    public void test_IsValidMove_returns_true_for_adjacent_moves()
    {
        gameController.SetSlotOwner(1, 1);
        gameController.SetSlotOwner(2, 0);
        bool isValid = gameController.IsValidMove(gameController.GetSlotByNumber(1),gameController.GetSlotByNumber(2),1);

        Assert.IsTrue(isValid);
    }

    [Test]
    public void test_IsValidMove_returns_false_for_nonadjacent_move()
    {
        gameController.SetSlotOwner(1, 1);
        bool isValid = gameController.IsValidMove(gameController.GetSlotByNumber(1),gameController.GetSlotByNumber(5),1);

        Assert.IsFalse(isValid);
    }

    [Test]
    public void test_IsValidMove_returns_false_for_if_target_occupied()
    {
        gameController.SetSlotOwner(1, 1);
        gameController.SetSlotOwner(2, 2);
        bool isValid = gameController.IsValidMove(gameController.GetSlotByNumber(1),gameController.GetSlotByNumber(2),1);

        Assert.IsFalse(isValid);
    }

    //Mill test cases

    [Test]
    public void test_CheckMill_returns_true_if_mill_formed()
    {
        gameController.SetSlotOwner(1, 1);
        gameController.SetSlotOwner(2, 1);
        gameController.SetSlotOwner(3, 1);
        bool isMill = gameController.CheckMill(3, 1);

        Assert.IsTrue(isMill);
    }

    [Test]
    public void test_CheckMill_returns_false_if_no_mill_forms()
    {
        gameController.SetSlotOwner(1, 1);
        gameController.SetSlotOwner(2, 2);
        gameController.SetSlotOwner(3, 1);

        bool isMill = gameController.CheckMill(3, 1);

        Assert.IsFalse(isMill);
    }

    [Test]
    public void test_CheckMill_returns_false_if_mill_is_incomplete()
    {
        gameController.SetSlotOwner(1, 1);
        gameController.SetSlotOwner(2, 1);

        bool isMill = gameController.CheckMill(2, 1);

        Assert.IsFalse(isMill);
    }

    //Slot lookup test cases

    [Test]
    public void test_GetSlotByNumber_returns_slot_if_valid_number()
    {
        SlotID slot = gameController.GetSlotByNumber(1);

        Assert.IsNotNull(slot);
        Assert.AreEqual(1, slot.slotNumber);
    }

    [Test]
    public void test_GetSlotByNumber_returns_null_if_invalid_number()
    {
        SlotID slot = gameController.GetSlotByNumber(999);

        Assert.IsNull(slot);
    }

    //Formatting time test cases

    [Test]
    public void test_FormatTime_returns_correct_output_for_zero_seconds()
    {
        string formatted = gameController.FormatTime(0);

        Assert.AreEqual("00:00", formatted);
    }

    [Test]
    public void test_FormatTime_returns_correct_output_for_five_seconds()
    {
        string formatted = gameController.FormatTime(5);

        Assert.AreEqual("00:05", formatted);
    }

    [Test]
    public void test_FormatTime_returns_correct_output_for_ninety_seconds()
    {
        string formatted = gameController.FormatTime(90);

        Assert.AreEqual("01:30", formatted);
    }

    [Test]
    public void test_FormatTime_negative_time_returns_zero()
    {
        string formatted = gameController.FormatTime(-10);

        Assert.AreEqual("00:00", formatted);
    }

    //Snapshot/Rewind test cases

    [Test]
    public void test_SaveSnapshot_adds_snapshot_to_history()
    {
        //Assign
        FieldInfo historyField = typeof(GameController).GetField("gameHistory",BindingFlags.NonPublic | BindingFlags.Instance);
        int initialCount =((List<GameSnapshot>)historyField.GetValue(gameController)).Count;

        //Act
        gameController.SaveSnapshot();
        int newCount =((List<GameSnapshot>)historyField.GetValue(gameController)).Count;

        //Assert
        Assert.AreEqual(initialCount + 1, newCount);
    }

    //EndTurn test cases

    [Test]
    public void test_EndTurn_switches_current_player()
    {
        //Assign
        FieldInfo currentPlayerField = typeof(GameController).GetField("CurrentPlayer",BindingFlags.Public | BindingFlags.Instance);
        object networkVar = currentPlayerField.GetValue(gameController);
        PropertyInfo valueProp =currentPlayerField.FieldType.GetProperty("Value");
        valueProp.SetValue(networkVar, 1);

        //Act
        gameController.EndTurn();
        int newPlayer = (int)valueProp.GetValue(networkVar);
        
        //Assert
        Assert.AreEqual(2, newPlayer);
    }

    //Stress test case

    [Test]
    public void test_multiple_slot_operations_wont_give_errors_under_stress()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 1; i <= 24; i++)
            {
                gameController.SetSlotOwner(i, (i % 2) + 1);
            }

            for (int i = 1; i <= 24; i++)
            {
                int owner = gameController.GetSlotOwner(i);

                Assert.IsTrue(owner == 1 || owner == 2);
            }
        });
    }

    [Test]
    public void test_multiple_mill_checks_will_not_throw_errors_under_stress()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int i = 1; i <= 24; i++)
            {
                gameController.CheckMill(i, 1);
                gameController.CheckMill(i, 2);
            }
        });
    }
}