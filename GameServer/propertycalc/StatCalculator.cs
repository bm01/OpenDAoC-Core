/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using System;

namespace DOL.GS.PropertyCalc
{
    [PropertyCalculator(eProperty.Stat_First, eProperty.Stat_Last)]
    public class StatCalculator : PropertyCalculator
    {
        public StatCalculator() { }

        public override int CalcValue(GameLiving living, eProperty property)
        {
            int propertyIndex = (int) property;
            int baseStat = living.GetBaseStat((eStat) property);
            int itemBonus = CalcValueFromItems(living, property);
            int buffBonus = CalcValueFromBuffs(living, property);
            int abilityBonus = living.AbilityBonus[propertyIndex];
            int debuff = Math.Abs(living.DebuffCategory[propertyIndex]);
            int specDebuff = Math.Abs(living.SpecDebuffCategory[propertyIndex]);
            int deathConDebuff = 0;

            // Special cases:
            // 1) ManaStat (base stat + acuity, players only).
            // 2) As of patch 1.64: - Acuity - This bonus will increase your casting stat, 
            //    whatever your casting stat happens to be. If you're a druid, you should get an increase to empathy, 
            //    while a bard should get an increase to charisma.  http://support.darkageofcamelot.com/kb/article.php?id=540
            // 3) Constitution lost at death, only affects players.

            if (living is GamePlayer player)
            {
                if (property == (eProperty) player.CharacterClass.ManaStat)
                {
                    if (IsClassAffectedByAcuityAbility(player.CharacterClass))
                        abilityBonus += player.AbilityBonus[(int)eProperty.Acuity];
                }

                deathConDebuff = player.TotalConstitutionLostAtDeath;
            }

            // Apply debuffs. 100% effectiveness against buffs, 50% effectiveness against item and base stats.
            // SpecDebuffs (Champion's) have 200% effectiveness against buffs.

            int unbuffedBonus = baseStat + itemBonus;

            if (specDebuff > 0 && buffBonus > 0)
                ApplyDebuff(ref specDebuff, ref buffBonus, 0.5);

            if (specDebuff > 0 && unbuffedBonus > 0)
                ApplyDebuff(ref specDebuff, ref unbuffedBonus, 2);

            if (debuff > 0 && buffBonus > 0)
                ApplyDebuff(ref debuff, ref buffBonus, 1);

            if (debuff > 0 && unbuffedBonus > 0)
                ApplyDebuff(ref debuff, ref unbuffedBonus, 2);

            int stat = unbuffedBonus + buffBonus + abilityBonus;
            stat = (int) (stat * living.BuffBonusMultCategory1.Get((int) property));
            stat -= (property == eProperty.Constitution) ? deathConDebuff : 0;
            return Math.Max(1, stat);

            static void ApplyDebuff(ref int debuff, ref int stat, double modifier)
            {
                double remainingDebuff = debuff - stat * modifier;

                if (remainingDebuff > 0)
                {
                    debuff = (int) remainingDebuff;
                    stat = 0;
                }
                else
                {
                    stat -= (int) (debuff / modifier);
                    debuff = 0;
                }
            }
        }

        public override int CalcValueFromBuffs(GameLiving living, eProperty property)
        {
            if (living == null)
                return 0;

            int propertyIndex = (int) property;
            int baseBuffBonus = living.BaseBuffBonusCategory[propertyIndex];
            int specBuffBonus = living.SpecBuffBonusCategory[propertyIndex];

            if (living is GamePlayer player)
            {
                if (property == (eProperty) player.CharacterClass.ManaStat)
                {
                    if (player.CharacterClass.ClassType == eClassType.ListCaster)
                        specBuffBonus += player.BaseBuffBonusCategory[(int)eProperty.Acuity];
                }
            }

            // Caps and cap increases. Only players actually have a buff bonus cap, pets don't.
            int baseBuffBonusCap = (living is GamePlayer) ? (int)(living.Level * 1.25) : short.MaxValue;
            int specBuffBonusCap = (living is GamePlayer) ? (int)(living.Level * 1.5 * 1.25) : short.MaxValue;

            baseBuffBonus = Math.Min(baseBuffBonus, baseBuffBonusCap);
            specBuffBonus = Math.Min(specBuffBonus, specBuffBonusCap);
            return baseBuffBonus + specBuffBonus;
        }

        public override int CalcValueFromItems(GameLiving living, eProperty property)
        {
            if (living == null)
                return 0;

            int itemBonus = living.ItemBonus[(int) property];
            int itemBonusCap = GetItemBonusCap(living);

            if (living is GamePlayer player)
            {
                if (property == (eProperty) player.CharacterClass.ManaStat)
                {
                    if (IsClassAffectedByAcuityAbility(player.CharacterClass))
                        itemBonus += living.ItemBonus[(int)eProperty.Acuity];
                }
            }

            int itemBonusCapIncrease = GetItemBonusCapIncrease(living, property);
            int mythicalItemBonusCapIncrease = GetMythicalItemBonusCapIncrease(living, property);
            return Math.Min(itemBonus, itemBonusCap + itemBonusCapIncrease + mythicalItemBonusCapIncrease);
        }

        public static int GetItemBonusCap(GameLiving living)
        {
            return living == null ? 0 : (int) (living.Level * 1.5);
        }

        public static int GetItemBonusCapIncrease(GameLiving living, eProperty property)
        {
            if (living == null)
                return 0;

            int itemBonusCapIncreaseCap = GetItemBonusCapIncreaseCap(living);
            int itemBonusCapIncrease = living.ItemBonus[(int)(eProperty.StatCapBonus_First - eProperty.Stat_First + property)];

            if (living is GamePlayer player)
            {
                if (property == (eProperty) player.CharacterClass.ManaStat)
                {
                    if (IsClassAffectedByAcuityAbility(player.CharacterClass))
                        itemBonusCapIncrease += living.ItemBonus[(int)eProperty.AcuCapBonus];
                }
            }

            return Math.Min(itemBonusCapIncrease, itemBonusCapIncreaseCap);
        }

        public static int GetMythicalItemBonusCapIncrease(GameLiving living, eProperty property)
        {
            if (living == null)
                return 0;

            int mythicalItemBonusCapIncreaseCap = GetMythicalItemBonusCapIncreaseCap(living);
            int mythicalItemBonusCapIncrease = living.ItemBonus[(int) (eProperty.MythicalStatCapBonus_First - eProperty.Stat_First + property)];
            int itemBonusCapIncrease = GetItemBonusCapIncrease(living, property);

            if (living is GamePlayer player)
            {
                if (property == (eProperty) player.CharacterClass.ManaStat)
                {
                    if (IsClassAffectedByAcuityAbility(player.CharacterClass))
                        mythicalItemBonusCapIncrease += living.ItemBonus[(int) eProperty.MythicalAcuCapBonus];
                }
            }

            if (mythicalItemBonusCapIncrease + itemBonusCapIncrease > 52)
                mythicalItemBonusCapIncrease = 52 - itemBonusCapIncrease;

            return Math.Min(mythicalItemBonusCapIncrease, mythicalItemBonusCapIncreaseCap);
        }

        public static int GetItemBonusCapIncreaseCap(GameLiving living)
        {
            return living == null ? 0 : living.Level / 2 + 1;
        }

        public static int GetMythicalItemBonusCapIncreaseCap(GameLiving living)
        {
            return living == null ? 0 : 52;
        }

        public static bool IsClassAffectedByAcuityAbility(ICharacterClass characterClass)
        {
            return (eCharacterClass) characterClass.ID is
                not eCharacterClass.Scout and
                not eCharacterClass.Hunter and
                not eCharacterClass.Ranger and
                not eCharacterClass.Nightshade;
        }
    }
}
