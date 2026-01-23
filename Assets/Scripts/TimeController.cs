using UnityEngine;
using UnityEngine.InputSystem;

public class TimeController : MonoBehaviour
{
    public static TimeController Instance;
    
    [Header("Input Settings")]
    [Tooltip("키를 눌러 시간을 멈춥니다.")]
    public Key freezeKey = Key.Q;
    
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
            if (StateManager.Instance.CurrentState() == State.TimeFreeze)
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
    
    public bool IsTimeFrozen => StateManager.Instance.CurrentState() == State.TimeFreeze;
    
    public void FreezeTime()
    {
        // 상태 체크
        if (!StateManager.Instance.CanUseAbility())
        {
            Debug.LogWarning("다른 능력 사용 중입니다!");
            return;
        }
        
        Time.timeScale = 0f;
        
        // 상태 변경
        StateManager.Instance.SetState(State.TimeFreeze);
        
        // 마우스 커서 표시 및 잠금 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        Debug.Log("시간이 멈췄습니다.");
    }
    
    public void ResumeTime(GameObject rewindTarget = null)
    {
        if (rewindTarget != null)
        {
            // 오브젝트 클릭 시 - 역행 시작
            selectedRewindObject = rewindTarget;
            StartRewind(rewindTarget);
        }
        else
        {
            // 단순 재개 (Q키 다시 누름)
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // 상태 초기화
            StateManager.Instance.SetState(State.Normal);

            Debug.Log("시간이 재개되었습니다.");
        }
    }

    void StartRewind(GameObject target)
    {
        // 시간 재개
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 상태 변경 (역행 시작)
        StateManager.Instance.SetState(State.TimeRewind);

        // TimeRewindObject는 나중에 추가할 예정
        Debug.Log($"{target.name}가 선택되었습니다. (TimeRewindObject는 아직 추가되지 않았습니다)");

        StateManager.Instance.SetState(State.Normal);
        Debug.Log("시간이 재개되었습니다.");
    }
}
