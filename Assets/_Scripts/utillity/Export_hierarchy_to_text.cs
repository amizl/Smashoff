using UnityEngine;
using System.Text;
using UnityEngine.SceneManagement;
using System.IO;

public class HierarchyLogger : MonoBehaviour
{
    private string filePath = @"E:\temp\Hierarchy.txt"; // Set the file path

    private void Start()
    {
        LogHierarchy();
    }

    [ContextMenu("Log Hierarchy")]
    void LogHierarchy()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
        sb.AppendLine("==================================");

        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            PrintHierarchy(go.transform, sb, 0);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // Ensure directory exists
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"Hierarchy exported to: {filePath}");
        }
        catch (IOException e)
        {
            Debug.LogError($"Failed to write file: {e.Message}");
        }
    }

    void PrintHierarchy(Transform obj, StringBuilder sb, int level)
    {
        string indent = new string('-', level);
        GameObject go = obj.gameObject;

        // Determine active state
        string activeState = go.activeInHierarchy ? "[ACTIVE]" : "[INACTIVE]";

        // GameObject details with active state
        sb.AppendLine($"{indent} {go.name} {activeState}");
        sb.AppendLine($"{indent}   Position: {obj.position}");
        sb.AppendLine($"{indent}   Rotation: {obj.rotation.eulerAngles}");
        sb.AppendLine($"{indent}   Scale: {obj.localScale}");

        // Log components
        Component[] components = go.GetComponents<Component>();
        sb.AppendLine($"{indent}   Components: [{string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}]");

        // Recursively log children
        foreach (Transform child in obj)
        {
            PrintHierarchy(child, sb, level + 1);
        }
    }
}
