using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

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
    private Material originalMaterial;
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
            // 시간 정지 상태에서만 작동 (TimeFreeze)
            if (StateManager.Instance.CurrentState() == State.TimeFreeze)
            {
                // 마우스 위치에서 레이캐스트
                Vector2 mousePos = Mouse.current.position.ReadValue();
                Ray ray = mainCamera.ScreenPointToRay(mousePos);
                
                // RaycastAll을 사용하여 non-convex collider도 감지
                RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, highlightLayer);
                
                if (hits.Length > 0)
                {
                    // 가장 가까운 hit 찾기
                    RaycastHit closestHit = hits[0];
                    float closestDistance = hits[0].distance;
                    
                    for (int i = 1; i < hits.Length; i++)
                    {
                        if (hits[i].distance < closestDistance)
                        {
                            closestDistance = hits[i].distance;
                            closestHit = hits[i];
                        }
                    }
                    
                    Renderer renderer = closestHit.collider.GetComponent<Renderer>();
                    
                    if (renderer != null && renderer != lastHighlighted)
                    {
                        // 이전 하이라이트 제거
                        if (lastHighlighted != null)
                            RemoveHighlight(lastHighlighted);
                        
                        // 새 하이라이트 적용
                        ApplyHighlight(renderer);
                        lastHighlighted = renderer;
                    }
                    
                    // 클릭 감지 (Input System은 timeScale 영향 안받음)
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        // 시간 재개 및 역행 시작
                        timeController.ResumeTime(closestHit.collider.gameObject);
                        
                        // 하이라이트 제거
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
            else
            {
                // 시간이 흐르는 중에는 하이라이트 제거
                if (lastHighlighted != null)
                {
                    RemoveHighlight(lastHighlighted);
                    lastHighlighted = null;
                }
            }
            
            yield return null; // 매 프레임 체크
        }
    }
    
    void ApplyHighlight(Renderer renderer)
    {
        if (renderer == null) return;
        
        // 원본 머티리얼 저장
        originalMaterial = renderer.sharedMaterial;
        
        // 새 머티리얼 인스턴스 생성
        Material newMaterial = new Material(renderer.sharedMaterial);
        
        // Emission 활성화
        newMaterial.EnableKeyword("_EMISSION");
        
        // Emission 색상 설정 (여러 방법 시도)
        newMaterial.SetColor("_EmissionColor", highlightColor * highlightIntensity);
        newMaterial.SetFloat("_EmissionScaleUI", highlightIntensity);
        
        // Global Illumination 설정 (중요!)
        newMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        
        // 머티리얼 적용
        renderer.material = newMaterial;
    }
    
    void RemoveHighlight(Renderer renderer)
    {
        if (renderer == null || originalMaterial == null) return;
        
        // 원본 머티리얼 복원
        renderer.material = originalMaterial;
    }
    
    void OnDestroy()
    {
        // 하이라이트 제거
        if (lastHighlighted != null)
            RemoveHighlight(lastHighlighted);
            
        // 머티리얼 정리
        if (highlightMaterial != null)
            Destroy(highlightMaterial);
    }
}
