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
#define  NOENCRYPTION
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using DOL.Database;
using DOL.Language;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.Housing;
using DOL.GS.Keeps;
using DOL.GS.PlayerTitles;
using DOL.GS.Quests;
using DOL.GS.RealmAbilities;
using DOL.GS.Spells;
using DOL.GS.Styles;
using log4net;

namespace DOL.GS.PacketHandler
{
    [PacketLib(168, GameClient.eClientVersion.Version168)]
    public class PacketLib168 : AbstractPacketLib, IPacketLib
    {
        private const int MaxPacketLength = 2048;

        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.68 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib168(GameClient client)
            : base(client)
        {
        }

        // Packets
        #region IPacketLib Members

        public virtual void SendVersionAndCryptKey()
        {
            // Construct the new packet
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CryptKey)))
            {
                // Enable encryption
                #if !NOENCRYPTION
				pak.WriteByte(0x01);
                #else
                pak.WriteByte(0x00);
                #endif

                // if(is_si)
                pak.WriteByte(0x32);

                // else
                //  pak.WriteByte(0x31);
                pak.WriteByte(ParseVersion((int)GameClient.Version, true));
                pak.WriteByte(ParseVersion((int)GameClient.Version, false));

                // pak.WriteByte(build);
                pak.WriteByte(0x00);

                #if !NOENCRYPTION
				byte[] publicKey = new byte[500];
				UInt32 keyLen = CryptLib168.ExportRSAKey(publicKey, (UInt32) 500, false);
				pak.WriteShort((ushort) keyLen);
				pak.Write(publicKey, 0, (int) keyLen);
				//From now on we expect RSA!
				((PacketEncoding168) m_gameClient.PacketProcessor.Encoding).EncryptionState = PacketEncoding168.eEncryptionState.RSAEncrypted;
                #endif

                SendTCP(pak);
            }
        }

        public virtual void SendWarlockChamberEffect(GamePlayer player)
        {
        }

        public virtual void SendLoginDenied(eLoginError et)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.LoginDenied)))
            {
                pak.WriteByte((byte)et); // Error Code
                pak.WriteByte(0x01);
                pak.WriteByte(ParseVersion((int)GameClient.Version, true));
                pak.WriteByte(ParseVersion((int)GameClient.Version, false));
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendLoginGranted()
        {
            SendLoginGranted(GameServer.ServerRules.GetColorHandling(GameClient));
        }

        public virtual void SendLoginGranted(byte color)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.LoginGranted)))
            {
                pak.WriteByte(0x01);
                pak.WriteByte(ParseVersion((int)GameClient.Version, true));
                pak.WriteByte(ParseVersion((int)GameClient.Version, false));
                pak.WriteByte(0x00);
                pak.WritePascalString(GameClient.Account.Name);
                pak.WritePascalString(GameServer.Instance.Configuration.ServerNameShort); // server name
                pak.WriteByte(0x0C); // Server ID
                pak.WriteByte(color);
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendSessionID()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SessionID)))
            {
                pak.WriteShortLowEndian((ushort)GameClient.SessionID);
                SendTCP(pak);
            }
        }

        public virtual void SendPingReply(ulong timestamp, ushort sequence)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PingReply)))
            {
                pak.WriteInt((uint)timestamp);
                pak.Fill(0x00, 4);
                pak.WriteShort((ushort)(sequence + 1));
                pak.Fill(0x00, 6);
                SendTCP(pak);
            }
        }

        public virtual void SendRealm(eRealm realm)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Realm)))
            {
                pak.WriteByte((byte)realm);
                SendTCP(pak);
            }
        }

        public virtual void SendCharacterOverview(eRealm realm)
        {
            int firstAccountSlot;
            switch (realm)
            {
                case eRealm.Albion:
                    firstAccountSlot = 100;
                    break;
                case eRealm.Midgard:
                    firstAccountSlot = 200;
                    break;
                case eRealm.Hibernia:
                    firstAccountSlot = 300;
                    break;
                default:
                    throw new Exception($"CharacterOverview requested for unknown realm {realm}");
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterOverview)))
            {
                pak.FillString(GameClient.Account.Name, 24);
                DOLCharacters[] characters = GameClient.Account.Characters;
                if (characters == null)
                {
                    pak.Fill(0x0, 1848);
                }
                else
                {
                    for (int i = firstAccountSlot; i < firstAccountSlot + 8; i++)
                    {
                        bool written = false;
                        for (int j = 0; j < characters.Length && written == false; j++)
                        {
                            if (characters[j].AccountSlot == i)
                        {
                            pak.FillString(characters[j].Name, 24);
                            pak.Fill(0x0, 24); // 0 String

                            Region reg = WorldMgr.GetRegion((ushort)characters[j].Region);
                            if (reg != null)
                            {
                                var description = GameClient.GetTranslatedSpotDescription(reg, characters[j].Xpos, characters[j].Ypos, characters[j].Zpos);
                                pak.FillString(description, 24);
                            }
                            else
                                {
                                    pak.Fill(0x0, 24); // No known location
                                }

                                pak.FillString(string.Empty, 24); // Class name

                            // pak.FillString(GamePlayer.RACENAMES[characters[j].Race], 24);
                            pak.FillString(GameClient.RaceToTranslatedName(characters[j].Race, characters[j].Gender), 24);
                            pak.WriteByte((byte)characters[j].Level);
                            pak.WriteByte((byte)characters[j].Class);
                            pak.WriteByte((byte)characters[j].Realm);
                            pak.WriteByte((byte)((((characters[j].Race & 0x10) << 2) + (characters[j].Race & 0x0F)) | (characters[j].Gender << 4)));

                            // race max value can be 0x1F
                            pak.WriteShortLowEndian((ushort)characters[j].CurrentModel);
                            pak.WriteByte((byte)characters[j].Region);
                            if (reg == null || (int)GameClient.ClientType > reg.Expansion)
                            {
                                pak.WriteByte(0x00);
                            }
                            else
                            {
                                pak.WriteByte((byte)(reg.Expansion + 1)); // 0x04-Cata zone, 0x05 - DR zone
                            }

                            pak.WriteInt(0x0); // Internal database ID
                            pak.WriteByte((byte)characters[j].Strength);
                            pak.WriteByte((byte)characters[j].Dexterity);
                            pak.WriteByte((byte)characters[j].Constitution);
                            pak.WriteByte((byte)characters[j].Quickness);
                            pak.WriteByte((byte)characters[j].Intelligence);
                            pak.WriteByte((byte)characters[j].Piety);
                            pak.WriteByte((byte)characters[j].Empathy);
                            pak.WriteByte((byte)characters[j].Charisma);

                            var items = GameServer.Database.SelectObjects<InventoryItem>(
                                "`OwnerID` = @OwnerID AND `SlotPosition` >= @SlotPositionMin AND `SlotPosition` <= @SlotPositionMax",
                                new[]
                                {
                                    new QueryParameter("@OwnerID", characters[j].ObjectId),
                                    new QueryParameter("@SlotPositionMin", 10),
                                    new QueryParameter("@SlotPositionMax", 29)
                                });

                            int found;
                            // 16 bytes: armor model
                            for (int k = 0x15; k < 0x1D; k++)
                            {
                                found = 0;
                                foreach (InventoryItem item in items)
                                {
                                    if (item.SlotPosition == k && found == 0)
                                    {
                                        pak.WriteShortLowEndian((ushort)item.Model);
                                        found = 1;
                                    }
                                }

                                if (found == 0)
                                {
                                    pak.WriteShort(0x00);
                                }
                            }

                            // 16 bytes: armor color
                            for (int k = 0x15; k < 0x1D; k++)
                            {
                                int l;
                                if (k == 0x15 + 3)
                                    {
                                    // shield emblem
                                    l = (int)eInventorySlot.LeftHandWeapon;
                                }
                                else
                                {
                                    l = k;
                                }

                                found = 0;
                                foreach (InventoryItem item in items)
                                {
                                    if (item.SlotPosition == l && found == 0)
                                    {
                                        if (item.Emblem != 0)
                                        {
                                            pak.WriteShortLowEndian((ushort)item.Emblem);
                                        }
                                        else
                                        {
                                            pak.WriteShortLowEndian((ushort)item.Color);
                                        }

                                        found = 1;
                                    }
                                }

                                if (found == 0)
                                {
                                    pak.WriteShort(0x00);
                                }
                            }

                            // 8 bytes: weapon model
                            for (int k = 0x0A; k < 0x0E; k++)
                            {
                                found = 0;
                                foreach (InventoryItem item in items)
                                {
                                    if (item.SlotPosition == k && found == 0)
                                    {
                                        pak.WriteShortLowEndian((ushort)item.Model);
                                        found = 1;
                                    }
                                }

                                if (found == 0)
                                {
                                    pak.WriteShort(0x00);
                                }
                            }

                            if (characters[j].ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.TwoHanded)
                            {
                                pak.WriteByte(0x02);
                                pak.WriteByte(0x02);
                            }
                            else if (characters[j].ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.Distance)
                            {
                                pak.WriteByte(0x03);
                                pak.WriteByte(0x03);
                            }
                            else
                            {
                                byte righthand = 0xFF;
                                byte lefthand = 0xFF;
                                foreach (InventoryItem item in items)
                                {
                                    if (item.SlotPosition == (int)eInventorySlot.RightHandWeapon)
                                    {
                                        righthand = 0x00;
                                    }

                                    if (item.SlotPosition == (int)eInventorySlot.LeftHandWeapon)
                                    {
                                        lefthand = 0x01;
                                    }
                                }

                                if (righthand == lefthand)
                                {
                                    if (characters[j].ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.TwoHanded)
                                    {
                                        righthand = lefthand = 0x02;
                                    }
                                    else if (characters[j].ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.Distance)
                                    {
                                        righthand = lefthand = 0x03;
                                    }
                                }

                                pak.WriteByte(righthand);
                                pak.WriteByte(lefthand);
                            }

                            if (reg == null || reg.Expansion != 1)
                            {
                                pak.WriteByte(0x00);
                            }
                            else
                            {
                                pak.WriteByte(0x01); // 0x01=char in ShroudedIsles zone, classic client can't "play"
                            }

                            // pak.WriteByte(0x00);
                            pak.WriteByte((byte)characters[j].Constitution);

                            // pak.Fill(0x00,2);
                            written = true;
                        }
                        }

                        if (written == false)
                        {
                            pak.Fill(0x0, 184);
                        }
                    }
                }

                pak.Fill(0x0, 0x68); // Don't know why so many trailing 0's | Corillian: Cuz they're stupid like that ;)

                SendTCP(pak);
            }
        }

        public virtual void SendDupNameCheckReply(string name, bool nameExists)
        {
            if (GameClient?.Account == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DupNameCheckReply)))
            {
                pak.FillString(name, 30);
                pak.FillString(GameClient.Account.Name, 20);
                pak.WriteByte((byte)(nameExists ? 0x1 : 0x0));
                pak.Fill(0x0, 3);
                SendTCP(pak);
            }
        }

        public virtual void SendBadNameCheckReply(string name, bool bad)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.BadNameCheckReply)))
            {
                pak.FillString(name, 30);
                pak.FillString(GameClient.Account.Name, 20);
                pak.WriteByte((byte)(bad ? 0x0 : 0x1));
                pak.Fill(0x0, 3);
                SendTCP(pak);
            }
        }

        public virtual void SendAttackMode(bool attackState)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.AttackMode)))
            {
                pak.WriteByte((byte)(attackState ? 0x01 : 0x00));
                pak.Fill(0x00, 3);

                SendTCP(pak);
            }
        }

        public virtual void SendCharCreateReply(string name)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterCreateReply)))
            {
                pak.FillString(name, 24);
                SendTCP(pak);
            }
        }

        public virtual void SendCharStatsUpdate()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.StatsUpdate), 36))
            {
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.STR));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.DEX));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.CON));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.QUI));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.INT));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.PIE));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.EMP));
                pak.WriteShort((ushort)GameClient.Player.GetBaseStat(eStat.CHR));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Strength) - GameClient.Player.GetBaseStat(eStat.STR)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Dexterity) - GameClient.Player.GetBaseStat(eStat.DEX)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Constitution) - GameClient.Player.GetBaseStat(eStat.CON)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Quickness) - GameClient.Player.GetBaseStat(eStat.QUI)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Intelligence) - GameClient.Player.GetBaseStat(eStat.INT)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Piety) - GameClient.Player.GetBaseStat(eStat.PIE)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Empathy) - GameClient.Player.GetBaseStat(eStat.EMP)));
                pak.WriteShort((ushort)(GameClient.Player.GetModified(eProperty.Charisma) - GameClient.Player.GetBaseStat(eStat.CHR)));
                pak.WriteShort((ushort)GameClient.Player.MaxHealth);
                pak.WriteByte(0x24); // TODO Unknown
                pak.WriteByte(0x25); // TODO Unknown

                SendTCP(pak);
            }
        }

        public virtual void SendCharResistsUpdate()
        {
        }

        public virtual void SendRegions()
        {
            RegionEntry[] entries = WorldMgr.GetRegionList();

            if (entries == null)
            {
                return;
            }

            int index = 0;
            int num = 0;
            int count = entries.Length;
            while (count > index)
            {
                using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ClientRegions)))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        while (index < count &&
                               (entries[index].id > byte.MaxValue || (int)GameClient.ClientType <= entries[index].expansion))
                        {
                            // skip high ID regions added with catacombs
                            index++;
                        }

                        if (index >= count)
                        {
                            // If we have no more entries
                            pak.Fill(0x0, 52);
                        }
                        else
                        {
                            pak.WriteByte((byte)++num);
                            pak.WriteByte((byte)entries[index].id);
                            pak.FillString(entries[index].name, 20);
                            pak.FillString(entries[index].fromPort, 5);
                            pak.FillString(entries[index].toPort, 5);

                            // Try to fix the region ip so UDP is enabled!
                            string ip = entries[index].ip;
                            if (ip == "any" || ip == "0.0.0.0" || ip == "127.0.0.1" || ip.StartsWith("10.13.") || ip.StartsWith("192.168."))
                            {
                                ip = ((IPEndPoint)GameClient.Socket.LocalEndPoint).Address.ToString();
                            }

                            pak.FillString(ip, 20);
                            index++;
                        }
                    }

                    SendTCP(pak);
                }
            }
        }

        public virtual void SendGameOpenReply()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.GameOpenReply)))
            {
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerPositionAndObjectID()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PositionAndObjectID)))
            {
                pak.WriteShort((ushort)GameClient.Player.ObjectID); // This is the player's objectid not Sessionid!!!
                pak.WriteShort((ushort)GameClient.Player.Z);
                pak.WriteInt((uint)GameClient.Player.X);
                pak.WriteInt((uint)GameClient.Player.Y);
                pak.WriteShort(GameClient.Player.Heading);

                int flags = 0;
                if (GameClient.Player.CurrentZone.IsDivingEnabled)
                {
                    flags = 0x80 | (GameClient.Player.IsUnderwater ? 0x01 : 0x00);
                }

                pak.WriteByte((byte)flags);

                pak.WriteByte(0x00); // TODO Unknown
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerJump(bool headingOnly)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterJump)))
            {
                pak.WriteInt((uint)(headingOnly ? 0 : GameClient.Player.X));
                pak.WriteInt((uint)(headingOnly ? 0 : GameClient.Player.Y));
                pak.WriteShort((ushort)GameClient.Player.ObjectID);
                pak.WriteShort((ushort)(headingOnly ? 0 : GameClient.Player.Z));
                pak.WriteShort(GameClient.Player.Heading);
                if (GameClient.Player.InHouse == false || GameClient.Player.CurrentHouse == null)
                {
                    pak.WriteShort(0);
                }
                else
                {
                    pak.WriteShort((ushort)GameClient.Player.CurrentHouse.HouseNumber);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendPlayerInitFinished(byte mobs)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterInitFinished)))
            {
                pak.WriteByte(mobs);
                SendTCP(pak);
            }
        }

        public virtual void SendUDPInitReply()
        {
            using (var pak = new GSUDPPacketOut(GetPacketCode(eServerPackets.UDPInitReply)))
            {
                Region playerRegion = null;
                if (!GameClient.Socket.Connected)
                {
                    return;
                }

                if (GameClient.Player?.CurrentRegion != null)
                {
                    playerRegion = GameClient.Player.CurrentRegion;
                }

                if (playerRegion == null)
                {
                    pak.Fill(0x0, 0x18);
                }
                else
                {
                    // Try to fix the region ip so UDP is enabled!
                    string ip = playerRegion.ServerIP;
                    if (ip == "any" || ip == "0.0.0.0" || ip == "127.0.0.1" || ip.StartsWith("10.13.") || ip.StartsWith("192.168."))
                    {
                        ip = ((IPEndPoint)GameClient.Socket.LocalEndPoint).Address.ToString();
                    }

                    pak.FillString(ip, 22);
                    pak.WriteShort(playerRegion.ServerPort);
                }

                SendUDP(pak, true);
            }
        }

        public virtual void SendTime()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Time)))
            {
                if (GameClient?.Player != null)
                {
                    pak.WriteInt(WorldMgr.GetCurrentGameTime(GameClient.Player));
                    pak.WriteInt(WorldMgr.GetDayIncrement(GameClient.Player));
                }
                else
                {
                    pak.WriteInt(WorldMgr.GetCurrentGameTime());
                    pak.WriteInt(WorldMgr.GetDayIncrement());
                }

                SendTCP(pak);
            }
        }

        public virtual void SendMessage(string msg, eChatType type, eChatLoc loc)
        {
            if (GameClient.ClientState == GameClient.eClientState.CharScreen)
            {
                return;
            }

            // types not supported by 1.68+ clients
            switch (type)
            {
                case eChatType.CT_ScreenCenterSmaller:
                case eChatType.CT_ScreenCenter:
                    return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Message)))
            {
                pak.WriteShort((ushort)GameClient.SessionID);
                pak.WriteShort(0x00);
                pak.WriteByte((byte)type);
                pak.Fill(0x0, 3);

                string str;
                if (loc == eChatLoc.CL_ChatWindow)
                {
                    str = "@@";
                }
                else if (loc == eChatLoc.CL_PopupWindow)
                {
                    str = "##";
                }
                else
                {
                    str = string.Empty;
                }

                str = string.Concat(str, msg);
                pak.WriteString(str);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerCreate(GamePlayer playerToCreate)
        {
            if (playerToCreate == null)
            {
                return;
            }

            Region playerRegion = playerToCreate.CurrentRegion;
            if (playerRegion == null)
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn("SendPlayerCreate: playerRegion == null");
                }

                return;
            }

            Zone playerZone = playerToCreate.CurrentZone;
            if (playerZone == null)
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn("SendPlayerCreate: playerZone == null");
                }

                return;
            }

            if (playerToCreate.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerCreate)))
            {
                pak.WriteShort((ushort)playerToCreate.Client.SessionID);
                pak.WriteShort((ushort)playerToCreate.ObjectID);
                pak.WriteShort((ushort)playerRegion.GetXOffInZone(playerToCreate.X, playerToCreate.Y));
                pak.WriteShort((ushort)playerRegion.GetYOffInZone(playerToCreate.X, playerToCreate.Y));

                // Dinberg:Instances - changing to ZoneSkinID for instance zones.
                pak.WriteByte((byte)playerZone.ZoneSkinID);
                pak.WriteByte(0);
                pak.WriteShort((ushort)playerToCreate.Z);
                pak.WriteShort(playerToCreate.Heading);
                pak.WriteShort(playerToCreate.Model);
                pak.WriteByte((byte)(playerToCreate.IsAlive ? 0x1 : 0x0));
                pak.WriteByte(0x00);
                pak.WriteByte(GameServer.ServerRules.GetLivingRealm(GameClient.Player, playerToCreate));
                pak.WriteByte(playerToCreate.GetDisplayLevel(GameClient.Player));
                pak.WriteByte((byte)(playerToCreate.IsStealthed ? 0x01 : 0x00));
                pak.WriteByte(0x00); // Unused (??)
                pak.WritePascalString(GameServer.ServerRules.GetPlayerName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerGuildName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerLastName(GameClient.Player, playerToCreate));
                pak.WriteByte(0x00); // Trialing 0 ... needed!
                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(playerToCreate.CurrentRegionID, (ushort)playerToCreate.ObjectID)] = GameTimer.GetTickCount();
            SendObjectGuildID(playerToCreate, playerToCreate.Guild);

            // used for nearest friendly/enemy object buttons and name colors on PvP server
        }

        public virtual void SendObjectGuildID(GameObject obj, Guild guild)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ObjectGuildID)))
            {
                pak.WriteShort((ushort)obj.ObjectID);
                if (guild == null)
                {
                    pak.WriteInt(0x00);
                }
                else
                {
                    pak.WriteShort(guild.ID);
                    pak.WriteShort(guild.ID);
                }

                pak.WriteShort(0x00); // seems random, not used by the client
                SendTCP(pak);
            }
        }

        public virtual void SendObjectUpdate(GameObject obj)
        {
            Zone z = obj.CurrentZone;

            if (z == null || GameClient.Player == null || GameClient.Player.IsVisibleTo(obj) == false)
            {
                return;
            }

            var xOffsetInZone = (ushort)(obj.X - z.XOffset);
            var yOffsetInZone = (ushort)(obj.Y - z.YOffset);
            ushort xOffsetInTargetZone = 0;
            ushort yOffsetInTargetZone = 0;
            ushort zOffsetInTargetZone = 0;

            int speed = 0;
            ushort targetZone = 0;
            byte flags = 0;
            int targetOid = 0;
            if (obj is GameNPC npc)
            {
                flags = (byte)(GameServer.ServerRules.GetLivingRealm(GameClient.Player, npc) << 6);

                if (GameClient.Account.PrivLevel < 2)
                {
                    // no name only if normal player
                    if ((npc.Flags & GameNPC.eFlags.CANTTARGET) != 0)
                    {
                        flags |= 0x01;
                    }

                    if ((npc.Flags & GameNPC.eFlags.DONTSHOWNAME) != 0)
                    {
                        flags |= 0x02;
                    }
                }

                if ((npc.Flags & GameNPC.eFlags.STATUE) != 0)
                {
                    flags |= 0x01;
                }

                if (npc.IsUnderwater)
                {
                    flags |= 0x10;
                }

                if ((npc.Flags & GameNPC.eFlags.FLYING) != 0)
                {
                    flags |= 0x20;
                }

                if (npc.IsMoving && !npc.IsAtTargetPosition)
                {
                    speed = npc.CurrentSpeed;
                    if (npc.TargetPosition.X != 0 || npc.TargetPosition.Y != 0 || npc.TargetPosition.Z != 0)
                    {
                        Zone tz = npc.CurrentRegion.GetZone(npc.TargetPosition.X, npc.TargetPosition.Y);
                        if (tz != null)
                        {
                            xOffsetInTargetZone = (ushort)(npc.TargetPosition.X - tz.XOffset);
                            yOffsetInTargetZone = (ushort)(npc.TargetPosition.Y - tz.YOffset);
                            zOffsetInTargetZone = (ushort)npc.TargetPosition.Z;

                            // Dinberg:Instances - zoneSkinID for object positioning clientside.
                            targetZone = tz.ZoneSkinID;
                        }
                    }

                    if (speed > 0x07FF)
                    {
                        speed = 0x07FF;
                    }
                    else if (speed < 0)
                    {
                        speed = 0;
                    }
                }

                GameObject target = npc.TargetObject;
                if (npc.AttackState && target != null && target.ObjectState == GameObject.eObjectState.Active && !npc.IsTurningDisabled)
                {
                    targetOid = (ushort)target.ObjectID;
                }
            }

            using (GSUDPPacketOut pak = new GSUDPPacketOut(GetPacketCode(eServerPackets.ObjectUpdate)))
            {
                pak.WriteShort((ushort)speed);

                if (obj is GameNPC)
                {
                    pak.WriteShort((ushort)(obj.Heading & 0xFFF));
                }
                else
                {
                    pak.WriteShort(obj.Heading);
                }

                pak.WriteShort(xOffsetInZone);
                pak.WriteShort(xOffsetInTargetZone);
                pak.WriteShort(yOffsetInZone);
                pak.WriteShort(yOffsetInTargetZone);
                pak.WriteShort((ushort)obj.Z);
                pak.WriteShort(zOffsetInTargetZone);
                pak.WriteShort((ushort)obj.ObjectID);
                pak.WriteShort((ushort)targetOid);

                // health
                if (obj is GameLiving living)
                {
                    pak.WriteByte(living.HealthPercent);
                }
                else
                {
                    pak.WriteByte(0);
                }

                // Dinberg:Instances - zoneskinID for positioning of objects clientside.
                flags |= (byte)(((z.ZoneSkinID & 0x100) >> 6) | ((targetZone & 0x100) >> 5));
                pak.WriteByte(flags);
                pak.WriteByte((byte)z.ZoneSkinID);

                // Dinberg:Instances - targetZone already accomodates for this feat.
                pak.WriteByte((byte)targetZone);
                SendUDP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID)] = GameTimer.GetTickCount();

            (obj as GameNPC)?.NPCUpdatedCallback();
        }

        public virtual void SendPlayerQuit(bool totalOut)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Quit)))
            {
                pak.WriteByte((byte)(totalOut ? 0x01 : 0x00));
                pak.WriteByte(GameClient.Player?.Level ?? 0);

                SendTCP(pak);
            }
        }

        public virtual void SendObjectRemove(GameObject obj)
        {
            // Remove from cache
            if (GameClient.GameObjectUpdateArray.ContainsKey(new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID)))
            {
                long dummy;
                GameClient.GameObjectUpdateArray.TryRemove(new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID), out dummy);
            }

            int oType = 0;
            if (obj is GamePlayer)
            {
                oType = 2;
            }
            else if (obj is GameNPC)
            {
                oType = ((GameLiving)obj).IsAlive ? 1 : 0;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.RemoveObject)))
            {
                pak.WriteShort((ushort)obj.ObjectID);
                pak.WriteShort((ushort)oType);
                SendTCP(pak);
            }
        }

        public virtual void SendObjectCreate(GameObject obj)
        {
            if (obj == null)
            {
                return;
            }

            if (obj.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ObjectCreate)))
            {
                pak.WriteShort((ushort)obj.ObjectID);
                if (obj is GameStaticItem item)
                {
                    pak.WriteShort((ushort)item.Emblem);
                }
                else
                {
                    pak.WriteShort(0);
                }

                pak.WriteShort(obj.Heading);
                pak.WriteShort((ushort)obj.Z);
                pak.WriteInt((uint)obj.X);
                pak.WriteInt((uint)obj.Y);
                int flag = ((byte)obj.Realm & 3) << 4;
                ushort model = obj.Model;
                if (obj.IsUnderwater)
                {
                    if (obj is GameNPC)
                    {
                        model |= 0x8000;
                    }
                    else
                    {
                        flag |= 0x01; // Underwater
                    }
                }

                pak.WriteShort(model);
                if (obj is GameKeepBanner)
                {
                    flag |= 0x08;
                }

                if (obj is GameStaticItemTimed && GameClient.Player != null &&
                    (obj as GameStaticItemTimed).IsOwner(GameClient.Player))
                {
                    flag |= 0x04;
                }

                pak.WriteShort((ushort)flag);

                string name = obj.Name;
                if (obj is GameStaticItem staticItem)
                {
                    var translation = GameClient.GetTranslation(staticItem);
                    if (translation != null)
                    {
                        if (staticItem is WorldInventoryItem)
                        {
                            // if (!Util.IsEmpty(((DBLanguageItem)translation).Name))
                            //    name = ((DBLanguageItem)translation).Name;
                        }
                        else
                        {
                            if (!Util.IsEmpty(((DBLanguageGameObject)translation).Name))
                            {
                                name = ((DBLanguageGameObject)translation).Name;
                            }
                        }
                    }
                }

                pak.WritePascalString(name);

                if (obj is IDoor)
                {
                    pak.WriteByte(4);
                    pak.WriteInt((uint)(obj as IDoor).DoorID);
                }
                else
                {
                    pak.WriteByte(0x00);
                }

                SendTCP(pak);
            }

            // Update Object Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID)] = GameTimer.GetTickCount();
        }

        public virtual void SendDebugMode(bool on)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DebugMode)))
            {
                if (GameClient.Account.PrivLevel == 1)
                {
                    pak.WriteByte(0x00);
                }
                else
                {
                    pak.WriteByte((byte)(on ? 0x01 : 0x00));
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public void SendModelChange(GameObject obj, ushort newModel)
        {
            if (obj is GameNPC npc)
            {
                SendModelAndSizeChange(obj, newModel, npc.Size);
            }
            else
            {
                SendModelAndSizeChange(obj, newModel, 0);
            }
        }

        public void SendModelAndSizeChange(GameObject obj, ushort newModel, byte newSize)
        {
            SendModelAndSizeChange((ushort)obj.ObjectID, newModel, newSize);
        }

        public virtual void SendModelAndSizeChange(ushort objectId, ushort newModel, byte newSize)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ModelChange)))
            {
                pak.WriteShort(objectId);
                pak.WriteShort(newModel);
                pak.WriteIntLowEndian(newSize);
                SendTCP(pak);
            }
        }

        public virtual void SendEmoteAnimation(GameObject obj, eEmote emote)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.EmoteAnimation)))
            {
                pak.WriteShort((ushort)obj.ObjectID);
                pak.WriteByte((byte)emote);
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendNPCCreate(GameNPC npc)
        {
            if (GameClient.Player == null || npc.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            if (npc is GameMovingObject o)
            {
                SendMovingObjectCreate(o);
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.NPCCreate)))
            {
                int speed = 0;
                ushort speedZ = 0;
                if (npc.IsMoving && !npc.IsAtTargetPosition)
                {
                    speed = npc.CurrentSpeed;
                    speedZ = (ushort)npc.TickSpeedZ;
                }

                pak.WriteShort((ushort)npc.ObjectID);
                pak.WriteShort((ushort)speed);
                pak.WriteShort(npc.Heading);
                pak.WriteShort((ushort)npc.Z);
                pak.WriteInt((uint)npc.X);
                pak.WriteInt((uint)npc.Y);
                pak.WriteShort(speedZ);
                pak.WriteShort(npc.Model);
                pak.WriteByte(npc.Size);
                pak.WriteByte(npc.GetDisplayLevel(GameClient.Player));

                var flags = (byte)(GameServer.ServerRules.GetLivingRealm(GameClient.Player, npc) << 6);
                if ((npc.Flags & GameNPC.eFlags.GHOST) != 0)
                {
                    flags |= 0x01;
                }

                if (npc.Inventory != null)
                {
                    flags |= 0x02; // If mob has equipment, then only show it after the client gets the 0xBD packet
                }

                if ((npc.Flags & GameNPC.eFlags.PEACE) != 0)
                {
                    flags |= 0x10;
                }

                if ((npc.Flags & GameNPC.eFlags.FLYING) != 0)
                {
                    flags |= 0x20;
                }

                pak.WriteByte(flags);
                pak.WriteByte(0x20); // TODO this is the default maxstick distance

                string add = string.Empty;
                if (GameClient.Account.PrivLevel > 1)
                {
                    if ((npc.Flags & GameNPC.eFlags.CANTTARGET) != 0)
                    {
                        add += "-DOR"; // indicates DOR flag for GMs
                    }

                    if ((npc.Flags & GameNPC.eFlags.DONTSHOWNAME) != 0)
                    {
                        add += "-NON"; // indicates NON flag for GMs
                    }
                }

                string name = npc.Name;
                string guildName = npc.GuildName;

                LanguageDataObject translation = GameClient.GetTranslation(npc);
                if (translation != null)
                {
                    if (!Util.IsEmpty(((DBLanguageNPC)translation).Name))
                    {
                        name = ((DBLanguageNPC)translation).Name;
                    }

                    if (!Util.IsEmpty(((DBLanguageNPC)translation).GuildName))
                    {
                        guildName = ((DBLanguageNPC)translation).GuildName;
                    }
                }

                if (name.Length + add.Length + 2 > 47) // clients crash with too long names
                {
                    name = name.Substring(0, 47 - add.Length - 2);
                }

                if (add.Length > 0)
                {
                    name = $"[{name}]{add}";
                }

                pak.WritePascalString(name);

                if (guildName.Length > 47)
                {
                    guildName = guildName.Substring(0, 47);
                }

                pak.WritePascalString(guildName);

                pak.WriteByte(0x00);
                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(npc.CurrentRegionID, (ushort)npc.ObjectID)] = 0;
        }

        public virtual void SendLivingEquipmentUpdate(GameLiving living)
        {
            if (GameClient.Player == null || living.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.EquipmentUpdate)))
            {
                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteByte((byte)((living.IsCloakHoodUp ? 0x01 : 0x00) | (int)living.ActiveQuiverSlot));

                // bit0 is hood up bit4 to 7 is active quiver
                pak.WriteByte(living.VisibleActiveWeaponSlots);

                if (living.Inventory != null)
                {
                    var items = living.Inventory.VisibleItems;
                    pak.WriteByte((byte)items.Count);
                    foreach (InventoryItem item in items)
                    {
                        pak.WriteByte((byte)item.SlotPosition);
                        var model = (ushort)(item.Model & 0x1FFF);
                        int texture = (item.Emblem != 0) ? item.Emblem : item.Color;

                        if ((texture & ~0xFF) != 0)
                        {
                            model |= 0x8000;
                        }
                        else if ((texture & 0xFF) != 0)
                        {
                            model |= 0x4000;
                        }

                        if (item.Effect != 0)
                        {
                            model |= 0x2000;
                        }

                        pak.WriteShort(model);

                        if ((texture & ~0xFF) != 0)
                        {
                            pak.WriteShort((ushort)texture);
                        }
                        else if ((texture & 0xFF) != 0)
                        {
                            pak.WriteByte((byte)texture);
                        }

                        if (item.Effect != 0)
                        {
                            pak.WriteShort((ushort)item.Effect);
                        }
                    }
                }
                else
                {
                    pak.WriteByte(0x00);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendRegionChanged()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.RegionChanged)))
            {
                // Dinberg - Changing to allow instances...
                pak.WriteShort(GameClient.Player.CurrentRegion.Skin);
                pak.WriteShort(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendUpdatePoints()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterPointsUpdate)))
            {
                pak.WriteInt((uint)GameClient.Player.RealmPoints);
                pak.WriteShort(GameClient.Player.LevelPermill);
                pak.WriteShort((ushort)GameClient.Player.SkillSpecialtyPoints);
                pak.WriteInt((uint)GameClient.Player.BountyPoints);
                pak.WriteShort((ushort)GameClient.Player.RealmSpecialtyPoints);
                pak.WriteShort(0); // unknown
                SendTCP(pak);
            }
        }

        public virtual void SendUpdateMoney()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MoneyUpdate)))
            {
                pak.WriteByte((byte)GameClient.Player.Copper);
                pak.WriteByte((byte)GameClient.Player.Silver);
                pak.WriteShort((ushort)GameClient.Player.Gold);
                pak.WriteShort((ushort)GameClient.Player.Mithril);
                pak.WriteShort((ushort)GameClient.Player.Platinum);
                SendTCP(pak);
            }
        }

        public virtual void SendUpdateMaxSpeed()
        {
            // Speed is in % not a fixed value!
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MaxSpeed)))
            {
                pak.WriteShort((ushort)(GameClient.Player.MaxSpeed * 100 / GamePlayer.PLAYER_BASE_SPEED));
                pak.WriteByte((byte)(GameClient.Player.IsTurningDisabled ? 0x01 : 0x00));

                // water speed in % of land speed if its over 0 i think
                pak.WriteByte((byte)Math.Min(byte.MaxValue, (GameClient.Player.MaxSpeed * 100 / GamePlayer.PLAYER_BASE_SPEED) * (GameClient.Player.GetModified(eProperty.WaterSpeed) * .01)));
                SendTCP(pak);
            }
        }

        public virtual void SendCombatAnimation(GameObject attacker, GameObject defender, ushort weaponId, ushort shieldId, int style, byte stance, byte result, byte targetHealthPercent)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CombatAnimation)))
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
                pak.WriteByte((byte)style);
                pak.WriteByte(stance);

                if (style > 0xFF)
                {
                    pak.WriteByte((byte)(result | 0x80));
                }
                else
                {
                    pak.WriteByte(result);
                }

                // If Health Percent is invalid get the living Health.
                if (defender is GameLiving living && targetHealthPercent > 100)
                {
                    targetHealthPercent = living.HealthPercent;
                }

                pak.WriteByte(targetHealthPercent);
                SendTCP(pak);
            }
        }

        public virtual void SendStatusUpdate()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            SendStatusUpdate((byte)(GameClient.Player.IsSitting ? 0x02 : 0x00));
        }

        public virtual void SendStatusUpdate(byte sittingFlag)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterStatusUpdate)))
            {
                pak.WriteByte(GameClient.Player.HealthPercent);
                pak.WriteByte(GameClient.Player.ManaPercent);
                pak.WriteShort((byte)(GameClient.Player.IsAlive ? 0x00 : 0x0f)); // 0x0F if dead
                pak.WriteByte(sittingFlag);
                pak.WriteByte(GameClient.Player.EndurancePercent);
                pak.WriteByte(GameClient.Player.ConcentrationPercent);
                pak.WriteByte(0);
                SendTCP(pak);
            }
        }

        public virtual void SendSpellCastAnimation(GameLiving spellCaster, ushort spellId, ushort castingTime)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SpellCastAnimation)))
            {
                pak.WriteShort((ushort)spellCaster.ObjectID);
                pak.WriteShort(spellId);
                pak.WriteShort(castingTime);
                pak.WriteShort(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendSpellEffectAnimation(GameObject spellCaster, GameObject spellTarget, ushort spellid,
                                                     ushort boltTime, bool noSound, byte success)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SpellEffectAnimation)))
            {
                pak.WriteShort((ushort)spellCaster.ObjectID);
                pak.WriteShort(spellid);
                pak.WriteShort((ushort)(spellTarget?.ObjectID ?? 0));
                pak.WriteShort(boltTime);
                pak.WriteByte((byte)(noSound ? 1 : 0));
                pak.WriteByte(success);
                pak.WriteShort(0xFFBF);
                SendTCP(pak);
            }
        }

        public virtual void SendRiding(GameObject rider, GameObject steed, bool dismount)
        {
            int slot = 0;
            if (steed is GameNPC npc && rider is GamePlayer && dismount == false)
            {
                slot = npc.RiderSlot((GamePlayer) rider);
            }

            if (slot == -1)
            {
                Log.Error($"SendRiding error, slot is -1 with rider {rider.Name} steed {steed.Name}");
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Riding)))
            {
                pak.WriteShort((ushort)rider.ObjectID);
                pak.WriteShort((ushort)steed.ObjectID);
                pak.WriteByte((byte)(dismount ? 0x00 : 0x01));
                pak.WriteByte((byte)slot);
                pak.WriteShort(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendFindGroupWindowUpdate(GamePlayer[] list)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.FindGroupUpdate)))
            {
                if (list != null)
                {
                    pak.WriteByte((byte)list.Length);
                    byte nbleader = 0;
                    byte nbsolo = 0x1E;
                    foreach (GamePlayer player in list)
                    {
                        pak.WriteByte(player.Group != null ? nbleader++ : nbsolo++);

                        pak.WriteByte(player.Level);
                        pak.WritePascalString(player.Name);
                        pak.WriteString(player.CharacterClass.Name, 4);

                        // Dinberg:Instances - have to write zoneskinID, it uses this to display the text 'x is in y'.
                        if (player.CurrentZone != null)
                        {
                            pak.WriteByte((byte)player.CurrentZone.ZoneSkinID);
                        }
                        else
                        {
                            pak.WriteByte(255);
                        }
                    }
                }
                else
                {
                    pak.WriteShort(0x0000);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendGroupInviteCommand(GamePlayer invitingPlayer, string inviteMessage)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte(0x05);
                pak.WriteShort((ushort)invitingPlayer.Client.SessionID); // data1
                pak.Fill(0x00, 6); // data2&data3
                pak.WriteByte(0x01);
                pak.WriteByte(0x00);
                if (inviteMessage.Length > 0)
                {
                    pak.WriteString(inviteMessage, inviteMessage.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendGuildInviteCommand(GamePlayer invitingPlayer, string inviteMessage)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte(0x03);
                pak.WriteShort((ushort)invitingPlayer.ObjectID); // data1
                pak.Fill(0x00, 6); // data2&data3
                pak.WriteByte(0x01);
                pak.WriteByte(0x00);
                if (inviteMessage.Length > 0)
                {
                    pak.WriteString(inviteMessage, inviteMessage.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendGuildLeaveCommand(GamePlayer invitingPlayer, string inviteMessage)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte(0x08);
                pak.WriteShort((ushort)invitingPlayer.ObjectID); // data1
                pak.Fill(0x00, 6); // data2&data3
                pak.WriteByte(0x01);
                pak.WriteByte(0x00);
                if (inviteMessage.Length > 0)
                {
                    pak.WriteString(inviteMessage, inviteMessage.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendQuestOfferWindow(GameNPC questNpc, GamePlayer player, RewardQuest quest)
        {
        }

        public virtual void SendQuestRewardWindow(GameNPC questNpc, GamePlayer player, RewardQuest quest)
        {
        }

        public virtual void SendQuestOfferWindow(GameNPC questNpc, GamePlayer player, DataQuest quest)
        {
        }

        public virtual void SendQuestRewardWindow(GameNPC questNpc, GamePlayer player, DataQuest quest)
        {
        }

        protected virtual void SendQuestWindow(GameNPC questNpc, GamePlayer player, RewardQuest quest, bool offer)
        {
        }

        protected virtual void SendQuestWindow(GameNPC questNpc, GamePlayer player, DataQuest quest, bool offer)
        {
        }

        // i'm reusing the questsubscribe command for quest abort since its 99% the same, only different event dets fired
        // data 3 defines wether it's subscribe or abort
        public virtual void SendQuestSubscribeCommand(GameNPC invitingNpc, ushort questid, string inviteMessage)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte(0x64);
                pak.WriteShort(questid); // questid, data1
                pak.WriteShort((ushort)invitingNpc.ObjectID); // data2
                pak.WriteShort(0x00); // 0x00 means subscribe data3
                pak.WriteShort(0x00);
                pak.WriteByte(0x01); // yes/no response
                pak.WriteByte(0x01); // autowrap message
                if (inviteMessage.Length > 0)
                {
                    pak.WriteString(inviteMessage, inviteMessage.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        // i'm reusing the questsubscribe command for quest abort since its 99% the same, only different event dets fired
        // data 3 defines wether it's subscribe or abort
        public virtual void SendQuestAbortCommand(GameNPC abortingNpc, ushort questid, string abortMessage)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte(0x64);
                pak.WriteShort(questid); // questid, data1
                pak.WriteShort((ushort)abortingNpc.ObjectID); // data2
                pak.WriteShort(0x01); // 0x01 means abort data3
                pak.WriteShort(0x00);
                pak.WriteByte(0x01); // yes/no response
                pak.WriteByte(0x01); // autowrap message
                if (abortMessage.Length > 0)
                {
                    pak.WriteString(abortMessage, abortMessage.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendDialogBox(eDialogCode code, ushort data1, ushort data2, ushort data3, ushort data4,
                                          eDialogType type, bool autoWrapText, string message)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte((byte)code);
                pak.WriteShort(data1); // data1
                pak.WriteShort(data2); // data2
                pak.WriteShort(data3); // data3
                pak.WriteShort(data4); // data4
                pak.WriteByte((byte)type);
                pak.WriteByte((byte)(autoWrapText ? 0x01 : 0x00));
                if (message.Length > 0)
                {
                    pak.WriteString(message, message.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendCustomDialog(string msg, CustomDialogResponse callback)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            lock (GameClient.Player)
            {
                GameClient.Player.CustomDialogCallback?.Invoke(GameClient.Player, 0x00);

                GameClient.Player.CustomDialogCallback = callback;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte((byte)eDialogCode.CustomDialog);
                pak.WriteShort((ushort)GameClient.SessionID); // data1
                pak.WriteShort(0x01); // custom dialog!    //data2
                pak.WriteShort(0x00); // data3
                pak.WriteShort(0x00);
                pak.WriteByte((byte)(callback == null ? 0x00 : 0x01)); // ok or yes/no response
                pak.WriteByte(0x01); // autowrap text
                if (msg.Length > 0)
                {
                    pak.WriteString(msg, msg.Length);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        [Obsolete("Shouldn't be used in favor of new LoS Check Manager")]
        public virtual void SendCheckLOS(GameObject checker, GameObject target, CheckLOSResponse callback)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            int targetOid = target?.ObjectID ?? 0;
            string key = $"LOS C:0x{checker.ObjectID} T:0x{targetOid}";
            CheckLOSResponse oldCallback;
            lock (GameClient.Player.TempProperties)
            {
                oldCallback = (CheckLOSResponse)GameClient.Player.TempProperties.getProperty<object>(key, null);
                GameClient.Player.TempProperties.setProperty(key, callback);
            }

            oldCallback?.Invoke(GameClient.Player, 0, 0); // not sure for this,  i want targetOID there

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CheckLOSRequest)))
            {
                pak.WriteShort((ushort)checker.ObjectID);
                pak.WriteShort((ushort)targetOid);
                pak.WriteShort(0x00); // ?
                pak.WriteShort(0x00); // ?
                SendTCP(pak);
            }
        }

        public virtual void SendCheckLOS(GameObject source, GameObject target, CheckLOSMgrResponse callback)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            int targetOid = target?.ObjectID ?? 0;
            int sourceOid = source?.ObjectID ?? 0;

            string key = $"LOSMGR C:0x{sourceOid} T:0x{targetOid}";

            CheckLOSMgrResponse oldCallback;
            lock (GameClient.Player.TempProperties)
            {
                oldCallback = (CheckLOSMgrResponse)GameClient.Player.TempProperties.getProperty<object>(key, null);
                GameClient.Player.TempProperties.setProperty(key, callback);
            }

            oldCallback?.Invoke(GameClient.Player, 0, 0, 0);

            using (var pak = new GSTCPPacketOut(0xD0))
            {
                pak.WriteShort((ushort)sourceOid);
                pak.WriteShort((ushort)targetOid);
                pak.WriteShort(0x00); // ?
                pak.WriteShort(0x00); // ?
                SendTCP(pak);
            }
        }

        public virtual void SendQuestUpdate(AbstractQuest quest)
        {
            int questIndex = 0;

            lock (GameClient.Player.QuestList)
            {
                foreach (AbstractQuest q in GameClient.Player.QuestList)
                {
                    if (q == quest)
                    {
                        SendQuestPacket(q, questIndex);
                        break;
                    }

                    if (q.Step != -1)
                    {
                        questIndex++;
                    }
                }
            }
        }

        public virtual void SendQuestListUpdate()
        {
            int questIndex = 0;
            lock (GameClient.Player.QuestList)
            {
                foreach (AbstractQuest quest in GameClient.Player.QuestList)
                {
                    if (quest.Step != -1)
                    {
                        SendQuestPacket(quest, questIndex);
                        questIndex++;
                    }
                }
            }
        }

        public virtual void SendGroupWindowUpdate()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x06);

                Group group = GameClient.Player.Group;
                pak.WriteByte(group?.MemberCount ?? 0x00);

                pak.WriteByte(0x01);
                pak.WriteByte(0x00);

                if (group != null)
                {
                    foreach (GameLiving updateLiving in group.GetMembersInTheGroup())
                    {
                        bool sameRegion = updateLiving.CurrentRegion == GameClient.Player.CurrentRegion;

                        pak.WriteByte(updateLiving.Level);
                        if (sameRegion)
                        {
                            pak.WriteByte(updateLiving.HealthPercent);
                            pak.WriteByte(updateLiving.ManaPercent);

                            byte playerStatus = 0;
                            if (!updateLiving.IsAlive)
                            {
                                playerStatus |= 0x01;
                            }

                            if (updateLiving.IsMezzed)
                            {
                                playerStatus |= 0x02;
                            }

                            if (updateLiving.IsDiseased)
                            {
                                playerStatus |= 0x04;
                            }

                            if (updateLiving.FindEffectOnTarget("DamageOverTime") != null)
                            {
                                playerStatus |= 0x08;
                            }

                            if (updateLiving is GamePlayer &&
                                (updateLiving as GamePlayer).Client.ClientState == GameClient.eClientState.Linkdead)
                            {
                                playerStatus |= 0x10;
                            }

                            if (updateLiving.CurrentRegion != GameClient.Player.CurrentRegion)
                            {
                                playerStatus |= 0x20;
                            }

                            pak.WriteByte(playerStatus);

                            // 0x00 = Normal , 0x01 = Dead , 0x02 = Mezzed , 0x04 = Diseased ,
                            // 0x08 = Poisoned , 0x10 = Link Dead , 0x20 = In Another Region
                            pak.WriteShort((ushort)updateLiving.ObjectID); // or session id?
                        }
                        else
                        {
                            pak.WriteInt(0x2000);
                            pak.WriteByte(0);
                        }

                        pak.WritePascalString(updateLiving.Name);
                        pak.WritePascalString(updateLiving is GamePlayer player ? player.CharacterClass.Name : "NPC");

                        // classname
                    }
                }

                SendTCP(pak);
            }
        }

        public void SendGroupMemberUpdate(bool updateIcons, GameLiving living)
        {
            Group group = GameClient.Player?.Group;
            if (group == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.GroupMemberUpdate)))
            {
                lock (group)
                {
                    // make sure group is not modified before update is sent else player index could change _before_ update
                    if (living.Group != group)
                    {
                        return;
                    }

                    WriteGroupMemberUpdate(pak, updateIcons, living);
                    pak.WriteByte(0x00);
                    SendTCP(pak);
                }
            }
        }

        public void SendGroupMembersUpdate(bool updateIcons)
        {
            Group group = GameClient.Player?.Group;
            if (group == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.GroupMemberUpdate)))
            {
                foreach (GameLiving living in group.GetMembersInTheGroup())
                {
                    WriteGroupMemberUpdate(pak, updateIcons, living);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendInventorySlotsUpdate(ICollection<int> slots)
        {
            // slots contain ints
            if (GameClient.Player == null)
            {
                return;
            }

            // clients crash if too long packet is sent
            // so we send big updates in parts
            if (slots == null || slots.Count <= ServerProperties.Properties.MAX_ITEMS_PER_PACKET)
            {
                SendInventorySlotsUpdateRange(slots, 0);
            }
            else
            {
                var updateSlots = new List<int>(ServerProperties.Properties.MAX_ITEMS_PER_PACKET);
                foreach (int slot in slots)
                {
                    updateSlots.Add(slot);
                    if (updateSlots.Count >= ServerProperties.Properties.MAX_ITEMS_PER_PACKET)
                    {
                        SendInventorySlotsUpdateRange(updateSlots, 0);
                        updateSlots.Clear();
                    }
                }

                if (updateSlots.Count > 0)
                {
                    SendInventorySlotsUpdateRange(updateSlots, 0);
                }
            }
        }

        public virtual void SendInventoryItemsUpdate(IDictionary<int, InventoryItem> updateItems, eInventoryWindowType windowType)
        {
        }

        protected virtual void SendInventoryItemsPartialUpdate(IDictionary<int, InventoryItem> items, eInventoryWindowType windowType)
        {
        }

        public virtual void SendInventoryItemsUpdate(ICollection<InventoryItem> itemsToUpdate)
        {
            SendInventoryItemsUpdate(eInventoryWindowType.Update, itemsToUpdate);
        }

        public virtual void SendInventoryItemsUpdate(eInventoryWindowType windowType, ICollection<InventoryItem> itemsToUpdate)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            if (itemsToUpdate == null)
            {
                SendInventorySlotsUpdateRange(null, windowType);
                return;
            }

            // clients crash if too long packet is sent
            // so we send big updates in parts
            var slotsToUpdate = new List<int>(Math.Min(ServerProperties.Properties.MAX_ITEMS_PER_PACKET, itemsToUpdate.Count));
            foreach (InventoryItem item in itemsToUpdate)
            {
                if (item == null)
                {
                    continue;
                }

                slotsToUpdate.Add(item.SlotPosition);
                if (slotsToUpdate.Count >= ServerProperties.Properties.MAX_ITEMS_PER_PACKET)
                {
                    SendInventorySlotsUpdateRange(slotsToUpdate, windowType);
                    slotsToUpdate.Clear();
                    windowType = eInventoryWindowType.Update;
                }
            }

            if (slotsToUpdate.Count > 0)
            {
                SendInventorySlotsUpdateRange(slotsToUpdate, windowType);
            }
        }

        public virtual void SendDoorState(Region region, IDoor door)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DoorState)))
            {
                int doorType = door.DoorID / 100000000;
                uint flag = door.Flag;

                // by default give all unflagged above ground non keep doors a default sound (excluding TrialsOfAtlantis zones)
                if (flag == 0 && doorType != 7 && region != null && region.IsDungeon == false && region.Expansion != (int)eClientExpansion.TrialsOfAtlantis)
                {
                    flag = 1;
                }

                pak.WriteInt((uint)door.DoorID);
                pak.WriteByte((byte)(door.State == eDoorState.Open ? 0x01 : 0x00));
                pak.WriteByte((byte)flag);
                pak.WriteByte(0xFF);
                pak.WriteByte(0x0);
                SendTCP(pak);
            }
        }

        public virtual void SendMerchantWindow(MerchantTradeItems tradeItemsList, eMerchantWindowType windowType)
        {

            if (tradeItemsList != null)
            {
                for (byte page = 0; page < MerchantTradeItems.MAX_PAGES_IN_TRADEWINDOWS; page++)
                {
                    IDictionary itemsInPage = tradeItemsList.GetItemsInPage(page);
                    if (itemsInPage == null || itemsInPage.Count == 0)
                    {
                        continue;
                    }

                    using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MerchantWindow)))
                    {
                        pak.WriteByte((byte)itemsInPage.Count); // Item count on this page
                        pak.WriteByte((byte)windowType);
                        pak.WriteByte(page); // Page number
                        pak.WriteByte(0x00); // Unused

                        for (ushort i = 0; i < MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS; i++)
                        {
                            if (!itemsInPage.Contains((int)i))
                            {
                                continue;
                            }

                            var item = (ItemTemplate)itemsInPage[(int)i];
                            if (item != null)
                            {
                                pak.WriteByte((byte)i); // Item index on page
                                pak.WriteByte((byte)item.Level);

                                // some objects use this for count
                                int value1;
                                int value2;
                                switch (item.Object_Type)
                                {
                                    case (int)eObjectType.Arrow:
                                    case (int)eObjectType.Bolt:
                                    case (int)eObjectType.Poison:
                                    case (int)eObjectType.GenericItem:
                                        {
                                            value1 = item.PackSize;
                                            value2 = value1 * item.Weight;
                                            break;
                                        }

                                    case (int)eObjectType.Thrown:
                                        {
                                            value1 = item.DPS_AF;
                                            value2 = item.PackSize;
                                            break;
                                        }

                                    case (int)eObjectType.Shield:
                                        {
                                            value1 = item.Type_Damage;
                                            value2 = item.Weight;
                                            break;
                                        }

                                    case (int)eObjectType.GardenObject:
                                        {
                                            value1 = 0;
                                            value2 = item.Weight;
                                            break;
                                        }

                                    default:
                                        {
                                            value1 = item.DPS_AF;
                                            value2 = item.Weight;
                                            break;
                                        }
                                }

                                pak.WriteByte((byte)value1);
                                pak.WriteByte((byte)item.SPD_ABS);
                                if (item.Object_Type == (int)eObjectType.GardenObject)
                                {
                                    pak.WriteByte((byte)item.DPS_AF);
                                }
                                else
                                {
                                    pak.WriteByte((byte)(item.Hand << 6));
                                }

                                pak.WriteByte((byte)((item.Type_Damage << 6) | item.Object_Type));

                                // 1 if item cannot be used by your class (greyed out)
                                if (GameClient.Player != null && GameClient.Player.HasAbilityToUseItem(item))
                                {
                                    pak.WriteByte(0x00);
                                }
                                else
                                {
                                    pak.WriteByte(0x01);
                                }

                                pak.WriteShort((ushort)value2);

                                // Item Price
                                pak.WriteInt((uint)item.Price);
                                pak.WriteShort((ushort)item.Model);
                                pak.WritePascalString(item.Name);
                            }
                            else
                            {
                                if (Log.IsErrorEnabled)
                                {
                                    Log.Error($"Merchant item template \'{((MerchantItem) itemsInPage[page * MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS + i]).ItemTemplateID}\' not found, abort!!!");
                                }

                                return;
                            }
                        }

                        SendTCP(pak);
                    }
                }
            }
            else
            {
                using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MerchantWindow)))
                {
                    pak.WriteByte(0); // Item count on this page
                    pak.WriteByte((byte)windowType); // Unknown 0x00
                    pak.WriteByte(0); // Page number
                    pak.WriteByte(0x00); // Unused
                    SendTCP(pak);
                }
            }
        }

        public virtual void SendTradeWindow()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TradeWindow)))
            {
                lock (GameClient.Player.TradeWindow.Sync)
                {
                    foreach (InventoryItem item in GameClient.Player.TradeWindow.TradeItems)
                    {
                        pak.WriteByte((byte)item.SlotPosition);
                    }

                    pak.Fill(0x00, 10 - GameClient.Player.TradeWindow.TradeItems.Count);

                    pak.WriteShort(0x0000);
                    pak.WriteShort((ushort)Money.GetMithril(GameClient.Player.TradeWindow.TradeMoney));
                    pak.WriteShort((ushort)Money.GetPlatinum(GameClient.Player.TradeWindow.TradeMoney));
                    pak.WriteShort((ushort)Money.GetGold(GameClient.Player.TradeWindow.TradeMoney));
                    pak.WriteShort((ushort)Money.GetSilver(GameClient.Player.TradeWindow.TradeMoney));
                    pak.WriteShort((ushort)Money.GetCopper(GameClient.Player.TradeWindow.TradeMoney));

                    pak.WriteShort(0x0000);
                    pak.WriteShort((ushort)Money.GetMithril(GameClient.Player.TradeWindow.PartnerTradeMoney));
                    pak.WriteShort((ushort)Money.GetPlatinum(GameClient.Player.TradeWindow.PartnerTradeMoney));
                    pak.WriteShort((ushort)Money.GetGold(GameClient.Player.TradeWindow.PartnerTradeMoney));
                    pak.WriteShort((ushort)Money.GetSilver(GameClient.Player.TradeWindow.PartnerTradeMoney));
                    pak.WriteShort((ushort)Money.GetCopper(GameClient.Player.TradeWindow.PartnerTradeMoney));

                    pak.WriteShort(0x0000);
                    ArrayList items = GameClient.Player.TradeWindow.PartnerTradeItems;
                    if (items != null)
                    {
                        pak.WriteByte((byte)items.Count);
                        pak.WriteByte(0x01);
                    }
                    else
                    {
                        pak.WriteShort(0x0000);
                    }

                    pak.WriteByte((byte)(GameClient.Player.TradeWindow.Repairing ? 0x01 : 0x00));
                    pak.WriteByte((byte)(GameClient.Player.TradeWindow.Combine ? 0x01 : 0x00));
                    if (items != null)
                    {
                        foreach (InventoryItem item in items)
                        {
                            pak.WriteByte((byte)item.SlotPosition);
                            pak.WriteByte((byte)item.Level);
                            pak.WriteByte((byte)item.DPS_AF); // dps_af
                            pak.WriteByte((byte)item.SPD_ABS); // spd_abs
                            pak.WriteByte((byte)(item.Hand << 6));
                            pak.WriteByte((byte)((item.Type_Damage > 3 ? 0 : item.Type_Damage << 6) | item.Object_Type));
                            pak.WriteShort((ushort)item.Weight); // weight
                            pak.WriteByte(item.ConditionPercent); // con %
                            pak.WriteByte(item.DurabilityPercent); // dur %
                            pak.WriteByte((byte)item.Quality); // qua %
                            pak.WriteByte((byte)item.Bonus); // bon %
                            pak.WriteShort((ushort)item.Model); // model
                            pak.WriteShort((ushort)item.Color); // color
                            pak.WriteShort((ushort)item.Effect); // weaponproc
                            if (item.Count > 1)
                            {
                                pak.WritePascalString(item.Count + " " + item.Name);
                            }
                            else
                            {
                                pak.WritePascalString(item.Name); // size and name item
                            }
                        }
                    }

                    if (GameClient.Player.TradeWindow is SelfCraftWindow)
                    {
                        pak.WritePascalString($"Combining for {GameClient.Player.Name}");
                    }
                    else
                    {
                        pak.WritePascalString($"Trading with {GameClient.Player.TradeWindow.Partner.Name}"); // transaction with ...
                    }

                    SendTCP(pak);
                }
            }
        }

        public virtual void SendCloseTradeWindow()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TradeWindow)))
            {
                pak.Fill(0x00, 40);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerDied(GamePlayer killedPlayer, GameObject killer)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerDeath)))
            {
                pak.WriteShort((ushort)killedPlayer.ObjectID);
                if (killer != null)
                {
                    pak.WriteShort((ushort)killer.ObjectID);
                }
                else
                {
                    pak.WriteShort(0x00);
                }

                pak.Fill(0x0, 4);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerRevive(GamePlayer revivedPlayer)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerRevive)))
            {
                pak.WriteShort((ushort)revivedPlayer.ObjectID);
                pak.WriteShort(0x00);
                SendTCP(pak);
            }
        }

        /// <summary>
        /// This is used to build a server side "Position Object"
        /// Usually Position Packet Should only be relayed
        /// The only purpose of this method is refreshing postion when there is Lag
        /// </summary>
        /// <param name="player"></param>
        public virtual void SendPlayerForgedPosition(GamePlayer player)
        {
            using (GSUDPPacketOut pak = new GSUDPPacketOut(GetPacketCode(eServerPackets.PlayerPosition)))
            {
                // PID
                pak.WriteShort((ushort)player.Client.SessionID);

                // Write Speed
                if (player.Steed != null && player.Steed.ObjectState == GameObject.eObjectState.Active)
                {
                    player.Heading = player.Steed.Heading;
                    pak.WriteShort(0x1800);
                }
                else
                {
                    short rSpeed = player.CurrentSpeed;
                    if (player.IsIncapacitated)
                    {
                        rSpeed = 0;
                    }

                    ushort content;
                    if (rSpeed < 0)
                    {
                        content = (ushort)((Math.Abs(rSpeed) > 511 ? 511 : Math.Abs(rSpeed)) + 0x200);
                    }
                    else
                    {
                        content = (ushort)(rSpeed > 511 ? 511 : rSpeed);
                    }

                    if (!player.IsAlive)
                    {
                        content += 5 << 10;
                    }
                    else
                    {
                        ushort state = 0;

                        if (player.IsSwimming)
                        {
                            state = 1;
                        }

                        if (player.IsClimbing)
                        {
                            state = 7;
                        }

                        if (player.IsSitting)
                        {
                            state = 4;
                        }

                        content += (ushort)(state << 10);
                    }

                    content += (ushort)(player.IsStrafing ? 1 << 13 : 0 << 13);

                    pak.WriteShort(content);
                }

                // Get Off Corrd
                int offX = player.X - player.CurrentZone.XOffset;
                int offY = player.Y - player.CurrentZone.YOffset;

                pak.WriteShort((ushort)player.Z);
                pak.WriteShort((ushort)offX);
                pak.WriteShort((ushort)offY);

                // Write Zone
                pak.WriteByte((byte)player.CurrentZone.ZoneSkinID);
                pak.WriteByte(0);

                // Copy Heading && Falling or Write Steed
                if (player.Steed != null && player.Steed.ObjectState == GameObject.eObjectState.Active)
                {
                    pak.WriteShort((ushort)player.Steed.ObjectID);
                    pak.WriteShort((ushort)player.Steed.RiderSlot(player));
                }
                else
                {
                    // Set Player always on ground, this is an "anti lag" packet
                    ushort contenthead = (ushort)(player.Heading + 0x1000);
                    pak.WriteShort(contenthead);

                    // No Fall Speed.
                    pak.WriteShort(0);
                }

                // Write Flags
                byte flagcontent = 0;

                if (player.IsDiving)
                {
                    flagcontent += 0x04;
                }

                if (player.IsWireframe)
                {
                    flagcontent += 0x01;
                }

                if (player.IsStealthed)
                {
                    flagcontent += 0x02;
                }

                if (player.IsTorchLighted)
                {
                    flagcontent += 0x80;
                }

                pak.WriteByte(flagcontent);

                // Write health + Attack
                byte healthcontent = (byte)(player.HealthPercent + (player.AttackState ? 0x80 : 0));

                pak.WriteByte(healthcontent);

                SendUDP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(player.CurrentRegionID, (ushort)player.ObjectID)] = GameTimer.GetTickCount();
        }

        public virtual void SendUpdatePlayer()
        {
            GamePlayer player = GameClient.Player;
            if (player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x03); // subcode
                pak.WriteByte(0x0d); // number of entry
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
                SendTCP(pak);
            }
        }

        public virtual void CheckLengthHybridSkillsPacket(ref GSTCPPacketOut pak, ref int maxSkills, ref int first)
        {
            maxSkills++;
        }

        public virtual void SendUpdatePlayerSkills()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            // Get Skills as "Usable Skills" which are in network order ! (with forced update)
            List<Tuple<Skill, Skill>> usableSkills = GameClient.Player.GetAllUsableSkills(true);

            bool sent = false; // set to true once we can't send packet anymore !
            int index = 0; // index of our position in the list !
            int total = usableSkills.Count; // cache List count.
            while (!sent)
            {
                int packetEntry = 0; // needed to tell client how much skill we send
                // using pak
                using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
                {
                    // Write header
                    pak.WriteByte(0x01); // subcode for skill
                    pak.WriteByte(0); // packet entries, can't know it for now...
                    pak.WriteByte(0x03); // subtype for following pages
                    pak.WriteByte((byte)index); // packet first entry

                    // getting pak filled
                    while (index < total)
                    {
                        // this item will break the limit, send the packet before, keep index as is to continue !
                        if ((index >= byte.MaxValue) || ((pak.Length + 8 + usableSkills[index].Item1.Name.Length) > 1000))
                        {
                            break;
                        }

                        // Enter Packet Values !! Format Level - Type - SpecialField - Bonus - Icon - Name
                        Skill skill = usableSkills[index].Item1;
                        Skill skillrelated = usableSkills[index].Item2;

                        if (skill is Specialization spec)
                        {
                            pak.WriteByte((byte)spec.Level);
                            pak.WriteByte((byte)spec.SkillType);
                            pak.WriteShort(0);
                            pak.WriteByte((byte)(GameClient.Player.GetModifiedSpecLevel(spec.KeyName) - spec.Level)); // bonus
                            pak.WriteShort(spec.Icon);
                            pak.WritePascalString(spec.Name);
                        }
                        else if (skill is Ability)
                        {
                            Ability ab = (Ability)skill;
                            pak.WriteByte(0);
                            pak.WriteByte((byte)ab.SkillType);
                            pak.WriteShort(0);
                            pak.WriteByte(0);
                            pak.WriteShort(ab.Icon);
                            pak.WritePascalString(ab.Name);
                        }
                        else if (skill is Spell)
                        {
                            Spell spell = (Spell)skill;
                            pak.WriteByte((byte)spell.Level);
                            pak.WriteByte((byte)spell.SkillType);

                            // spec index for this Spell - Special for Song and Unknown Indexes...
                            int spin;
                            if (spell.SkillType == eSkillPage.Songs)
                            {
                                spin = 0xFF;
                            }
                            else
                            {
                                // find this line Specialization index !
                                if (skillrelated is SpellLine line && !Util.IsEmpty(line.Spec))
                                {
                                    spin = usableSkills.FindIndex(sk => (sk.Item1 is Specialization) && ((Specialization)sk.Item1).KeyName == line.Spec);

                                    if (spin == -1)
                                    {
                                        spin = 0xFE;
                                    }
                                }
                                else
                                {
                                    spin = 0xFE;
                                }
                            }

                            pak.WriteShort((ushort)spin); // special index for spellline
                            pak.WriteByte(0); // bonus
                            pak.WriteShort(spell.InternalIconID > 0 ? spell.InternalIconID : spell.Icon); // icon
                            pak.WritePascalString(spell.Name);
                        }
                        else if (skill is Style)
                        {
                            Style style = (Style)skill;
                            pak.WriteByte((byte)style.SpecLevelRequirement);
                            pak.WriteByte((byte)style.SkillType);

                            // Special pre-requisite (First byte is Pre-requisite Icon / second Byte is prerequisite code...)
                            int pre = 0;

                            switch (style.OpeningRequirementType)
                            {
                                case Style.eOpening.Offensive:
                                    pre = (int)style.AttackResultRequirement; // last result of our attack against enemy hit, miss, target blocked, target parried, ...
                                    if (style.AttackResultRequirement == Style.eAttackResultRequirement.Style)
                                    {
                                        // get style requirement value... find prerequisite style index from specs beginning...
                                        int styleindex = Math.Max(0, usableSkills.FindIndex(it => (it.Item1 is Style) && it.Item1.ID == style.OpeningRequirementValue));
                                        int speccount = Math.Max(0, usableSkills.FindIndex(it => (it.Item1 is Specialization) == false));
                                        pre |= ((byte)(100 + styleindex - speccount)) << 8;
                                    }

                                    break;
                                case Style.eOpening.Defensive:
                                    pre = 100 + (int)style.AttackResultRequirement; // last result of enemies attack against us hit, miss, you block, you parry, ...
                                    break;
                                case Style.eOpening.Positional:
                                    pre = 200 + style.OpeningRequirementValue;
                                    break;
                            }

                            // style required?
                            if (pre == 0)
                            {
                                pre = 0x100;
                            }

                            pak.WriteShort((ushort)pre);
                            pak.WriteByte(GlobalConstants.GetSpecToInternalIndex(style.Spec)); // index specialization in bonus...
                            pak.WriteShort(style.Icon);
                            pak.WritePascalString(style.Name);
                        }

                        packetEntry++;
                        index++;
                    }

                    // test if we finished sending packets
                    if (index >= total || index >= byte.MaxValue)
                    {
                        sent = true;
                    }

                    // rewrite header for count.
                    pak.Position = 4;
                    pak.WriteByte((byte)packetEntry);

                    if (!sent)
                    {
                        pak.WriteByte(99);
                    }

                    SendTCP(pak);
                }
            }

            // Send List Cast Spells...
            SendNonHybridSpellLines();

            // clear trainer cache
            GameClient.TrainerSkillCache = null;
        }

        /// <summary>
        /// Send non hybrid and advanced spell lines
        /// </summary>
        public virtual void SendNonHybridSpellLines()
        {
            GamePlayer player = GameClient.Player;
            if (player == null)
            {
                return;
            }

            List<Tuple<SpellLine, List<Skill>>> spellsXLines = player.GetAllUsableListSpells(true);

            int lineIndex = 0;
            foreach (var spXsl in spellsXLines)
            {
                // Prepare packet
                using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
                {
                    // Add Line Header
                    pak.WriteByte(0x02); // subcode
                    pak.WriteByte((byte)(spXsl.Item2.Count + 1)); // number of entry
                    pak.WriteByte(0x02); // subtype
                    pak.WriteByte((byte)lineIndex); // number of line

                    pak.WriteByte(0); // level, not used when spell line
                    pak.WriteShort(0); // icon, not used when spell line
                    pak.WritePascalString(spXsl.Item1.Name);

                    // Add All Spells...
                    foreach (Skill sp in spXsl.Item2)
                    {
                        int reqLevel;
                        if (sp is Style style)
                        {
                            reqLevel = style.SpecLevelRequirement;
                        }
                        else if (sp is Ability)
                        {
                            reqLevel = ((Ability)sp).SpecLevelRequirement;
                        }
                        else
                        {
                            reqLevel = sp.Level;
                        }

                        pak.WriteByte((byte)reqLevel);
                        pak.WriteShort(sp.Icon);
                        pak.WritePascalString(sp.Name);
                    }

                    // Send
                    SendTCP(pak);
                }

                lineIndex++;
            }
        }

        public virtual void SendUpdateCraftingSkills()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x08); // subcode
                pak.WriteByte((byte)GameClient.Player.CraftingSkills.Count); // count
                pak.WriteByte(0x03); // subtype
                pak.WriteByte(0x00); // unk

                foreach (KeyValuePair<eCraftingSkill, int> de in GameClient.Player.CraftingSkills)
                {
                    AbstractCraftingSkill curentCraftingSkill = CraftingMgr.getSkillbyEnum(de.Key);
                    pak.WriteShort(Convert.ToUInt16(de.Value)); // points
                    pak.WriteByte(curentCraftingSkill.Icon); // icon
                    pak.WriteInt(1);
                    pak.WritePascalString(curentCraftingSkill.Name); // name
                }

                SendTCP(pak);
            }
        }

        public virtual void SendUpdateWeaponAndArmorStats()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x05); // subcode
                pak.WriteByte(6); // number of entries
                pak.WriteByte(0x00); // subtype
                pak.WriteByte(0x00); // unk

                // weapondamage
                var wd = (int)(GameClient.Player.WeaponDamage(GameClient.Player.AttackWeapon) * 100.0);
                pak.WriteByte((byte)(wd / 100));
                pak.WritePascalString(" ");
                pak.WriteByte((byte)(wd % 100));
                pak.WritePascalString(" ");

                // weaponskill
                int ws = GameClient.Player.DisplayedWeaponSkill;
                pak.WriteByte((byte)(ws >> 8));
                pak.WritePascalString(" ");
                pak.WriteByte((byte)(ws & 0xff));
                pak.WritePascalString(" ");

                // overall EAF
                int eaf = GameClient.Player.EffectiveOverallAF;
                pak.WriteByte((byte)(eaf >> 8));
                pak.WritePascalString(" ");
                pak.WriteByte((byte)(eaf & 0xff));
                pak.WritePascalString(" ");

                SendTCP(pak);
            }
        }

        public virtual void SendEncumberance()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Encumberance)))
            {
                pak.WriteShort((ushort)GameClient.Player.MaxEncumberance); // encumb total
                pak.WriteShort((ushort)GameClient.Player.Encumberance); // encumb used
                SendTCP(pak);
            }
        }

        public virtual void SendCustomTextWindow(string caption, IList<string> text)
        {
            if (text == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                if (caption == null)
                {
                    caption = string.Empty;
                }

                if (caption.Length > byte.MaxValue)
                {
                    caption = caption.Substring(0, byte.MaxValue);
                }

                pak.WritePascalString(caption); // window caption

                WriteCustomTextWindowData(pak, text);

                // Trailing Zero!
                pak.WriteByte(0);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerTitles()
        {
            IList<string> text = GameClient.Player.FormatStatistics();

            text.Add(" ");
            text.Add("Titles:");

            foreach (IPlayerTitle title in GameClient.Player.Titles)
            {
                text.Add($"- {title.GetDescription(GameClient.Player)}");
            }

            SendCustomTextWindow("Player Statistics", text);
        }

        public virtual void SendPlayerTitleUpdate(GamePlayer player)
        {
        }

        public virtual void SendAddFriends(string[] friendNames)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.AddFriend)))
            {
                foreach (string friend in friendNames)
                {
                    pak.WritePascalString(friend);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendRemoveFriends(string[] friendNames)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.RemoveFriend)))
            {
                foreach (string friend in friendNames)
                {
                    pak.WritePascalString(friend);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendTimerWindow(string title, int seconds)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TimerWindow)))
            {
                pak.WriteShort((ushort)seconds);
                pak.WriteByte((byte)title.Length);
                pak.WriteByte(1);
                pak.WriteString(title.Length > byte.MaxValue ? title.Substring(0, byte.MaxValue) : title);
                SendTCP(pak);
            }
        }

        public virtual void SendCloseTimerWindow()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TimerWindow)))
            {
                pak.WriteShort(0);
                pak.WriteByte(0);
                pak.WriteByte(0);
                SendTCP(pak);
            }
        }

        public virtual void SendCustomTrainerWindow(int type, List<Tuple<Specialization, List<Tuple<Skill, byte>>>> tree)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {
                if (tree != null && tree.Count > 0)
                {
                    pak.WriteByte((byte)type); // index for Champion Line ID (returned for training request)
                    pak.WriteByte(0); // Spec points available for this player.
                    pak.WriteByte(2); // Champion Window Type
                    pak.WriteByte(0);
                    pak.WriteByte((byte)tree.Count); // Count of sublines

                    for (int skillIndex = 0; skillIndex < tree.Count; skillIndex++)
                    {
                        pak.WriteByte((byte)(skillIndex + 1));
                        pak.WriteByte((byte)tree[skillIndex].Item2.Count(t => t.Item1 != null)); // Count of item for this line

                        for (int itemIndex = 0; itemIndex < tree[skillIndex].Item2.Count; itemIndex++)
                        {
                            Skill sk = tree[skillIndex].Item2[itemIndex].Item1;

                            if (sk != null)
                            {
                                pak.WriteByte((byte)(itemIndex + 1));

                                if (sk is Style)
                                {
                                    pak.WriteByte(2);
                                }
                                else if (sk is Spell)
                                {
                                    pak.WriteByte(3);
                                }
                                else
                                {
                                    pak.WriteByte(4);
                                }

                                pak.WriteShortLowEndian(sk.Icon); // Icon should be style icon + 3352 ???
                                pak.WritePascalString(sk.Name);

                                // Skill Status
                                pak.WriteByte(1); // 0 = disable, 1 = trained, 2 = can train

                                // Attached Skill
                                if (tree[skillIndex].Item2[itemIndex].Item2 == 2)
                                {
                                    pak.WriteByte(2); // count of attached skills
                                    pak.WriteByte((byte)(skillIndex << 8 + itemIndex));
                                    pak.WriteByte((byte)((skillIndex + 2) << 8 + itemIndex));
                                }
                                else if (tree[skillIndex].Item2[itemIndex].Item2 == 3)
                                {
                                    pak.WriteByte(3); // count of attached skills
                                    pak.WriteByte((byte)(skillIndex << 8 + itemIndex));
                                    pak.WriteByte((byte)((skillIndex + 1) << 8 + itemIndex));
                                    pak.WriteByte((byte)((skillIndex + 2) << 8 + itemIndex));
                                }
                                else
                                {
                                    // doesn't support other count
                                    pak.WriteByte(0);
                                }
                            }
                        }
                    }

                    SendTCP(pak);
                }
            }
        }

        public virtual void SendChampionTrainerWindow(int type)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            GamePlayer player = GameClient.Player;

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {
                // Get Player CL Spec
                var clspec = player.GetSpecList().Where(sp => sp is LiveChampionsSpecialization).Cast<LiveChampionsSpecialization>().FirstOrDefault();

                // check if the tree can be used
                List<Tuple<MiniLineSpecialization, List<Tuple<Skill, byte>>>> tree = null;
                if (clspec != null)
                {
                    tree = clspec.GetTrainerTreeDisplay(player, clspec.RetrieveTypeForIndex(type));
                }

                if (tree != null && tree.Count > 0)
                {
                    pak.WriteByte((byte)type); // index for Champion Line ID (returned for training request)
                    pak.WriteByte((byte)player.ChampionSpecialtyPoints); // Spec points available for this player.
                    pak.WriteByte(2); // Champion Window Type
                    pak.WriteByte(0);
                    pak.WriteByte((byte)tree.Count); // Count of sublines

                    for (int skillIndex = 0; skillIndex < tree.Count; skillIndex++)
                    {
                        pak.WriteByte((byte)(skillIndex + 1));
                        pak.WriteByte((byte)tree[skillIndex].Item2.Count(t => t.Item1 != null)); // Count of item for this line

                        for (int itemIndex = 0; itemIndex < tree[skillIndex].Item2.Count; itemIndex++)
                        {
                            Skill sk = tree[skillIndex].Item2[itemIndex].Item1;

                            if (sk != null)
                            {
                                pak.WriteByte((byte)(itemIndex + 1));

                                if (sk is Style)
                                {
                                    pak.WriteByte(2);
                                }
                                else if (sk is Spell)
                                {
                                    pak.WriteByte(3);
                                }
                                else
                                {
                                    pak.WriteByte(1);
                                }

                                pak.WriteShortLowEndian(sk.Icon); // Icon should be style icon + 3352 ???
                                pak.WritePascalString(sk.Name);

                                // Skill Status
                                pak.WriteByte(clspec.GetSkillStatus(tree, skillIndex, itemIndex).Item1); // 0 = disable, 1 = trained, 2 = can train

                                // Attached Skill
                                if (tree[skillIndex].Item2[itemIndex].Item2 == 2)
                                {
                                    pak.WriteByte(2); // count of attached skills
                                    pak.WriteByte((byte)(skillIndex << 8 + itemIndex));
                                    pak.WriteByte((byte)((skillIndex + 2) << 8 + itemIndex));
                                }
                                else if (tree[skillIndex].Item2[itemIndex].Item2 == 3)
                                {
                                    pak.WriteByte(3); // count of attached skills
                                    pak.WriteByte((byte)(skillIndex << 8 + itemIndex));
                                    pak.WriteByte((byte)((skillIndex + 1) << 8 + itemIndex));
                                    pak.WriteByte((byte)((skillIndex + 2) << 8 + itemIndex));
                                }
                                else
                                {
                                    // doesn't support other count
                                    pak.WriteByte(0);
                                }
                            }
                        }
                    }

                    SendTCP(pak);
                }
            }
        }

        public virtual void SendTrainerWindow()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {
                IList<Specialization> specs = GameClient.Player.GetSpecList().Where(it => it.Trainable).ToList();
                pak.WriteByte((byte)specs.Count);
                pak.WriteByte((byte)GameClient.Player.SkillSpecialtyPoints);
                pak.WriteByte(0);
                pak.WriteByte(0);

                int i = 0;
                foreach (Specialization spec in specs)
                {
                    pak.WriteByte((byte)i++);
                    pak.WriteByte((byte)spec.Level);
                    pak.WriteByte((byte)(spec.Level + 1));
                    pak.WritePascalString(spec.Name);
                }

                SendTCP(pak);
            }

            // send RA usable by this class
            var raList = SkillBase.GetClassRealmAbilities(GameClient.Player.CharacterClass.ID)
                .Where(ra => !(ra is RR5RealmAbility))
                .ToList();

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {
                pak.WriteByte((byte)raList.Count);
                pak.WriteByte((byte)GameClient.Player.RealmSpecialtyPoints);
                pak.WriteByte(1);
                pak.WriteByte(0);

                int i = 0;
                foreach (RealmAbility ra in raList)
                {
                    int level = GameClient.Player.GetAbilityLevel(ra.KeyName);
                    pak.WriteByte((byte)i++);
                    pak.WriteByte((byte)level);
                    pak.WriteByte((byte)ra.CostForUpgrade(level));
                    bool canBeUsed = ra.CheckRequirement(GameClient.Player);
                    pak.WritePascalString(canBeUsed ? ra.Name : $"[{ra.Name}]");
                }

                SendTCP(pak);
            }
        }

        public virtual void SendInterruptAnimation(GameLiving living)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.InterruptSpellCast)))
            {
                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteShort(1);
                SendTCP(pak);
            }
        }

        public virtual void SendDisableSkill(ICollection<Tuple<Skill, int>> skills)
        {
            if (GameClient.Player == null)
            {
                return;
            }

            var disabledSpells = new List<Tuple<byte, byte, ushort>>();
            var disabledSkills = new List<Tuple<ushort, ushort>>();

            var listspells = GameClient.Player.GetAllUsableListSpells();
            var listskills = GameClient.Player.GetAllUsableSkills();
            int specCount = listskills.Count(sk => sk.Item1 is Specialization);

            // Get through all disabled skills
            foreach (Tuple<Skill, int> disabled in skills)
            {

                // Check if spell
                byte lsIndex = 0;
                foreach (var ls in listspells)
                {
                    int index = ls.Item2.FindIndex(sk => sk.SkillType == disabled.Item1.SkillType && sk.ID == disabled.Item1.ID);

                    if (index > -1)
                    {
                        disabledSpells.Add(new Tuple<byte, byte, ushort>(lsIndex, (byte)index, (ushort)(disabled.Item2 > 0 ? disabled.Item2 / 1000 + 1 : 0)));
                        break;
                    }

                    lsIndex++;
                }

                int skIndex = listskills.FindIndex(skt => disabled.Item1.SkillType == skt.Item1.SkillType && disabled.Item1.ID == skt.Item1.ID) - specCount;

                if (skIndex > -1)
                {
                    disabledSkills.Add(new Tuple<ushort, ushort>((ushort)skIndex, (ushort)(disabled.Item2 > 0 ? disabled.Item2 / 1000 + 1 : 0)));
                }
            }

            if (disabledSkills.Count > 0)
            {
                // Send matching hybrid spell match
                using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DisableSkills)))
                {
                    byte countskill = (byte)Math.Min(disabledSkills.Count, 255);
                    if (countskill > 0)
                    {
                        pak.WriteShort(0); // duration unused
                        pak.WriteByte(countskill); // count...
                        pak.WriteByte(1); // code for hybrid skill

                        for (int i = 0 ; i < countskill ; i++)
                        {
                            pak.WriteShort(disabledSkills[i].Item1); // index
                            pak.WriteShort(disabledSkills[i].Item2); // duration
                        }

                        SendTCP(pak);
                    }
                }
            }

            if (disabledSpells.Count > 0)
            {
                var groupedDuration = disabledSpells.GroupBy(sp => sp.Item3);
                foreach (var groups in groupedDuration)
                {
                    // Send matching list spell match
                    using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DisableSkills)))
                    {
                        byte total = (byte)Math.Min(groups.Count(), 255);
                        if (total > 0)
                        {
                            pak.WriteShort(groups.Key); // duration
                            pak.WriteByte(total); // count...
                            pak.WriteByte(2); // code for list spells

                            for (int i = 0 ; i < total ; i++)
                            {
                                pak.WriteByte(groups.ElementAt(i).Item1); // line index
                                pak.WriteByte(groups.ElementAt(i).Item2); // spell index
                            }

                            SendTCP(pak);
                        }
                    }
                }
            }
        }

        public virtual void SendUpdateIcons(IList changedEffects, ref int lastUpdateEffectsCount)
        {
            byte fxcount = 0;
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.UpdateIcons)))
            {
                lock (GameClient.Player.EffectList)
                {
                    foreach (IGameEffect effect in GameClient.Player.EffectList)
                    {
                        if (effect.Icon != 0)
                        {
                            fxcount++;
                        }
                    }

                    pak.WriteByte(fxcount);
                    pak.WriteByte(0); // unknown
                    pak.WriteByte(0); // unknown
                    pak.WriteByte(0); // unknown
                    byte i = 0;
                    foreach (IGameEffect effect in GameClient.Player.EffectList)
                    {
                        if (effect.Icon != 0)
                        {
                            pak.WriteByte((effect is GameSpellEffect || effect.Icon > 5000) ? i++ : (byte)0xff);
                            pak.WriteByte(0);
                            pak.WriteShort(effect.Icon);

                            // pak.WriteShort(effect.IsFading ? (ushort)1 : (ushort) (effect.RemainingTime/1000));
                            pak.WriteShort((ushort)(effect.RemainingTime / 1000));
                            pak.WriteShort(effect.InternalID); // reference for shift+i or cancel spell
                            pak.WritePascalString(effect.Name);
                        }
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendLevelUpSound()
        {
            // not sure what package this is, but it triggers the mob color update
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.RegionSound)))
            {
                pak.WriteShort((ushort)GameClient.Player.ObjectID);
                pak.WriteByte(1); // level up sounds
                pak.WriteByte((byte)GameClient.Player.Realm);
                SendTCP(pak);
            }
        }

        public virtual void SendRegionEnterSound(byte soundId)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.RegionSound)))
            {
                pak.WriteShort((ushort)GameClient.Player.ObjectID);
                pak.WriteByte(2); // region enter sounds
                pak.WriteByte(soundId);
                SendTCP(pak);
            }
        }

        public virtual void SendDebugMessage(string format, params object[] parameters)
        {
            if (GameClient.Account.PrivLevel > (int)ePrivLevel.Player || ServerProperties.Properties.ENABLE_DEBUG)
            {
                SendMessage(string.Format("[DEBUG] " + format, parameters), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public virtual void SendDebugPopupMessage(string format, params object[] parameters)
        {
            if (GameClient.Account.PrivLevel > (int)ePrivLevel.Player || ServerProperties.Properties.ENABLE_DEBUG)
            {
                SendMessage(string.Format("[DEBUG] " + format, parameters), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }

        public virtual void SendEmblemDialogue()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.EmblemDialogue)))
            {
                pak.Fill(0x00, 4);
                SendTCP(pak);
            }
        }

        // FOR GM to test param and see min and max of each param
        public virtual void SendWeather(uint x, uint width, ushort speed, ushort fogdiffusion, ushort intensity)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Weather)))
            {
                pak.WriteInt(x);
                pak.WriteInt(width);
                pak.WriteShort(fogdiffusion);
                pak.WriteShort(speed);
                pak.WriteShort(intensity);
                pak.WriteShort(0); // 0x0508, 0xEB51, 0xFFBF
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerModelTypeChange(GamePlayer player, byte modelType)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerModelTypeChange)))
            {
                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(modelType);
                pak.WriteByte((byte)(modelType == 3 ? 0x08 : 0x00)); // unused?
                SendTCP(pak);
            }
        }

        public virtual void SendObjectDelete(GameObject obj)
        {
            // Remove from Cache
            if (GameClient.GameObjectUpdateArray.ContainsKey(new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID)))
            {
                long dummy;
                GameClient.GameObjectUpdateArray.TryRemove(new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID), out dummy);
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ObjectDelete)))
            {
                pak.WriteShort((ushort)obj.ObjectID);
                pak.WriteShort(1); // TODO: unknown
                SendTCP(pak);
            }
        }

        public virtual void SendConcentrationList()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ConcentrationList)))
            {
                lock (GameClient.Player.ConcentrationEffects)
                {
                    pak.WriteByte((byte)GameClient.Player.ConcentrationEffects.Count);
                    pak.WriteByte(0); // unknown
                    pak.WriteByte(0); // unknown
                    pak.WriteByte(0); // unknown

                    for (int i = 0; i < GameClient.Player.ConcentrationEffects.Count; i++)
                    {
                        IConcentrationEffect effect = GameClient.Player.ConcentrationEffects[i];
                        pak.WriteByte((byte)i);
                        pak.WriteByte(0); // unknown
                        pak.WriteByte(effect.Concentration);
                        pak.WriteShort(effect.Icon);
                        pak.WritePascalString(effect.Name.Length > 14 ? $"{effect.Name.Substring(0, 12)}.." : effect.Name);

                        if (effect.OwnerName.Length > 14)
                        {
                            pak.WritePascalString(effect.OwnerName.Substring(0, 12) + "..");
                        }
                        else
                        {
                            pak.WritePascalString(effect.OwnerName);
                        }
                    }
                }

                SendTCP(pak);
            }

            SendStatusUpdate(); // send status update for convinience, mostly the conc has changed
        }

        public void SendChangeTarget(GameObject newTarget)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ChangeTarget)))
            {
                pak.WriteShort((ushort)(newTarget?.ObjectID ?? 0));
                pak.WriteShort(0); // unknown
                SendTCP(pak);
            }
        }

        public void SendChangeGroundTarget(Point3D newTarget)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ChangeGroundTarget)))
            {
                pak.WriteInt((uint)(newTarget?.X ?? 0));
                pak.WriteInt((uint)(newTarget?.Y ?? 0));
                pak.WriteInt((uint)(newTarget?.Z ?? 0));
                SendTCP(pak);
            }
        }

        public virtual void SendPetWindow(GameLiving pet, ePetWindowAction windowAction, eAggressionState aggroState, eWalkState walkState)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PetWindow)))
            {
                pak.WriteShort((ushort)(pet?.ObjectID ?? 0));
                pak.WriteByte(0x00); // unused
                pak.WriteByte(0x00); // unused
                switch (windowAction)

                    // 0-released, 1-normal, 2-just charmed? | Roach: 0-close window, 1-update window, 2-create window
                {
                    case ePetWindowAction.Open:
                        pak.WriteByte(2);
                        break;
                    case ePetWindowAction.Update:
                        pak.WriteByte(1);
                        break;
                    default:
                        pak.WriteByte(0);
                        break;
                }

                switch (aggroState) // 1-aggressive, 2-defensive, 3-passive
                {
                    case eAggressionState.Aggressive:
                        pak.WriteByte(1);
                        break;
                    case eAggressionState.Defensive:
                        pak.WriteByte(2);
                        break;
                    case eAggressionState.Passive:
                        pak.WriteByte(3);
                        break;
                    default:
                        pak.WriteByte(0);
                        break;
                }

                switch (walkState) // 1-follow, 2-stay, 3-goto, 4-here
                {
                    case eWalkState.Follow:
                        pak.WriteByte(1);
                        break;
                    case eWalkState.Stay:
                        pak.WriteByte(2);
                        break;
                    case eWalkState.GoTarget:
                        pak.WriteByte(3);
                        break;
                    case eWalkState.ComeHere:
                        pak.WriteByte(4);
                        break;
                    default:
                        pak.WriteByte(0);
                        break;
                }

                pak.WriteByte(0x00); // unused

                if (pet != null)
                {
                    lock (pet.EffectList)
                    {
                        int count = 0;
                        foreach (IGameEffect effect in pet.EffectList)
                        {
                            pak.WriteShort(effect.Icon); // 0x08 - null terminated - (byte) list of shorts - spell icons on pet
                            if (++count > 8)
                            {
                                break;
                            }
                        }
                    }
                }

                pak.WriteByte(0x00); // null termination

                SendTCP(pak);
            }
        }

        public virtual void SendKeepInfo(IGameKeep keep)
        {
        }

        public virtual void SendKeepRealmUpdate(IGameKeep keep)
        {
        }

        public virtual void SendKeepRemove(IGameKeep keep)
        {
        }

        public virtual void SendKeepComponentInfo(IGameKeepComponent keepComponent)
        {
        }

        public virtual void SendKeepComponentDetailUpdate(IGameKeepComponent keepComponent)
        {
        }

        public virtual void SendKeepComponentRemove(IGameKeepComponent keepComponent)
        {
        }

        public virtual void SendWarmapUpdate(ICollection<IGameKeep> list)
        {
        }

        public virtual void SendWarmapBonuses()
        {
        }

        public virtual void SendWarmapDetailUpdate(List<List<byte>> fights, List<List<byte>> groups)
        {
        }

        // housing
        public virtual void SendHouse(House house)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseCreate)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort((ushort)house.Z);
                pak.WriteInt((uint)house.X);
                pak.WriteInt((uint)house.Y);
                pak.WriteShort(house.Heading);
                pak.WriteShort((ushort)house.PorchRoofColor);
                pak.WriteShort((ushort)house.GetPorchAndGuildEmblemFlags());
                pak.WriteShort((ushort)house.Emblem);
                pak.WriteByte((byte)house.Model);
                pak.WriteByte((byte)house.RoofMaterial);
                pak.WriteByte((byte)house.WallMaterial);
                pak.WriteByte((byte)house.DoorMaterial);
                pak.WriteByte((byte)house.TrussMaterial);
                pak.WriteByte((byte)house.PorchMaterial);
                pak.WriteByte((byte)house.WindowMaterial);
                pak.WriteByte(0x03);
                pak.WritePascalString(house.Name);

                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray[new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber)] = GameTimer.GetTickCount();
        }

        public virtual void SendRemoveHouse(House house)
        {
            // Remove from cache
            if (GameClient.HouseUpdateArray.ContainsKey(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber)))
            {
                long dummy;
                GameClient.HouseUpdateArray.TryRemove(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber), out dummy);
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseCreate)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort((ushort)house.Z);
                pak.WriteInt((uint)house.X);
                pak.WriteInt((uint)house.Y);
                pak.Fill(0x00, 15);
                pak.WriteByte(0x03);
                pak.WritePascalString(string.Empty);

                SendTCP(pak);
            }
        }

        public virtual void SendHousePayRentDialog(string title)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                pak.WriteByte(0x00);
                pak.WriteByte((byte)eDialogCode.HousePayRent);
                pak.Fill(0x00, 8); // empty
                pak.WriteByte(0x02); // type
                pak.WriteByte(0x01); // wrap
                if (title.Length > 0)
                {
                    pak.WriteString(title); // title ??
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendGarden(House house)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseChangeGarden)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte((byte)house.OutdoorItems.Count);
                pak.WriteByte(0x80);

                foreach (var entry in house.OutdoorItems.OrderBy(entry => entry.Key))
                {
                    var item = entry.Value;
                    pak.WriteByte((byte)entry.Key);
                    pak.WriteShort((ushort)item.Model);
                    pak.WriteByte((byte)item.Position);
                    pak.WriteByte((byte)item.Rotation);
                }

                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray.UpdateIfExists(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber), GameTimer.GetTickCount());
        }

        public virtual void SendGarden(House house, int i)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseChangeGarden)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte(0x01);
                pak.WriteByte(0x00); // update
                var item = house.OutdoorItems[i];
                pak.WriteByte((byte)i);
                pak.WriteShort((ushort)item.Model);
                pak.WriteByte((byte)item.Position);
                pak.WriteByte((byte)item.Rotation);
                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray.UpdateIfExists(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber), GameTimer.GetTickCount());
        }

        public virtual void SendHouseOccupied(House house, bool flagHouseOccuped)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseChangeGarden)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte(0x00);
                pak.WriteByte((byte)(flagHouseOccuped ? 1 : 0));

                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray.UpdateIfExists(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber), GameTimer.GetTickCount());
        }

        public virtual void SendEnterHouse(House house)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseEnter)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort(25000); // constant!
                pak.WriteInt((uint)house.X);
                pak.WriteInt((uint)house.Y);
                pak.WriteShort(house.Heading); // useless/ignored by client.
                pak.WriteByte(0x00);
                pak.WriteByte((byte)house.GetGuildEmblemFlags()); // emblem style
                pak.WriteShort((ushort)house.Emblem); // emblem
                pak.WriteByte(0x00);
                pak.WriteByte(0x00);
                pak.WriteByte((byte)house.Model);
                pak.WriteByte(0x00);
                pak.WriteByte(0x00);
                pak.WriteByte(0x00);
                pak.WriteByte((byte)house.Rug1Color);
                pak.WriteByte((byte)house.Rug2Color);
                pak.WriteByte((byte)house.Rug3Color);
                pak.WriteByte((byte)house.Rug4Color);
                pak.WriteByte(0x00);

                SendTCP(pak);
            }
        }

        public virtual void SendExitHouse(House house, ushort unknown = 0)
        {
            // do not send anything if client is leaving house due to linkdeath
            if (GameClient?.Player != null && GameClient.ClientState != GameClient.eClientState.Linkdead)
            {
                using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseExit)))
                {
                    pak.WriteShort((ushort)house.HouseNumber);
                    pak.WriteShort(unknown);
                    SendTCP(pak);
                }
            }
        }

        public virtual void SendToggleHousePoints(House house)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseTogglePoints)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte(0x04);
                pak.WriteByte(0x00);

                SendTCP(pak);
            }
        }

        public virtual void SendHouseUsersPermissions(House house)
        {
            if (house == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseUserPermissions)))
            {
                pak.WriteByte((byte)house.HousePermissions.Count()); // number of permissions
                pak.WriteByte(0x00); // ?
                pak.WriteShort((ushort)house.HouseNumber); // house number

                foreach (var entry in house.HousePermissions)
                {
                    // grab permission
                    var perm = entry.Value;

                    pak.WriteByte((byte)entry.Key); // Slot
                    pak.WriteByte(0x00); // ?
                    pak.WriteByte(0x00); // ?
                    pak.WriteByte((byte)perm.PermissionType); // Type (Guild, Class, Race ...)
                    pak.WriteByte((byte)perm.PermissionLevel); // Level (Friend, Visitor ...)
                    pak.WritePascalString(perm.DisplayName);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendFurniture(House house)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HousingItem)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte((byte)house.IndoorItems.Count);
                pak.WriteByte(0x80); // 0x00 = update, 0x80 = complete package

                foreach (var entry in house.IndoorItems.OrderBy(entry => entry.Key))
                {
                    var item = entry.Value;
                    WriteHouseFurniture(pak, item, entry.Key);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendFurniture(House house, int i)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HousingItem)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte(0x01); // cnt
                pak.WriteByte(0x00); // upd
                var item = house.IndoorItems[i];
                WriteHouseFurniture(pak, item, i);
                SendTCP(pak);
            }
        }

        public virtual void SendRentReminder(House house)
        {
            // 0:00:58.047 S=>C 0xF7 show help window (topicIndex:106 houseLot?:4281)
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HelpWindow)))
            {
                pak.WriteShort(106); // short index
                pak.WriteShort((ushort)house.HouseNumber); // short lot
                SendTCP(pak);
            }
        }

        public virtual void SendStarterHelp()
        {
            // * 0:00:57.984 S=>C 0xF7 show help window (topicIndex:1 houseLot?:0)
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HelpWindow)))
            {
                pak.WriteShort(1); // short index
                pak.WriteShort(0); // short lot
                SendTCP(pak);
            }
        }

        public virtual void SendPlaySound(eSoundType soundType, ushort soundId)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlaySound)))
            {
                pak.WriteShort((ushort)soundType);
                pak.WriteShort(soundId);
                pak.Fill(0x00, 8);
                SendTCP(pak);
            }
        }

        public virtual void SendKeepClaim(IGameKeep keep, byte flag)
        {
        }

        public virtual void SendKeepComponentUpdate(IGameKeep keep, bool levelUp)
        {
        }

        public virtual void SendKeepComponentInteract(IGameKeepComponent component)
        {
        }

        public virtual void SendKeepComponentHookPoint(IGameKeepComponent component, int selectedHookPointIndex)
        {
        }

        public virtual void SendClearKeepComponentHookPoint(IGameKeepComponent component, int selectedHookPointIndex)
        {
        }

        public virtual void SendHookPointStore(GameKeepHookPoint hookPoint)
        {
        }

        public virtual void SendMovingObjectCreate(GameMovingObject obj)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MovingObjectCreate)))
            {
                pak.WriteShort((ushort)obj.ObjectID);
                pak.WriteShort(0);
                pak.WriteShort(obj.Heading);
                pak.WriteShort((ushort)obj.Z);
                pak.WriteInt((uint)obj.X);
                pak.WriteInt((uint)obj.Y);
                pak.WriteShort(obj.Model);
                int flag = obj.Type() | ((byte)obj.Realm == 3 ? 0x40 : (byte)obj.Realm << 4) | obj.GetDisplayLevel(GameClient.Player) << 9;
                pak.WriteShort((ushort)flag); // (0x0002-for Ship,0x7D42-for catapult,0x9602,0x9612,0x9622-for ballista)
                pak.WriteShort(obj.Emblem); // emblem
                pak.WriteShort(0);
                pak.WriteInt(0);

                string name = obj.Name;

                LanguageDataObject translation = GameClient.GetTranslation(obj);
                if (translation != null)
                {
                    if (!Util.IsEmpty(((DBLanguageNPC)translation).Name))
                    {
                        name = ((DBLanguageNPC)translation).Name;
                    }
                }

                pak.WritePascalString(name);/*pak.WritePascalString(obj.Name);*/
                pak.WriteByte(0); // trailing ?
                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(obj.CurrentRegionID, (ushort)obj.ObjectID)] = GameTimer.GetTickCount();
        }

        public virtual void SendSiegeWeaponInterface(GameSiegeWeapon siegeWeapon, int time)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SiegeWeaponInterface)))
            {
                var flag = (ushort)((siegeWeapon.EnableToMove ? 1 : 0) | siegeWeapon.AmmoType << 8);
                pak.WriteShort(flag); // byte Ammo,  byte SiegeMoving(1/0)
                pak.WriteByte(0);
                pak.WriteByte(0); // Close interface(1/0)
                pak.WriteByte((byte)(time / 10)); // time in 1000ms
                pak.WriteByte((byte)siegeWeapon.Ammo.Count); // external ammo count
                pak.WriteByte((byte)siegeWeapon.SiegeWeaponTimer.CurrentAction);
                pak.WriteByte((byte)siegeWeapon.AmmoSlot);
                pak.WriteShort(siegeWeapon.Effect);
                pak.WriteShort((ushort)time); // time (?)
                pak.WriteInt((uint)siegeWeapon.ObjectID);

                string name = siegeWeapon.Name;

                LanguageDataObject translation = GameClient.GetTranslation(siegeWeapon);
                if (translation != null)
                {
                    if (!Util.IsEmpty(((DBLanguageNPC)translation).Name))
                    {
                        name = ((DBLanguageNPC)translation).Name;
                    }
                }

                pak.WritePascalString(name + " (" + siegeWeapon.CurrentState + ")");
                foreach (InventoryItem item in siegeWeapon.Ammo)
                {
                    pak.WriteByte((byte)item.SlotPosition);
                    pak.WriteByte((byte)item.Level);
                    pak.WriteByte((byte)item.DPS_AF);
                    pak.WriteByte((byte)item.SPD_ABS);
                    pak.WriteByte((byte)(item.Hand * 64));
                    pak.WriteByte((byte)((item.Type_Damage * 64) + item.Object_Type));
                    pak.WriteShort((ushort)item.Weight);
                    pak.WriteByte(item.ConditionPercent); // % of con
                    pak.WriteByte(item.DurabilityPercent); // % of dur
                    pak.WriteByte((byte)item.Quality); // % of qua
                    pak.WriteByte((byte)item.Bonus); // % bonus
                    pak.WriteShort((ushort)item.Model);
                    if (item.Emblem != 0)
                    {
                        pak.WriteShort((ushort)item.Emblem);
                    }
                    else
                    {
                        pak.WriteShort((ushort)item.Color);
                    }

                    pak.WriteShort((ushort)item.Effect);
                    if (item.Count > 1)
                    {
                        pak.WritePascalString(item.Count + " " + item.Name);
                    }
                    else
                    {
                        pak.WritePascalString(item.Name);
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendSiegeWeaponCloseInterface()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SiegeWeaponInterface)))
            {
                pak.WriteShort(0);
                pak.WriteShort(1);
                pak.Fill(0, 13);
                SendTCP(pak);
            }
        }

        public virtual void SendSiegeWeaponAnimation(GameSiegeWeapon siegeWeapon)
        {
            if (siegeWeapon == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SiegeWeaponAnimation)))
            {
                pak.WriteInt((uint)siegeWeapon.ObjectID);
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.X ?? (siegeWeapon.GroundTarget?.X ?? 0)));
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.Y ?? (siegeWeapon.GroundTarget?.Y ?? 0)));
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.Z ?? (siegeWeapon.GroundTarget?.Z ?? 0)));
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.ObjectID ?? 0));
                pak.WriteShort(siegeWeapon.Effect);
                pak.WriteShort((ushort)(siegeWeapon.SiegeWeaponTimer.TimeUntilElapsed / 100));
                pak.WriteByte((byte)siegeWeapon.SiegeWeaponTimer.CurrentAction);
                pak.Fill(0, 3);
                SendTCP(pak);
            }
        }

        public virtual void SendSiegeWeaponFireAnimation(GameSiegeWeapon siegeWeapon, int timer)
        {
            if (siegeWeapon == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SiegeWeaponAnimation)))
            {
                pak.WriteInt((uint)siegeWeapon.ObjectID);
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.X ?? 0));
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.Y ?? 0));
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.Z + 50 ?? 0));
                pak.WriteInt((uint)(siegeWeapon.TargetObject?.ObjectID ?? 0));
                pak.WriteShort(siegeWeapon.Effect);
                pak.WriteShort((ushort)(timer / 100));
                pak.WriteByte((byte)SiegeTimer.eAction.Fire);
                pak.WriteByte(0xAA);
                pak.WriteShort(0xFFBF);
                SendTCP(pak);
            }
        }

        public virtual void SendNPCsQuestEffect(GameNPC npc, eQuestIndicator indicator)
        {
        }

        public virtual void SendHexEffect(GamePlayer player, byte effect1, byte effect2, byte effect3, byte effect4,
                                          byte effect5)
        {
        }

        public virtual void SendLivingDataUpdate(GameLiving living, bool updateStrings)
        {
            if (living == null)
            {
                return;
            }

            if (living is GamePlayer player)
            {
                SendObjectRemove(living);
                SendPlayerCreate(player);
                SendLivingEquipmentUpdate(player);
            }
            else if (living is GameNPC npc)
            {
                SendNPCCreate(npc);
                if (npc.Inventory != null)
                {
                    SendLivingEquipmentUpdate(npc);
                }
            }
        }

        public virtual void SendSoundEffect(ushort soundId, ushort zoneId, ushort x, ushort y, ushort z, ushort radius)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SoundEffect)))
            {
                pak.WriteShort(soundId);
                pak.WriteShort(zoneId);
                pak.WriteShort(x);
                pak.WriteShort(y);
                pak.WriteShort(z);
                pak.WriteShort(radius);
                SendTCP(pak);
            }
        }

        public virtual void SendSetControlledHorse(GamePlayer player)
        {
        }

        public virtual void SendControlledHorse(GamePlayer player, bool flag)
        {
        }

        public virtual void SendCrash(string str)
        {
            using (var pak = new GSTCPPacketOut(0x86))
            {
                pak.WriteByte(0xFF);
                pak.WritePascalString(str);
                SendTCP(pak);
            }
        }

        public virtual void SendRvRGuildBanner(GamePlayer player, bool show)
        {
        }

        public virtual void SendPlayerFreeLevelUpdate()
        {
        }

        public virtual void SendRegionColorScheme(byte color)
        {
        }

        public virtual void SendRegionColorScheme()
        {
            SendRegionColorScheme(GameServer.ServerRules.GetColorHandling(GameClient));
        }

        public virtual void SendVampireEffect(GameLiving living, bool show)
        {
        }

        public virtual void SendXFireInfo(byte flag)
        {
        }

        public virtual void SendMarketExplorerWindow()
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MarketExplorerWindow)))
            {
                pak.WriteByte(255);
                pak.Fill(0, 3);
                SendTCP(pak);
            }
        }

        public virtual void SendMarketExplorerWindow(IList<InventoryItem> items, byte page, byte maxpage)
        {
            if (GameClient?.Player == null)
            {
                return;
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MarketExplorerWindow)))
            {
                pak.WriteByte((byte)items.Count);
                pak.WriteByte(page);
                pak.WriteByte(maxpage);
                pak.WriteByte(0);
                foreach (InventoryItem item in items)
                {
                    pak.WriteByte((byte)items.IndexOf(item));
                    pak.WriteByte((byte)item.Level);
                    int value1; // some object types use this field to display count
                    int value2; // some object types use this field to display count
                    switch (item.Object_Type)
                    {
                        case (int)eObjectType.Arrow:
                        case (int)eObjectType.Bolt:
                        case (int)eObjectType.Poison:
                        case (int)eObjectType.GenericItem:
                            value1 = item.PackSize;
                            value2 = item.SPD_ABS; break;
                        case (int)eObjectType.Thrown:
                            value1 = item.DPS_AF;
                            value2 = item.PackSize; break;
                        case (int)eObjectType.Instrument:
                            value1 = item.DPS_AF == 2 ? 0 : item.DPS_AF; // 0x00 = Lute ; 0x01 = Drum ; 0x03 = Flute
                            value2 = 0; break; // unused
                        case (int)eObjectType.Shield:
                            value1 = item.Type_Damage;
                            value2 = item.DPS_AF; break;
                        case (int)eObjectType.GardenObject:
                        case (int)eObjectType.HouseWallObject:
                        case (int)eObjectType.HouseFloorObject:
                            value1 = 0;
                            value2 = item.SPD_ABS; break;
                        default:
                            value1 = item.DPS_AF;
                            value2 = item.SPD_ABS; break;
                    }

                    pak.WriteByte((byte)value1);
                    pak.WriteByte((byte)value2);
                    if (item.Object_Type == (int)eObjectType.GardenObject)
                    {
                        pak.WriteByte((byte)item.DPS_AF);
                    }
                    else
                    {
                        pak.WriteByte((byte)(item.Hand << 6));
                    }

                    pak.WriteByte((byte)((item.Type_Damage > 3 ? 0 : item.Type_Damage << 6) | item.Object_Type));
                    pak.WriteByte((byte)(GameClient.Player.HasAbilityToUseItem(item.Template) ? 0 : 1));
                    pak.WriteShort((ushort)(item.PackSize > 1 ? item.Weight * item.PackSize : item.Weight));
                    pak.WriteByte(item.ConditionPercent);
                    pak.WriteByte(item.DurabilityPercent);
                    pak.WriteByte((byte)item.Quality);
                    pak.WriteByte((byte)item.Bonus);
                    pak.WriteShort((ushort)item.Model);
                    if (item.Emblem != 0)
                    {
                        pak.WriteShort((ushort)item.Emblem);
                    }
                    else
                    {
                        pak.WriteShort((ushort)item.Color);
                    }

                    pak.WriteShort((byte)item.Effect);
                    pak.WriteShort(item.OwnerLot);// lot
                    pak.WriteInt((uint)item.SellPrice);

                    if (ServerProperties.Properties.CONSIGNMENT_USE_BP)
                    {
                        string bpPrice = string.Empty;
                        if (item.SellPrice > 0)
                        {
                            bpPrice = $"[{item.SellPrice} BP";
                        }

                        if (item.Count > 1)
                        {
                            pak.WritePascalString($"{item.Count} {item.Name}");
                        }
                        else if (item.PackSize > 1)
                        {
                            pak.WritePascalString($"{item.PackSize} {item.Name}{bpPrice}");
                        }
                        else
                        {
                            pak.WritePascalString(item.Name + bpPrice);
                        }
                    }
                    else
                    {
                        if (item.Count > 1)
                        {
                            pak.WritePascalString($"{item.Count} {item.Name}");
                        }
                        else if (item.PackSize > 1)
                        {
                            pak.WritePascalString(item.PackSize + " " + item.Name);
                        }
                        else
                        {
                            pak.WritePascalString(item.Name);
                        }
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendMasterLevelWindow(byte ml)
        {
            // If required ML=0 then send current player ML data
            byte mlRequired = ml == 0 ? ((byte)GameClient.Player.MLLevel == 0 ? (byte)1 : (byte)GameClient.Player.MLLevel) : ml;

            double mlXpPercent;
            if (GameClient.Player.MLLevel < 10)
            {
                mlXpPercent = 100.0 * GameClient.Player.MLExperience / GameClient.Player.GetMLExperienceForLevel(GameClient.Player.MLLevel + 1);
            }
            else
            {
                mlXpPercent = 100.0; // ML10 has no MLXP, so always 100%
            }

            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MasterLevelWindow)))
            {
                pak.WriteByte((byte)mlXpPercent); // MLXP (displayed in window)
                pak.WriteByte(0x64);
                pak.WriteByte((byte)(GameClient.Player.MLLevel + 1)); // ML level + 1
                pak.WriteByte(0x00);
                pak.WriteByte(ml); // Required ML

                if (mlRequired < 10)
                {
                    // ML level completion is displayed client side for Step 11
                    for (int i = 1; i < 11; i++)
                    {
                        string description = GameClient.Player.GetMLStepDescription(mlRequired, i);
                        pak.WritePascalString(description);
                    }
                }
                else
                {
                    pak.WriteByte(0x00);
                }

                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendConsignmentMerchantMoney(long money)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ConsignmentMerchantMoney)))
            {
                pak.WriteByte((byte)Money.GetCopper(money));
                pak.WriteByte((byte)Money.GetSilver(money));
                pak.WriteShort((ushort)Money.GetGold(money));

                // Yes, these are sent in reverse order! - tolakram confirmed 1.98 - 1.109
                pak.WriteShort((ushort)Money.GetMithril(money));
                pak.WriteShort((ushort)Money.GetPlatinum(money));

                SendTCP(pak);
            }
        }

        public virtual void SendMinotaurRelicMapRemove(byte id)
        {
        }

        public virtual void SendMinotaurRelicMapUpdate(byte id, ushort region, int x, int y, int z)
        {
        }

        public virtual void SendMinotaurRelicWindow(GamePlayer player, int spell, bool flag)
        {
        }

        public virtual void SendMinotaurRelicBarUpdate(GamePlayer player, int xp)
        {
        }

        public virtual void SendBlinkPanel(byte flag)
        {
        }

        /// <summary>
        /// The bow prepare animation
        /// </summary>
        public virtual int BowPrepare => 0x01F4;

        /// <summary>
        /// The bow shoot animation
        /// </summary>
        public virtual int BowShoot => 0x1F7;

        /// <summary>
        /// one dual weapon hit animation
        /// </summary>
        public virtual int OneDualWeaponHit => 0x1F5;

        /// <summary>
        /// both dual weapons hit animation
        /// </summary>
        public virtual int BothDualWeaponHit => 0x1F6;

        #endregion

        protected virtual void SendQuestPacket(AbstractQuest quest, int index)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry)))
            {
                pak.WriteByte((byte)index);

                if (quest.Step <= 0)
                {
                    pak.WriteByte(0);
                    pak.WriteByte(0);
                    pak.WriteByte(0);
                }
                else
                {
                    string name = quest.Name;
                    string desc = quest.Description;
                    if (name.Length > byte.MaxValue)
                    {
                        if (Log.IsWarnEnabled)
                        {
                            Log.Warn($"{quest.GetType()}: name is too long for 1.68+ clients ({name.Length}) \'{name}\'");
                        }

                        name = name.Substring(0, byte.MaxValue);
                    }

                    if (desc.Length > byte.MaxValue)
                    {
                        if (Log.IsWarnEnabled)
                        {
                            Log.Warn($"{quest.GetType()}: description is too long for 1.68+ clients ({desc.Length}) \'{desc}\'");
                        }

                        desc = desc.Substring(0, byte.MaxValue);
                    }

                    pak.WriteByte((byte)name.Length);
                    pak.WriteByte((byte)desc.Length);
                    pak.WriteByte(0);
                    pak.WriteStringBytes(name); // Write Quest Name without trailing 0
                    pak.WriteStringBytes(desc); // Write Quest Description without trailing 0
                }

                SendTCP(pak);
            }
        }

        protected virtual void SendTaskInfo()
        {
        }

        protected string BuildTaskString()
        {
            if (GameClient.Player == null)
            {
                return string.Empty;
            }

            AbstractTask task = GameClient.Player.Task;
            AbstractMission pMission = GameClient.Player.Mission;

            AbstractMission gMission = null;
            if (GameClient.Player.Group != null)
            {
                gMission = GameClient.Player.Group.Mission;
            }

            // all the task info is sent in name field
            var taskStr = task == null
                ? "You have no current personal task.\n"
                : $"[{task.Name}] {task.Description}.\n";

            string personalMission = string.Empty;
            if (pMission != null)
            {
                personalMission = $"[{pMission.Name}] {pMission.Description}.\n";
            }

            string groupMission = string.Empty;
            if (gMission != null)
            {
                groupMission = $"[{gMission.Name}] {gMission.Description}.\n";
            }

            string realmMission = string.Empty;

            string name = taskStr + personalMission + groupMission + realmMission;

            if (name.Length > ushort.MaxValue)
            {
                if (Log.IsWarnEnabled)
                {
                    Log.Warn($"Task packet name is too long for 1.71 clients ({name.Length}) \'{name}\'");
                }

                name = name.Substring(0, ushort.MaxValue);
            }

            if (name.Length > 2048 - 10)
            {
                name = name.Substring(0, 2048 - 10 - name.Length);
            }

            return name;
        }

        protected virtual void WriteGroupMemberUpdate(GSTCPPacketOut pak, bool updateIcons, GameLiving living)
        {
            pak.WriteByte((byte)(living.GroupIndex + 1)); // From 1 to 8
            bool sameRegion = living.CurrentRegion == GameClient.Player.CurrentRegion;

            if (sameRegion)
            {
                if (living is GamePlayer player)
                {
                    pak.WriteByte(player.CharacterClass.HealthPercentGroupWindow);
                }
                else
                {
                    pak.WriteByte(living.HealthPercent);
                }

                pak.WriteByte(living.ManaPercent);

                byte playerStatus = 0;
                if (!living.IsAlive)
                {
                    playerStatus |= 0x01;
                }

                if (living.IsMezzed)
                {
                    playerStatus |= 0x02;
                }

                if (living.IsDiseased)
                {
                    playerStatus |= 0x04;
                }

                if (living.FindEffectOnTarget("DamageOverTime") != null)
                {
                    playerStatus |= 0x08;
                }

                if (living is GamePlayer gamePlayer && gamePlayer.Client.ClientState == GameClient.eClientState.Linkdead)
                {
                    playerStatus |= 0x10;
                }

                pak.WriteByte(playerStatus);

                // 0x00 = Normal , 0x01 = Dead , 0x02 = Mezzed , 0x04 = Diseased ,
                // 0x08 = Poisoned , 0x10 = Link Dead , 0x20 = In Another Region
                if (updateIcons)
                {
                    pak.WriteByte((byte)(0x80 | living.GroupIndex));
                    lock (living.EffectList)
                    {
                        byte i = 0;
                        foreach (IGameEffect effect in living.EffectList)
                        {
                            if (effect is GameSpellEffect)
                            {
                                i++;
                            }
                        }

                        pak.WriteByte(i);
                        foreach (IGameEffect effect in living.EffectList)
                        {
                            if (effect is GameSpellEffect)
                        {
                            pak.WriteShort(effect.Icon);
                        }
                        }
                    }
                }
            }
            else
            {
                pak.WriteShort(0);
                pak.WriteByte(0x20);
                if (updateIcons)
                {
                    pak.WriteByte((byte)(0x80 | living.GroupIndex));
                    pak.WriteByte(0);
                }
            }
        }

        protected virtual void SendInventorySlotsUpdateRange(ICollection<int> slots, eInventoryWindowType windowType)
        {
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.InventoryUpdate)))
            {
                pak.WriteByte((byte)(slots?.Count ?? 0));
                pak.WriteByte((byte)((GameClient.Player.IsCloakHoodUp ? 0x01 : 0x00) | (int)GameClient.Player.ActiveQuiverSlot));

                // bit0 is hood up bit4 to 7 is active quiver
                pak.WriteByte(GameClient.Player.VisibleActiveWeaponSlots);
                pak.WriteByte((byte)windowType); // preAction (0x00 - Do nothing)
                if (slots != null)
                {
                    foreach (int updatedSlot in slots)
                    {
                        if (updatedSlot >= (int)eInventorySlot.Consignment_First && updatedSlot <= (int)eInventorySlot.Consignment_Last)
                        {
                            pak.WriteByte(
                                (byte)(updatedSlot - (int)eInventorySlot.Consignment_First + (int)eInventorySlot.HousingInventory_First));
                        }
                        else
                        {
                            pak.WriteByte((byte)updatedSlot);
                        }

                        InventoryItem item = GameClient.Player.Inventory.GetItem((eInventorySlot)updatedSlot);

                        if (item == null)
                        {
                            pak.Fill(0x00, 18);
                            continue;
                        }

                        pak.WriteByte((byte)item.Level);

                        int value1; // some object types use this field to display count
                        int value2; // some object types use this field to display count
                        switch (item.Object_Type)
                        {
                            case (int)eObjectType.Arrow:
                            case (int)eObjectType.Bolt:
                            case (int)eObjectType.Poison:
                            case (int)eObjectType.GenericItem:
                                value1 = item.Count;
                                value2 = item.SPD_ABS;
                                break;
                            case (int)eObjectType.Thrown:
                                value1 = item.DPS_AF;
                                value2 = item.Count;
                                break;
                            case (int)eObjectType.Instrument:
                                value1 = item.DPS_AF == 2 ? 0 : item.DPS_AF; // 0x00 = Lute ; 0x01 = Drum ; 0x03 = Flute
                                value2 = 0;
                                break; // unused
                            case (int)eObjectType.Shield:
                                value1 = item.Type_Damage;
                                value2 = item.DPS_AF;
                                break;
                            case (int)eObjectType.GardenObject:
                                value1 = 0;
                                value2 = item.SPD_ABS;
                                break;
                            default:
                                value1 = item.DPS_AF;
                                value2 = item.SPD_ABS;
                                break;
                        }

                        pak.WriteByte((byte)value1);
                        pak.WriteByte((byte)value2);

                        if (item.Object_Type == (int)eObjectType.GardenObject)
                        {
                            pak.WriteByte((byte)item.DPS_AF);
                        }
                        else
                        {
                            pak.WriteByte((byte)(item.Hand << 6));
                        }

                        pak.WriteByte((byte)((item.Type_Damage > 3 ? 0 : item.Type_Damage << 6) | item.Object_Type));
                        pak.WriteShort((ushort)item.Weight);
                        pak.WriteByte(item.ConditionPercent); // % of con
                        pak.WriteByte(item.DurabilityPercent); // % of dur
                        pak.WriteByte((byte)item.Quality); // % of qua
                        pak.WriteByte((byte)item.Bonus); // % bonus
                        pak.WriteShort((ushort)item.Model);
                        if (item.Emblem != 0)
                        {
                            pak.WriteShort((ushort)item.Emblem);
                        }
                        else
                        {
                            pak.WriteShort((ushort)item.Color);
                        }

                        pak.WriteShort((ushort)item.Effect);
                        string name = item.Name;
                        if (item.Count > 1)
                        {
                            name = $"{item.Count} {name}";
                        }

                        if (item.SellPrice > 0)
                        {
                            if (ServerProperties.Properties.CONSIGNMENT_USE_BP)
                            {
                                name += $"[{item.SellPrice} BP]";
                            }
                            else
                            {
                                name += $"[{Money.GetString(item.SellPrice)}]";
                            }
                        }

                        pak.WritePascalString(name);
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendInventoryItemsPartialUpdate(List<InventoryItem> items, eInventoryWindowType windowType)
        {
        }

        protected void WriteCustomTextWindowData(GSTCPPacketOut pak, IList<string> text)
        {
            byte line = 0;
            bool needBreak = false;

            foreach (var listStr in text)
            {
                string str = listStr;

                if (str != null)
                {
                    if (pak.Position + 4 > MaxPacketLength) // line + pascalstringline(1) + trailingZero
                    {
                        return;
                    }

                    pak.WriteByte(++line);

                    while (str.Length > byte.MaxValue)
                    {
                        string s = str.Substring(0, byte.MaxValue);

                        if (pak.Position + s.Length + 2 > MaxPacketLength)
                        {
                            needBreak = true;
                            break;
                        }

                        pak.WritePascalString(s);
                        str = str.Substring(byte.MaxValue, str.Length - byte.MaxValue);
                        if (line >= 200 || pak.Position + Math.Min(byte.MaxValue, str.Length) + 2 >= MaxPacketLength)
                        {
                            // line + pascalstringline(1) + trailingZero
                            return;
                        }

                        pak.WriteByte(++line);
                    }

                    if (pak.Position + str.Length + 2 > MaxPacketLength) // str.Length + trailing zero
                    {
                        str = str.Substring(0, (int)Math.Max(Math.Min(1, str.Length), MaxPacketLength - pak.Position - 2));
                        needBreak = true;
                    }

                    pak.WritePascalString(str);

                    if (needBreak || line >= 200) // Check max packet length or max stings in window (0 - 199)
                    {
                        break;
                    }
                }
            }
        }

        protected virtual void WriteHouseFurniture(GSTCPPacketOut pak, IndoorItem item, int index)
        {
            pak.WriteByte((byte)index);
            pak.WriteShort((ushort)item.Model);
            pak.WriteShort((ushort)item.Color);
            pak.WriteByte(0x00);
            pak.WriteByte(0x00);
            pak.WriteShort((ushort)item.X);
            pak.WriteShort((ushort)item.Y);
            pak.WriteShort((ushort)item.Rotation);

            int size = item.Size;
            if (size == 0)
            {
                size = 100;
            }

            pak.WriteByte((byte)size);
            pak.WriteByte((byte)item.Position);
            pak.WriteByte((byte)(item.PlacementMode - 2));
        }

        public virtual void SendDelveInfo(string info)
        {
        }
    }
}