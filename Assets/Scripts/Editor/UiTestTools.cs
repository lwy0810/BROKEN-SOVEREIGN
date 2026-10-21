using BrokenSovereign.Enemy;
using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// UI 확인용 메뉴. 이랑진군 보스가 들어오기 전 보스 체력바·격파 연출을 일반 적으로 확인합니다.
    /// Play 중에 쓰면 씬에 저장되지 않습니다.
    /// </summary>
    public static class UiTestTools
    {
        [MenuItem("BrokenSovereign/UI/선택한 적을 보스로 표시 (테스트)")]
        private static void MarkSelectedAsBoss()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null || selected.GetComponent<BrokenSovereign.Combat.Health>() == null)
            {
                Debug.LogWarning("[UiTestTools] Health가 있는 적을 선택하세요.");
                return;
            }

            BossStatus boss = selected.GetComponent<BossStatus>();

            if (boss == null)
            {
                boss = Undo.AddComponent<BossStatus>(selected);
            }

            // 보호막·결정 표시도 함께 확인
            boss.SetShield(200f, 400f);
            boss.SetCrystals(2, 8f, 1);
            Debug.Log($"[UiTestTools] {selected.name}에 BossStatus 추가 (보호막 200/400, 결정 2개)");
        }

        [MenuItem("BrokenSovereign/UI/선택한 적을 보스로 표시 (테스트)", true)]
        private static bool MarkSelectedAsBossValidate()
        {
            return Selection.activeGameObject != null;
        }
    }
}
