using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class ObjectHighlighter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("카메라 참조 (비어있으면 Main Camera 자동 사용)")]
    public Camera mainCamera;
    
    [Header("Highlight Settings")]
    [Tooltip("하이라이트 색상")]
    public Color highlightColor = Color.yellow;
    
    [Tooltip("하이라이트 강도")]
    public float highlightIntensity = 2f;
    
    [Tooltip("하이라이트할 레이어")]
    public LayerMask highlightLayer = -1; // 모든 레이어
    
    private Renderer lastHighlighted;
    private Renderer rewindHighlighted; // 리와인드 중인 오브젝트의 하이라이트
    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    private Material highlightMaterial;
    private TimeController timeController;
    
    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
            
        timeController = TimeController.Instance;
        
        // 하이라이트 머티리얼 생성
        highlightMaterial = new Material(Shader.Find("Standard"));
        highlightMaterial.EnableKeyword("_EMISSION");
        highlightMaterial.SetColor("_EmissionColor", highlightColor * highlightIntensity);
        
        // 코루틴 시작 (timeScale 영향 안받음)
        StartCoroutine(CheckHover());
    }
    
    IEnumerator CheckHover()
    {
        while (true)
        {
            // 참조가 없으면 다시 찾기
            if (timeController == null)
                timeController = TimeController.Instance;
            if (mainCamera == null)
                mainCamera = Camera.main;
            
            State currentState = StateManager.Instance != null ? StateManager.Instance.CurrentState() : State.Normal;
            
            // 시간 정지 상태에서만 작동 (TimeFreeze)
            if (currentState == State.TimeFreeze)
            {
                // 마우스 위치에서 레이캐스트
                if (Mouse.current != null && mainCamera != null)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    Ray ray = mainCamera.ScreenPointToRay(mousePos);
                    
                    // RaycastAll을 사용하여 non-convex collider도 감지
                    RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, highlightLayer);
                    
                    if (hits.Length > 0)
                    {
                        // 가장 가까운 hit 찾기 (추적 가능한 Rigidbody 있는 것만)
                        RaycastHit? validHit = null;
                        float closestDistance = float.MaxValue;
                        
                        for (int i = 0; i < hits.Length; i++)
                        {
                            if (hits[i].distance >= closestDistance) continue;
                            if (!HasTrackedRigidbody(hits[i].collider.gameObject)) continue;
                            
                            closestDistance = hits[i].distance;
                            validHit = hits[i];
                        }
                        
                        if (validHit.HasValue)
                        {
                            RaycastHit closestHit = validHit.Value;
                            Renderer renderer = closestHit.collider.GetComponent<Renderer>();
                            if (renderer == null)
                                renderer = closestHit.collider.GetComponentInParent<Renderer>();
                            if (renderer == null)
                                renderer = closestHit.collider.GetComponentInChildren<Renderer>();
                            
                            if (renderer != null && renderer != lastHighlighted)
                            {
                                if (lastHighlighted != null)
                                    RemoveHighlight(lastHighlighted);
                                ApplyHighlight(renderer);
                                lastHighlighted = renderer;
                            }
                            else if (renderer == null && lastHighlighted != null)
                            {
                                RemoveHighlight(lastHighlighted);
                                lastHighlighted = null;
                            }
                            
                            if (Mouse.current.leftButton.wasPressedThisFrame && timeController != null)
                            {
                                timeController.ResumeTime(closestHit.collider.gameObject);
                                rewindHighlighted = lastHighlighted;
                                lastHighlighted = null;
                            }
                        }
                        else
                        {
                            if (lastHighlighted != null)
                            {
                                RemoveHighlight(lastHighlighted);
                                lastHighlighted = null;
                            }
                        }
                    }
                    else
                    {
                        // 아무것도 안 맞으면 하이라이트 제거
                        if (lastHighlighted != null)
                        {
                            RemoveHighlight(lastHighlighted);
                            lastHighlighted = null;
                        }
                    }
                }
            }
            // 리와인드 상태일 때 리와인드 중인 오브젝트에 하이라이트 유지
            else if (currentState == State.TimeRewind)
            {
                // 리와인드 중인 오브젝트 확인
                GameObject rewindObj = TimeRewindManager.Instance != null ? TimeRewindManager.Instance.GetRewindingGameObject() : null;
                
                if (rewindObj != null)
                {
                    Renderer rewindRenderer = rewindObj.GetComponent<Renderer>();
                    if (rewindRenderer == null)
                        rewindRenderer = rewindObj.GetComponentInChildren<Renderer>();
                    if (rewindRenderer == null)
                        rewindRenderer = rewindObj.GetComponentInParent<Renderer>();
                    
                    // 리와인드 중인 오브젝트에 하이라이트 적용
                    if (rewindRenderer != null && rewindRenderer != rewindHighlighted)
                    {
                        // 이전 리와인드 하이라이트 제거
                        if (rewindHighlighted != null)
                            RemoveHighlight(rewindHighlighted);
                        
                        // 새 리와인드 하이라이트 적용
                        ApplyHighlight(rewindRenderer);
                        rewindHighlighted = rewindRenderer;
                    }
                }
                
                // 마우스 하이라이트는 제거
                if (lastHighlighted != null)
                {
                    RemoveHighlight(lastHighlighted);
                    lastHighlighted = null;
                }
            }
            else
            {
                // Normal 상태: 모든 하이라이트 제거
                if (lastHighlighted != null)
                {
                    RemoveHighlight(lastHighlighted);
                    lastHighlighted = null;
                }
                
                // 리와인드가 끝났으면 리와인드 하이라이트도 제거
                if (rewindHighlighted != null)
                {
                    RemoveHighlight(rewindHighlighted);
                    rewindHighlighted = null;
                }
            }
            
            yield return null; // 매 프레임 체크
        }
    }
    
    /// <summary>
    /// 리와인드 가능 여부와 동일: Rigidbody(본인/부모/자식)가 있고 TimeRewindManager에 추적 중이면 true.
    /// </summary>
    bool HasTrackedRigidbody(GameObject go)
    {
        if (go == null || TimeRewindManager.Instance == null) return false;
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = go.GetComponentInChildren<Rigidbody>();
        return rb != null && TimeRewindManager.Instance.IsTracked(rb);
    }
    
    void ApplyHighlight(Renderer renderer)
    {
        if (renderer == null) return;
        
        // 이미 하이라이트된 경우 스킵
        if (originalMaterials.ContainsKey(renderer)) return;
        
        // 원본 머티리얼 저장
        Material originalMaterial = renderer.sharedMaterial;
        
        if (originalMaterial == null)
        {
            Debug.LogWarning($"ObjectHighlighter: {renderer.gameObject.name}에 머티리얼이 없습니다!");
            return;
        }
        
        // 원본 머티리얼을 딕셔너리에 저장
        originalMaterials[renderer] = originalMaterial;
        
        // 새 머티리얼 인스턴스 생성
        Material newMaterial = new Material(renderer.sharedMaterial);
        
        // 방법 1: 색상 직접 변경 (가장 확실한 방법)
        // 여러 색상 프로퍼티를 순서대로 체크 (URP shader 우선)
        bool colorChanged = false;
        string[] colorProperties = { "_BaseColor", "_Color", "_MainColor", "_MainTint", "_TintColor" };
        
        foreach (string propName in colorProperties)
        {
            if (newMaterial.HasProperty(propName))
            {
                Color originalColor = newMaterial.GetColor(propName);
                // 원본 색상과 하이라이트 색상을 블렌드
                newMaterial.SetColor(propName, Color.Lerp(originalColor, highlightColor, 0.9f));
                colorChanged = true;
                break; // 첫 번째로 찾은 프로퍼티만 사용
            }
        }
        
        // 방법 2: Emission도 시도 (Built-in Standard)
        if (newMaterial.HasProperty("_EmissionColor"))
        {
            newMaterial.EnableKeyword("_EMISSION");
            newMaterial.SetColor("_EmissionColor", highlightColor * highlightIntensity);
            newMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else if (newMaterial.HasProperty("_Emission"))
        {
            newMaterial.EnableKeyword("_EMISSION");
            newMaterial.SetColor("_Emission", highlightColor * highlightIntensity);
        }
        
        // 머티리얼 적용
        renderer.material = newMaterial;
    }
    
    void RemoveHighlight(Renderer renderer)
    {
        if (renderer == null) return;
        
        // 딕셔너리에서 원본 머티리얼 찾기
        if (originalMaterials.ContainsKey(renderer))
        {
            // 원본 머티리얼 복원
            renderer.material = originalMaterials[renderer];
            
            // 딕셔너리에서 제거
            originalMaterials.Remove(renderer);
        }
    }
    
    void OnDestroy()
    {
        // 모든 하이라이트 제거
        if (lastHighlighted != null)
            RemoveHighlight(lastHighlighted);
        if (rewindHighlighted != null)
            RemoveHighlight(rewindHighlighted);
        
        // 딕셔너리에 남아있는 모든 하이라이트 제거
        foreach (var kvp in originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.material = kvp.Value;
            }
        }
        originalMaterials.Clear();
            
        // 머티리얼 정리
        if (highlightMaterial != null)
            Destroy(highlightMaterial);
    }
}
