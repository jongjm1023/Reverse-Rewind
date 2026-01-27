using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TimeRewindManager : MonoBehaviour
{
    public static TimeRewindManager Instance;

    [Header("Settings")]
    public float maxRecordTime = 20f;
    public float rewindDuration = 10f; // 한 번 역행할 때 돌아갈 시간
    public float recordInterval = 0.02f;

    [Header("Optimization Settings")]
    public float maxStationarySaveTime = 10f; // 정지 상태는 최대 10초까지만 기록
    public float stationaryThreshold = 0.08f; // 정지라고 판단할 속도 오차값

    [Header("Object Tracking")]
    public LayerMask trackableLayers = -1;

    public struct ObjectState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;

        public ObjectState(Rigidbody rb)
        {
            position = rb.position;
            rotation = rb.rotation;
            linearVelocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity;
            angularVelocity = rb.isKinematic ? Vector3.zero : rb.angularVelocity;
        }

        // 정지 상태인지 확인하는 헬퍼 함수
        public bool IsStationary(float threshold)
        {
            return linearVelocity.sqrMagnitude < threshold * threshold && 
                   angularVelocity.sqrMagnitude < threshold * threshold;
        }
    }

    private Dictionary<Rigidbody, Queue<ObjectState>> objectHistories = new Dictionary<Rigidbody, Queue<ObjectState>>();
    private Dictionary<Rigidbody, int> stationaryCounts = new Dictionary<Rigidbody, int>();
    private List<Rigidbody> trackedObjects = new List<Rigidbody>();

    private bool isRewinding = false;
    private MonoBehaviour disabledComponentBuffer;
    private Rigidbody currentRewindingRb = null;

    private Rigidbody rewindTargetRb;
    private List<ObjectState> rewindHistoryList;
    private int rewindCurrentIndex;
    private int rewindTargetIndex;
    private bool rewindWasK;
    private bool rewindWasG;
    private float rewindWasGearSpeed;

    public bool IsRewinding => isRewinding;
    
    public Rigidbody GetRewindingRigidbody() => currentRewindingRb;

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
            if (rewindTargetRb != null) FinishRewind(rewindTargetRb, rewindWasK, rewindWasG);
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
        rewindWasGearSpeed = 0f;
    }

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
        stationaryCounts.Clear(); 
        
        foreach (var rb in FindObjectsOfType<Rigidbody>())
        {
            AddTrackableObject(rb);
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
                stationaryCounts[rb] = 0; 
            }
        }
    }

    void RecordFrame()
    {
        int maxFrameCount = Mathf.RoundToInt(maxRecordTime / Time.fixedDeltaTime);
        int maxStationaryFrames = Mathf.RoundToInt(maxStationarySaveTime / Time.fixedDeltaTime);

        for (int i = trackedObjects.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = trackedObjects[i];
            if (rb == null) { trackedObjects.RemoveAt(i); continue; }

            if (!objectHistories.ContainsKey(rb)) 
            {
                objectHistories[rb] = new Queue<ObjectState>();
                stationaryCounts[rb] = 0;
            }

            Queue<ObjectState> history = objectHistories[rb];
            ObjectState newState = new ObjectState(rb);

            // GearRotator가 있는 오브젝트는 정지 상태 판단에서 제외 (항상 기록)
            bool hasGearRotator = rb.GetComponent<GearRotator>() != null;
            bool isCurrentlyStationary = hasGearRotator ? false : newState.IsStationary(stationaryThreshold);

            if (isCurrentlyStationary)
            {
                if (stationaryCounts[rb] >= maxStationaryFrames)
                {
                    continue; 
                }
                stationaryCounts[rb]++;
            }
            else
            {
                stationaryCounts[rb] = 0;
            }

            history.Enqueue(newState);

            while (history.Count > maxFrameCount)
            {
                history.Dequeue();
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

        rewindHistoryList = new List<ObjectState>(historyQueue);
        
        // 정지 구간 스킵 로직 (GearRotator가 있는 오브젝트는 스킵하지 않음)
        int lastIndex = rewindHistoryList.Count - 1;
        bool hasGearRotator = rb.GetComponent<GearRotator>() != null;
        
        if (!hasGearRotator)
        {
            while (lastIndex > 0 && rewindHistoryList[lastIndex].IsStationary(stationaryThreshold))
            {
                lastIndex--;
            }
            
            if (lastIndex < 0) lastIndex = rewindHistoryList.Count - 1;
        }

        rewindCurrentIndex = lastIndex; 

        int framesToRewind = Mathf.RoundToInt(rewindSeconds / Time.fixedDeltaTime);
        rewindTargetIndex = Mathf.Max(0, rewindCurrentIndex - framesToRewind);

        if (rewindCurrentIndex <= rewindTargetIndex)
        {
             rewindTargetIndex = Mathf.Max(0, rewindCurrentIndex - 10);
        }

        isRewinding = true;
        currentRewindingRb = rb;
        rewindTargetRb = rb;

        rewindWasK = rb.isKinematic;
        rewindWasG = rb.useGravity;
        
        rb.isKinematic = true; 
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var gear = rb.GetComponent<GearRotator>();
        if (gear != null) 
        { 
            // 역행 중에는 속도를 반대로 설정하여 반대 방향으로 회전
            rewindWasGearSpeed = gear.speed;
            gear.speed = -gear.speed; // 반대 방향으로 회전
            disabledComponentBuffer = gear; 
        }

        MoveToFrame(rewindCurrentIndex); 

        return true;
    }

    void ProcessRewindFrame()
    {
        // 역행 중인 오브젝트가 파괴되었는지 확인
        if (rewindTargetRb == null || rewindHistoryList == null)
        {
            // 오브젝트가 파괴되었으면 역행 상태 정리
            ClearRewindState();
            if (StateManager.Instance != null)
            {
                StateManager.Instance.SetState(State.Normal);
            }
            return;
        }

        if (rewindCurrentIndex <= rewindTargetIndex)
        {
            if (rewindTargetRb != null && objectHistories.ContainsKey(rewindTargetRb))
            {
                Queue<ObjectState> q = objectHistories[rewindTargetRb];
                q.Clear();
                for (int i = 0; i <= rewindCurrentIndex; i++)
                {
                    q.Enqueue(rewindHistoryList[i]);
                }
                
                if (rewindHistoryList[rewindCurrentIndex].IsStationary(stationaryThreshold))
                    stationaryCounts[rewindTargetRb] = 1; 
                else
                    stationaryCounts[rewindTargetRb] = 0;
            }

            FinishRewind(rewindTargetRb, rewindWasK, rewindWasG);
            ClearRewindState();
            if (StateManager.Instance != null)
            {
                StateManager.Instance.SetState(State.Normal);
            }
            return;
        }

        rewindCurrentIndex--;
        MoveToFrame(rewindCurrentIndex);
    }

    void MoveToFrame(int index)
    {
        if (rewindTargetRb == null) return; // 오브젝트가 파괴되었으면 처리하지 않음
        
        if (index >= 0 && index < rewindHistoryList.Count)
        {
            ObjectState state = rewindHistoryList[index];
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
                if (rewindHistoryList != null && rewindCurrentIndex >= 0 && rewindCurrentIndex < rewindHistoryList.Count)
                {
                    rb.linearVelocity = rewindHistoryList[rewindCurrentIndex].linearVelocity;
                    rb.angularVelocity = rewindHistoryList[rewindCurrentIndex].angularVelocity;
                }
                rb.WakeUp();
            }
            if (disabledComponentBuffer != null) 
            { 
                var gear = disabledComponentBuffer as GearRotator;
                if (gear != null)
                {
                    // 원래 속도로 복원
                    gear.speed = rewindWasGearSpeed;
                }
                disabledComponentBuffer = null; 
            }
        }
        isRewinding = false;
        currentRewindingRb = null;
    }

    // 현재 역행 중인 오브젝트의 GameObject 반환
    public GameObject GetRewindingGameObject()
    {
        return currentRewindingRb != null ? currentRewindingRb.gameObject : null;
    }

    // 해당 Rigidbody가 기록되고 있는지 확인
    public bool IsTracked(Rigidbody rb)
    {
        return rb != null && objectHistories.ContainsKey(rb);
    }

    // 현재 저장된 프레임(기록)의 개수를 반환 (디버깅용)
    public int GetRecordedStateCount(Rigidbody rb)
    {
        if (rb != null && objectHistories.ContainsKey(rb))
        {
            return objectHistories[rb].Count;
        }
        return 0;
    }
}