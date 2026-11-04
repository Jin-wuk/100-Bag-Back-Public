using System;
using System.Collections.Generic;
using BagBack100.Monster;

namespace BagBack100.Dungeon
{
    /// <summary>
    /// Role: 현재 층에 살아 있는 몬스터 등록부와 층 클리어 판정 (POCO, DungeonGenerator가 층 생명주기에 맞춰 Begin/Arm/End 호출).
    ///       일반층: 소환수 포함 전부 사라지면 클리어. 보스층: Boss 데이터 몬스터가 모두 사라지면 클리어(Boss가 0마리면 일반층 규칙).
    ///       사망 전리품 이벤트(OnMonsterDied)는 개체 정보가 없고 강제 제거·층 정리 파괴를 놓치므로 쓰지 않는다.
    /// Dependencies: MonsterController(static OnSpawned / OnRemoved)
    /// Events: 없음 (클리어 시 생성자에서 받은 콜백 1회 호출)
    /// </summary>
    public class FloorClearTracker
    {
        private readonly HashSet<MonsterController> _alive = new HashSet<MonsterController>();
        private readonly List<MonsterController> _remainingBuffer = new List<MonsterController>(16);
        private readonly Action<IReadOnlyList<MonsterController>> _onCleared;

        private bool _isTracking;
        private bool _isArmed;
        private bool _bossOnly;

        public int AliveCount => _alive.Count;

        /// <param name="onCleared">클리어 순간 1회 호출. 인자는 아직 남아 있는 몬스터(보스층 정리 대상, 일반층은 빈 목록)의 복사본</param>
        public FloorClearTracker(Action<IReadOnlyList<MonsterController>> onCleared)
        {
            _onCleared = onCleared;
        }

        /// <summary>층 배치 직전에 호출. 이후 Awake에서 발행되는 몬스터 생성을 모두 받는다.</summary>
        public void Begin(bool isBossFloor)
        {
            End();
            _bossOnly = isBossFloor;
            _isTracking = true;
            MonsterController.OnSpawned += HandleSpawned;
            MonsterController.OnRemoved += HandleRemoved;
        }

        /// <summary>
        /// 층 배치·안개 초기화·층 진입 알림이 끝난 뒤 호출. 이때부터 클리어를 판정한다.
        /// 보스·소환 여부는 Awake 시점에 아직 주입되지 않으므로 여기서 처음 조회한다.
        /// </summary>
        public void Arm()
        {
            if (!_isTracking) return;
            _alive.RemoveWhere(m => m == null);
            if (_bossOnly && CountBosses() == 0)
            {
                // 보스 데이터가 없는 보스층(회색 박스 시험용)은 전원 처치로 클리어
                _bossOnly = false;
            }
            _isArmed = true;
            EvaluateClear();
        }

        /// <summary>층 정리 직전·소유자 파괴 시 호출. 층 전환으로 몬스터가 파괴되며 들어오는 제거가 클리어를 띄우지 않게 한다.</summary>
        public void End()
        {
            if (_isTracking)
            {
                MonsterController.OnSpawned -= HandleSpawned;
                MonsterController.OnRemoved -= HandleRemoved;
            }
            _isTracking = false;
            _isArmed = false;
            _alive.Clear();
        }

        private void HandleSpawned(MonsterController monster)
        {
            if (monster != null) _alive.Add(monster);
        }

        private void HandleRemoved(MonsterController monster)
        {
            if (!_alive.Remove(monster)) return;
            EvaluateClear();
        }

        private void EvaluateClear()
        {
            if (!_isArmed) return;

            bool cleared = _bossOnly ? CountBosses() == 0 : _alive.Count == 0;
            if (!cleared) return;

            // 콜백 안의 보스층 정리(DespawnWithoutLoot)가 동기적으로 OnRemoved를 다시 부르므로,
            // 먼저 판정을 끄고 남은 개체를 복사본으로 넘긴다 (컬렉션 변경 예외·중복 클리어 방지)
            _isArmed = false;
            _remainingBuffer.Clear();
            foreach (MonsterController monster in _alive)
            {
                if (monster != null) _remainingBuffer.Add(monster);
            }
            _onCleared?.Invoke(_remainingBuffer);
        }

        private int CountBosses()
        {
            int count = 0;
            foreach (MonsterController monster in _alive)
            {
                if (monster != null && monster.IsBoss) count++;
            }
            return count;
        }
    }
}
