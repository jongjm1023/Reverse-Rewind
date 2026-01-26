using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0, 5, -10);
    public float sensitivity = 0.5f;
    private Vector2 pitchLimits = new Vector2(-70, 60);

    // [추가] 외부에서 제어할 Z축 회전값 (0 = 정상, 180 = 뒤집힘)
    [HideInInspector] 
    public float targetZRoll = 0f; 

    private float currentYaw = 0f;
    private float currentPitch = 0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        currentYaw = angles.y;
        currentPitch = angles.x;
        // Cursor settings...
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (Time.timeScale != 0f)
        {
            HandleRotation();
        }

        // [핵심 변경] Z축 회전(targetZRoll)을 포함하여 회전 계산
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, targetZRoll);

        // 회전된 오프셋을 적용하여 위치 결정
        // 180도 뒤집히면 offset도 같이 뒤집혀서 플레이어 머리 위(화면상)를 유지하게 됨
        Vector3 desiredPosition = target.position + rotation * offset;

        transform.position = desiredPosition;

        // [핵심 변경] LookAt을 할 때 카메라의 '위쪽' 방향을 회전된 상태에 맞춤
        // 이걸 안 하면 LookAt이 강제로 화면을 똑바로 세워버림
        Vector3 worldUp = rotation * Vector3.up; 
        transform.LookAt(target.position, worldUp);
    }

    void HandleRotation()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        // 중력이 뒤집혔을 때(targetZRoll이 180도) 카메라 조작을 반대로 처리
        // 카메라가 180도 회전되어 있으므로 마우스 입력도 반대로 적용해야 함
        bool isFlipped = targetZRoll > 90f; // 180도인지 확인 (0도가 아니면 뒤집힌 상태)
        
        if (isFlipped)
        {
            // 뒤집힌 상태: 마우스 입력 반대로
            currentYaw -= mouseDelta.x * sensitivity;
            currentPitch += mouseDelta.y * sensitivity;
        }
        else
        {
            // 정상 상태: 기본 동작
            currentYaw += mouseDelta.x * sensitivity;
            currentPitch -= mouseDelta.y * sensitivity;
        }
        
        currentPitch = Mathf.Clamp(currentPitch, pitchLimits.x, pitchLimits.y);
    }
}