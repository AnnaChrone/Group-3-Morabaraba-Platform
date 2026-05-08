using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GameTypeData", menuName = "Morabaraba/Game Type Data")]
public class GameTypeData : ScriptableObject
{
    public string gameTypeName;
    public int totalSlots;
    public int piecesPerPlayer;
    public TextAsset adjacencyData;
    public TextAsset millData;

    [System.NonSerialized]
    public Dictionary<int, int[]> adjacency;

    [System.NonSerialized]
    public List<int[]> mills;

    public void LoadData()
    {
        adjacency = new Dictionary<int, int[]>();
        mills = new List<int[]>();

        // Load adjacency data
        if (adjacencyData != null)
        {
            string json = adjacencyData.text;
            // Manual parsing for adjacency
            int idx = 0;
            while ((idx = json.IndexOf("{\"slot\":", idx)) != -1)
            {
                int slotStart = json.IndexOf(":", idx) + 1;
                int slotEnd = json.IndexOf(",", slotStart);
                if (slotEnd == -1) slotEnd = json.IndexOf("}", slotStart);

                if (int.TryParse(json.Substring(slotStart, slotEnd - slotStart).Trim(), out int slot))
                {
                    int connStart = json.IndexOf("[", slotEnd);
                    int connEnd = json.IndexOf("]", connStart);
                    string connStr = json.Substring(connStart + 1, connEnd - connStart - 1);
                    string[] parts = connStr.Split(',');
                    int[] connections = new int[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                        int.TryParse(parts[i].Trim(), out connections[i]);
                    adjacency[slot] = connections;
                }
                idx = slotEnd;
            }
            Debug.Log($"Loaded {adjacency.Count} adjacency entries for {gameTypeName}");
        }

        // Load mill data
        if (millData != null)
        {
            string json = millData.text;
            // Find all mill arrays: look for [ and ] pairs
            int start = 0;
            while ((start = json.IndexOf('[', start)) != -1)
            {
                int end = json.IndexOf(']', start);
                if (end == -1) break;

                string millStr = json.Substring(start + 1, end - start - 1);
                string[] parts = millStr.Split(',');
                List<int> millList = new List<int>();
                foreach (string part in parts)
                {
                    if (int.TryParse(part.Trim(), out int slot))
                        millList.Add(slot);
                }
                if (millList.Count >= 3)
                    mills.Add(millList.ToArray());
                start = end + 1;
            }
            Debug.Log($"Loaded {mills.Count} mills for {gameTypeName}");
        }
    }
}

[System.Serializable]
public class AdjacencyWrapper
{
    public AdjacencyData[] adjacencies;
}

[System.Serializable]
public class AdjacencyData
{
    public int slot;
    public int[] connections;
}

[System.Serializable]
public class MillWrapper
{
    public MillData[] mills;
}

[System.Serializable]
public class MillData
{
    public int[] mill;
}