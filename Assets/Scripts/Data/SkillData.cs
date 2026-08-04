// File: Assets/Scripts/Data/SkillData.cs
namespace DimensionLooter.Data
{
    public class SkillData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DimensionLooter.Core.Define.SkillType Type { get; set; }
        public int MpCost { get; set; }
        public float Cooldown { get; set; }
        public DimensionLooter.Core.Define.SkillEffectType EffectType { get; set; }
        public int EffectValue { get; set; }
        public float Duration { get; set; }
        public string Description { get; set; }
    }
}
