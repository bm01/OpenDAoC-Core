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
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using System.Linq;

namespace DOL.GS.Spells
{
	/// <summary>
	/// Debuffs two stats at once, goes into specline bonus category
	/// </summary>	
	public abstract class DualStatDebuff : SingleStatDebuff
	{
		public override eBuffBonusCategory BonusCategory1 { get { return eBuffBonusCategory.Debuff; } }
		public override eBuffBonusCategory BonusCategory2 { get { return eBuffBonusCategory.Debuff; } }

        // public override void ApplyEffectOnTarget(GameLiving target, double effectiveness)
        // {
		// 	var debuffs = target.effectListComponent.GetSpellEffects()
		// 						.Where(x => x.SpellHandler is DualStatDebuff);

		// 	foreach (var debuff in debuffs)
		// 	{
		// 		var debuffSpell = debuff.SpellHandler as DualStatDebuff;

		// 		if (debuffSpell.Property1 == this.Property1 && debuffSpell.Property2 == this.Property2 && debuffSpell.Spell.Value >= Spell.Value)
		// 		{
		// 			// Old Spell is Better than new one
		// 			SendSpellResistAnimation(target);
		// 			this.MessageToCaster(eChatType.CT_SpellResisted, "{0} already has that effect.", target.GetName(0, true));
		// 			MessageToCaster("Wait until it expires. Spell Failed.", eChatType.CT_SpellResisted);
		// 			// Prevent Adding.
		// 			return;
		// 		}
		// 	}

		// 	base.ApplyEffectOnTarget(target, effectiveness);
		// }

        // constructor
        public DualStatDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
	}

	/// <summary>
	/// Str/Con stat specline debuff
	/// </summary>
	[SpellHandler(eSpellType.StrengthConstitutionDebuff)]
	public class StrengthConDebuff : DualStatDebuff
	{
		public override eProperty Property1 { get { return eProperty.Strength; } }
		public override eProperty Property2 { get { return eProperty.Constitution; } }

		// constructor
		public StrengthConDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
	}

	/// <summary>
	/// Dex/Qui stat specline debuff
	/// </summary>
	[SpellHandler(eSpellType.DexterityQuicknessDebuff)]
	public class DexterityQuiDebuff : DualStatDebuff
	{
		public override eProperty Property1 { get { return eProperty.Dexterity; } }
		public override eProperty Property2 { get { return eProperty.Quickness; } }

		// constructor
		public DexterityQuiDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
	}
	/// Dex/Con Debuff for assassin poisons
	/// <summary>
	/// Dex/Con stat specline debuff
	/// </summary>
	[SpellHandler(eSpellType.DexterityConstitutionDebuff)]
	public class DexterityConDebuff : DualStatDebuff
	{
		public override eProperty Property1 { get { return eProperty.Dexterity; } }
		public override eProperty Property2 { get { return eProperty.Constitution; } }

		// constructor
		public DexterityConDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
	}

	[SpellHandler(eSpellType.WeaponSkillConstitutionDebuff)]
	public class WeaponskillConDebuff : DualStatDebuff
	{
		public override eProperty Property1 { get { return eProperty.WeaponSkill; } }
		public override eProperty Property2 { get { return eProperty.Constitution; } }
		public WeaponskillConDebuff(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
	}
}
