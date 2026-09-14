using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Core;

namespace DimensionLooter.Hub
{
    [RequireComponent(typeof(Collider2D))]
    public class InteractableController : MonoBehaviour
    {
        [SerializeField] private HubObjectType _objectType;
        [SerializeField] private OutlineEffect _outlineEffect;

        private void Awake()
        {
            if (_outlineEffect == null)
            {
                _outlineEffect = GetComponent<OutlineEffect>();
            }
        }

        private void OnMouseEnter()
        {
            // UI 위에 마우스가 있을 때는 무시
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            _outlineEffect?.SetOutline(true);
        }

        private void OnMouseExit()
        {
            _outlineEffect?.SetOutline(false);
        }

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // TODO: 플레이어 이동(안건 2) 도입 시 여기에 거리 체크 로직 추가
            // if (Vector2.Distance(player.position, transform.position) > interactRange) return;

            // 클릭 이벤트를 전역으로 방송 (HubSystem이 수신)
            GlobalEvents.OnHubObjectClicked?.Invoke(_objectType);
        }
    }
}
