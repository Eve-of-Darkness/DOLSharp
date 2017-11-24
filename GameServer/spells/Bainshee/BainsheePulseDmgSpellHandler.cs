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
using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using DOL.Events;
using DOL.GS.Effects;

namespace DOL.GS.Spells
{
    /// <summary>
    ///
    /// </summary>
    [SpellHandler("BainsheePulseDmg")]
    public class BainsheePulseDmgSpellHandler : SpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public const string FOCUS_WEAK = "FocusSpellHandler.Online";
        /// <summary>
        /// Execute direct damage spell
        /// </summary>
        /// <param name="target"></param>
        public override void FinishSpellCast(GameLiving target)
        {
            if (Spell.Pulse != 0)
            {
                GameEventMgr.AddHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(EventAction));
                GameEventMgr.AddHandler(Caster, GameLivingEvent.Dying, new DOLEventHandler(EventAction));
            }

            Caster.Mana -= PowerCost(target);
            base.FinishSpellCast(target);
        }

        public override bool CancelPulsingSpell(GameLiving living, string spellType)
        {
            lock (living.ConcentrationEffects)
            {
                for (int i = 0; i < living.ConcentrationEffects.Count; i++)
                {
                    PulsingSpellEffect effect = living.ConcentrationEffects[i] as PulsingSpellEffect;
                    if (effect == null)
                    {
                        continue;
                    }

                    if (effect.SpellHandler.Spell.SpellType == spellType)
                    {
                        effect.Cancel(false);
                        GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(EventAction));
                        GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Dying, new DOLEventHandler(EventAction));
                        return true;
                    }
                }
            }

            return false;
        }

        public void EventAction(DOLEvent e, object sender, EventArgs args)
        {
            if (!(sender is GameLiving))
            {
                return;
            }

            if (Spell.Pulse != 0 && CancelPulsingSpell(Caster, Spell.SpellType))
            {
                MessageToCaster("You cancel your effect.", eChatType.CT_Spell);
            }
        }

        #region LOS on Keeps

        private const string LOSEFFECTIVENESS = "LOS Effectivness";

        /// <summary>
        /// execute direct effect
        /// </summary>
        /// <param name="target">target that gets the damage</param>
        /// <param name="effectiveness">factor from 0..1 (0%-100%)</param>
        public override void OnDirectEffect(GameLiving target, double effectiveness)
        {
            if (target == null)
            {
                return;
            }

            bool spellOK = !(Spell.Target == "Frontal" || Spell.Target == "Enemy" && Spell.Radius > 0 && Spell.Range == 0);

            if (!spellOK || CheckLOS(Caster))
            {
                GamePlayer player = null;
                if (target is GamePlayer gamePlayer)
                {
                    player = gamePlayer;
                }
                else
                {
                    if (Caster is GamePlayer player1)
                    {
                        player = player1;
                    }
                    else if ((Caster as GameNPC)?.Brain is IControlledBrain)
                    {
                        IControlledBrain brain = ((GameNPC) Caster).Brain as IControlledBrain;
                        player = brain.GetPlayerOwner();
                    }
                }

                if (player != null)
                {
                    player.TempProperties.setProperty(LOSEFFECTIVENESS, effectiveness);
                    player.Out.SendCheckLOS(Caster, target, new CheckLOSResponse(DealDamageCheckLOS));
                }
                else
                {
                    DealDamage(target, effectiveness);
                }
            }
            else
            {
                DealDamage(target, effectiveness);
            }
        }

        private bool CheckLOS(GameLiving living)
        {
            foreach (AbstractArea area in living.CurrentAreas)
            {
                if (area.CheckLOS)
                {
                    return true;
                }
            }

            return false;
        }

        private void DealDamageCheckLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if (player == null) // Hmm
            {
                return;
            }

            if ((response & 0x100) == 0x100)
            {
                try
                {
                    if (Caster.CurrentRegion.GetObject(targetOID) is GameLiving target)
                    {
                        double effectiveness = (double)player.TempProperties.getProperty<object>(LOSEFFECTIVENESS, null);
                        DealDamage(target, effectiveness);
                    }
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                    {
                        log.Error($"targetOID:{targetOID} caster:{Caster} exception:{e}");
                    }
                }
            }
        }

        private void DealDamage(GameLiving target, double effectiveness)
        {
            if (!target.IsAlive || target.ObjectState != GameObject.eObjectState.Active)
            {
                return;
            }

            // calc damage
            AttackData ad = CalculateDamageToTarget(target, effectiveness);
            DamageTarget(ad, true);
            SendDamageMessages(ad);
            target.StartInterruptTimer(target.SpellInterruptDuration, ad.AttackType, Caster);
        }

        /*
         * We need to send resist spell los check packets because spell resist is calculated first, and
         * so you could be inside keep and resist the spell and be interupted when not in view
         */
        protected override void OnSpellResisted(GameLiving target)
        {
            if (target is GamePlayer player && Caster.TempProperties.getProperty("player_in_keep_property", false))
            {
                player.Out.SendCheckLOS(Caster, player, new CheckLOSResponse(ResistSpellCheckLOS));
            }
            else
            {
                SpellResisted(target);
            }
        }

        private void ResistSpellCheckLOS(GamePlayer player, ushort response, ushort targetOID)
        {
            if ((response & 0x100) == 0x100)
            {
                try
                {
                    if (Caster.CurrentRegion.GetObject(targetOID) is GameLiving target)
                    {
                        SpellResisted(target);
                    }
                }
                catch (Exception e)
                {
                    if (log.IsErrorEnabled)
                    {
                        log.Error($"targetOID:{targetOID} caster:{Caster} exception:{e}");
                    }
                }
            }
        }

        private void SpellResisted(GameLiving target)
        {
            base.OnSpellResisted(target);
        }
        #endregion

        // constructor
        public BainsheePulseDmgSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}
