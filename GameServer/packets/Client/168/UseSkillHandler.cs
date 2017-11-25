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
using System.Collections.Generic;
using DOL.GS.Styles;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandler(PacketHandlerType.TCP, eClientPackets.UseSkill, "Handles Player Use Skill Request.", eClientStatus.PlayerInGame)]
    public class UseSkillHandler : IPacketHandler
    {

        #region IPacketHandler Members

        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            int flagSpeedData = packet.ReadShort();
            int index = packet.ReadByte();
            int type = packet.ReadByte();

            new UseSkillAction(client.Player, flagSpeedData, index, type).Start(1);
        }

        #endregion

        #region Nested type: UseSkillAction

        /// <summary>
        /// Handles player use skill actions
        /// </summary>
        private class UseSkillAction : RegionAction
        {
            /// <summary>
            /// The speed and flags data
            /// </summary>
            private readonly int _flagSpeedData;

            /// <summary>
            /// The skill index
            /// </summary>
            private readonly int _index;

            /// <summary>
            /// The skill type
            /// </summary>
            private readonly int _type;

            /// <summary>
            /// Constructs a new UseSkillAction
            /// </summary>
            /// <param name="actionSource">The action source</param>
            /// <param name="flagSpeedData">The skill type</param>
            /// <param name="index">The skill index</param>
            /// <param name="type">The skill type</param>
            public UseSkillAction(GamePlayer actionSource, int flagSpeedData, int index, int type)
                : base(actionSource)
            {
                _flagSpeedData = flagSpeedData;
                _index = index;
                _type = type;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            protected override void OnTick()
            {
                GamePlayer player = (GamePlayer)m_actionSource;
                if (player == null)
                {
                    return;
                }

                if ((_flagSpeedData & 0x200) != 0)
                {
                    player.CurrentSpeed = (short)(-(_flagSpeedData & 0x1ff)); // backward movement
                }
                else
                {
                    player.CurrentSpeed = (short)(_flagSpeedData & 0x1ff); // forwardmovement
                }

                player.IsStrafing = (_flagSpeedData & 0x4000) != 0;
                player.TargetInView = (_flagSpeedData & 0xa000) != 0; // why 2 bits? that has to be figured out
                player.GroundTargetInView = (_flagSpeedData & 0x1000) != 0;

                List<Tuple<Skill, Skill>> snap = player.GetAllUsableSkills();

                Skill sk = null;
                Skill sksib = null;

                // we're not using a spec !
                if (_type > 0)
                {

                    // find the first non-specialization index.
                    int begin = Math.Max(0, snap.FindIndex(it => (it.Item1 is Specialization) == false));

                    // are we in list ?
                    if (_index + begin < snap.Count)
                    {
                        sk = snap[_index + begin].Item1;
                        sksib = snap[_index + begin].Item2;
                    }
                }
                else
                {
                    // mostly a spec !
                    if (_index < snap.Count)
                    {
                        sk = snap[_index].Item1;
                        sksib = snap[_index].Item2;
                    }
                }

                // we really got a skill !
                if (sk != null)
                {
                    // Test if we can use it !
                    int reuseTime = player.GetSkillDisabledDuration(sk);
                    if (reuseTime > 60000)
                    {
                        player.Out.SendMessage($"You must wait {reuseTime / 60000} minutes {reuseTime % 60000 / 1000} seconds to use this ability!", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        if (player.Client.Account.PrivLevel < 2)
                        {
                            return;
                        }
                    }
                    else if (reuseTime > 0)
                    {
                        player.Out.SendMessage($"You must wait {reuseTime / 1000 + 1} seconds to use this ability!", eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        if (player.Client.Account.PrivLevel < 2)
                        {
                            return;
                        }
                    }

                    // See what we should do depending on skill type !
                    if (sk is Specialization spec)
                    {
                        ISpecActionHandler handler = SkillBase.GetSpecActionHandler(spec.KeyName);
                        handler?.Execute(spec, player);
                    }
                    else if (sk is Ability)
                    {
                        Ability ab = (Ability)sk;
                        IAbilityActionHandler handler = SkillBase.GetAbilityActionHandler(ab.KeyName);
                        if (handler != null)
                        {
                            handler.Execute(ab, player);
                            return;
                        }

                        ab.Execute(player);
                    }
                    else if (sk is Spell)
                    {
                        if (sksib is SpellLine line)
                        {
                            player.CastSpell((Spell)sk, line);
                        }
                    }
                    else if (sk is Style)
                    {
                        player.ExecuteWeaponStyle((Style)sk);
                    }
                }

                if (sk == null)
                {
                    player.Out.SendMessage("Skill is not implemented.", eChatType.CT_Advise, eChatLoc.CL_SystemWindow);
                }
            }
        }

        #endregion
    }
}