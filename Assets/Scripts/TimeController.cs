using UnityEngine;
using UnityEngine.InputSystem;

public class TimeController : MonoBehaviour
{
    public static TimeController Instance;
    
    [Header("Input Settings")]
    [Tooltip("키를 눌러 시간을 멈춥니다.")]
    public Key freezeKey = Key.Q;
    
    private bool isTimeFrozen = false;
    private GameObject selectedRewindObject = null;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Update()
    {
        // Input System은 timeScale 영향 안받음
        if (Keyboard.current != null && Keyboard.current[freezeKey].wasPressedThisFrame)
        {
            if (isTimeFrozen)
            {
                // 이미 멈춰있으면 재개 (역행 없이)
                ResumeTime();
            }
            else
            {
                // 시간 멈춤
                FreezeTime();
            }
        }
    }
    
    public bool IsTimeFrozen => isTimeFrozen;
    
    public void FreezeTime()
    {
        Time.timeScale = 0f;
        isTimeFrozen = true;
        Debug.Log("시간이 멈췄습니다.");
    }
    
    public void ResumeTime(GameObject rewindTarget = null)
    {
        Time.timeScale = 1f;
        isTimeFrozen = false;
        
        if (rewindTarget != null)
        {
            selectedRewindObject = rewindTarget;
            StartRewind(rewindTarget);
        }
        else
        {
            Debug.Log("시간이 재개되었습니다.");
        }
    }
    
    void StartRewind(GameObject target)
    {
        // TimeRewindObject는 나중에 추가할 예정 (3단계에서 생성)
        // 일단 시간 재개만 처리
        Debug.Log($"{target.name}가 선택되었습니다. (TimeRewindObject는 아직 추가되지 않았습니다)");
    }
}
