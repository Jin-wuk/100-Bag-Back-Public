// File: Assets/Scripts/Gameplay/PlayerController.cs
using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Core;

namespace DimensionLooter.Gameplay
{
    /// <summary>
    /// 2D 마우스 클릭으로 이동하며, 몬스터(Target) 클릭 시 사거리 내로 다가가는 역할을 수행합니다.
    /// [Why] 완벽한 2D 탑다운 게임 환경에 맞춰 3D NavMesh를 제거하고 2D Raycast와 선형 이동 로직으로 전면 교체했습니다.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Tooltip("플레이어 이동 속도")]
        [SerializeField] private float _moveSpeed = 5f;
        
        [Tooltip("상호작용 사거리 (MVP 하드코딩)")]
        [SerializeField] private float _stoppingDistance = 1.5f;

        private Vector2 _targetPosition;
        private IInteractable _targetInteractable;
        private bool _isMoving = false;

        private void Awake()
        {
            _targetPosition = transform.position;
        }

        private void Update()
        {
            // 1. UI 위를 클릭한 경우 이동 무시 (가방 켜졌을 때 이동 방지)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // 2. 2D 마우스 클릭 감지
            if (Input.GetMouseButtonDown(0))
            {
                // 화면 좌표를 2D 월드 좌표로 변환
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                
                // 클릭한 위치에 2D 콜라이더가 있는지 감지
                RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

                if (hit.collider != null && hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    // 몬스터(상호작용 객체) 클릭
                    _targetInteractable = interactable;
                    _targetPosition = hit.collider.transform.position;
                    _isMoving = true;
                }
                else
                {
                    // 허공(땅) 클릭
                    _targetInteractable = null;
                    _targetPosition = mousePos;
                    _isMoving = true;
                }
            }

            // 3. 2D 이동 및 사거리 체크
            if (_isMoving)
            {
                // 목표를 향해 등속 이동 (추후 장애물 회피가 필요해지면 A* 등 2D 패스파인딩 도입 필요)
                transform.position = Vector2.MoveTowards(transform.position, _targetPosition, _moveSpeed * Time.deltaTime);
                
                float distance = Vector2.Distance(transform.position, _targetPosition);

                if (_targetInteractable == null)
                {
                    // 단순 땅 이동 완료
                    if (distance <= 0.05f) _isMoving = false;
                }
                else
                {
                    // 몬스터와의 거리가 사거리 이내로 들어옴
                    if (distance <= _stoppingDistance)
                    {
                        _isMoving = false;
                        _targetInteractable.Interact(gameObject); // 전투 팝업 발생!
                        _targetInteractable = null;
                    }
                }
            }
        }
    }
}
