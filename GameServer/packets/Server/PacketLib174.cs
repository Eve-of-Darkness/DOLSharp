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
using System;
using System.Collections.Generic;
using System.Reflection;
using DOL.Database;
using DOL.GS.Keeps;
using log4net;

namespace DOL.GS.PacketHandler
{
    [PacketLib(174, GameClient.eClientVersion.Version174)]
    public class PacketLib174 : PacketLib173
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.74 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib174(GameClient client)
            : base(client)
        {
        }

        public override void SendCharacterOverview(eRealm realm)
        {
            int firstAccountSlot;
            switch (realm)
            {
                case eRealm.Albion: firstAccountSlot = 100; break;
                case eRealm.Midgard: firstAccountSlot = 200; break;
                case eRealm.Hibernia: firstAccountSlot = 300; break;
                default: throw new Exception($"CharacterOverview requested for unknown realm {realm}");
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterOverview)))
            {
                pak.FillString(GameClient.Account.Name, 24);
                DOLCharacters[] characters = GameClient.Account.Characters;
                if (characters == null)
                {
                    pak.Fill(0x0, 1840);
                }
                else
                {
                    for (int i = firstAccountSlot; i < firstAccountSlot + 10; i++)
                    {
                        bool written = false;
                        for (int j = 0; j < characters.Length && written == false; j++)
                        {
                            if (characters[j].AccountSlot == i)
                            {
                                pak.FillString(characters[j].Name, 24);
                                var items = GameServer.Database.SelectObjects<InventoryItem>(
                                    "`OwnerID` = @OwnerID AND `SlotPosition` >= @SlotPositionMin AND `SlotPosition` <= @SlotPositionMax",
                                    new[]
                                    {
                                        new QueryParameter("@OwnerID", characters[j].ObjectId),
                                        new QueryParameter("@SlotPositionMin", 10),
                                        new QueryParameter("@SlotPositionMax", 37)
                                    });

                                byte extensionTorso = 0;
                                byte extensionGloves = 0;
                                byte extensionBoots = 0;
                                foreach (InventoryItem item in items)
                                {
                                    switch (item.SlotPosition)
                                    {
                                        case 22:
                                            extensionGloves = item.Extension;
                                            break;
                                        case 23:
                                            extensionBoots = item.Extension;
                                            break;
                                        case 25:
                                            extensionTorso = item.Extension;
                                            break;
                                    }
                                }

                                pak.WriteByte(0x01);
                                pak.WriteByte(characters[j].EyeSize);
                                pak.WriteByte(characters[j].LipSize);
                                pak.WriteByte(characters[j].EyeColor);
                                pak.WriteByte(characters[j].HairColor);
                                pak.WriteByte(characters[j].FaceType);
                                pak.WriteByte(characters[j].HairStyle);
                                pak.WriteByte((byte)((extensionBoots << 4) | extensionGloves));
                                pak.WriteByte((byte)((extensionTorso << 4) | (characters[j].IsCloakHoodUp ? 0x1 : 0x0)));
                                pak.WriteByte(characters[j].CustomisationStep); // 1 = auto generate config, 2= config ended by player, 3= enable config to player
                                pak.WriteByte(characters[j].MoodType);
                                pak.Fill(0x0, 13); // 0 String

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

                                if (characters[j].Class == 0)
                                {
                                    pak.FillString(string.Empty, 24); // Class name
                                }
                                else
                                {
                                    pak.FillString(((eCharacterClass)characters[j].Class).ToString(), 24); // Class name
                                }

                                // pak.FillString(GamePlayer.RACENAMES[characters[j].Race], 24);
                                pak.FillString(GameClient.RaceToTranslatedName(characters[j].Race, characters[j].Gender), 24);
                                pak.WriteByte((byte)characters[j].Level);
                                pak.WriteByte((byte)characters[j].Class);
                                pak.WriteByte((byte)characters[j].Realm);
                                pak.WriteByte((byte)((((characters[j].Race & 0x10) << 2) + (characters[j].Race & 0x0F)) | (characters[j].Gender << 4))); // race max value can be 0x1F
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

                                // pak.WriteByte(0x00); // unk2
                                pak.WriteByte((byte)characters[j].Constitution);
                                written = true;
                            }
                        }

                        if (written == false)
                        {
                            pak.Fill(0x0, 184);
                        }
                    }

                    // pak.Fill(0x0,184); //Slot 9
                    //              pak.Fill(0x0,184); //Slot 10
                }

                pak.Fill(0x0, 0x82); // Don't know why so many trailing 0's | Corillian: Cuz they're stupid like that ;)

                SendTCP(pak);
            }
        }

        public override void SendPlayerCreate(GamePlayer playerToCreate)
        {
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

            if (GameClient.Player == null || playerToCreate.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerCreate172)))
            {
                pak.WriteShort((ushort)playerToCreate.Client.SessionID);
                pak.WriteShort((ushort)playerToCreate.ObjectID);
                pak.WriteShort(playerToCreate.Model);
                pak.WriteShort((ushort)playerToCreate.Z);

                // Dinberg:Instances - zoneSkinID for object positioning clientside (as zones are hardcoded).
                pak.WriteShort(playerZone.ZoneSkinID);
                pak.WriteShort((ushort)playerRegion.GetXOffInZone(playerToCreate.X, playerToCreate.Y));
                pak.WriteShort((ushort)playerRegion.GetYOffInZone(playerToCreate.X, playerToCreate.Y));
                pak.WriteShort(playerToCreate.Heading);

                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.EyeSize)); // 1-4 = Eye Size / 5-8 = Nose Size
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.LipSize)); // 1-4 = Ear size / 5-8 = Kin size
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.MoodType)); // 1-4 = Ear size / 5-8 = Kin size
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.EyeColor)); // 1-4 = Skin Color / 5-8 = Eye Color
                pak.WriteByte(playerToCreate.GetDisplayLevel(GameClient.Player));
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.HairColor)); // Hair: 1-4 = Color / 5-8 = unknown
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.FaceType)); // 1-4 = Unknown / 5-8 = Face type
                pak.WriteByte(playerToCreate.GetFaceAttribute(eCharFacePart.HairStyle)); // 1-4 = Unknown / 5-8 = Hair Style

                int flags = (GameServer.ServerRules.GetLivingRealm(GameClient.Player, playerToCreate) & 0x03) << 2;
                if (playerToCreate.IsAlive == false)
                {
                    flags |= 0x01;
                }

                if (playerToCreate.IsUnderwater)
                {
                    flags |= 0x02; // swimming
                }

                if (playerToCreate.IsStealthed)
                {
                    flags |= 0x10;
                }

                if (playerToCreate.IsWireframe)
                {
                    flags |= 0x20;
                }

                pak.WriteByte((byte)flags);
                pak.WriteByte(0x00); // new in 1.74

                pak.WritePascalString(GameServer.ServerRules.GetPlayerName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerGuildName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerLastName(GameClient.Player, playerToCreate));

                // RR 12 / 13
                pak.WritePascalString(GameServer.ServerRules.GetPlayerPrefixName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerTitle(GameClient.Player, playerToCreate)); // new in 1.74, NewTitle
                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(playerToCreate.CurrentRegionID, (ushort)playerToCreate.ObjectID)] = GameTimer.GetTickCount();

            SendObjectGuildID(playerToCreate, playerToCreate.Guild); // used for nearest friendly/enemy object buttons and name colors on PvP server
        }

        public override void SendPlayerPositionAndObjectID()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PositionAndObjectID)))
            {
                pak.WriteShort((ushort)GameClient.Player.ObjectID); // This is the player's objectid not Sessionid!!!
                pak.WriteShort((ushort)GameClient.Player.Z);
                pak.WriteInt((uint)GameClient.Player.X);
                pak.WriteInt((uint)GameClient.Player.Y);
                pak.WriteShort(GameClient.Player.Heading);

                int flags = 0;
                Zone zone = GameClient.Player.CurrentZone;
                if (zone == null)
                {
                    return;
                }

                if (GameClient.Player.CurrentZone.IsDivingEnabled)
                {
                    flags = 0x80 | (GameClient.Player.IsUnderwater ? 0x01 : 0x00);
                }

                pak.WriteByte((byte)flags);

                pak.WriteByte(0x00);    // TODO Unknown (Instance ID: 0xB0-0xBA, 0xAA-0xAF)

                if (zone.IsDungeon)
                {
                    pak.WriteShort((ushort)(zone.XOffset / 0x2000));
                    pak.WriteShort((ushort)(zone.YOffset / 0x2000));
                }
                else
                {
                    pak.WriteShort(0);
                    pak.WriteShort(0);
                }

                // Dinberg - Changing to allow instances...
                pak.WriteShort(GameClient.Player.CurrentRegion.Skin);
                pak.WritePascalString(GameServer.Instance.Configuration.ServerNameShort); // new in 1.74, same as in SendLoginGranted
                pak.WriteByte(0x00); // TODO: unknown, new in 1.74
                SendTCP(pak);
            }
        }

        protected override void WriteGroupMemberUpdate(GSTCPPacketOut pak, bool updateIcons, GameLiving living)
        {
            base.WriteGroupMemberUpdate(pak, updateIcons, living);
            WriteGroupMemberMapUpdate(pak, living);
        }

        protected virtual void WriteGroupMemberMapUpdate(GSTCPPacketOut pak, GameLiving living)
        {
            bool sameRegion = living.CurrentRegion == GameClient.Player.CurrentRegion;
            if (sameRegion && living.CurrentSpeed != 0) // todo : find a better way to detect when player change coord
            {
                Zone zone = living.CurrentZone;
                if (zone == null)
                {
                    return;
                }

                pak.WriteByte((byte)(0x40 | living.GroupIndex));

                // Dinberg - ZoneSkinID for group members aswell.
                pak.WriteShort(zone.ZoneSkinID);
                pak.WriteShort((ushort)(living.X - zone.XOffset));
                pak.WriteShort((ushort)(living.Y - zone.YOffset));
            }
        }

        public override void SendRegionChanged()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            SendRegions();
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.RegionChanged)))
            {
                // Dinberg - Changing to allow instances...
                pak.WriteShort(GameClient.Player.CurrentRegion.Skin);

                // Dinberg:Instances - also need to continue the bluff here, with zoneSkinID, for
                // clientside positions of objects.
                pak.WriteShort(GameClient.Player.CurrentZone.ZoneSkinID); // Zone ID?
                pak.WriteShort(0x00); // ?
                pak.WriteShort(0x01); // cause region change ?
                pak.WriteByte(0x0C); // Server ID
                pak.WriteByte(0); // ?
                pak.WriteShort(0xFFBF); // ?
                SendTCP(pak);
            }
        }

        public override void SendSpellEffectAnimation(GameObject spellCaster, GameObject spellTarget, ushort spellid, ushort boltTime, bool noSound, byte success)
        {
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SpellEffectAnimation)))
            {
                pak.WriteShort((ushort)spellCaster.ObjectID);
                pak.WriteShort(spellid);
                pak.WriteShort((ushort)(spellTarget?.ObjectID ?? 0));
                pak.WriteShort(boltTime);
                pak.WriteByte((byte)(noSound ? 1 : 0));
                pak.WriteByte(success);
                SendTCP(pak);
            }
        }

        public override void CheckLengthHybridSkillsPacket(ref GSTCPPacketOut pak, ref int maxSkills, ref int first)
        {
            if (pak.Length > 1000)
            {
                pak.Position = 4;
                pak.WriteByte((byte)(maxSkills - first));
                pak.WriteByte(0x03); // subtype
                pak.WriteByte((byte)first);
                SendTCP(pak);
                pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate));
                pak.WriteByte(0x01); // subcode
                pak.WriteByte((byte)maxSkills); // number of entry
                pak.WriteByte(0x03); // subtype
                pak.WriteByte((byte)first);
                first = maxSkills;
            }

            maxSkills++;
        }

        public override void SendWarmapBonuses()
        {
            if (GameClient.Player == null)
            {
                return;
            }

            int albTowers = 0;
            int midTowers = 0;
            int hibTowers = 0;
            int albKeeps = 0;
            int midKeeps = 0;
            int hibKeeps = 0;
            int ownerDfTowers = 0;
            eRealm ownerDf = eRealm.None;
            foreach (AbstractGameKeep keep in GameServer.KeepManager.GetFrontierKeeps())
            {
                switch (keep.Realm)
                {
                    case eRealm.Albion:
                        if (keep is GameKeep)
                        {
                            albKeeps++;
                        }
                        else
                        {
                            albTowers++;
                        }

                        break;
                    case eRealm.Midgard:
                        if (keep is GameKeep)
                        {
                            midKeeps++;
                        }
                        else
                        {
                            midTowers++;
                        }

                        break;
                    case eRealm.Hibernia:
                        if (keep is GameKeep)
                        {
                            hibKeeps++;
                        }
                        else
                        {
                            hibTowers++;
                        }

                        break;
                }
            }

            if (albTowers > midTowers && albTowers > hibTowers)
            {
                ownerDf = eRealm.Albion;
                ownerDfTowers = albTowers;
            }
            else if (midTowers > albTowers && midTowers > hibTowers)
            {
                ownerDf = eRealm.Midgard;
                ownerDfTowers = midTowers;
            }
            else if (hibTowers > albTowers && hibTowers > midTowers)
            {
                ownerDf = eRealm.Hibernia;
                ownerDfTowers = hibTowers;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.WarmapBonuses)))
            {
                int realmKeeps = 0;
                int realmTowers = 0;
                switch (GameClient.Player.Realm)
                {
                    case eRealm.Albion:
                        realmKeeps = albKeeps;
                        realmTowers = albTowers;
                        break;
                    case eRealm.Midgard:
                        realmKeeps = midKeeps;
                        realmTowers = midTowers;
                        break;
                    case eRealm.Hibernia:
                        realmKeeps = hibKeeps;
                        realmTowers = hibTowers;
                        break;
                }

                pak.WriteByte((byte)realmKeeps);
                pak.WriteByte((byte)(((byte)RelicMgr.GetRelicCount(GameClient.Player.Realm, eRelicType.Magic)) << 4 | (byte)RelicMgr.GetRelicCount(GameClient.Player.Realm, eRelicType.Strength)));
                pak.WriteByte((byte)ownerDf);
                pak.WriteByte((byte)realmTowers);
                pak.WriteByte((byte)ownerDfTowers);
                SendTCP(pak);
            }
        }

        public override void SendLivingEquipmentUpdate(GameLiving living)
        {
            if (GameClient.Player == null || living.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.EquipmentUpdate)))
            {

                ICollection<InventoryItem> items = null;
                if (living.Inventory != null)
                {
                    items = living.Inventory.VisibleItems;
                }

                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteByte((byte)((living.IsCloakHoodUp ? 0x01 : 0x00) | (int)living.ActiveQuiverSlot)); // bit0 is hood up bit4 to 7 is active quiver

                pak.WriteByte(living.VisibleActiveWeaponSlots);
                if (items != null)
                {
                    pak.WriteByte((byte)items.Count);
                    foreach (InventoryItem item in items)
                    {
                        pak.WriteByte((byte)item.SlotPosition);

                        ushort model = (ushort)(item.Model & 0x1FFF);
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

                        if (item.SlotPosition > Slot.RANGED || item.SlotPosition < Slot.RIGHTHAND)
                        {
                            pak.WriteByte(item.Extension);
                        }

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

        public override void SendVampireEffect(GameLiving living, bool show)
        {
            if (GameClient.Player == null || living == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {

                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteByte(0x4); // Vampire (can fly)
                pak.WriteByte((byte)(show ? 0 : 1)); // 0-enable, 1-disable
                pak.WriteInt(0);

                SendTCP(pak);
            }
        }
    }
}
