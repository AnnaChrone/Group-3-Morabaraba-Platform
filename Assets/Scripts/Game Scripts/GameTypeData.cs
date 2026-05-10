using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GameTypeData", menuName = "Morabaraba/Game Type Data")]
public class GameTypeData : ScriptableObject
{
    public string gameTypeName;
    public int totalSlots;
    public int piecesPerPlayer;

    [System.NonSerialized]
    public Dictionary<int, int[]> adjacency;

    [System.NonSerialized]
    public List<int[]> mills;

    
}

