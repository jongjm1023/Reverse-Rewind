using UnityEngine;

public class Laser : MonoBehaviour
{
    public Transform targetPoint; // 반대편 Receiver
    public string targetTag = "Player"; // 감지할 대상의 태그

    private float laserRadius = 0.4f; // 레이저 감지 두께

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

        // 0. 시작 지점에 무언가 겹쳐있는지 확인 (OverlapSphere)
        Collider[] overlaps = Physics.OverlapSphere(transform.position, 0.2f);
        foreach (var col in overlaps)
        {
            if (IsValidTarget(col))
            {
                // 시작하자마자 막힘
                lineRenderer.SetPosition(1, transform.position);
                HandleHit(col);
                return;
            }
        }

        // 2. 방향과 거리 계산
        Vector3 direction = targetPoint.position - transform.position;
        float maxDistance = direction.magnitude; // Receiver까지의 거리

        // 3. 스피어캐스트 발사 (SphereCastAll로 모두 가져온 뒤 필터링)
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, laserRadius, direction, maxDistance);

        // 거리순으로 정렬 (가까운 순서대로 확인해야 함)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? validHit = null;

        foreach (var hit in hits)
        {
            if (!IsValidTarget(hit.collider))
            {
                continue;
            }

            // 첫 번째 유효한 충돌체 발견
            validHit = hit;
            break;
        }

        if (validHit.HasValue)
        {
            RaycastHit hit = validHit.Value;

            // A. 시각 효과: 레이저가 물체 표면에서 끊기게 함
            lineRenderer.SetPosition(1, hit.point);

            // B. 감지 로직
            HandleHit(hit.collider);
        }
        else
        {
            // 아무것도 안 닿았으면 Receiver까지 쭉 그림
            lineRenderer.SetPosition(1, targetPoint.position);
        }
    }

    // 유효한 타겟인지 확인하는 로직 분리
    bool IsValidTarget(Collider col)
    {
        // 자신(Laser 본체)이나 Wind 태그는 무시
        if (col.gameObject == gameObject || col.CompareTag("Wind") || col.CompareTag("Floor") || col.CompareTag("Text"))
        {
            return false;
        }
        return true;
    }

    // 충돌 처리 로직 분리
    void HandleHit(Collider col)
    {
        PlayerController player = col.GetComponentInParent<PlayerController>();

        if (player != null)
        {
            if (player.CompareTag(targetTag))
            {
                Debug.Log("<color=red>침입자 감지! 경보 울림!</color>");
                player.Respawn();
            }
        }
    }
}