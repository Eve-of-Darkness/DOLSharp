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
#define NOENCRYPTION
using DOL.GS.PlayerTitles;
using DOL.GS.Housing;

namespace DOL.GS.PacketHandler
{
    [PacketLib(179, GameClient.eClientVersion.Version179)]
    public class PacketLib179 : PacketLib178
    {

        /// <summary>
        /// Constructs a new PacketLib for Version 1.79 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib179(GameClient client) : base(client)
        {
        }

        public override void SendUpdatePlayer()
        {
            GamePlayer player = GameClient.Player;
            if (player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x03); // subcode
                pak.WriteByte(0x0f); // number of entry
                pak.WriteByte(0x00); // subtype
                pak.WriteByte(0x00); // unk
                // entry :
                pak.WriteByte(player.GetDisplayLevel(GameClient.Player)); // level
                pak.WritePascalString(player.Name); // player name
                pak.WriteByte((byte)(player.MaxHealth >> 8)); // maxhealth high byte ?
                pak.WritePascalString(player.CharacterClass.Name); // class name
                pak.WriteByte((byte)(player.MaxHealth & 0xFF)); // maxhealth low byte ?
                pak.WritePascalString(/*"The "+*/player.CharacterClass.Profession); // Profession
                pak.WriteByte(0x00); // unk
                pak.WritePascalString(player.CharacterClass.GetTitle(player, player.Level)); // player level
                // todo make function to calcule realm rank
                // client.Player.RealmPoints
                // todo i think it s realmpoint percent not realrank
                pak.WriteByte((byte)player.RealmLevel); // urealm rank
                pak.WritePascalString(player.RealmRankTitle(player.Client.Account.Language)); // Realm title
                pak.WriteByte((byte)player.RealmSpecialtyPoints); // realm skill points
                pak.WritePascalString(player.CharacterClass.BaseName); // base class
                pak.WriteByte((byte)(HouseMgr.GetHouseNumberByPlayer(player) >> 8)); // personal house high byte
                pak.WritePascalString(player.GuildName); // Guild name
                pak.WriteByte((byte)(HouseMgr.GetHouseNumberByPlayer(player) & 0xFF)); // personal house low byte
                pak.WritePascalString(player.LastName); // Last name
                pak.WriteByte((byte)(player.MLLevel + 1)); // ML Level (+1)
                pak.WritePascalString(player.RaceName); // Race name
                pak.WriteByte(0x0);

                pak.WritePascalString(player.GuildRank?.Title ?? string.Empty);
                pak.WriteByte(0x0);

                AbstractCraftingSkill skill = CraftingMgr.getSkillbyEnum(player.CraftingPrimarySkill);
                pak.WritePascalString(skill?.Name ?? "None");

                pak.WriteByte(0x0);
                pak.WritePascalString(player.CraftTitle.GetValue(player, player)); // crafter title: legendary alchemist
                pak.WriteByte(0x0);
                pak.WritePascalString(player.MLTitle.GetValue(player, player)); // ML title

                // new in 1.75
                pak.WriteByte(0x0);
                pak.WritePascalString(player.CurrentTitle != PlayerTitleMgr.ClearTitle ? player.CurrentTitle.GetValue(player, player) : "None");

                // new in 1.79
                if (player.Champion)
                {
                    pak.WriteByte((byte)(player.ChampionLevel + 1)); // Champion Level (+1)
                }
                else
                {
                    pak.WriteByte(0x0);
                }

                pak.WritePascalString(player.CLTitle.GetValue(player, player)); // Champion Title
                SendTCP(pak);
            }
        }

        public override void SendUpdatePoints()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterPointsUpdate)))
            {
                pak.WriteInt((uint)GameClient.Player.RealmPoints);
                pak.WriteShort(GameClient.Player.LevelPermill);
                pak.WriteShort((ushort)GameClient.Player.SkillSpecialtyPoints);
                pak.WriteInt((uint)GameClient.Player.BountyPoints);
                pak.WriteShort((ushort)GameClient.Player.RealmSpecialtyPoints);
                pak.WriteShort(GameClient.Player.ChampionLevelPermill);
                SendTCP(pak);
            }
        }
    }
}
