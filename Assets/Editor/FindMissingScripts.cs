using UnityEngine;
using UnityEditor;

public class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts")]
    static void Find()
    {
        Debug.Log("Started searching for missing scripts...");

        int count = 0;

        GameObject[] gos = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject go in gos)
        {
            Component[] components = go.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    count++;
                    Debug.Log("Missing script in: " + GetFullPath(go), go);
                }
            }
        }

        Debug.Log("Finished. Missing scripts found: " + count);
    }

    static string GetFullPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}