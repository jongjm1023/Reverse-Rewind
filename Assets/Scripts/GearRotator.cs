using UnityEngine;

public class GearRotator : MonoBehaviour
{
    public float speed = 50f;
    public Vector3 rotationAxis = new Vector3(1, 0, 0);
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update가 아니라 'FixedUpdate'를 써야 물리 충돌이 정확해집니다.
    void FixedUpdate()
    {
        // 현재 각도에서 조금 더 회전한 각도를 계산
        Quaternion deltaRotation = Quaternion.Euler(rotationAxis * speed * Time.fixedDeltaTime);
        
        // Rigidbody를 통해 "물리적으로" 회전시킴 (플레이어를 밀어낼 수 있음)
        rb.MoveRotation(rb.rotation * deltaRotation);
    }
}
