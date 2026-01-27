using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;

public class TimeController : MonoBehaviour
{
    public static TimeController Instance;
    
    [Header("Input Settings")]
    [Tooltip("키를 눌러 시간을 멈춥니다.")]
    public Key freezeKey = Key.Q;
    
    [Header("References")]
    public PlayerController player;

    // 현재 시간이 멈췄는지 확인하는 프로퍼티
    public bool IsTimeFrozen => StateManager.Instance != null && StateManager.Instance.CurrentState() == State.TimeFreeze;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }
    
    void Start()
    {
        FindPlayer();
        // 씬 로드 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 리로드 시 참조 다시 찾기
        FindPlayer();
        // 씬 리로드 시 상태 초기화
        if (StateManager.Instance != null)
        {
            StateManager.Instance.SetState(State.Normal);
        }
        // 시간 스케일 초기화
        Time.timeScale = 1f;
        ToggleCursor(false);
        
        // 중력 상태 초기화
        ResetGravityState();
    }

    // 중력 상태 초기화 헬퍼 메서드
    private void ResetGravityState()
    {
        // MapFlipper 찾아서 중력 초기화
        MapFlipper mapFlipper = FindObjectOfType<MapFlipper>();
        if (mapFlipper != null)
        {
            mapFlipper.ResetGravity();
        }
        else
        {
            // MapFlipper가 없어도 Physics.gravity는 초기화
            Physics.gravity = new Vector3(0, -9.81f, 0);
        }
        
        // CameraFollow의 targetZRoll 초기화
        CameraFollow cameraFollow = FindObjectOfType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.targetZRoll = 0f;
        }
        
        // 플레이어 회전 초기화
        if (player != null)
        {
            player.transform.rotation = Quaternion.identity;
        }
    }

    void FindPlayer()
    {
        if (player == null) player = FindObjectOfType<PlayerController>();
    }
    
    void Update()
    {
        // 키 입력 감지 (Input System 패키지 사용 시)
        if (Keyboard.current != null && Keyboard.current[freezeKey].wasPressedThisFrame)
        {
            if (IsTimeFrozen)
            {
                // 이미 멈춰있으면 그냥 재개 (타겟 없이)
                ResumeTime(); 
            }
            else
            {
                // 시간 멈춤 시도
                FreezeTime();
            }
        }
    }
    
    public void FreezeTime()
    {
        // 1. 상태 및 조건 체크
        if (StateManager.Instance == null || !StateManager.Instance.CanUseAbility())
        {
            Debug.LogWarning("다른 능력 사용 중이거나 상태 매니저가 없습니다!");
            return;
        }

        if (player != null && !player.IsGrounded())
        {
            Debug.LogWarning("공중에서는 능력을 사용할 수 없습니다!");
            return;
        }
        
        // 2. 시간 정지 수행
        Time.timeScale = 0f;
        StateManager.Instance.SetState(State.TimeFreeze);
        ToggleCursor(true); // 커서 보이기
        
        Debug.Log("시간이 멈췄습니다.");
    }
    
    // rewindTarget이 null이면 단순 재개, 있으면 역행 시작
    public void ResumeTime(GameObject rewindTarget = null)
    {
        if (rewindTarget != null)
        {
            StartRewind(rewindTarget);
        }
        else
        {
            // 단순 재개
            SetNormalState();
            Debug.Log("시간이 재개되었습니다.");
        }
    }

    private void StartRewind(GameObject target)
    {
        // 1. 매니저 설정값 가져오기
        float duration = TimeRewindManager.Instance.rewindDuration;
        
        // 2. 역행 시도 (매니저 호출)
        bool success = TimeRewindManager.Instance.RewindObject(target, duration);

        if (success)
        {
            // 성공 시: 시간을 다시 흐르게 하고 역행 상태로 전환
            Time.timeScale = 1f;
            ToggleCursor(false);
            StateManager.Instance.SetState(State.TimeRewind);
            
            Debug.Log($"{target.name} 역행 시작 ({duration}초)");
            StartCoroutine(WaitForRewindComplete());
        }
        else
        {
            // 실패 시: 즉시 일반 상태로 복귀 및 에러 로그
            SetNormalState();
            LogRewindFailure(target);
        }
    }

    // 역행이 끝날 때까지 대기하는 코루틴
    IEnumerator WaitForRewindComplete()
    {
        // 매니저가 역행 중이라고 하는 동안 대기
        while (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
        {
            yield return null;
        }

        // 끝났으면 정상 상태로
        StateManager.Instance.SetState(State.Normal);
        Debug.Log("역행 완료. 정상 상태 복귀.");
    }

    // [헬퍼] 정상 상태(게임 플레이)로 되돌리기
    private void SetNormalState()
    {
        Time.timeScale = 1f;
        StateManager.Instance.SetState(State.Normal);
        ToggleCursor(false);
    }

    // [헬퍼] 커서 잠금/해제 통합 관리
    private void ToggleCursor(bool show)
    {
        Cursor.visible = show;
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // [헬퍼] 역행 실패 원인 분석 로그 (디버깅용)
    private void LogRewindFailure(GameObject target)
    {
        // Rigidbody 찾기 로직 (부모/자식 포함)
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (!rb) rb = target.GetComponentInParent<Rigidbody>();
        if (!rb) rb = target.GetComponentInChildren<Rigidbody>();

        string msg = $"{target.name} 역행 실패: ";
        if (rb == null) msg += "Rigidbody 없음";
        else if (!TimeRewindManager.Instance.IsTracked(rb)) msg += "추적 대상 아님 (Kinematic 여부 확인)";
        else msg += $"기록 없음 (Count: {TimeRewindManager.Instance.GetRecordedStateCount(rb)})";

        Debug.LogWarning(msg);
    }
}