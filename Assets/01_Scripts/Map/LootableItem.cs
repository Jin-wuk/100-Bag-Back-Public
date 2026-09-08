using UnityEngine;
using DimensionLooter.Data.Core;
using DimensionLooter.Core;

namespace DimensionLooter.Map
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class LootableItem : MonoBehaviour
    {
        private RuntimeItem _item;
        private SpriteRenderer _spriteRenderer;

        public void Setup(RuntimeItem item)
        {
            _item = item;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            
            if (_item != null && _item.ItemData != null)
            {
                _spriteRenderer.sprite = _item.ItemData.itemIcon;
                
                // 콜라이더 크기를 스프라이트에 맞춤
                var collider = GetComponent<BoxCollider2D>();
                collider.size = _spriteRenderer.bounds.size;
                collider.isTrigger = true;
            }
        }

        private void OnMouseDown()
        {
            if (_item == null) return;

            // 아이템을 마우스로 집어들었다는 전역 이벤트 발생
            GlobalEvents.OnItemPickedUpByMouse?.Invoke(_item);
            
            // 맵에서는 사라짐 (풀링 사용 시 Destroy 대신 ReturnToPool 호출)
            Destroy(gameObject);
        }
    }
}
