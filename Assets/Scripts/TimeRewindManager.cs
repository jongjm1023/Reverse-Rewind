using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TimeRewindManager : MonoBehaviour
{
    public static TimeRewindManager Instance;

    [Header("Settings")]
    public float maxRecordTime = 20f;
    // FixedUpdate를 사용하므로 실제 기록 간격은 Project Settings > Time > Fixed Timestep(기본 0.02)을 따릅니다.
    // 호환성을 위해 변수는 남겨두지만 로직에서는 Time.fixedDeltaTime이 기준이 됩니다.
    public float recordInterval = 0.02f; 
    public float rewindDuration = 10f; 

    [Header("Object Tracking")]
    public LayerMask trackableLayers = -1;

    public struct ObjectState
    {
        public Vector3 position;
        public Quaternion rotation;
        // 역행 종료 후 자연스러운 물리 연결을 위해 속도 저장
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;

        // Index 방식이므로 TimeStamp는 이제 필요 없습니다 (데이터 절약)
        public ObjectState(Rigidbody rb)
        {
            position = rb.position;
            rotation = rb.rotation;
            linearVelocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity;
            angularVelocity = rb.isKinematic ? Vector3.zero : rb.angularVelocity;
        }
    }

    private Dictionary<Rigidbody, Queue<ObjectState>> objectHistories = new Dictionary<Rigidbody, Queue<ObjectState>>();
    private List<Rigidbody> trackedObjects = new List<Rigidbody>();

    private bool isRewinding = false;
    private MonoBehaviour disabledComponentBuffer;
    private Rigidbody currentRewindingRb = null;

    // --- Frame-by-Frame 역행 변수 ---
    private Rigidbody rewindTargetRb;
    private List<ObjectState> rewindHistoryList; // 큐를 리스트로 변환하여 인덱스 접근
    private int rewindCurrentIndex; // 현재 재생 중인 프레임 번호
    private int rewindTargetIndex;  // 목표 프레임 번호
    private bool rewindWasK;
    private bool rewindWasG;

    public bool IsRewinding => isRewinding;
    
    public Rigidbody GetRewindingRigidbody() => currentRewindingRb;
    public GameObject GetRewindingGameObject() => currentRewindingRb != null ? currentRewindingRb.gameObject : null;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        FindAllTrackableObjects();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isRewinding)
        {
            if (rewindTargetRb != null)
                FinishRewind(rewindTargetRb, rewindWasK, rewindWasG);
            ClearRewindState();
        }
        FindAllTrackableObjects();
    }

    void ClearRewindState()
    {
        isRewinding = false;
        currentRewindingRb = null;
        disabledComponentBuffer = null;
        rewindTargetRb = null;
        rewindHistoryList = null;
    }

    // [핵심 1] 모든 로직을 FixedUpdate로 통합
    // Time.timeScale = 0일 때 FixedUpdate는 실행되지 않음 -> 기록 자동 중단 -> 데이터 공백 없음
    void FixedUpdate()
    {
        if (isRewinding)
        {
            ProcessRewindFrame();
        }
        else
        {
            RecordFrame();
        }
    }

    void FindAllTrackableObjects()
    {
        trackedObjects.Clear();
        objectHistories.Clear();
        
        foreach (var rb in FindObjectsOfType<Rigidbody>())
        {
            if (((1 << rb.gameObject.layer) & trackableLayers) != 0)
            {
                trackedObjects.Add(rb);
                objectHistories[rb] = new Queue<ObjectState>();
            }
        }
    }

    // [핵심 2] 시간 계산 없이 프레임 단위 기록
    void RecordFrame()
    {
        // 20초 분량의 프레임 개수 계산 (예: 20 / 0.02 = 1000개)
        int maxFrameCount = Mathf.RoundToInt(maxRecordTime / Time.fixedDeltaTime);

        for (int i = trackedObjects.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = trackedObjects[i];
            if (rb == null) { trackedObjects.RemoveAt(i); continue; }

            if (!objectHistories.ContainsKey(rb)) objectHistories[rb] = new Queue<ObjectState>();
            Queue<ObjectState> history = objectHistories[rb];

            // 현재 상태 저장 (TimeStamp 불필요)
            history.Enqueue(new ObjectState(rb));

            // 개수 초과 시 오래된 것 삭제
            while (history.Count > maxFrameCount)
            {
                history.Dequeue();
            }
        }
    }

    public void AddTrackableObject(Rigidbody rb)
    {
        if (rb != null && !trackedObjects.Contains(rb))
        {
            if (((1 << rb.gameObject.layer) & trackableLayers) != 0)
            {
                trackedObjects.Add(rb);
                objectHistories[rb] = new Queue<ObjectState>();
            }
        }
    }

    public bool RewindObject(GameObject targetObject, float rewindSeconds)
    {
        Rigidbody rb = targetObject.GetComponent<Rigidbody>();
        if (rb == null) rb = targetObject.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = targetObject.GetComponentInChildren<Rigidbody>();

        if (rb == null || !objectHistories.ContainsKey(rb)) return false;

        if (isRewinding && rewindTargetRb != null)
        {
            FinishRewind(rewindTargetRb, rewindWasK, rewindWasG);
            ClearRewindState();
        }

        var historyQueue = objectHistories[rb];
        if (historyQueue.Count == 0) return false;

        // 큐를 리스트로 변환 (인덱스로 접근하기 위해)
        rewindHistoryList = new List<ObjectState>(historyQueue);

        // 되감기할 프레임 수 계산 (예: 10초 / 0.02 = 500프레임)
        int framesToRewind = Mathf.RoundToInt(rewindSeconds / Time.fixedDeltaTime);

        // [핵심 3] 인덱스 설정
        // 리스트의 맨 끝(최신 데이터)부터 시작
        rewindCurrentIndex = rewindHistoryList.Count - 1;
        // 목표 인덱스 (0보다 작으면 0으로)
        rewindTargetIndex = Mathf.Max(0, rewindCurrentIndex - framesToRewind);

        isRewinding = true;
        currentRewindingRb = rb;
        rewindTargetRb = rb;

        rewindWasK = rb.isKinematic;
        rewindWasG = rb.useGravity;
        
        // MovePosition을 쓰기 위해 Kinematic 설정
        rb.isKinematic = true; 
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var gear = rb.GetComponent<GearRotator>();
        if (gear != null) { gear.enabled = false; disabledComponentBuffer = gear; }

        // 시작 즉시 첫 위치로 이동 (안정성)
        MoveToFrame(rewindCurrentIndex);

        return true;
    }

    // [핵심 4] Frame-by-Frame 재생
    void ProcessRewindFrame()
    {
        if (rewindTargetRb == null || rewindHistoryList == null) return;

        // 목표 지점 도달 확인
        if (rewindCurrentIndex <= rewindTargetIndex)
        {
            // 미래 데이터 삭제 (Future Pruning)
            if (rewindTargetRb != null && objectHistories.ContainsKey(rewindTargetRb))
            {
                Queue<ObjectState> q = objectHistories[rewindTargetRb];
                q.Clear();
                // 0번부터 현재 도달한 프레임까지만 복구
                for (int i = 0; i <= rewindCurrentIndex; i++)
                {
                    q.Enqueue(rewindHistoryList[i]);
                }
            }

            FinishRewind(rewindTargetRb, rewindWasK, rewindWasG);
            ClearRewindState();
            return;
        }

        // 인덱스를 하나 줄임 (1프레임 과거로)
        rewindCurrentIndex--;
        MoveToFrame(rewindCurrentIndex);
    }

    void MoveToFrame(int index)
    {
        if (index >= 0 && index < rewindHistoryList.Count)
        {
            ObjectState state = rewindHistoryList[index];
            // MovePosition: 물리 엔진을 통해 이동 (마찰력 발생)
            rewindTargetRb.MovePosition(state.position);
            rewindTargetRb.MoveRotation(state.rotation);
        }
    }

    void FinishRewind(Rigidbody rb, bool k, bool g)
    {
        if (rb != null)
        {
            rb.isKinematic = k;
            rb.useGravity = g;
            if (!k) 
            {
                // [추가] 물리적 연속성을 위해 역행 종료 시점의 속도 복원
                if (rewindHistoryList != null && rewindCurrentIndex >= 0 && rewindCurrentIndex < rewindHistoryList.Count)
                {
                    rb.linearVelocity = rewindHistoryList[rewindCurrentIndex].linearVelocity;
                    rb.angularVelocity = rewindHistoryList[rewindCurrentIndex].angularVelocity;
                }
                rb.WakeUp();
            }
            if (disabledComponentBuffer != null) { disabledComponentBuffer.enabled = true; disabledComponentBuffer = null; }
        }
        isRewinding = false;
        currentRewindingRb = null;
    }

    // 호환성 유지용 메서드
    public int GetRecordedStateCount(Rigidbody rb)
    {
        if (rb != null && objectHistories.ContainsKey(rb)) return objectHistories[rb].Count;
        return 0;
    }
    
    public bool IsTracked(Rigidbody rb)
    {
        return objectHistories.ContainsKey(rb);
    }
}