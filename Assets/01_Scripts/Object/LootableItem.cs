using UnityEngine;
using BagBack100.Data.Core;
using BagBack100.Core;

namespace BagBack100.Map
{
    /// <summary>
    /// Role: 바닥에 떨어진 전리품. 밟으면 획득 이벤트를 던진다. 플레이어·몬스터와 같은 정렬 순서(1)에서 Y 정렬(기준점 = 칸 중심)로 벽 뒤에 가려진다.
    /// Dependencies: TileSystem
    /// Events: GlobalEvents.OnItemPickedUpByPlayer
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class LootableItem : MonoBehaviour
    {
        [Tooltip("플레이어·몬스터·내부 장애물과 같은 Y 정렬 순서")]
        [SerializeField] private int _sortingOrder = 1;

        private RuntimeItem _item;
        private SpriteRenderer _spriteRenderer;
        private BoxCollider2D _collider;

        public Vector3Int CellPos { get; private set; }

        private void Awake()
        {
            CacheComponents();
        }

        // 비활성 상태로 생성되어 Awake 전에 Setup이 불려도 동작하도록 따로 둔다
        private void CacheComponents()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
        }

        public void Setup(RuntimeItem item, Vector3Int cellPos)
        {
            _item = item;
            CellPos = cellPos;
            CacheComponents();

            if (_item != null && _item.ItemData != null)
            {
                _spriteRenderer.sprite = _item.ItemData.itemIcon;
                _spriteRenderer.sortingOrder = _sortingOrder;
                _spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

                // 콜라이더 크기를 스프라이트에 맞춤
                _collider.size = _spriteRenderer.bounds.size;
                _collider.isTrigger = true;
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
