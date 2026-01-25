using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class TimeRewindManager : MonoBehaviour
{
    public static TimeRewindManager Instance;

    [Header("Settings")]
    public float maxRecordTime = 20f;
    public float recordInterval = 0.02f;
    public float rewindDuration = 10f; 

    [Header("Object Tracking")]
    public LayerMask trackableLayers = -1;

    public struct ObjectState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public float timeStamp;

        public ObjectState(Rigidbody rb, float time)
        {
            position = rb.position;
            rotation = rb.rotation;
            linearVelocity = rb.isKinematic ? Vector3.zero : rb.linearVelocity;
            angularVelocity = rb.isKinematic ? Vector3.zero : rb.angularVelocity;
            timeStamp = time;
        }
    }

    private Dictionary<Rigidbody, Queue<ObjectState>> objectHistories = new Dictionary<Rigidbody, Queue<ObjectState>>();
    private List<Rigidbody> trackedObjects = new List<Rigidbody>();

    private bool isRewinding = false;
    private Coroutine rewindCoroutine = null;
    private MonoBehaviour disabledComponentBuffer;
    private Rigidbody currentRewindingRb = null;

    public bool IsRewinding => isRewinding;
    
    /// <summary>
    /// 현재 리와인드 중인 Rigidbody를 반환합니다. 없으면 null입니다.
    /// </summary>
    public Rigidbody GetRewindingRigidbody() => currentRewindingRb;
    
    /// <summary>
    /// 현재 리와인드 중인 GameObject를 반환합니다. 없으면 null입니다.
    /// </summary>
    public GameObject GetRewindingGameObject() => currentRewindingRb != null ? currentRewindingRb.gameObject : null;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        FindAllTrackableObjects();
        StartCoroutine(RecordLoop());
        // 씬 로드 이벤트 구독
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 리로드 시 진행 중인 Rewind 중단 및 상태 초기화
        if (isRewinding)
        {
            if (rewindCoroutine != null)
            {
                StopCoroutine(rewindCoroutine);
                rewindCoroutine = null;
            }
            
            // Rewind 상태 초기화
            isRewinding = false;
            currentRewindingRb = null;
            disabledComponentBuffer = null;
        }
        
        // 씬 리로드 시 모든 오브젝트를 다시 찾음
        FindAllTrackableObjects();
    }

    // Time.timeScale이 0일 때 WaitForSeconds도 멈추므로 별도의 정지 체크 불필요
    IEnumerator RecordLoop()
    {
        var wait = new WaitForSeconds(recordInterval);
        
        while (true)
        {
            if (!isRewinding)
            {
                RecordAllStates();
            }
            yield return wait;
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

    void RecordAllStates()
    {
        // Time.time은 timeScale=0일 때 멈추므로 "순수 플레이 시간"으로 사용 가능
        float currentTime = Time.time;

        for (int i = trackedObjects.Count - 1; i >= 0; i--)
        {
            Rigidbody rb = trackedObjects[i];
            if (rb == null) { trackedObjects.RemoveAt(i); continue; }

            if (!objectHistories.ContainsKey(rb)) objectHistories[rb] = new Queue<ObjectState>();
            Queue<ObjectState> history = objectHistories[rb];

            history.Enqueue(new ObjectState(rb, currentTime));

            while (history.Count > 0 && (currentTime - history.Peek().timeStamp) > maxRecordTime)
            {
                history.Dequeue();
            }
        }
    }

    /// <summary>
    /// Rigidbody를 추적 목록에 추가합니다. 외부에서 호출 가능합니다.
    /// 레이어 필터링이 적용됩니다.
    /// </summary>
    public void AddTrackableObject(Rigidbody rb)
    {
        if (rb != null && !trackedObjects.Contains(rb))
        {
            // 레이어 필터링
            if (((1 << rb.gameObject.layer) & trackableLayers) != 0)
            {
                trackedObjects.Add(rb);
                objectHistories[rb] = new Queue<ObjectState>();
            }
        }
    }

    public bool RewindObject(GameObject targetObject, float rewindSeconds)
    {
        // 자식/부모 오브젝트에서 유연하게 Rigidbody 찾기
        Rigidbody rb = targetObject.GetComponent<Rigidbody>();
        if (rb == null) rb = targetObject.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = targetObject.GetComponentInChildren<Rigidbody>();

        if (rb == null || !objectHistories.ContainsKey(rb)) return false;
        
        if (isRewinding && rewindCoroutine != null) StopCoroutine(rewindCoroutine);
        
        rewindCoroutine = StartCoroutine(RewindProcess(rb, rewindSeconds));
        return true;
    }

    IEnumerator RewindProcess(Rigidbody targetRb, float rewindSeconds)
    {
        isRewinding = true;
        currentRewindingRb = targetRb;

        bool wasKinematic = targetRb.isKinematic;
        bool wasGravity = targetRb.useGravity;
        targetRb.isKinematic = true; 
        targetRb.useGravity = false;
        targetRb.linearVelocity = Vector3.zero;
        targetRb.angularVelocity = Vector3.zero;

        var gear = targetRb.GetComponent<GearRotator>();
        if (gear != null) { gear.enabled = false; disabledComponentBuffer = gear; }

        var historyList = new List<ObjectState>(objectHistories[targetRb]);
        if (historyList.Count == 0) { FinishRewind(targetRb, wasKinematic, wasGravity); yield break; }

        float startTime = historyList[historyList.Count - 1].timeStamp;
        float targetTime = Mathf.Max(startTime - rewindSeconds, historyList[0].timeStamp);
        float virtualPlayhead = startTime;

        while (virtualPlayhead > targetTime)
        {
            if (targetRb == null) break;

            virtualPlayhead -= Time.unscaledDeltaTime; 

            for (int i = historyList.Count - 1; i >= 0; i--)
            {
                if (historyList[i].timeStamp <= virtualPlayhead)
                {
                    targetRb.position = historyList[i].position;
                    targetRb.rotation = historyList[i].rotation;
                    break;
                }
            }
            yield return null;
        }

        if (targetRb != null && objectHistories.ContainsKey(targetRb))
        {
            Queue<ObjectState> q = objectHistories[targetRb];
            q.Clear();
            foreach (var state in historyList)
            {
                if (state.timeStamp <= targetTime) q.Enqueue(state);
                else break;
            }
        }

        FinishRewind(targetRb, wasKinematic, wasGravity);
    }

    void FinishRewind(Rigidbody rb, bool k, bool g)
    {
        if (rb != null)
        {
            rb.isKinematic = k;
            rb.useGravity = g;
            if(!k) rb.WakeUp(); 
            if (disabledComponentBuffer != null) { disabledComponentBuffer.enabled = true; disabledComponentBuffer = null; }
        }
        
        isRewinding = false;
        currentRewindingRb = null;
        rewindCoroutine = null;
    }

    public int GetRecordedStateCount(Rigidbody rb)
    {
        if (rb != null && objectHistories.ContainsKey(rb))
        {
            return objectHistories[rb].Count;
        }
        return 0;
    }
    
    public bool IsTracked(Rigidbody rb)
    {
        return objectHistories.ContainsKey(rb);
    }
}