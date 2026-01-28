using UnityEngine;
using UnityEngine.SceneManagement;

public class Main : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Name of the scene to load.")]
    public string targetSceneName;

    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            Debug.Log($"Loading Scene by Name: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("SceneTransitionZone: No Target Scene Name specified!");
        }
    }
}
