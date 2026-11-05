using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BagBack100.Core;
using BagBack100.Dungeon.Data;

namespace BagBack100.UI
{
    /// <summary>
    /// Role: 층계 던전 HUD. 현재 층 상시 표시, 층 진입 배너, 층 전환 페이드, 층 클리어·탈출구 등장 알림. 이벤트를 받아 화면만 갱신한다.
    ///       문구 서식: {0} = 지역 번호, {1} = 층, {2} = 지역 이름 (한글 TMP 폰트가 들어오면 {2} 서식으로 바꾼다).
    /// Dependencies: GlobalEvents, UIToast, RegionConfigSO(보스층 판정)
    /// Events: 없음 (구독: GlobalEvents.OnFloorTransitionStarted, GlobalEvents.OnFloorEntered, GlobalEvents.OnFloorCleared)
    /// </summary>
    public class FloorHUD : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("상시 층 표시 (예: Stage 1-3F)")]
        [SerializeField] private TextMeshProUGUI _floorLabel;
        [Tooltip("층 진입 배너 루트 (알파로 나타나고 사라짐)")]
        [SerializeField] private CanvasGroup _bannerGroup;
        [SerializeField] private TextMeshProUGUI _bannerText;
        [Tooltip("층 전환 암전용 전체 화면 이미지")]
        [SerializeField] private Image _fadeImage;
        [Tooltip("층 클리어·탈출구 등장 알림 (가방 패널 밖 HUD 캔버스의 UIToast)")]
        [SerializeField] private UIToast _toast;

        [Header("Text Format")]
        [SerializeField] private string _labelFormat = "Stage {0}-{1}F";
        [SerializeField] private string _bannerFormat = "Stage {0} - {1}F";
        // 한글 TMP 폰트가 들어오면 "층 클리어" / "탈출구가 열렸습니다"로 바꾼다
        [SerializeField] private string _clearMessage = "Floor Clear";
        [SerializeField] private string _exitOpenedMessage = "Floor Clear - Exit opened!";
        // 보스층은 처치 후 그 층을 떠날 때까지 무적이고 탈출구가 항상 열린다 (기획 07 Q29). 무적 표시는 이 문구만 (유저 결정)
        [SerializeField] private string _bossClearMessage = "Boss defeated - You are safe";

        [Header("Banner Timing (실시간 초)")]
        [SerializeField, Min(0f)] private float _bannerFadeSeconds = 0.3f;
        [SerializeField, Min(0f)] private float _bannerHoldSeconds = 1.4f;

        private float _screenFadeSeconds = 0.3f;
        private Coroutine _fadeRoutine;
        private Coroutine _bannerRoutine;

        private void OnEnable()
        {
            GlobalEvents.OnFloorTransitionStarted += HandleTransitionStarted;
            GlobalEvents.OnFloorEntered += HandleFloorEntered;
            GlobalEvents.OnFloorCleared += HandleFloorCleared;
        }

        private void OnDisable()
        {
            GlobalEvents.OnFloorTransitionStarted -= HandleTransitionStarted;
            GlobalEvents.OnFloorEntered -= HandleFloorEntered;
            GlobalEvents.OnFloorCleared -= HandleFloorCleared;
        }

        private void Awake()
        {
            SetFadeAlpha(0f);
            if (_bannerGroup != null) _bannerGroup.alpha = 0f;
            if (_floorLabel != null) _floorLabel.text = string.Empty;
        }

        private void HandleTransitionStarted(float fadeSeconds)
        {
            _screenFadeSeconds = fadeSeconds;
            StartFade(1f);
        }

        private void HandleFloorEntered(int regionId, string regionName, int floor)
        {
            if (_floorLabel != null) _floorLabel.text = string.Format(_labelFormat, regionId, floor, regionName);
            if (_bannerText != null) _bannerText.text = string.Format(_bannerFormat, regionId, floor, regionName);

            StartFade(0f);
            if (_bannerGroup != null)
            {
                if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
                _bannerRoutine = StartCoroutine(BannerRoutine());
            }
        }

        private void HandleFloorCleared(int floor, bool exitOpened)
        {
            if (_toast == null) return;
            if (RegionConfigSO.IsBossFloor(floor)) _toast.Show(_bossClearMessage);
            else _toast.Show(exitOpened ? _exitOpenedMessage : _clearMessage);
        }

        private void StartFade(float target)
        {
            if (_fadeImage == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(target));
        }

        // 층 전환 중에는 게임이 일시정지(timeScale 0)되므로 모든 연출은 실시간으로 돈다
        private IEnumerator FadeRoutine(float target)
        {
            float start = _fadeImage.color.a;
            float elapsed = 0f;
            while (elapsed < _screenFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Lerp(start, target, elapsed / _screenFadeSeconds));
                yield return null;
            }
            SetFadeAlpha(target);
            _fadeRoutine = null;
        }

        private IEnumerator BannerRoutine()
        {
            yield return FadeGroup(0f, 1f);
            yield return new WaitForSecondsRealtime(_bannerHoldSeconds);
            yield return FadeGroup(1f, 0f);
            _bannerRoutine = null;
        }

        private IEnumerator FadeGroup(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < _bannerFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _bannerGroup.alpha = Mathf.Lerp(from, to, elapsed / _bannerFadeSeconds);
                yield return null;
            }
            _bannerGroup.alpha = to;
        }

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeImage == null) return;
            Color c = _fadeImage.color;
            c.a = alpha;
            _fadeImage.color = c;
        }
    }
}
