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
using System.Reflection;
using log4net;

namespace DOL.GS.PacketHandler.Client.v168
{
    /// <summary>
    /// Handles spell cast requests from client
    /// </summary>
    [PacketHandler(PacketHandlerType.TCP, eClientPackets.UseSpell, "Handles Player Use Spell Request.", eClientStatus.PlayerInGame)]
    public class UseSpellHandler : IPacketHandler
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            int flagSpeedData = packet.ReadShort();
            int heading = packet.ReadShort();
            int xOffsetInZone = packet.ReadShort();
            int yOffsetInZone = packet.ReadShort();
            int currentZoneId = packet.ReadShort();
            int realZ = packet.ReadShort();

            Zone newZone = WorldMgr.GetZone((ushort)currentZoneId);
            if (newZone == null)
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn($"Unknown zone in UseSpellHandler: {currentZoneId} player: {client.Player.Name}");
                }
            }
            else
            {
                client.Player.X = newZone.XOffset + xOffsetInZone;
                client.Player.Y = newZone.YOffset + yOffsetInZone;
                client.Player.Z = realZ;
                client.Player.MovementStartTick = Environment.TickCount;
            }

            int spellLevel = packet.ReadByte();
            int spellLineIndex = packet.ReadByte();

            client.Player.Heading = (ushort)(heading & 0xfff);

            new UseSpellAction(client.Player, flagSpeedData, spellLevel, spellLineIndex).Start(1);
        }

        /// <summary>
        /// Handles player use spell actions
        /// </summary>
        private class UseSpellAction : RegionAction
        {
            /// <summary>
            /// The speed and flags data
            /// </summary>
            private readonly int _flagSpeedData;

            /// <summary>
            /// The used spell level
            /// </summary>
            private readonly int _spellLevel;

            /// <summary>
            /// The used spell line index
            /// </summary>
            private readonly int _spellLineIndex;

            /// <summary>
            /// Constructs a new UseSpellAction
            /// </summary>
            /// <param name="actionSource">The action source</param>
            /// <param name="flagSpeedData">The speed and flags data</param>
            /// <param name="spellLevel">The used spell level</param>
            /// <param name="spellLineIndex">The used spell line index</param>
            public UseSpellAction(GamePlayer actionSource, int flagSpeedData, int spellLevel, int spellLineIndex)
                : base(actionSource)
            {
                _flagSpeedData = flagSpeedData;
                _spellLevel = spellLevel;
                _spellLineIndex = spellLineIndex;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            protected override void OnTick()
            {
                GamePlayer player = (GamePlayer)m_actionSource;

                if ((_flagSpeedData & 0x200) != 0)
                {
                    player.CurrentSpeed = (short)(-(_flagSpeedData & 0x1ff)); // backward movement
                }
                else
                {
                    player.CurrentSpeed = (short)(_flagSpeedData & 0x1ff); // forward movement
                }

                player.IsStrafing = (_flagSpeedData & 0x4000) != 0;
                player.TargetInView = (_flagSpeedData & 0xa000) != 0; // why 2 bits? that has to be figured out
                player.GroundTargetInView = (_flagSpeedData & 0x1000) != 0;

                List<Tuple<SpellLine, List<Skill>>> snap = player.GetAllUsableListSpells();
                Skill sk = null;
                SpellLine sl = null;
                
                if (_spellLineIndex < snap.Count)
                {
                    int index = snap[_spellLineIndex].Item2.FindIndex(s =>
                    {
                        switch (s)
                        {
                            case Spell item:
                                return item.Level == _spellLevel;
                            case Styles.Style item:
                                return item.SpecLevelRequirement == _spellLevel;
                            case Ability item:
                                return item.SpecLevelRequirement == _spellLevel;
                            default:
                                return false;
                        }
                    });

                    if (index > -1)
                    {
                        sk = snap[_spellLineIndex].Item2[index];
                    }

                    sl = snap[_spellLineIndex].Item1;
                }

                if (sk is Spell spell && sl != null)
                {
                    player.CastSpell(spell, sl);
                }
                else if (sk is Styles.Style)
                {
                    player.ExecuteWeaponStyle((Styles.Style)sk);
                }
                else if (sk is Ability)
                {
                    Ability ab = (Ability)sk;
                    IAbilityActionHandler handler = SkillBase.GetAbilityActionHandler(ab.KeyName);
                    handler?.Execute(ab, player);

                    ab.Execute(player);
                }
                else
                {
                    if (Log.IsWarnEnabled)
                    {
                        Log.Warn(
                            $"Client <{player.Client.Account.Name}> requested incorrect spell at level {_spellLevel} in spell-line {sl?.Name ?? "unkown"}");
                    }

                    player.Out.SendMessage($"Error : Spell (Line {_spellLineIndex}, Level {_spellLevel}) can't be resolved...", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                }
            }
        }
    }
}