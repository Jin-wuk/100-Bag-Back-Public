using UnityEngine;

namespace BagBack100.Data.Models
{
    /// <summary>
    /// Role: 귀환석 정적 데이터 (기획 07 Q31). 비전투 상태에서 확인창을 거쳐 즉시 던전을 탈출하는 1회용 소모품.
    ///       포션과 같은 사용 경로(퀵슬롯·가방 더블클릭)를 타도록 ConsumableDataSO를 상속하며, 회복 수치는 쓰지 않는다.
    /// Dependencies: ConsumableDataSO
    /// Events: 없음
    /// </summary>
    [CreateAssetMenu(fileName = "NewReturnStoneData", menuName = "BagBack100/Data/Items/Return Stone")]
    public class ReturnStoneDataSO : ConsumableDataSO
    {
    }
}
