using UnityEngine;

public class Belt : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 5.0f;
    
    [Header("Tag Settings")]
    public string playerTag = "Player";

    [Header("Visuals")]
    public Renderer beltRenderer;
    public string textureName = "_BaseMap";
    public float visualSpeedMultiplier = 0.03f;

    private void Update()
    {
        // 1. 텍스처 스크롤 (비주얼)
        if (beltRenderer != null)
        {
            float offset = Time.time * speed * visualSpeedMultiplier;
            float currentOffsetY = beltRenderer.material.GetTextureOffset(textureName).y;
            beltRenderer.material.SetTextureOffset(textureName, new Vector2(offset, currentOffsetY));
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Rigidbody rb = collision.collider.attachedRigidbody;

        // 물리 오브젝트만 처리
        if (rb != null && !rb.isKinematic)
        {
            // 2. 방향 판별 (위/아래 뫼비우스 로직)
            Vector3 contactNormal = collision.GetContact(0).normal;
            float directionDot = Vector3.Dot(transform.up, contactNormal);
            
            Vector3 moveDirection = Vector3.zero;

            if (directionDot > 0.5f) moveDirection = transform.right;        // 윗면
            else if (directionDot < -0.5f) moveDirection = -transform.right; // 아랫면
            else return; // 옆면 무시

            // 3. [핵심] 대상에 따른 이동 방식 분기 처리
            
            // A. 플레이어인 경우 (또는 스스로 움직이는 몬스터 등)
            if (collision.gameObject.CompareTag(playerTag))
            {
                // "위치 더하기" 방식 사용 -> 플레이어의 달리기 속도와 벨트 속도가 합산됨 (무빙워크 효과)
                // 마찰력 문제 해결을 위해 frictionCombine = Minimum 설정된 Physic Material 사용 권장
                Vector3 beltDelta = moveDirection * speed * Time.fixedDeltaTime;
                rb.MovePosition(rb.position + beltDelta);
            }
            // B. 보드, 상자 등 일반 물체인 경우
            else
            {
                // "속도 강제" 방식 사용 -> 실제 물리 속도를 가지게 됨
                // 보드가 '진짜 속도'를 가지므로, 그 위에 탄 플레이어에게 마찰력이 정상 작동함!
                
                Vector3 targetVelocity = moveDirection * speed;
                Vector3 currentVelocity = rb.linearVelocity; // Unity 6 (구버전은 .velocity)

                // Y축(중력)은 건드리지 않고, 수평 속도만 벨트 속도로 고정
                rb.linearVelocity = new Vector3(targetVelocity.x, currentVelocity.y, targetVelocity.z);
            }
        }
    }
}