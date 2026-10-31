using UnityEngine;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 중첩 Canvas의 그리기 순서를 런타임에 강제한다. 메뉴·대화상자·안내 문구가 같은 Canvas의 다른 패널(대장간·잡화상 등) 뒤에 가려지지 않게 한다.
    ///       프리팹 편집 화면에서는 부모 Canvas가 없어 overrideSorting이 저장되지 않으므로 실행 시점에 설정한다.
    /// Dependencies: Canvas
    /// Events: 없음
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class UISortingOverride : MonoBehaviour
    {
        [SerializeField] private int _sortingOrder = 100;

        private void Awake()
        {
            var canvas = GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _sortingOrder;
        }
    }
}
