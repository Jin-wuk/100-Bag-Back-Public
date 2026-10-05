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
    }
}
