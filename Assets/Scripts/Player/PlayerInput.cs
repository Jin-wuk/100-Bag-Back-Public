using System;
using UnityEngine;
using UnityEngine.EventSystems;
using DimensionLooter.Core;
using DimensionLooter.UI;

namespace DimensionLooter.Player
{
    public class PlayerInput : MonoBehaviour
    {
        public event Action<Vector3> OnTileClicked;
        
        [Header("Settings")]
        [Tooltip("경로 탐색 시 장애물로 인식할 레이어")]
        [SerializeField] private LayerMask _obstacleLayerMask;

        private Camera _mainCamera;
        private MovementPreview _movementPreview;

        private void Start()
        {
            _mainCamera = Camera.main;
            // 런타임에 프리뷰 UI를 캐싱 (매니저나 DI가 없으므로 임시로 Find 사용, Update 내 호출 아님)
            _movementPreview = FindObjectOfType<MovementPreview>();
        }

        private void Update()
        {
            // UI 위를 클릭하거나 호버링 중일 때는 무시
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (_movementPreview != null)
                    _movementPreview.Hide();
                return;
            }

            Vector3 mouseWorldPos = GetMouseWorldPosition();
            Vector2Int tilePos = new Vector2Int(Mathf.RoundToInt(mouseWorldPos.x), Mathf.RoundToInt(mouseWorldPos.y));

            // 프리뷰 업데이트
            UpdatePreview(tilePos);

            // 마우스 좌클릭 시 이동 명령
            if (Input.GetMouseButtonDown(0))
            {
                bool isObstacle = Pathfinder.IsObstacle(tilePos, _obstacleLayerMask);
                if (!isObstacle)
                {
                    Vector3 targetPos = new Vector3(tilePos.x, tilePos.y, 0f);
                    OnTileClicked?.Invoke(targetPos);
                }
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            if (_mainCamera == null) return Vector3.zero;
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = -_mainCamera.transform.position.z;
            return _mainCamera.ScreenToWorldPoint(mouseScreenPos);
        }

        private void UpdatePreview(Vector2Int tilePos)
        {
            if (_movementPreview == null) return;

            bool isObstacle = Pathfinder.IsObstacle(tilePos, _obstacleLayerMask);
            _movementPreview.ShowPreview(new Vector3(tilePos.x, tilePos.y, 0f), !isObstacle);
        }
    }
}
