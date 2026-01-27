using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class MapFlipper : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    public CameraFollow cameraFollow;

    [Header("Settings")]
    public float duration = 1.0f;

    private bool isGravityInverted = false;
    private Vector3 defaultGravity = new Vector3(0, -9.81f, 0); // 기본값 설정

    void Start()
    {
        defaultGravity = Physics.gravity;
    }

    // 씬 리로드 시 중력 상태 초기화를 위한 공개 메서드
    public void ResetGravity()
    {
        isGravityInverted = false;
        
        // defaultGravity가 설정되지 않았으면 기본값 사용
        if (defaultGravity == Vector3.zero)
        {
            defaultGravity = new Vector3(0, -9.81f, 0);
        }
        
        Physics.gravity = defaultGravity;
        
        // 카메라 회전 초기화
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

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            AttemptFlip();
        }
    }

    private void AttemptFlip()
    {
        if (StateManager.Instance.CurrentState() != State.Normal) return;
        if (player != null && !player.IsGrounded())
        {
            Debug.LogWarning("공중에서는 능력을 사용할 수 없습니다!");
            return;
        }

        StartCoroutine(FlipRoutine());
    }

    IEnumerator FlipRoutine()
    {
        StateManager.Instance.SetState(State.MapFlip);

        // 시간 정지
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        isGravityInverted = !isGravityInverted;

        // 물리 설정
        Physics.gravity = isGravityInverted ? -defaultGravity : defaultGravity;

        // 목표값 설정
        float startCamRoll = cameraFollow.targetZRoll;
        float endCamRoll = isGravityInverted ? 180f : 0f;

        Quaternion startPlayerRot = player.transform.rotation;
        Vector3 gravityDir = Physics.gravity.normalized;
        Vector3 currentForward = player.transform.forward;
        Vector3 targetUp = -gravityDir;
        Quaternion endPlayerRot = Quaternion.LookRotation(currentForward, targetUp);

        // 애니메이션 실행
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 시간이 멈춰있으므로 unscaledDeltaTime 사용
            // 안 쓰면 elapsed가 증가하지 않아 무한 루프
            elapsed += Time.unscaledDeltaTime; 
            
            float t = elapsed / duration;
            t = Mathf.Clamp01(t); // 혹시 모를 오버슈팅 방지
            t = t * t * (3f - 2f * t);

            cameraFollow.targetZRoll = Mathf.Lerp(startCamRoll, endCamRoll, t);
            player.transform.rotation = Quaternion.Slerp(startPlayerRot, endPlayerRot, t);

            // 시간이 멈춰도 코루틴은 다음 프레임까지 대기해야 함
            yield return null; 
        }

        // 마무리
        cameraFollow.targetZRoll = endCamRoll;
        player.transform.rotation = endPlayerRot;

        // 시간 복구
        Time.timeScale = originalTimeScale;

        StateManager.Instance.SetState(State.Normal);
    }
}