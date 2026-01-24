using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class MapFlipper : MonoBehaviour
{
    [Header("References")]
    public Transform mapRoot;
    public PlayerController player;

    [Header("Settings")]
    public float duration = 1.0f;

    private List<RigidbodyData> activeRigidbodies = new List<RigidbodyData>();

    // Rigidbody의 원래 상태를 저장하기 위한 구조체
    struct RigidbodyData
    {
        public Rigidbody rb;
        public bool wasKinematic;
    }

    void Update()
    {
        if (StateManager.Instance.CurrentState() != State.MapFlip && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartCoroutine(FlipRoutine());
        }
    }

    IEnumerator FlipRoutine()
    {
        // 상태 체크
        if (!StateManager.Instance.CanUseAbility())
        {
            Debug.LogWarning("다른 능력 사용 중입니다!");
            yield break;
        }

        // [추가] 지상 체크
        if (player != null && !player.IsGrounded())
        {
            Debug.LogWarning("공중에서는 능력을 사용할 수 없습니다!");
            yield break;
        }
        
        StateManager.Instance.SetState(State.MapFlip);

        // 1. 플레이어 상태 저장 및 중력 해제
        Quaternion initialPlayerRotation = Quaternion.identity;
        if (player != null)
        {
            initialPlayerRotation = player.transform.rotation;
        }

        // 2. 모든 Rigidbody 물리 정지 (맵 내부의 물체들)
        FreezeAllPhysics();

        // 3. 맵 회전 설정
        Quaternion startRotation = mapRoot.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0, 0, 180f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // SmoothStep

            mapRoot.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            // [중요] 플레이어가 맵의 자식이라면 회전값이 변하므로 매 프레임 고정
            if (player != null)
            {
                player.transform.rotation = initialPlayerRotation;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        mapRoot.rotation = endRotation;

        // 4. 상태 복구
        UnfreezeAllPhysics();

        if (player != null)
        {
            player.transform.rotation = initialPlayerRotation;
        }

        // 상태 초기화
        StateManager.Instance.SetState(State.Normal);
    }

    private void FreezeAllPhysics()
    {
        activeRigidbodies.Clear();
        // 맵 내에 있는 모든 Rigidbody를 찾습니다. (성능을 위해 mapRoot 하위만 찾는 것이 좋습니다)
        Rigidbody[] rbs = mapRoot.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rbs)
        {
            // 현재 상태 저장
            activeRigidbodies.Add(new RigidbodyData { rb = rb, wasKinematic = rb.isKinematic });
            
            // 물리 연산 중지 (속도 초기화 및 Kinematic 설정)
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    private void UnfreezeAllPhysics()
    {
        foreach (RigidbodyData data in activeRigidbodies)
        {
            if (data.rb != null)
            {
                // 원래 Kinematic이 아니었던 물체들만 다시 물리 연산 활성화
                data.rb.isKinematic = data.wasKinematic;
            }
        }
        activeRigidbodies.Clear();
    }
}