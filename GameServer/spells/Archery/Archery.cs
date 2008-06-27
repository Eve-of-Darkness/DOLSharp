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
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.Effects;
using DOL.Events;

using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace DOL.GS.Spells
{
	[SpellHandler("Archery")]
	public class Archery : ArrowSpellHandler
	{
        public override bool CheckBeginCast(GameLiving selectedTarget)
		{
			if (m_caster.ObjectState != GameLiving.eObjectState.Active)	return false;
			if (!m_caster.IsAlive)
			{
				MessageToCaster("You are dead and can't cast!", eChatType.CT_System);
				return false;
			}
			GameSpellEffect Phaseshift = SpellHandler.FindEffectOnTarget(Caster, "Phaseshift");
			if (Phaseshift != null && (Spell.InstrumentRequirement == 0 || Spell.SpellType == "Mesmerize"))
			{
				MessageToCaster("You're phaseshifted and can't cast a spell", eChatType.CT_System);
				return false;
			}

			// Apply Mentalist RA5L
			SelectiveBlindnessEffect SelectiveBlindness = (SelectiveBlindnessEffect)Caster.EffectList.GetOfType(typeof(SelectiveBlindnessEffect));
			if (SelectiveBlindness != null)
			{
				GameLiving EffectOwner = SelectiveBlindness.EffectSource;
				if(EffectOwner==selectedTarget)
				{
					if (m_caster is GamePlayer)
						((GamePlayer)m_caster).Out.SendMessage(string.Format("{0} is invisible to you!", selectedTarget.GetName(0, true)), eChatType.CT_Missed, eChatLoc.CL_SystemWindow);
					
					return false;
				}
			}
            if (selectedTarget!=null&&selectedTarget.HasAbility("DamageImmunity"))
			{
				MessageToCaster(selectedTarget.Name + " is immune to this effect!", eChatType.CT_SpellResisted);
				return false;
			} 
			if (m_caster.IsSitting)
			{
				MessageToCaster("You can't cast while sitting!", eChatType.CT_SpellResisted);
				return false;
			}    
			if (m_spell.RecastDelay > 0)
			{
				int left = m_caster.GetSkillDisabledDuration(m_spell);
				if (left > 0)
				{
					MessageToCaster("You must wait " + (left / 1000 + 1).ToString() + " seconds to use this spell!", eChatType.CT_System);
					return false;
				}
			}
			String targetType = m_spell.Target.ToLower();
			if (targetType == "area")
			{
				if (!WorldMgr.CheckDistance(m_caster, m_caster.GroundTarget, CalculateSpellRange()))
				{
					MessageToCaster("Your area target is out of range.  Select a closer target.", eChatType.CT_SpellResisted);
					return false;
				}
			}
			
			if (Caster.AttackWeapon!=null&&(Caster.AttackWeapon.Object_Type == 15 || Caster.AttackWeapon.Object_Type == 18 || Caster.AttackWeapon.Object_Type == 9))
            {
                if (Spell.LifeDrainReturn == 1 && (!(Caster.IsStealthed)))
                {
                    MessageToCaster("You must be stealthed and wielding a bow to use this ability!", eChatType.CT_Spell);
                    return false;
                }
                return true;
            }
            else
            {
                if (Spell.LifeDrainReturn == 1)
                {
                    MessageToCaster("You must be stealthed and wielding a bow to use this ability!", eChatType.CT_Spell);
                    return false;
                }
            	MessageToCaster("You must be wielding a bow to use this ability!", eChatType.CT_Spell);
                return false;
            }
        }
		
		public override void SendSpellMessages()
		{
			MessageToCaster("You prepare " + Spell.Name, eChatType.CT_Spell);
		}
		
		public override AttackData CalculateDamageToTarget(GameLiving target, double effectiveness)
		{		
			AttackData ad = base.CalculateDamageToTarget(target, effectiveness);
			GamePlayer player; 
			GameSpellEffect bladeturn = FindEffectOnTarget(target, "Bladeturn");
			if (bladeturn != null)
			{
				switch (Spell.LifeDrainReturn)
				{
					case 0 :
							if (Caster is GamePlayer)
							{
								player = Caster as GamePlayer;
								player.Out.SendMessage("Your strike was absorbed by a magical barrier!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
							}								
							if (target is GamePlayer)
							{
								player = target as GamePlayer;	
								player.Out.SendMessage("The blow was absorbed by a magical barrier!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
								ad.AttackResult = GameLiving.eAttackResult.Missed;
								bladeturn.Cancel(false);
							}
							break;
					case 1 :
							if (target is GamePlayer)
							{
								player = target as GamePlayer;	
								player.Out.SendMessage("A shot penetrated your magic barrier!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
							}
							ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
							break;
					case 2:
							player = target as GamePlayer;	
							player.Out.SendMessage("A shot penetrated your magic barrier!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
							ad.AttackResult = GameLiving.eAttackResult.HitUnstyled;
							bladeturn.Cancel(false);
							break;						
				}
				return ad;
            }
			if (Spell.DamageType == eDamageType.Thrust)
			{
                GameSpellEffect ef = FindEffectOnTarget(Caster, "ArrowDamageTypes");
                if ( ef != null)
					ad.DamageType = ef.SpellHandler.Spell.DamageType;
            }
			return ad;			
		}
		
		public override void FinishSpellCast(GameLiving target)
		{
			if(target==null) return;
			if(Caster==null) return;
			if(Caster is GamePlayer&&Caster.IsStealthed)
				(Caster as GamePlayer).Stealth(false);
				
			if (target.Realm == 0 || Caster.Realm == 0)
			{
				target.LastAttackedByEnemyTickPvE = target.CurrentRegion.Time;
                Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
			}
			else
			{
				target.LastAttackedByEnemyTickPvP = target.CurrentRegion.Time;
                Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;
			}
			base.FinishSpellCast(target);
		}

        public override int CalculateCastingTime()
        {
            if (Spell.LifeDrainReturn == 2) return 6000;
            return base.CalculateCastingTime();
        }

        public override int CalculateNeededPower(GameLiving target) { return 0; }

        public override int CalculateEnduranceCost() { return (int)(Caster.MaxEndurance * (Spell.Power * .01)); }
		
		public override IList DelveInfo
		{
			get
			{
				ArrayList list = new ArrayList();
				//list.Add("Function: " + (Spell.SpellType == "" ? "(not implemented)" : Spell.SpellType));
				//list.Add(" "); //empty line
				list.Add(Spell.Description);
				list.Add(" "); //empty line
				if (Spell.InstrumentRequirement != 0)
					list.Add("Instrument require: " + GlobalConstants.InstrumentTypeToName(Spell.InstrumentRequirement));
				if (Spell.Damage != 0)
					list.Add("Damage: " + Spell.Damage.ToString("0.###;0.###'%'"));
				else if (Spell.Value != 0)
					list.Add("Value: " + Spell.Value.ToString("0.###;0.###'%'"));
				list.Add("Target: " + Spell.Target);
				if (Spell.Range != 0)
					list.Add("Range: " + Spell.Range);
				if (Spell.Duration >= ushort.MaxValue * 1000)
					list.Add("Duration: Permanent.");
				else if (Spell.Duration > 60000)
					list.Add(string.Format("Duration: {0}:{1} min", Spell.Duration / 60000, (Spell.Duration % 60000 / 1000).ToString("00")));
				else if (Spell.Duration != 0)
					list.Add("Duration: " + (Spell.Duration / 1000).ToString("0' sec';'Permanent.';'Permanent.'"));
				if (Spell.Frequency != 0)
					list.Add("Frequency: " + (Spell.Frequency * 0.001).ToString("0.0"));
				if (Spell.Power != 0)
					list.Add("Endurance cost: " + Spell.Power.ToString("0;0'%'"));
				list.Add("Casting time: " + (Spell.CastTime * 0.001).ToString("0.0## sec;-0.0## sec;'instant'"));
				if (Spell.RecastDelay > 60000)
					list.Add("Recast time: " + (Spell.RecastDelay / 60000).ToString() + ":" + (Spell.RecastDelay % 60000 / 1000).ToString("00") + " min");
				else if (Spell.RecastDelay > 0)
					list.Add("Recast time: " + (Spell.RecastDelay / 1000).ToString() + " sec");
				if (Spell.Radius != 0)
					list.Add("Radius: " + Spell.Radius);
				if (Spell.DamageType != eDamageType.Natural)
					list.Add("Damage: " + GlobalConstants.DamageTypeToName(Spell.DamageType));
				return list;
			}
		}

		public Archery(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
	}	
}