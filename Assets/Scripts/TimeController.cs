using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class TimeController : MonoBehaviour
{
    public static TimeController Instance;
    
    [Header("Input Settings")]
    [Tooltip("키를 눌러 시간을 멈춥니다.")]
    public Key freezeKey = Key.Q;
    
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

        // 오브젝트를 되감기처럼 역행 시작 (비동기)
        float rewindSeconds = TimeRewindManager.Instance.rewindDuration;
        bool rewindStarted = TimeRewindManager.Instance.RewindObject(target, rewindSeconds);

        if (!rewindStarted)
        {
            // 역행 시작 실패 - 상태를 정상으로 복귀
            StateManager.Instance.SetState(State.Normal);
            
            // 실패 원인 확인을 위해 상세 정보 출력
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb == null) rb = target.GetComponentInParent<Rigidbody>();
            if (rb == null) rb = target.GetComponentInChildren<Rigidbody>();
            
            if (rb == null)
            {
                Debug.LogWarning($"{target.name}의 역행을 시작할 수 없습니다: Rigidbody를 찾을 수 없습니다.");
            }
            else if (!TimeRewindManager.Instance.IsTracked(rb))
            {
                Debug.LogWarning($"{target.name}의 역행을 시작할 수 없습니다: {rb.gameObject.name}는 추적 중인 오브젝트가 아닙니다. (현재 Kinematic: {rb.isKinematic}, 추적 목록에 없음)");
            }
            else
            {
                int recordCount = TimeRewindManager.Instance.GetRecordedStateCount(rb);
                Debug.LogWarning($"{target.name}의 역행을 시작할 수 없습니다: {rb.gameObject.name}의 기록된 상태가 없습니다. (기록 개수: {recordCount})");
            }
            return;
        }

        // 상태 변경 (역행 시작)
        StateManager.Instance.SetState(State.TimeRewind);

        // 역행은 코루틴으로 비동기 실행되므로 여기서는 상태만 설정
        // 역행 완료는 TimeRewindManager에서 처리
        Debug.Log($"{target.name}의 역행을 시작합니다. ({rewindSeconds}초 전으로 되감기)");
        
        // 역행 완료를 기다리는 코루틴 시작
        StartCoroutine(WaitForRewindComplete());
    }

    // 역행이 완료될 때까지 기다린 후 상태를 정상으로 복귀
    IEnumerator WaitForRewindComplete()
    {
        // TimeRewindManager가 역행 중인지 확인
        while (TimeRewindManager.Instance != null && TimeRewindManager.Instance.IsRewinding)
        {
            yield return null;
        }

        // 역행 완료 후 정상 상태로 복귀
        StateManager.Instance.SetState(State.Normal);
        Debug.Log("역행이 완료되었습니다.");
    }
}
