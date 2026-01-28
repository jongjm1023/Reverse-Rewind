using UnityEngine;
using UnityEngine.SceneManagement;

public class Main : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Name of the scene to load.")]
    public string targetSceneName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // 메인 씬으로 돌아왔을 때 커서가 보이도록 설정
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

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
