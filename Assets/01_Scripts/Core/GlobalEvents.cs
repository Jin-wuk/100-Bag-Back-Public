using UnityEngine;
using System;
using BagBack100.Data.Core;
using BagBack100.Hub;

namespace BagBack100.Core
{
    public static class GlobalEvents
    {
        // 몬스터 사망 시: 몬스터 위치, 드랍 풀 ID
        public static Action<Vector3, int> OnMonsterDied;

        // 아이템이 맵에 버려질 때: 아이템 정보, 버려질 위치
        public static Action<RuntimeItem, Vector3> OnItemDroppedOnGround;

        // 플레이어가 발로 밟아 아이템을 획득할 때 (성공 여부 콜백)
        public static Action<RuntimeItem, Action<bool>> OnItemPickedUpByPlayer;

        // 허브 상호작용: 포탈, 대장간, 상점 등을 클릭했을 때 (HubObjectType)
        public static Action<HubObjectType> OnHubObjectClicked;

        // 플레이어가 타일을 이동하여 셀 좌표가 변경되었을 때 (Fog of War 등 갱신용)
        public static Action<Vector3Int> OnPlayerCellChanged;

        // 엔티티(몬스터/플레이어) 피격 시: 월드 위치, 데미지량, 강타(3타/차징) 여부, 플레이어 피격 여부
        public static Action<Vector3, int, bool, bool> OnEntityDamaged;

        // 카메라 흔들림 및 역경직 요청: 강도(Intensity), 지속시간(Duration), 히트스톱 시간(HitStop)
        public static Action<float, float, float> OnCombatImpact;
    }
}
