using UnityEngine;
using System;
using BagBack100.Data.Core;
using BagBack100.Hub;

namespace BagBack100.Core
{
    /// <summary>
    /// Role: 도메인 간 결합을 끊기 위한 전역 이벤트 모음 (몬스터 사망, 아이템 드랍·버리기·획득, 피격, 타격감 등).
    /// Dependencies: RuntimeItem, HubObjectType
    /// Events: 이 클래스 자체가 이벤트 정의
    /// </summary>
    public static class GlobalEvents
    {
        // 몬스터 사망 시: 몬스터 위치, 드랍 풀 ID
        public static Action<Vector3, int> OnMonsterDied;

        // 아이템이 맵에 버려질 때: 아이템 정보, 버려질 위치
        public static Action<RuntimeItem, Vector3> OnItemDroppedOnGround;

        // 가방에서 아이템을 버리기 요청: 아이템, 플레이어 위치, 반경(칸), 성공 여부 콜백 (반경 안에 빈 칸이 없으면 false)
        public static Action<RuntimeItem, Vector3, int, Action<bool>> OnItemDiscardRequested;

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

        // 층계 던전: 다음 층으로 전환을 시작할 때: 페이드 시간(초, 실시간). 같은 시간 뒤 새 층이 만들어진다
        public static Action<float> OnFloorTransitionStarted;

        // 층계 던전: 층 생성이 끝나 플레이어가 들어섰을 때: 지역 번호, 지역 이름, 층 번호(1~10)
        public static Action<int, string, int> OnFloorEntered;
    }
}
