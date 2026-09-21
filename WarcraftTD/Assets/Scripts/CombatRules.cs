using UnityEngine;

namespace WarcraftTD
{
    public enum DamageType
    {
        Normal,
        Piercing,
        Magic,
        Poison
    }

    public enum ArmorType
    {
        Light,
        Medium,
        Heavy,
        Magic
    }

    public static class CombatRules
    {
        public static float Resolve(float rawDamage, DamageType damage, ArmorType armor)
        {
            float multiplier = damage switch
            {
                DamageType.Normal => armor switch
                {
                    ArmorType.Light => 1f,
                    ArmorType.Medium => 0.7f,
                    ArmorType.Heavy => 0.45f,
                    _ => 1f
                },
                DamageType.Piercing => armor switch
                {
                    ArmorType.Light => 2f,
                    ArmorType.Medium => 1f,
                    ArmorType.Heavy => 0.6f,
                    _ => 1f
                },
                DamageType.Magic => armor == ArmorType.Magic ? 1f : 2f,
                DamageType.Poison => 1.5f,
                _ => 1f
            };
            return Mathf.Max(0f, rawDamage * multiplier);
        }
    }
}
