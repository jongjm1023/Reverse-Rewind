using UnityEngine;

public class Laser : MonoBehaviour
{
    public Transform targetPoint; // 반대편 Receiver
    public string targetTag = "Player"; // 감지할 대상의 태그

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2; // 시작점, 끝점
    }

    void Update()
    {
        if (targetPoint == null) return;

        ShootLaser();
    }

    void ShootLaser()
    {
        // 1. 레이저의 시작점은 내 위치
        lineRenderer.SetPosition(0, transform.position);

        // 2. 방향과 거리 계산
        Vector3 direction = targetPoint.position - transform.position;
        float maxDistance = direction.magnitude; // Receiver까지의 거리

        // 3. 레이캐스트 발사 (보이지 않는 광선 쏘기)
        RaycastHit hit;
        // 내 위치에서, 타겟 방향으로, 타겟까지의 거리만큼 쏨
        if (Physics.Raycast(transform.position, direction, out hit, maxDistance))
        {
            // 무언가에 닿았다면?
            
            // A. 시각 효과: 레이저가 물체 표면에서 끊기게 함 (리얼함 UP)
            lineRenderer.SetPosition(1, hit.point);

            // B. 감지 로직: 닿은 게 플레이어인가?
            // 자식 Collider에 닿았을 경우를 대비해 부모에서 컴포넌트를 찾습니다.
            PlayerController player = hit.collider.GetComponentInParent<PlayerController>();

            if (player != null)
            {
                // 플레이어 태그 확인 (옵션: PlayerController가 있다면 플레이어로 간주해도 됨)
                if (player.CompareTag(targetTag))
                {
                    Debug.Log("<color=red>침입자 감지! 경보 울림!</color>");
                    player.Respawn();
                }
            }
        }
        else
        {
            // 아무것도 안 닿았으면 Receiver까지 쭉 그림
            lineRenderer.SetPosition(1, targetPoint.position);
        }
    }
}