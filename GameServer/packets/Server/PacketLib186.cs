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

namespace DOL.GS.PacketHandler
{
    [PacketLib(186, GameClient.eClientVersion.Version186)]
    public class PacketLib186 : PacketLib185
    {
        /// <summary>
        /// Constructs a new PacketLib for Version 1.86 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib186(GameClient client)
            : base(client)
        {
        }

        /// <summary>
        /// The bow prepare animation
        /// </summary>
        public override int BowPrepare => 0x3E80;

        /// <summary>
        /// one dual weapon hit animation
        /// </summary>
        public override int OneDualWeaponHit => 0x3E81;

        /// <summary>
        /// both dual weapons hit animation
        /// </summary>
        public override int BothDualWeaponHit => 0x3E82;

        /// <summary>
        /// The bow shoot animation
        /// </summary>
        public override int BowShoot => 0x3E83;

        public override void SendCombatAnimation(GameObject attacker, GameObject defender, ushort weaponId, ushort shieldId, int style, byte stance, byte result, byte targetHealthPercent)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CombatAnimation)))
            {
                if (attacker != null)
                {
                    pak.WriteShort((ushort)attacker.ObjectID);
                }
                else
                {
                    pak.WriteShort(0x00);
                }

                if (defender != null)
                {
                    pak.WriteShort((ushort)defender.ObjectID);
                }
                else
                {
                    pak.WriteShort(0x00);
                }

                pak.WriteShort(weaponId);
                pak.WriteShort(shieldId);
                pak.WriteShortLowEndian((ushort)style);
                pak.WriteByte(stance);
                pak.WriteByte(result);

                // If Health Percent is invalid get the living Health.
                if (defender is GameLiving living && targetHealthPercent > 100)
                {
                    targetHealthPercent = living.HealthPercent;
                }

                pak.WriteByte(targetHealthPercent);
                pak.WriteByte(0);// unk
                SendTCP(pak);
            }
        }

        public override void SendMinotaurRelicMapRemove(byte id)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MinotaurRelicMapRemove)))
            {
                pak.WriteIntLowEndian(id);
                SendTCP(pak);
            }
        }

        public override void SendMinotaurRelicMapUpdate(byte id, ushort region, int x, int y, int z)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MinotaurRelicMapUpdate)))
            {

                pak.WriteIntLowEndian(id);
                pak.WriteIntLowEndian(region);
                pak.WriteIntLowEndian((uint)x);
                pak.WriteIntLowEndian((uint)y);
                pak.WriteIntLowEndian((uint)z);

                SendTCP(pak);
            }
        }

        public override void SendMinotaurRelicWindow(GamePlayer player, int effect, bool flag)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {

                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(13);
                if (flag)
                {
                    pak.WriteByte(0);
                    pak.WriteInt((uint)effect);
                }
                else
                {
                    pak.WriteByte(1);
                    pak.WriteInt((uint)effect);
                }

                SendTCP(pak);
            }
        }

        public override void SendMinotaurRelicBarUpdate(GamePlayer player, int xp)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {

                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(14);
                pak.WriteByte(0);

                // 4k maximum
                if (xp > 4000)
                {
                    xp = 4000;
                }

                if (xp < 0)
                {
                    xp = 0;
                }

                pak.WriteInt((uint)xp);

                SendTCP(pak);
            }
        }
    }
}
