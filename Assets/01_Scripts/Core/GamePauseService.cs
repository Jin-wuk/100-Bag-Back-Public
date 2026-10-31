using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BagBack100.Core
{
    /// <summary>
    /// Role: Time.timeScale의 단일 소유자. 일시정지(가방 열기 등) 요청과 히트스톱을 함께 관리해,
    ///       히트스톱이 끝나며 timeScale을 1로 되돌려 일시정지가 풀리는 충돌을 막는다.
    ///       timeScale은 이 서비스만 쓴다 (다른 스크립트에서 직접 대입 금지).
    /// Dependencies: 없음
    /// Events: OnPauseChanged(bool isPaused)
    /// </summary>
    public static class GamePauseService
    {
        public static event Action<bool> OnPauseChanged;

        // 여러 UI가 동시에 일시정지를 요청해도 마지막 요청자가 해제할 때까지 유지되도록 요청자 집합으로 관리
        private static readonly HashSet<object> _owners = new HashSet<object>();
        private static float _hitStopScale = 1f;
        private static bool _isHitStopActive;

        public static bool IsPaused => _owners.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            // 도메인 리로드 비활성화(Enter Play Mode Options) 시 이전 플레이의 정적 상태가 남지 않도록 초기화
            _owners.Clear();
            _isHitStopActive = false;
            _hitStopScale = 1f;
            OnPauseChanged = null;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        // 일시정지 상태로 씬이 바뀌어 다음 씬(허브)이 멈춘 채 시작되는 것을 방지
        private static void HandleSceneUnloaded(Scene scene) => ReleaseAll();

        public static void Request(object owner)
        {
            if (owner == null || !_owners.Add(owner)) return;
            if (_owners.Count == 1) NotifyPauseChanged();
            else ApplyTimeScale();
        }

        public static void Release(object owner)
        {
            if (owner == null || !_owners.Remove(owner)) return;
            if (_owners.Count == 0) NotifyPauseChanged();
        }

        public static void ReleaseAll()
        {
            bool wasPaused = IsPaused;
            _owners.Clear();
            _isHitStopActive = false;
            _hitStopScale = 1f;
            if (wasPaused) NotifyPauseChanged();
            else ApplyTimeScale();
        }

        /// <summary>히트스톱 시작. 일시정지 중이면 timeScale은 0으로 유지된다.</summary>
        public static void ApplyHitStop(float scale)
        {
            _isHitStopActive = true;
            _hitStopScale = Mathf.Clamp01(scale);
            ApplyTimeScale();
        }

        /// <summary>히트스톱 종료. 일시정지 중이면 0을 유지하고, 아니면 1로 복귀한다.</summary>
        public static void ClearHitStop()
        {
            _isHitStopActive = false;
            _hitStopScale = 1f;
            ApplyTimeScale();
        }

        private static void NotifyPauseChanged()
        {
            ApplyTimeScale();
            OnPauseChanged?.Invoke(IsPaused);
        }

        private static void ApplyTimeScale()
        {
            if (IsPaused) Time.timeScale = 0f;
            else Time.timeScale = _isHitStopActive ? _hitStopScale : 1f;
        }
    }
}
