// EnsureSingleEventSystem.cs
// 목적: 실행 중 EventSystem이 2개 이상이면 App 씬의 것만 남기고 제거
using UnityEngine;
using UnityEngine.EventSystems;

public class EnsureSingleEventSystem : MonoBehaviour
{
    void Awake()
    {
        // 현재 씬에 속한 EventSystem 우선 보존
        var keep = GetComponentInChildren<EventSystem>(true);

        // 프로젝트 전역에서 활성/비활성 모두 조회(유니티6 API)
        var all = Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var es in all)
        {
            // keep가 없으면 첫 번째를 보존
            if (keep == null) { keep = es; continue; }

            // 보존 대상이 아니면 제거
            if (es != keep) Destroy(es.gameObject);
        }
    }
}
