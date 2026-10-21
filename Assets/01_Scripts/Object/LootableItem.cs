using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Core;

namespace BagBack100.Map
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class LootableItem : MonoBehaviour
    {
        private RuntimeItem _item;
        private SpriteRenderer _spriteRenderer;

        public Vector3Int CellPos { get; private set; }

        public void Setup(RuntimeItem item, Vector3Int cellPos)
        {
            _item = item;
            CellPos = cellPos;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (_item != null && _item.ItemData != null)
            {
                _spriteRenderer.sprite = _item.ItemData.itemIcon;
                _spriteRenderer.sortingOrder = 10; // 타일맵 위로 올리기 위해 높은 값 할당
                
                // 콜라이더 크기를 스프라이트에 맞춤
                var collider = GetComponent<BoxCollider2D>();
                collider.size = _spriteRenderer.bounds.size;
                collider.isTrigger = true;
            }
        }

        public void Setup(RuntimeItem item)
        {
            Setup(item, Vector3Int.zero);
        }

        public void PickUpByPlayer()
        {
            if (_item == null) return;

            bool isPickedUp = false;
            GlobalEvents.OnItemPickedUpByPlayer?.Invoke(_item, (success) => isPickedUp = success);
            
            Debug.Log($"[LootableItem] 픽업 시도 - 아이템: {_item.ItemData.itemName}, 타일: {CellPos}, 인벤토리 수납 성공 여부: {isPickedUp}");

            if (isPickedUp)
            {
                TileSystem.Instance.RemoveItemTile(CellPos);
                Destroy(gameObject);
            }
        }

    }
}
