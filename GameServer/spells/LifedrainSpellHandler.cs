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

using DOL.GS.Keeps;
using DOL.GS.PacketHandler;

namespace DOL.GS.Spells
{
    [SpellHandler(eSpellType.Lifedrain)]
    public class LifedrainSpellHandler : DirectDamageSpellHandler
    {
	    
		protected override void DealDamage(GameLiving target)
		{
			if (target == null || !target.IsAlive || target.ObjectState != GameLiving.eObjectState.Active) return;

			if (target is not (GamePlayer or GameNPC or GameKeepDoor or GameRelicDoor)) return;
			// calc damage and healing
			AttackData ad = CalculateDamageToTarget(target);
			// "Your life energy is stolen!"
			MessageToLiving(target, Spell.Message1, eChatType.CT_Spell);
			SendDamageMessages(ad);
			DamageTarget(ad, true);
			StealLife(ad);
			target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
		}

        /// <summary>
        /// Uses percent of damage to heal the caster
        /// </summary>
        public virtual void StealLife(AttackData ad)
        {
            if (ad == null) return;
            if (!m_caster.IsAlive) return;

            int heal = (ad.Damage + ad.CriticalDamage) * m_spell.LifeDrainReturn / 100;
            /*
            if (m_caster.IsDiseased)
            {
                MessageToCaster("You are diseased!", eChatType.CT_SpellResisted);
                heal >>= 1;
            }*/
            
            if (ad.Target is (GameKeepDoor or GameRelicDoor))
			{
				heal = 0;
			}
            
            if (heal <= 0) return;
            heal = m_caster.ChangeHealth(m_caster, eHealthChangeType.Spell, heal);

            if (heal > 0)
            {
                MessageToCaster("You steal " + heal + " hit point" + (heal == 1 ? "." : "s."), eChatType.CT_Spell);
            }
            else
            {
                MessageToCaster("You cannot absorb any more life.", eChatType.CT_SpellResisted);
            }
        }

        // constructor
        public LifedrainSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
