using UnityEngine;
using UnityEngine.UI;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 컨테이너 그리드의 칸 한 개 배경. 잠금 여부(현재 칸 수를 넘는 칸)에 따라 배경 스프라이트를 바꾼다.
    ///       잠긴 칸은 ItemContainerUIBase가 슬롯 목록에서 빼므로 드롭·E키 이동 대상이 되지 않는다.
    /// Dependencies: 없음
    /// Events: 없음
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class GridCellUI : MonoBehaviour
    {
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _lockedSprite;

        private Image _background;

        public bool IsLocked { get; private set; }

        private void Awake()
        {
            _background = GetComponent<Image>();
        }

        public void SetLocked(bool locked)
        {
            IsLocked = locked;
            if (_background == null) _background = GetComponent<Image>();
            Sprite sprite = locked ? _lockedSprite : _normalSprite;
            if (sprite != null) _background.sprite = sprite;
            // 잠금 스프라이트는 가운데 자물쇠가 있어 Sliced로 늘리면 아이콘이 늘어난다
            _background.type = locked ? Image.Type.Simple : Image.Type.Sliced;
        }
    }
}
