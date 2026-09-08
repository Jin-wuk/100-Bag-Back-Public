using UnityEngine;
using System;
using DimensionLooter.Data.Core;

namespace DimensionLooter.Core
{
    public static class GlobalEvents
    {
        // 몬스터 사망 시: 몬스터 위치, 드랍 풀 ID
        public static Action<Vector3, int> OnMonsterDied;

        // 아이템이 맵에 버려질 때: 아이템 정보, 버려질 위치
        public static Action<RuntimeItem, Vector3> OnItemDroppedOnGround;

        // 맵의 아이템을 클릭해서 집어들 때
        public static Action<RuntimeItem> OnItemPickedUpByMouse;
    }
}
