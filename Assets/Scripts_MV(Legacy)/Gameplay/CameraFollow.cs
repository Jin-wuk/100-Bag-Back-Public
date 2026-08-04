// File: Assets/Scripts/Gameplay/CameraFollow.cs
using UnityEngine;

namespace DimensionLooter.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private Vector3 _offset = new Vector3(0, 0, -10);

        private void Start()
        {
            if (_target == null)
            {
                var player = FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    _target = player.transform;
                }
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 desiredPosition = _target.position + _offset;
            // 부드러운 이동 (Lerp)
            transform.position = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
        }
    }
}
