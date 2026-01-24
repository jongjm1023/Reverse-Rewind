using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class TimeRewindManager : MonoBehaviour
{
    public static TimeRewindManager Instance;

    [Header("Settings")]
    public float maxRecordTime = 20f;

    public float recordInterval = 0.02f;

    // 실제로 사용되지 않지만 기존 코드 유지를 위해 남겨둠 (호출 시 인자값 사용됨)
    public float rewindDuration = 10f; 

    [Header("Object Tracking")]
    public LayerMask trackableLayers = -1;
    public Transform mapRoot;

    // 오브젝트 상태 데이터 구조체 
    public struct ObjectState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public float timeStamp;

        public ObjectState(Vector3 pos, Quaternion rot, Vector3 linVel, Vector3 angVel, float time)
        {
            position = pos;
            rotation = rot;
            linearVelocity = linVel;
            angularVelocity = angVel;
            timeStamp = time;
        }
    }

    // 각 오브젝트별 상태 히스토리 (순환 버퍼)
    private Dictionary<Rigidbody, Queue<ObjectState>> objectHistories = new Dictionary<Rigidbody, Queue<ObjectState>>();
    
    // 추적 중인 모든 Rigidbody 리스트
    private List<Rigidbody> trackedObjects = new List<Rigidbody>();

    // 기록 시작 시간 (realtimeSinceStartup 사용 - timeScale 영향 안받음)
    private float recordStartTime;

    // 역행 중인지 여부
    private bool isRewinding = false;
    private Rigidbody currentRewindTarget = null;
    private Coroutine rewindCoroutine = null;

    /// <summary>
    /// 현재 역행 중인지 여부
    /// </summary>
    public bool IsRewinding => isRewinding;

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

    void Start()
    {
        // realtimeSinceStartup 사용 - timeScale 영향 안받음
        recordStartTime = Time.realtimeSinceStartup;
        FindAllTrackableObjects();
        StartCoroutine(RecordStatesCoroutine());
    }

    // 모든 Rigidbody를 찾는 헬퍼 메서드
    private Rigidbody[] GetAllRigidbodies()
    {
        if (mapRoot != null)
        {
            return mapRoot.GetComponentsInChildren<Rigidbody>();
        }
        else
        {
            return FindObjectsOfType<Rigidbody>();
        }
    }

    // 씬의 모든 추적 가능한 Rigidbody를 찾습니다
    void FindAllTrackableObjects()
    {
        trackedObjects.Clear();
        objectHistories.Clear();

        Rigidbody[] allRigidbodies = GetAllRigidbodies();

        foreach (Rigidbody rb in allRigidbodies)
        {
            // 레이어 필터링
            if (trackableLayers == (trackableLayers | (1 << rb.gameObject.layer)))
            {
                // Kinematic이 아닌 것만 추적 (움직일 수 있는 것만)
                if (!rb.isKinematic)
                {
                    trackedObjects.Add(rb);
                    objectHistories[rb] = new Queue<ObjectState>();
                }
            }
        }

        Debug.Log($"TimeRewindManager: {trackedObjects.Count}개의 오브젝트를 추적 시작합니다.");
    }

    // 주기적으로 모든 오브젝트의 상태를 기록
    IEnumerator RecordStatesCoroutine()
    {
        while (true)
        {
            // 역행 중인 오브젝트를 제외하고 기록
            RecordAllStates();

            // recordInterval이 0이면 매 프레임 기록, 아니면 지정된 간격으로 기록
            if (recordInterval > 0f)
            {
                yield return new WaitForSecondsRealtime(recordInterval);
            }
            else
            {
                yield return null; // 매 프레임 기록
            }
        }
    }

    /// <summary>
    /// 현재 시점의 모든 추적 오브젝트의 상태를 기록합니다
    /// </summary>
    void RecordAllStates()
    {
        // 시간 정지 상태에서는 기록하지 않음
        if (StateManager.Instance != null && StateManager.Instance.CurrentState() == State.TimeFreeze)
        {
            return;
        }

        // realtimeSinceStartup 사용 - timeScale 영향 안받음
        float currentTime = Time.realtimeSinceStartup;
        float relativeTime = currentTime - recordStartTime;

        // 오브젝트가 삭제되었을 수 있으므로 리스트 정리
        List<Rigidbody> toRemove = new List<Rigidbody>();

        // 기존 추적 목록의 오브젝트들 처리
        foreach (Rigidbody rb in trackedObjects)
        {
            if (rb == null)
            {
                toRemove.Add(rb);
                continue;
            }

            // 역행 중인 오브젝트는 kinematic이어도 추적 목록에 유지하고 기록만 하지 않음
            if (isRewinding && currentRewindTarget == rb)
            {
                continue;
            }

            // Kinematic이 되었으면 추적 중지 (단, 역행 중인 오브젝트는 제외)
            if (rb.isKinematic)
            {
                toRemove.Add(rb);
                continue;
            }

            // 현재 상태 기록
            ObjectState state = new ObjectState(
                rb.position,
                rb.rotation,
                rb.linearVelocity,
                rb.angularVelocity,
                relativeTime
            );

            Queue<ObjectState> history = objectHistories[rb];
            history.Enqueue(state);

            // 최대 기록 시간을 초과하는 오래된 데이터 제거
            while (history.Count > 0 && (relativeTime - history.Peek().timeStamp) > maxRecordTime)
            {
                history.Dequeue();
            }
        }

        // 삭제된 오브젝트 정리
        foreach (Rigidbody rb in toRemove)
        {
            trackedObjects.Remove(rb);
            if (objectHistories.ContainsKey(rb))
            {
                objectHistories.Remove(rb);
            }
        }

        // 새로운 추적 가능한 오브젝트 찾기 (추적 목록에 없지만 추적 가능한 오브젝트)
        Rigidbody[] allRigidbodies = GetAllRigidbodies();

        foreach (Rigidbody rb in allRigidbodies)
        {
            // 이미 추적 중이면 스킵
            if (trackedObjects.Contains(rb))
            {
                continue;
            }

            // 레이어 필터링
            if (trackableLayers == (trackableLayers | (1 << rb.gameObject.layer)))
            {
                // Kinematic이 아닌 것만 추적 (움직일 수 있는 것만)
                if (!rb.isKinematic)
                {
                    // 새로운 오브젝트 발견 - 추적 목록에 추가
                    AddTrackableObject(rb);
                }
            }
        }
    }

    /// <summary>
    /// 특정 오브젝트를 되감기처럼 역행시킵니다 (코루틴)
    /// </summary>
    /// <returns>역행이 성공적으로 시작되었는지 여부</returns>
    public bool RewindObject(GameObject targetObject, float rewindSeconds)
    {
        // Rigidbody 찾기 (자식 오브젝트에서도 찾기)
        Rigidbody targetRb = targetObject.GetComponent<Rigidbody>();
        if (targetRb == null)
        {
            targetRb = targetObject.GetComponentInParent<Rigidbody>();
        }
        if (targetRb == null)
        {
            targetRb = targetObject.GetComponentInChildren<Rigidbody>();
        }

        if (targetRb == null)
        {
            Debug.LogWarning($"TimeRewindManager: {targetObject.name}에 Rigidbody가 없습니다!");
            return false;
        }

        // 추적 목록에 없으면 실패
        if (!objectHistories.ContainsKey(targetRb))
        {
            Debug.LogWarning($"TimeRewindManager: {targetRb.gameObject.name}는 추적 중인 오브젝트가 아닙니다! (RecordAllStates에서 자동으로 추가되어야 합니다)");
            return false;
        }

        Queue<ObjectState> history = objectHistories[targetRb];
        if (history.Count == 0)
        {
            Debug.LogWarning($"TimeRewindManager: {targetRb.gameObject.name}의 기록된 상태가 없습니다!");
            return false;
        }

        // 이미 역행 중이면 중지
        if (isRewinding && rewindCoroutine != null)
        {
            StopCoroutine(rewindCoroutine);
            isRewinding = false;
        }

        // 역행 코루틴 시작
        rewindCoroutine = StartCoroutine(RewindObjectCoroutine(targetRb, rewindSeconds));
        return true;
    }

    /// <summary>
    /// 오브젝트를 되감기처럼 역행시키는 코루틴 (수정됨: 시간 기반 재생 + 속도 복원 + 미래 삭제)
    /// </summary>
    IEnumerator RewindObjectCoroutine(Rigidbody targetRb, float rewindSeconds)
    {
        isRewinding = true;
        currentRewindTarget = targetRb;

        // 원래 상태 저장
        bool wasKinematic = targetRb.isKinematic;
        bool wasGravity = targetRb.useGravity;

        // 역행 중에는 kinematic으로 설정하여 물리 엔진의 간섭을 완전히 차단
        targetRb.isKinematic = true;
        targetRb.useGravity = false;
        targetRb.linearVelocity = Vector3.zero;
        targetRb.angularVelocity = Vector3.zero;

        // 히스토리를 리스트로 변환
        List<ObjectState> stateList = new List<ObjectState>(objectHistories[targetRb]);
        
        if (stateList.Count == 0)
        {
            Debug.LogWarning($"TimeRewindManager: {targetRb.gameObject.name}의 기록된 상태가 없습니다!");
            RestoreRigidbodyState(targetRb, wasKinematic, wasGravity);
            yield break;
        }

        // 시간 계산
        float lastTimeStamp = stateList[stateList.Count - 1].timeStamp;
        float targetTime = lastTimeStamp - rewindSeconds;
        
        // 데이터가 없는 경우 최소 시간으로 제한 (stateList[0].timeStamp)
        if (targetTime < stateList[0].timeStamp)
        {
            targetTime = stateList[0].timeStamp;
        }

        // WaitForSecondsRealtime 대신 Playhead(재생 헤드)를 사용하여 정확한 시간을 역추적
        float currentPlayhead = lastTimeStamp;
        int searchStartIndex = stateList.Count - 1; // 검색 최적화를 위한 인덱스

        while (currentPlayhead > targetTime)
        {
            // 역행 중인 오브젝트가 파괴됐으면 즉시 종료 (예: 스위치 밟아서 큐브 생성기가 기존 큐브 제거)
            if (targetRb == null)
            {
                isRewinding = false;
                currentRewindTarget = null;
                rewindCoroutine = null;
                Debug.Log("TimeRewindManager: 역행 대상이 파괴되어 역행을 중단합니다.");
                yield break;
            }

            // 실제 흐른 시간(unscaledDeltaTime)만큼 Playhead를 과거로 이동
            currentPlayhead -= Time.unscaledDeltaTime;

            // 목표 시간보다 더 갔다면 보정
            if (currentPlayhead < targetTime) currentPlayhead = targetTime;

            // 현재 Playhead 시간과 가장 가까운(작거나 같은) 과거 데이터 찾기
            int foundIndex = -1;
            for (int i = searchStartIndex; i >= 0; i--)
            {
                if (stateList[i].timeStamp <= currentPlayhead)
                {
                    foundIndex = i;
                    searchStartIndex = i; // 다음 프레임에서는 여기서부터 검색 (최적화)
                    break;
                }
            }

            if (foundIndex != -1)
            {
                // 위치 동기화
                ObjectState state = stateList[foundIndex];
                targetRb.transform.position = state.position;
                targetRb.transform.rotation = state.rotation;
                targetRb.position = state.position;
                targetRb.rotation = state.rotation;
            }

            yield return null; // 매 프레임 대기
        }

        // 루프 탈출 직후에도 파괴됐을 수 있음 (스위치 등으로 제거)
        if (targetRb == null)
        {
            isRewinding = false;
            currentRewindTarget = null;
            rewindCoroutine = null;
            Debug.Log("TimeRewindManager: 역행 대상이 파괴되어 역행을 중단합니다.");
            yield break;
        }

        // 최종 도달한 상태 확정 (오차 제거)
        int finalIndex = 0;
        // targetTime 이하인 가장 최신 상태 찾기
        for (int i = stateList.Count - 1; i >= 0; i--)
        {
            if (stateList[i].timeStamp <= targetTime)
            {
                finalIndex = i;
                break;
            }
        }
        
        ObjectState finalState = stateList[finalIndex];
        
        // 최종 위치 적용
        targetRb.transform.position = finalState.position;
        targetRb.transform.rotation = finalState.rotation;
        targetRb.position = finalState.position;
        targetRb.rotation = finalState.rotation;
        
        // 물리 엔진 싱크 대기
        yield return new WaitForFixedUpdate();

        // Kinematic 상태 복원
        targetRb.isKinematic = wasKinematic;
        targetRb.useGravity = wasGravity;

        if (!wasKinematic)
        {
            targetRb.linearVelocity = Vector3.zero;
            targetRb.angularVelocity = Vector3.zero;
        }
        // 역행한 시점 이후(미래)의 데이터를 큐에서 제거
        
        Queue<ObjectState> history = objectHistories[targetRb];
        history.Clear();

        // 0부터 finalIndex(현재 시점)까지의 데이터만 남기고 큐 재구성
        for (int i = 0; i <= finalIndex; i++)
        {
            history.Enqueue(stateList[i]);
        }

        // 역행 완료
        isRewinding = false;
        currentRewindTarget = null;
        rewindCoroutine = null;

        Debug.Log($"TimeRewindManager: {targetRb.gameObject.name}의 역행이 완료되었습니다. (기록 시간: {finalState.timeStamp:F2}초, 남은 기록: {history.Count}개)");
    }

    /// <summary>
    /// Rigidbody 상태를 복원하고 역행 상태를 초기화합니다
    /// </summary>
    private void RestoreRigidbodyState(Rigidbody rb, bool wasKinematic, bool wasGravity)
    {
        if (rb == null) return;
        
        rb.isKinematic = wasKinematic;
        rb.useGravity = wasGravity;
        isRewinding = false;
        currentRewindTarget = null;
        rewindCoroutine = null;
    }

    /// <summary>
    /// 동적으로 오브젝트를 추적 목록에 추가합니다
    /// </summary>
    public void AddTrackableObject(Rigidbody rb)
    {
        if (rb != null && !rb.isKinematic && !trackedObjects.Contains(rb))
        {
            trackedObjects.Add(rb);
            objectHistories[rb] = new Queue<ObjectState>();
            Debug.Log($"TimeRewindManager: {rb.gameObject.name}를 추적 목록에 추가했습니다.");
        }
    }

    /// <summary>
    /// 오브젝트를 추적 목록에서 제거합니다
    /// </summary>
    public void RemoveTrackableObject(Rigidbody rb)
    {
        if (trackedObjects.Contains(rb))
        {
            trackedObjects.Remove(rb);
            if (objectHistories.ContainsKey(rb))
            {
                objectHistories.Remove(rb);
            }
        }
    }

    /// <summary>
    /// 특정 오브젝트의 기록된 상태 개수를 반환합니다
    /// </summary>
    public int GetRecordedStateCount(Rigidbody rb)
    {
        if (objectHistories.ContainsKey(rb))
        {
            return objectHistories[rb].Count;
        }
        return 0;
    }

    /// <summary>
    /// 특정 Rigidbody가 추적 중인지 확인합니다
    /// </summary>
    public bool IsTracked(Rigidbody rb)
    {
        return objectHistories.ContainsKey(rb);
    }
}