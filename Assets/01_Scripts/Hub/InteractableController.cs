using UnityEngine;
using UnityEngine.EventSystems;
using BagBack100.Core;

namespace BagBack100.Hub
{
    /// <summary>
    /// Role: 허브 클릭 오브젝트(포탈·창고·대장간·잡화상) 하나. 마우스를 올리면 외곽선을 켜고, 클릭하면 오브젝트 종류를 전역 이벤트로 알린다.
    /// Dependencies: OutlineEffect
    /// Events: GlobalEvents.OnHubObjectClicked
    /// </summary>
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

            // 클릭으로 전체 화면 UI(월드맵 등)가 열리면 OnMouseExit가 오지 않아 외곽선이 남으므로 먼저 끈다
            _outlineEffect?.SetOutline(false);

            // 클릭 이벤트를 전역으로 방송 (HubSystem이 수신)
            GlobalEvents.OnHubObjectClicked?.Invoke(_objectType);
        }
    }
}
