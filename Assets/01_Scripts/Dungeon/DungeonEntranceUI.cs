using UnityEngine;
using UnityEngine.SceneManagement;

namespace BagBack100.Dungeon
{
    public class DungeonEntranceUI : MonoBehaviour
    {
        [Header("Scene Transition Settings")]
        [Tooltip("진입할 던전 씬의 이름 (Build Settings에 추가되어야 함)")]
        [SerializeField] private string _dungeonSceneName = "03_DungeonScene";

        /// <summary>
        /// 허브씬의 던전 입장 버튼의 UI OnClick 이벤트에 연결할 메서드.
        /// </summary>
        public void OnClickEnterDungeon()
        {
            if (!string.IsNullOrEmpty(_dungeonSceneName))
            {
                SceneManager.LoadScene(_dungeonSceneName);
            }
        }

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            // TODO: 필요한 경우 여기에 열림 애니메이션 추가
        }

        public void HidePanel()
        {
            gameObject.SetActive(false);
        }
    }
}
