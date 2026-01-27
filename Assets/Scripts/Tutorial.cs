using UnityEngine;
using TMPro; // TextMeshPro를 쓰기 위해 필수!

public class Tutorial : MonoBehaviour
{
    [Header("자막 설정")]
    public GameObject subtitlePanel;  // 자막 배경(또는 텍스트 자체)
    public TextMeshProUGUI subtitleText; // 텍스트 컴포넌트
    [TextArea]
    public string message; // 이 구역에서 띄울 메시지 내용

    // 무언가 트리거 안에 들어왔을 때
    private void OnTriggerEnter(Collider other)
    {
        // 들어온 녀석이 "Player" 태그를 달고 있다면
        if (other.CompareTag("Player"))
        {
            subtitleText.text = message; // 메시지 교체
            subtitlePanel.SetActive(true); // 자막 켜기
        }
    }

    // 트리거 밖으로 나갔을 때
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 현재 표시된 메시지가 이 구역의 메시지와 같을 때만 끈다.
            // (다른 구역에 이미 진입해서 메시지가 바뀌었다면 끄지 않음)
            if (subtitleText.text == message)
            {
                subtitlePanel.SetActive(false); // 자막 끄기
            }
        }
    }
}