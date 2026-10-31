using System;
using System.Collections.Generic;
using UnityEngine;

namespace BagBack100.Data.Models
{
    [Serializable]
    public struct ShopEntry
    {
        public ItemDataSO item;
        [Min(0)] public int price;
    }

    /// <summary>
    /// Role: 상점 판매 목록과 구매 가격 (잡화상: 포션·기본 장비 등). 판매 가격(매입가)은 아이템의 baseValue를 쓴다.
    /// Dependencies: ItemDataSO
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewShopCatalog", menuName = "BagBack100/Data/ShopCatalog")]
    public class ShopCatalogSO : ScriptableObject
    {
        public string shopName = "General Store";
        public List<ShopEntry> entries = new List<ShopEntry>();
    }
}
