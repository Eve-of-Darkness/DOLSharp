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
using DOL.Database;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using DOL.AI.Brain;
using DOL.GS.Behaviour;
using DOL.GS.Effects;
using DOL.GS.Housing;
using DOL.GS.Keeps;
using DOL.GS.PlayerTitles;
using DOL.GS.Quests;
using DOL.GS.RealmAbilities;
using DOL.GS.Spells;
using DOL.GS.Styles;
using DOL.Language;
using log4net;

namespace DOL.GS.PacketHandler
{
    [PacketLib(1109, GameClient.eClientVersion.Version1109)]
    public class PacketLib1109 : AbstractPacketLib, IPacketLib
    {
        private const int MaxPacketLength = 2048;

        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Client Version 1.109
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib1109(GameClient client)
            : base(client)
        {
        }

        // properties from 186
        /// <summary>
        /// The bow prepare animation
        /// </summary>
        public virtual int BowPrepare => 0x3E80;

        /// <summary>
        /// The bow shoot animation
        /// </summary>
        public virtual int BowShoot => 0x3E83;

        /// <summary>
        /// one dual weapon hit animation
        /// </summary>
        public virtual int OneDualWeaponHit => 0x3E81;

        /// <summary>
        /// both dual weapons hit animation
        /// </summary>
        public virtual int BothDualWeaponHit => 0x3E82;

        public virtual void SendWarlockChamberEffect(GamePlayer player)
        {
            // 173
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {

                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(3);

                SortedList sortList = new SortedList
                {
                    {1, null},
                    {2, null},
                    {3, null},
                    {4, null},
                    {5, null}
                };

                lock (player.EffectList)
                {
                    foreach (IGameEffect fx in player.EffectList)
                    {
                        if (fx is GameSpellEffect effect)
                        {
                            if (effect.SpellHandler.Spell != null && effect.SpellHandler.Spell.SpellType == "Chamber")
                            {
                                ChamberSpellHandler chamber = (ChamberSpellHandler)effect.SpellHandler;
                                sortList[chamber.EffectSlot] = effect;
                            }
                        }
                    }

                    foreach (GameSpellEffect effect in sortList.Values)
                    {
                        if (effect == null)
                        {
                            pak.WriteByte(0);
                        }
                        else
                        {
                            ChamberSpellHandler chamber = (ChamberSpellHandler)effect.SpellHandler;
                            if (chamber.PrimarySpell != null && chamber.SecondarySpell == null)
                            {
                                pak.WriteByte(3);
                            }
                            else if (chamber.PrimarySpell != null && chamber.SecondarySpell != null)
                            {
                                if (chamber.SecondarySpell.SpellType == "Lifedrain")
                                {
                                    pak.WriteByte(0x11);
                                }
                                else if (chamber.SecondarySpell.SpellType.IndexOf("SpeedDecrease", StringComparison.Ordinal) != -1)
                                {
                                    pak.WriteByte(0x33);
                                }
                                else if (chamber.SecondarySpell.SpellType == "PowerRegenBuff")
                                {
                                    pak.WriteByte(0x77);
                                }
                                else if (chamber.SecondarySpell.SpellType == "DirectDamage")
                                {
                                    pak.WriteByte(0x66);
                                }
                                else if (chamber.SecondarySpell.SpellType == "SpreadHeal")
                                {
                                    pak.WriteByte(0x55);
                                }
                                else if (chamber.SecondarySpell.SpellType == "Nearsight")
                                {
                                    pak.WriteByte(0x44);
                                }
                                else if (chamber.SecondarySpell.SpellType == "DamageOverTime")
                                {
                                    pak.WriteByte(0x22);
                                }
                            }
                        }
                    }
                }

                // pak.WriteByte(0x11);
                // pak.WriteByte(0x22);
                // pak.WriteByte(0x33);
                // pak.WriteByte(0x44);
                // pak.WriteByte(0x55);
                pak.WriteInt(0);

                foreach (GamePlayer plr in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player != plr)
                    {
                        plr.Client.PacketProcessor.SendTCP(pak);
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendVersionAndCryptKey()
        {
            // 168
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

        public virtual void SendLoginDenied(eLoginError et)
        {
            // 168
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

        public virtual void SendLoginGranted(byte color)
        {
            // 175
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.LoginGranted)))
            {
                pak.WriteByte(0x01); // isSI
                pak.WriteByte(ParseVersion((int)GameClient.Version, true));
                pak.WriteByte(ParseVersion((int)GameClient.Version, false));

                // pak.WriteByte(build);
                pak.WriteByte(0x00);
                pak.WritePascalString(GameClient.Account.Name);
                pak.WritePascalString(GameServer.Instance.Configuration.ServerNameShort); // server name
                pak.WriteByte(0x0C); // Server ID
                pak.WriteByte(color);
                pak.WriteByte(0x00);
                pak.WriteByte(0x00); // new in 1.75
                SendTCP(pak);
            }
        }

        public virtual void SendLoginGranted()
        {
            // 175
            // [Freya] Nidel: Can use realm button in character selection screen
            if (ServerProperties.Properties.ALLOW_ALL_REALMS || GameClient.Account.PrivLevel > (int)ePrivLevel.Player)
            {
                SendLoginGranted(1);
            }
            else
            {
                SendLoginGranted(GameServer.ServerRules.GetColorHandling(GameClient));
            }
        }

        public virtual void SendSessionID()
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SessionID)))
            {
                pak.WriteShortLowEndian((ushort)GameClient.SessionID);
                SendTCP(pak);
            }
        }

        public virtual void SendPingReply(ulong timestamp, ushort sequence)
        {
            // 168
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
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Realm)))
            {
                pak.WriteByte((byte)realm);
                SendTCP(pak);
            }
        }

        public virtual void SendCharacterOverview(eRealm realm)
        {
            // 1104
            if (realm < eRealm._FirstPlayerRealm || realm > eRealm._LastPlayerRealm)
            {
                throw new Exception($"CharacterOverview requested for unknown realm {realm}");
            }

            int firstSlot = (byte)realm * 100;

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterOverview)))
            {
                pak.FillString(GameClient.Account.Name, 24);

                if (GameClient.Account.Characters == null)
                {
                    pak.Fill(0x0, 1880);
                }
                else
                {
                    Dictionary<int, DOLCharacters> charsBySlot = new Dictionary<int, DOLCharacters>();
                    foreach (DOLCharacters c in GameClient.Account.Characters)
                    {
                        try
                        {
                            charsBySlot.Add(c.AccountSlot, c);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"SendCharacterOverview - Duplicate char in slot? Slot: {c.AccountSlot}, Account: {c.AccountName}", ex);
                        }
                    }

                    var itemsByOwnerId = new Dictionary<string, Dictionary<eInventorySlot, InventoryItem>>();

                    if (charsBySlot.Any())
                    {
                        var allItems = GameServer.Database.SelectObjects<InventoryItem>(
                                "`OwnerID` = @OwnerID AND `SlotPosition` >= @MinEquipable AND `SlotPosition` <= @MaxEquipable",
                                charsBySlot.Select(kv => new[]
                                {
                                    new QueryParameter("@OwnerID", kv.Value.ObjectId),
                                    new QueryParameter("@MinEquipable", (int) eInventorySlot.MinEquipable),
                                    new QueryParameter("@MaxEquipable", (int) eInventorySlot.MaxEquipable)
                                }))
                            .SelectMany(objs => objs)
                            .ToList();

                        foreach (InventoryItem item in allItems)
                        {
                            try
                            {
                                if (!itemsByOwnerId.ContainsKey(item.OwnerID))
                                {
                                    itemsByOwnerId.Add(item.OwnerID, new Dictionary<eInventorySlot, InventoryItem>());
                                }

                                itemsByOwnerId[item.OwnerID].Add((eInventorySlot)item.SlotPosition, item);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"SendCharacterOverview - Duplicate item on character? OwnerID: {item.OwnerID}, SlotPosition: {item.SlotPosition}, Account: {GameClient.Account.Name}", ex);
                            }
                        }
                    }

                    for (int i = firstSlot; i < (firstSlot + 10); i++)
                    {
                        if (!charsBySlot.TryGetValue(i, out var c))
                        {
                            pak.Fill(0x0, 188);
                        }
                        else
                        {
                            if (!itemsByOwnerId.TryGetValue(c.ObjectId, out var charItems))
                            {
                                charItems = new Dictionary<eInventorySlot, InventoryItem>();
                            }

                            byte extensionTorso = 0;
                            byte extensionGloves = 0;
                            byte extensionBoots = 0;

                            if (charItems.TryGetValue(eInventorySlot.TorsoArmor, out var item))
                            {
                                extensionTorso = item.Extension;
                            }

                            if (charItems.TryGetValue(eInventorySlot.HandsArmor, out item))
                            {
                                extensionGloves = item.Extension;
                            }

                            if (charItems.TryGetValue(eInventorySlot.FeetArmor, out item))
                            {
                                extensionBoots = item.Extension;
                            }

                            pak.Fill(0x00, 4);// new heading bytes in from 1.99 relocated in 1.104
                            pak.FillString(c.Name, 24);
                            pak.WriteByte(0x01);
                            pak.WriteByte(c.EyeSize);
                            pak.WriteByte(c.LipSize);
                            pak.WriteByte(c.EyeColor);
                            pak.WriteByte(c.HairColor);
                            pak.WriteByte(c.FaceType);
                            pak.WriteByte(c.HairStyle);
                            pak.WriteByte((byte)((extensionBoots << 4) | extensionGloves));
                            pak.WriteByte((byte)((extensionTorso << 4) | (c.IsCloakHoodUp ? 0x1 : 0x0)));
                            pak.WriteByte(c.CustomisationStep); // 1 = auto generate config, 2= config ended by player, 3= enable config to player
                            pak.WriteByte(c.MoodType);
                            pak.Fill(0x0, 13); // 0 String

                            string locationDescription = string.Empty;
                            Region region = WorldMgr.GetRegion((ushort)c.Region);
                            if (region != null)
                            {
                                locationDescription = GameClient.GetTranslatedSpotDescription(region, c.Xpos, c.Ypos, c.Zpos);
                            }

                            pak.FillString(locationDescription, 24);

                            string classname = string.Empty;
                            if (c.Class != 0)
                            {
                                classname = ((eCharacterClass)c.Class).ToString();
                            }

                            pak.FillString(classname, 24);

                            string racename = GameClient.RaceToTranslatedName(c.Race, c.Gender);
                            pak.FillString(racename, 24);

                            pak.WriteByte((byte)c.Level);
                            pak.WriteByte((byte)c.Class);
                            pak.WriteByte((byte)c.Realm);
                            pak.WriteByte((byte)((((c.Race & 0x10) << 2) + (c.Race & 0x0F)) | (c.Gender << 4))); // race max value can be 0x1F
                            pak.WriteShortLowEndian((ushort)c.CurrentModel);
                            pak.WriteByte((byte)c.Region);
                            if (region == null || (int)GameClient.ClientType > region.Expansion)
                            {
                                pak.WriteByte(0x00);
                            }
                            else
                            {
                                pak.WriteByte((byte)(region.Expansion + 1)); // 0x04-Cata zone, 0x05 - DR zone
                            }

                            pak.WriteInt(0x0); // Internal database ID
                            pak.WriteByte((byte)c.Strength);
                            pak.WriteByte((byte)c.Dexterity);
                            pak.WriteByte((byte)c.Constitution);
                            pak.WriteByte((byte)c.Quickness);
                            pak.WriteByte((byte)c.Intelligence);
                            pak.WriteByte((byte)c.Piety);
                            pak.WriteByte((byte)c.Empathy);
                            pak.WriteByte((byte)c.Charisma);

                            charItems.TryGetValue(eInventorySlot.RightHandWeapon, out var rightHandWeapon);
                            charItems.TryGetValue(eInventorySlot.LeftHandWeapon, out var leftHandWeapon);
                            charItems.TryGetValue(eInventorySlot.TwoHandWeapon, out var twoHandWeapon);
                            charItems.TryGetValue(eInventorySlot.DistanceWeapon, out var distanceWeapon);

                            charItems.TryGetValue(eInventorySlot.HeadArmor, out var helmet);
                            charItems.TryGetValue(eInventorySlot.HandsArmor, out var gloves);
                            charItems.TryGetValue(eInventorySlot.FeetArmor, out var boots);
                            charItems.TryGetValue(eInventorySlot.TorsoArmor, out var torso);
                            charItems.TryGetValue(eInventorySlot.Cloak, out var cloak);
                            charItems.TryGetValue(eInventorySlot.LegsArmor, out var legs);
                            charItems.TryGetValue(eInventorySlot.ArmsArmor, out var arms);

                            pak.WriteShortLowEndian((ushort)(helmet?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(gloves?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(boots?.Model ?? 0));

                            ushort rightHandColor = 0;
                            if (rightHandWeapon != null)
                            {
                                rightHandColor = (ushort)(rightHandWeapon.Emblem != 0 ? rightHandWeapon.Emblem : rightHandWeapon.Color);
                            }

                            pak.WriteShortLowEndian(rightHandColor);

                            pak.WriteShortLowEndian((ushort)(torso?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(cloak?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(legs?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(arms?.Model ?? 0));

                            ushort helmetColor = 0;
                            if (helmet != null)
                            {
                                helmetColor = (ushort)(helmet.Emblem != 0 ? helmet.Emblem : helmet.Color);
                            }

                            pak.WriteShortLowEndian(helmetColor);

                            ushort glovesColor = 0;
                            if (gloves != null)
                            {
                                glovesColor = (ushort)(gloves.Emblem != 0 ? gloves.Emblem : gloves.Color);
                            }

                            pak.WriteShortLowEndian(glovesColor);

                            ushort bootsColor = 0;
                            if (boots != null)
                            {
                                bootsColor = (ushort)(boots.Emblem != 0 ? boots.Emblem : boots.Color);
                            }

                            pak.WriteShortLowEndian(bootsColor);

                            ushort leftHandWeaponColor = 0;
                            if (leftHandWeapon != null)
                            {
                                leftHandWeaponColor = (ushort)(leftHandWeapon.Emblem != 0 ? leftHandWeapon.Emblem : leftHandWeapon.Color);
                            }

                            pak.WriteShortLowEndian(leftHandWeaponColor);

                            ushort torsoColor = 0;
                            if (torso != null)
                            {
                                torsoColor = (ushort)(torso.Emblem != 0 ? torso.Emblem : torso.Color);
                            }

                            pak.WriteShortLowEndian(torsoColor);

                            ushort cloakColor = 0;
                            if (cloak != null)
                            {
                                cloakColor = (ushort)(cloak.Emblem != 0 ? cloak.Emblem : cloak.Color);
                            }

                            pak.WriteShortLowEndian(cloakColor);

                            ushort legsColor = 0;
                            if (legs != null)
                            {
                                legsColor = (ushort)(legs.Emblem != 0 ? legs.Emblem : legs.Color);
                            }

                            pak.WriteShortLowEndian(legsColor);

                            ushort armsColor = 0;
                            if (arms != null)
                            {
                                armsColor = (ushort)(arms.Emblem != 0 ? arms.Emblem : arms.Color);
                            }

                            pak.WriteShortLowEndian(armsColor);

                            // weapon models
                            pak.WriteShortLowEndian((ushort)(rightHandWeapon?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(leftHandWeapon?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(twoHandWeapon?.Model ?? 0));
                            pak.WriteShortLowEndian((ushort)(distanceWeapon?.Model ?? 0));

                            if (c.ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.TwoHanded)
                            {
                                pak.WriteByte(0x02);
                                pak.WriteByte(0x02);
                            }
                            else if (c.ActiveWeaponSlot == (byte)GameLiving.eActiveWeaponSlot.Distance)
                            {
                                pak.WriteByte(0x03);
                                pak.WriteByte(0x03);
                            }
                            else
                            {
                                byte righthand = 0xFF;
                                byte lefthand = 0xFF;

                                if (rightHandWeapon != null)
                                {
                                    righthand = 0x00;
                                }

                                if (leftHandWeapon != null)
                                {
                                    lefthand = 0x01;
                                }

                                pak.WriteByte(righthand);
                                pak.WriteByte(lefthand);
                            }

                            if (region == null || region.Expansion != 1)
                            {
                                pak.WriteByte(0x00);
                            }
                            else
                            {
                                pak.WriteByte(0x01); // 0x01=char in SI zone, classic client can't "play"
                            }

                            pak.WriteByte((byte)c.Constitution);
                        }
                    }
                }

                pak.Fill(0x0, 94);
                SendTCP(pak);
            }
        }

        public virtual void SendDupNameCheckReply(string name, bool nameExists)
        {
            // 1104
            if (GameClient?.Account == null)
            {
                return;
            }

            // This presents the user with Name Not Allowed which may not be correct but at least it prevents duplicate char creation
            // - tolakram
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DupNameCheckReply)))
            {
                pak.FillString(name, 30);
                pak.FillString(GameClient.Account.Name, 24);
                pak.WriteByte((byte)(nameExists ? 0x1 : 0x0));
                pak.Fill(0x0, 3);
                SendTCP(pak);
            }
        }

        public virtual void SendBadNameCheckReply(string name, bool bad)
        {
            // 168
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
            // 168
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
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterCreateReply)))
            {
                pak.FillString(name, 24);
                SendTCP(pak);
            }
        }

        public virtual void SendCharStatsUpdate()
        {
            // 175
            if (GameClient.Player == null)
            {
                return;
            }

            eStat[] updateStats =
            {
                eStat.STR,
                eStat.DEX,
                eStat.CON,
                eStat.QUI,
                eStat.INT,
                eStat.PIE,
                eStat.EMP,
                eStat.CHR,
            };

            int[] baseStats = new int[updateStats.Length];
            int[] modStats = new int[updateStats.Length];
            int[] itemCaps = new int[updateStats.Length];

            int itemCap = (int)(GameClient.Player.Level * 1.5);
            int bonusCap = GameClient.Player.Level / 2 + 1;
            for (int i = 0; i < updateStats.Length; i++)
            {
                int cap = itemCap;
                switch ((eProperty)updateStats[i])
                {
                    case eProperty.Strength:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.StrCapBonus];
                        break;
                    case eProperty.Dexterity:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.DexCapBonus];
                        break;
                    case eProperty.Constitution:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.ConCapBonus];
                        break;
                    case eProperty.Quickness:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.QuiCapBonus];
                        break;
                    case eProperty.Intelligence:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.IntCapBonus];
                        break;
                    case eProperty.Piety:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.PieCapBonus];
                        break;
                    case eProperty.Charisma:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.ChaCapBonus];
                        break;
                    case eProperty.Empathy:
                        cap += GameClient.Player.ItemBonus[(int)eProperty.EmpCapBonus];
                        break;
                }

                if (updateStats[i] == GameClient.Player.CharacterClass.ManaStat)
                {
                    cap += GameClient.Player.ItemBonus[(int)eProperty.AcuCapBonus];
                }

                itemCaps[i] = Math.Min(cap, itemCap + bonusCap);
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.StatsUpdate)))
            {

                // base
                for (int i = 0; i < updateStats.Length; i++)
                {
                    baseStats[i] = GameClient.Player.GetBaseStat(updateStats[i]);

                    if (updateStats[i] == eStat.CON)
                    {
                        baseStats[i] -= GameClient.Player.TotalConstitutionLostAtDeath;
                    }

                    pak.WriteShort((ushort)baseStats[i]);
                }

                pak.WriteShort(0);

                // buffs/debuffs only; remove base, item bonus, RA bonus, class bonus
                for (int i = 0; i < updateStats.Length; i++)
                {
                    modStats[i] = GameClient.Player.GetModified((eProperty)updateStats[i]);

                    int abilityBonus = GameClient.Player.AbilityBonus[(int)updateStats[i]];

                    int acuityItemBonus = 0;
                    if (updateStats[i] == GameClient.Player.CharacterClass.ManaStat)
                    {
                        if (GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Scout && GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Hunter && GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Ranger)
                        {
                            abilityBonus += GameClient.Player.AbilityBonus[(int)eProperty.Acuity];

                            if (GameClient.Player.CharacterClass.ClassType != eClassType.PureTank)
                            {
                                acuityItemBonus = GameClient.Player.ItemBonus[(int)eProperty.Acuity];
                            }
                        }
                    }

                    int buff = modStats[i] - baseStats[i];
                    buff -= abilityBonus;
                    buff -= Math.Min(itemCaps[i], GameClient.Player.ItemBonus[(int)updateStats[i]] + acuityItemBonus);

                    pak.WriteShort((ushort)buff);
                }

                pak.WriteShort(0);

                // item bonuses
                foreach (eStat stat in updateStats)
                {
                    int acuityItemBonus = 0;

                    if (stat == GameClient.Player.CharacterClass.ManaStat)
                    {
                        if (GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Scout && GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Hunter && GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Ranger)
                        {

                            if (GameClient.Player.CharacterClass.ClassType != eClassType.PureTank)
                            {
                                acuityItemBonus = GameClient.Player.ItemBonus[(int)eProperty.Acuity];
                            }
                        }
                    }

                    pak.WriteShort((ushort)(GameClient.Player.ItemBonus[(int)stat] + acuityItemBonus));
                }

                pak.WriteShort(0);

                // item caps
                for (int i = 0; i < updateStats.Length; i++)
                {
                    pak.WriteByte((byte)itemCaps[i]);
                }

                pak.WriteByte(0);

                // RA bonuses
                foreach (eStat stat in updateStats)
                {
                    int acuityItemBonus = 0;
                    if (GameClient.Player.CharacterClass.ClassType != eClassType.PureTank && (int)stat == (int)GameClient.Player.CharacterClass.ManaStat)
                    {
                        if (GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Scout && GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Hunter && GameClient.Player.CharacterClass.ID != (int)eCharacterClass.Ranger)
                        {
                            acuityItemBonus = GameClient.Player.AbilityBonus[(int)eProperty.Acuity];
                        }
                    }

                    pak.WriteByte((byte)(GameClient.Player.AbilityBonus[(int)stat] + acuityItemBonus));
                }

                pak.WriteByte(0);

                // Why don't we and mythic use this class bonus byte?
                // pak.Fill(0, 9);
                // if (m_gameClient.Player.CharacterClass.ID == (int)eCharacterClass.Vampiir)
                //  pak.WriteByte((byte)(m_gameClient.Player.Level - 5)); // Vampire bonuses
                // else
                pak.WriteByte(0x00); // FF if resists packet
                pak.WriteByte((byte)GameClient.Player.TotalConstitutionLostAtDeath);
                pak.WriteShort((ushort)GameClient.Player.MaxHealth);
                pak.WriteShort(0);

                SendTCP(pak);
            }
        }

        public virtual void SendCharResistsUpdate()
        {
            // 175
            if (GameClient.Player == null)
            {
                return;
            }

            eResist[] updateResists =
            {
                eResist.Crush,
                eResist.Slash,
                eResist.Thrust,
                eResist.Heat,
                eResist.Cold,
                eResist.Matter,
                eResist.Body,
                eResist.Spirit,
                eResist.Energy,
            };

            int[] racial = new int[updateResists.Length];
            int[] caps = new int[updateResists.Length];

            int cap = (GameClient.Player.Level >> 1) + 1;
            for (int i = 0; i < updateResists.Length; i++)
            {
                caps[i] = cap;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.StatsUpdate)))
            {

                // racial resists
                for (int i = 0; i < updateResists.Length; i++)
                {
                    racial[i] = SkillBase.GetRaceResist(GameClient.Player.Race, updateResists[i]);
                    pak.WriteShort((ushort)racial[i]);
                }

                // buffs/debuffs only; remove base, item bonus, RA bonus, race bonus
                for (int i = 0; i < updateResists.Length; i++)
                {
                    int mod = GameClient.Player.GetModified((eProperty)updateResists[i]);
                    int buff = mod - racial[i] - GameClient.Player.AbilityBonus[(int)updateResists[i]] - Math.Min(caps[i], GameClient.Player.ItemBonus[(int)updateResists[i]]);
                    pak.WriteShort((ushort)buff);
                }

                // item bonuses
                for (int i = 0; i < updateResists.Length; i++)
                {
                    pak.WriteShort((ushort)GameClient.Player.ItemBonus[(int)updateResists[i]]);
                }

                // item caps
                for (int i = 0; i < updateResists.Length; i++)
                {
                    pak.WriteByte((byte)caps[i]);
                }

                // RA bonuses
                for (int i = 0; i < updateResists.Length; i++)
                {
                    pak.WriteByte((byte)GameClient.Player.AbilityBonus[(int)updateResists[i]]);
                }

                pak.WriteByte(0xFF); // FF if resists packet
                pak.WriteByte(0);
                pak.WriteShort(0);
                pak.WriteShort(0);

                SendTCP(pak);
            }
        }

        public virtual void SendRegions()
        {
            // 173
            if (GameClient.Player != null)
            {
                if (!GameClient.Socket.Connected)
                {
                    return;
                }

                Region region = WorldMgr.GetRegion(GameClient.Player.CurrentRegionID);
                if (region == null)
                {
                    return;
                }

                using (GSTCPPacketOut pak = new GSTCPPacketOut(0xB1))
                {
                    // pak.WriteByte((byte)((region.Expansion + 1) << 4)); // Must be expansion
                    pak.WriteByte(0); // but this packet sended when client in old region. but this field must show expanstion for jump destanation region
                    // Dinberg - trying to get instances to work.
                    pak.WriteByte((byte)region.Skin); // This was pak.WriteByte((byte)region.ID);
                    pak.Fill(0, 20);
                    pak.FillString(region.ServerPort.ToString(), 5);
                    pak.FillString(region.ServerPort.ToString(), 5);
                    string ip = region.ServerIP;
                    if (ip == "any" || ip == "0.0.0.0" || ip == "127.0.0.1" || ip.StartsWith("10.13.") || ip.StartsWith("192.168."))
                    {
                        ip = ((IPEndPoint)GameClient.Socket.LocalEndPoint).Address.ToString();
                    }

                    pak.FillString(ip, 20);
                    SendTCP(pak);
                }
            }
            else
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
                    using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ClientRegions)))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            while (index < count && (int)GameClient.ClientType <= entries[index].expansion)
                            {
                                index++;
                            }

                            if (index >= count)
                            { // If we have no more entries
                                pak.Fill(0x0, 52);
                            }
                            else
                            {
                                pak.WriteByte((byte)(++num));
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
        }

        public virtual void SendGameOpenReply()
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.GameOpenReply)))
            {
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerPositionAndObjectID()
        {
            // 174
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

        public virtual void SendPlayerJump(bool headingOnly)
        {
            // 168
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
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterInitFinished)))
            {
                pak.WriteByte(mobs);
                SendTCP(pak);
            }
        }

        public virtual void SendUDPInitReply()
        {
            // 168
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
            // 168
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
            // 173
            if (GameClient.ClientState == GameClient.eClientState.CharScreen)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Message)))
            {
                pak.WriteShort(0xFFFF);
                pak.WriteShort((ushort)GameClient.SessionID);
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

                pak.WriteString(str + msg);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerCreate(GamePlayer playerToCreate)
        {
            // 180
            if (playerToCreate == null)
            {
                if (Log.IsErrorEnabled)
                {
                    Log.Error("SendPlayerCreate: playerToCreate == null");
                }

                return;
            }

            if (GameClient.Player == null)
            {
                if (Log.IsErrorEnabled)
                {
                    Log.Error("SendPlayerCreate: m_gameClient.Player == null");
                }

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

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlayerCreate172)))
            {

                pak.WriteShort((ushort)playerToCreate.Client.SessionID);
                pak.WriteShort((ushort)playerToCreate.ObjectID);
                pak.WriteShort(playerToCreate.Model);
                pak.WriteShort((ushort)playerToCreate.Z);

                // Dinberg:Instances - send out the 'fake' zone ID to the client for positioning purposes.
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

                if (playerToCreate.CharacterClass.ID == (int)eCharacterClass.Vampiir)
                {
                    flags |= 0x40; // Vamp fly
                }

                pak.WriteByte((byte)flags);
                pak.WriteByte(0x00); // new in 1.74

                pak.WritePascalString(GameServer.ServerRules.GetPlayerName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerGuildName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerLastName(GameClient.Player, playerToCreate));

                // RR 12 / 13
                pak.WritePascalString(GameServer.ServerRules.GetPlayerPrefixName(GameClient.Player, playerToCreate));
                pak.WritePascalString(GameServer.ServerRules.GetPlayerTitle(GameClient.Player, playerToCreate)); // new in 1.74, NewTitle
                if (playerToCreate.IsOnHorse)
                {
                    pak.WriteByte(playerToCreate.ActiveHorse.ID);
                    if (playerToCreate.ActiveHorse.BardingColor == 0 && playerToCreate.ActiveHorse.Barding != 0 && playerToCreate.Guild != null)
                    {
                        int newGuildBitMask = (playerToCreate.Guild.Emblem & 0x010000) >> 9;
                        pak.WriteByte((byte)(playerToCreate.ActiveHorse.Barding | newGuildBitMask));
                        pak.WriteShortLowEndian((ushort)playerToCreate.Guild.Emblem);
                    }
                    else
                    {
                        pak.WriteByte(playerToCreate.ActiveHorse.Barding);
                        pak.WriteShort(playerToCreate.ActiveHorse.BardingColor);
                    }

                    pak.WriteByte(playerToCreate.ActiveHorse.Saddle);
                    pak.WriteByte(playerToCreate.ActiveHorse.SaddleColor);
                }
                else
                {
                    pak.WriteByte(0); // trailing zero
                }

                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(playerToCreate.CurrentRegionID, (ushort)playerToCreate.ObjectID)] = GameTimer.GetTickCount();

            SendObjectGuildID(playerToCreate, playerToCreate.Guild); // used for nearest friendly/enemy object buttons and name colors on PvP server

            if (playerToCreate.GuildBanner != null)
            {
                SendRvRGuildBanner(playerToCreate, true);
            }
        }

        public virtual void SendObjectGuildID(GameObject obj, Guild guild)
        {
            // 168
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

        public virtual void SendPlayerQuit(bool totalOut)
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Quit)))
            {
                pak.WriteByte((byte)(totalOut ? 0x01 : 0x00));
                pak.WriteByte(GameClient.Player?.Level ?? 0);

                SendTCP(pak);
            }
        }

        public virtual void SendObjectRemove(GameObject obj)
        {
            // 168
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
            // 176
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
                if (obj is Keeps.GameKeepBanner)
                {
                    flag |= 0x08;
                }

                if (obj is GameStaticItemTimed timed && GameClient.Player != null && timed.IsOwner(GameClient.Player))
                {
                    flag |= 0x04;
                }

                pak.WriteShort((ushort)flag);
                if (obj is GameStaticItem staticItem)
                {
                    int newEmblemBitMask = (staticItem.Emblem & 0x010000) << 9;
                    pak.WriteInt((uint)newEmblemBitMask);// TODO other bits
                }
                else
                {
                    pak.WriteInt(0);
                }

                string name = obj.Name;
                if (obj is GameStaticItem gameStaticItem)
                {
                    var translation = GameClient.GetTranslation(gameStaticItem);
                    if (translation != null)
                    {
                        if (gameStaticItem is WorldInventoryItem)
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

                pak.WritePascalString(name.Length > 48 ? name.Substring(0, 48) : name);

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
            // 168
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
            // 168
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
            // 168
            SendModelAndSizeChange((ushort)obj.ObjectID, newModel, newSize);
        }

        public virtual void SendModelAndSizeChange(ushort objectId, ushort newModel, byte newSize)
        {
            // 168
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
            // 168
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
            // 171
            if (GameClient.Player == null || npc.IsVisibleTo(GameClient.Player) == false)
            {
                return;
            }

            // Added by Suncheck - Mines are not shown to enemy players
            if (npc is GameMine mine)
            {
                if (GameServer.ServerRules.IsAllowedToAttack(mine.Owner, GameClient.Player, true))
                {
                    return;
                }
            }

            if (npc is GameMovingObject o)
            {
                SendMovingObjectCreate(o);
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.NPCCreate)))
            {
                int speed = 0;
                ushort speedZ = 0;

                if (!npc.IsAtTargetPosition)
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
                byte level = npc.GetDisplayLevel(GameClient.Player);
                if ((npc.Flags & GameNPC.eFlags.STATUE) != 0)
                {
                    level |= 0x80;
                }

                pak.WriteByte(level);

                byte flags = (byte)(GameServer.ServerRules.GetLivingRealm(GameClient.Player, npc) << 6);
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

                if ((npc.Flags & GameNPC.eFlags.TORCH) != 0)
                {
                    flags |= 0x04;
                }

                pak.WriteByte(flags);
                pak.WriteByte(0x20); // TODO this is the default maxstick distance

                string add = string.Empty;
                byte flags2 = 0x00;
                if (npc.Brain is IControlledBrain)
                {
                    flags2 |= 0x80; // have Owner
                }

                if ((npc.Flags & GameNPC.eFlags.CANTTARGET) != 0)
                {
                    if (GameClient.Account.PrivLevel > 1)
                    {
                        add += "-DOR"; // indicates DOR flag for GMs
                    }
                    else
                    {
                        flags2 |= 0x01;
                    }
                }

                if ((npc.Flags & GameNPC.eFlags.DONTSHOWNAME) != 0)
                {
                    if (GameClient.Account.PrivLevel > 1)
                    {
                        add += "-NON"; // indicates NON flag for GMs
                    }
                    else
                    {
                        flags2 |= 0x02;
                    }
                }

                if ((npc.Flags & GameNPC.eFlags.STEALTH) > 0)
                {
                    flags2 |= 0x04;
                }

                eQuestIndicator questIndicator = npc.GetQuestIndicator(GameClient.Player);

                if (questIndicator == eQuestIndicator.Available)
                {
                    flags2 |= 0x08;// hex 8 - quest available
                }

                if (questIndicator == eQuestIndicator.Finish)
                {
                    flags2 |= 0x10;// hex 16 - quest finish
                }

                // flags2 |= 0x20;//hex 32 - water mob?
                // flags2 |= 0x40;//hex 64 - unknown
                // flags2 |= 0x80;//hex 128 - has owner
                pak.WriteByte(flags2); // flags 2

                byte flags3 = 0x00;
                if (questIndicator == eQuestIndicator.Lesson)
                {
                    flags3 |= 0x01;
                }

                if (questIndicator == eQuestIndicator.Lore)
                {
                    flags3 |= 0x02;
                }

                pak.WriteByte(flags3); // new in 1.71 (region instance ID from StoC_0x20) OR flags 3?
                pak.WriteShort(0x00); // new in 1.71 unknown

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
                pak.WritePascalString(guildName.Length > 47 ? guildName.Substring(0, 47) : guildName);
                pak.WriteByte(0x00);
                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(npc.CurrentRegionID, (ushort)npc.ObjectID)] = 0;
        }

        public virtual void SendLivingEquipmentUpdate(GameLiving living)
        {
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.EquipmentUpdate)))
            {

                ICollection<InventoryItem> items = null;
                if (living.Inventory != null)
                {
                    items = living.Inventory.VisibleItems;
                }

                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteByte(living.VisibleActiveWeaponSlots);
                pak.WriteByte((byte)living.CurrentSpeed); // new in 189b+, speed
                pak.WriteByte((byte)((living.IsCloakInvisible ? 0x01 : 0x00) | (living.IsHelmInvisible ? 0x02 : 0x00))); // new in 189b+, cloack/helm visibility
                pak.WriteByte((byte)((living.IsCloakHoodUp ? 0x01 : 0x00) | (int)living.ActiveQuiverSlot)); // bit0 is hood up bit4 to 7 is active quiver

                if (items != null)
                {
                    pak.WriteByte((byte)items.Count);
                    foreach (InventoryItem item in items)
                    {
                        ushort model = (ushort)(item.Model & 0x1FFF);
                        int slot = item.SlotPosition;

                        // model = GetModifiedModel(model);
                        int texture = item.Emblem != 0 ? item.Emblem : item.Color;
                        if (item.SlotPosition == Slot.LEFTHAND || item.SlotPosition == Slot.CLOAK) // for test only cloack and shield
                        {
                            slot = slot | ((texture & 0x010000) >> 9); // slot & 0x80 if new emblem
                        }

                        pak.WriteByte((byte)slot);
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
                            pak.WriteByte((byte)item.Effect);
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
            // 174
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

        public virtual void SendUpdatePoints()
        {
            // 190
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
                pak.WriteLongLowEndian((ulong)GameClient.Player.Experience);
                pak.WriteLongLowEndian((ulong)GameClient.Player.ExperienceForNextLevel);
                pak.WriteLongLowEndian(0);// champExp
                pak.WriteLongLowEndian(0);// champExpNextLevel
                SendTCP(pak);
            }
        }

        public virtual void SendUpdateMoney()
        {
            // 168
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
            // 168
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

        public virtual void SendDelveInfo(string info)
        {
            // 168
        }


        public virtual void SendCombatAnimation(GameObject attacker, GameObject defender, ushort weaponId, ushort shieldId, int style, byte stance, byte result, byte targetHealthPercent)
        {
            // 168
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

        public virtual void SendStatusUpdate()
        {
            // 168
            if (GameClient.Player == null)
            {
                return;
            }

            SendStatusUpdate((byte)(GameClient.Player.IsSitting ? 0x02 : 0x00));
        }

        public virtual void SendStatusUpdate(byte sittingFlag)
        {
            // 190
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.CharacterStatusUpdate)))
            {
                pak.WriteByte(GameClient.Player.HealthPercent);
                pak.WriteByte(GameClient.Player.ManaPercent);
                pak.WriteByte(sittingFlag);
                pak.WriteByte(GameClient.Player.EndurancePercent);
                pak.WriteByte(GameClient.Player.ConcentrationPercent);

                pak.WriteByte(0);// unk
                pak.WriteShort((ushort)GameClient.Player.MaxMana);
                pak.WriteShort((ushort)GameClient.Player.MaxEndurance);
                pak.WriteShort((ushort)GameClient.Player.MaxConcentration);
                pak.WriteShort((ushort)GameClient.Player.MaxHealth);
                pak.WriteShort((ushort)GameClient.Player.Health);
                pak.WriteShort((ushort)GameClient.Player.Endurance);
                pak.WriteShort((ushort)GameClient.Player.Mana);
                pak.WriteShort((ushort)GameClient.Player.Concentration);
                SendTCP(pak);
            }
        }

        public virtual void SendSpellCastAnimation(GameLiving spellCaster, ushort spellId, ushort castingTime)
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SpellCastAnimation)))
            {
                pak.WriteShort((ushort)spellCaster.ObjectID);
                pak.WriteShort(spellId);
                pak.WriteShort(castingTime);
                pak.WriteShort(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendSpellEffectAnimation(GameObject spellCaster, GameObject spellTarget, ushort spellid, ushort boltTime, bool noSound, byte success)
        {
            // 174
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

        public virtual void SendRiding(GameObject rider, GameObject steed, bool dismount)
        {
            // 168
            int slot = 0;
            if (steed is GameNPC npc && rider is GamePlayer && dismount == false)
            {
                slot = npc.RiderSlot((GamePlayer)rider);
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
            // 176
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.FindGroupUpdate)))
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

                        // Dinberg:Instances - We use ZoneSkinID to bluff our way to victory and
                        // trick the client for positioning objects (as IDs are hard coded).
                        pak.WriteShort(player.CurrentZone?.ZoneSkinID ?? 0);

                        pak.WriteByte(0); // duration
                        pak.WriteByte(0); // objective
                        pak.WriteByte(0);
                        pak.WriteByte(0);
                        pak.WriteByte((byte)(player.Group != null ? 1 : 0));
                        pak.WriteByte(0);
                    }
                }
                else
                {
                    pak.WriteByte(0);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendGroupInviteCommand(GamePlayer invitingPlayer, string inviteMessage)
        { 
            // 168
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

        public virtual void SendDialogBox(eDialogCode code, ushort data1, ushort data2, ushort data3, ushort data4,
            eDialogType type, bool autoWrapText, string message)
        {
            // 168
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
            // 168
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
            // 168
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
            // 168
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

        public virtual void SendGuildLeaveCommand(GamePlayer invitingPlayer, string inviteMessage)
        {
            // 168
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

        public virtual void SendGuildInviteCommand(GamePlayer invitingPlayer, string inviteMessage)
        {
            // 168
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

        public virtual void SendQuestOfferWindow(GameNPC questNpc, GamePlayer player, RewardQuest quest)
        {
            // 187
            SendQuestWindow(questNpc, player, quest, true);
        }

        protected virtual void SendQuestWindow(GameNPC questNpc, GamePlayer player, RewardQuest quest, bool offer)
        {
            // 187
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                ushort questId = QuestMgr.GetIDForQuestType(quest.GetType());
                pak.WriteShort(offer ? (byte)0x22 : (byte)0x21); // Dialog
                pak.WriteShort(questId);
                pak.WriteShort((ushort)questNpc.ObjectID);
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(offer ? (byte)0x02 : (byte)0x01); // Accept/Decline or Finish/Not Yet
                pak.WriteByte(0x01); // Wrap
                pak.WritePascalString(quest.Name);

                pak.WritePascalString(quest.Summary.Length > 255 ? quest.Summary.Substring(0, 255) : quest.Summary);

                if (offer)
                {
                    if (quest.Story.Length > (ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH)
                    {
                        pak.WriteShort((ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH);
                        pak.WriteStringBytes(quest.Story.Substring(0, (ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH));
                    }
                    else
                    {
                        pak.WriteShort((ushort)quest.Story.Length);
                        pak.WriteStringBytes(quest.Story);
                    }
                }
                else
                {
                    if (quest.Conclusion.Length > (ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH)
                    {
                        pak.WriteShort((ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH);
                        pak.WriteStringBytes(quest.Conclusion.Substring(0, (ushort)ServerProperties.Properties.MAX_REWARDQUEST_DESCRIPTION_LENGTH));
                    }
                    else
                    {
                        pak.WriteShort((ushort)quest.Conclusion.Length);
                        pak.WriteStringBytes(quest.Conclusion);
                    }
                }

                pak.WriteShort(questId);
                pak.WriteByte((byte)quest.Goals.Count); // #goals count
                foreach (RewardQuest.QuestGoal goal in quest.Goals)
                {
                    pak.WritePascalString($"{goal.Description}\r");
                }

                pak.WriteByte((byte)quest.Level);
                pak.WriteByte((byte)quest.Rewards.MoneyPercent);
                pak.WriteByte((byte)quest.Rewards.ExperiencePercent(player));
                pak.WriteByte((byte)quest.Rewards.BasicItems.Count);
                foreach (ItemTemplate reward in quest.Rewards.BasicItems)
                {
                    WriteTemplateData(pak, reward, 1);
                }

                pak.WriteByte((byte)quest.Rewards.ChoiceOf);
                pak.WriteByte((byte)quest.Rewards.OptionalItems.Count);
                foreach (ItemTemplate reward in quest.Rewards.OptionalItems)
                {
                    WriteTemplateData(pak, reward, 1);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendQuestRewardWindow(GameNPC questNpc, GamePlayer player, RewardQuest quest)
        {
            // 187
            SendQuestWindow(questNpc, player, quest, false);
        }

        public virtual void SendQuestOfferWindow(GameNPC questNpc, GamePlayer player, DataQuest quest)
        {
            // 194
            SendQuestWindow(questNpc, player, quest, true);
        }

        private const ushort MaxStoryLength = 1000;   // Via trial and error, 1.108 client.
                                                      // Often will cut off text around 990 but longer strings do not result in any errors. -Tolakram
        protected virtual void SendQuestWindow(GameNPC questNpc, GamePlayer player, DataQuest quest, bool offer)
        {
            // 194
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.Dialog)))
            {
                ushort questId = quest.ClientQuestId;
                pak.WriteShort(offer ? (byte)0x22 : (byte)0x21); // Dialog
                pak.WriteShort(questId);
                pak.WriteShort((ushort)questNpc.ObjectID);
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(0x00); // unknown
                pak.WriteByte(offer ? (byte)0x02 : (byte)0x01); // Accept/Decline or Finish/Not Yet
                pak.WriteByte(0x01); // Wrap
                pak.WritePascalString(quest.Name);

                string personalizedSummary = BehaviourUtils.GetPersonalizedMessage(quest.Description, player);
                if (personalizedSummary.Length > 255)
                {
                    pak.WritePascalString(personalizedSummary.Substring(0, 255)); // Summary is max 255 bytes or client will crash !
                }
                else
                {
                    pak.WritePascalString(personalizedSummary);
                }

                if (offer)
                {
                    string personalizedStory = BehaviourUtils.GetPersonalizedMessage(quest.Story, player);

                    if (personalizedStory.Length > MaxStoryLength)
                    {
                        pak.WriteShort(MaxStoryLength);
                        pak.WriteStringBytes(personalizedStory.Substring(0, MaxStoryLength));
                    }
                    else
                    {
                        pak.WriteShort((ushort)personalizedStory.Length);
                        pak.WriteStringBytes(personalizedStory);
                    }
                }
                else
                {
                    if (quest.FinishText.Length > MaxStoryLength)
                    {
                        pak.WriteShort(MaxStoryLength);
                        pak.WriteStringBytes(quest.FinishText.Substring(0, MaxStoryLength));
                    }
                    else
                    {
                        pak.WriteShort((ushort)quest.FinishText.Length);
                        pak.WriteStringBytes(quest.FinishText);
                    }
                }

                pak.WriteShort(questId);
                pak.WriteByte((byte)quest.StepTexts.Count); // #goals count
                foreach (string text in quest.StepTexts)
                {
                    string t = text;

                    // Need to protect for any text length > 255.  It does not crash client but corrupts RewardQuest display -Tolakram
                    if (text.Length > 253)
                    {
                        t = text.Substring(0, 253);
                    }

                    pak.WritePascalString($"{t}\r");
                }

                pak.WriteInt((uint)quest.MoneyReward());
                pak.WriteByte((byte)quest.ExperiencePercent(player));
                pak.WriteByte((byte)quest.FinalRewards.Count);
                foreach (ItemTemplate reward in quest.FinalRewards)
                {
                    WriteItemData(pak, GameInventoryItem.Create(reward));
                }

                pak.WriteByte(quest.NumOptionalRewardsChoice);
                pak.WriteByte((byte)quest.OptionalRewards.Count);
                foreach (ItemTemplate reward in quest.OptionalRewards)
                {
                    WriteItemData(pak, GameInventoryItem.Create(reward));
                }

                SendTCP(pak);
            }
        }

        public virtual void SendQuestRewardWindow(GameNPC questNpc, GamePlayer player, DataQuest quest)
        {
            // 194
            SendQuestWindow(questNpc, player, quest, false);
        }

        // i'm reusing the questsubscribe command for quest abort since its 99% the same, only different event dets fired
        // data 3 defines wether it's subscribe or abort
        public virtual void SendQuestSubscribeCommand(GameNPC invitingNpc, ushort questid, string inviteMessage)
        {
            // 168
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
            // 168
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

        public virtual void SendGroupWindowUpdate()
        {
            // 169
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x06);

                Group group = GameClient.Player.Group;
                pak.WriteByte(group?.MemberCount ?? 0x00);

                pak.WriteByte(0x01);
                pak.WriteByte(0x00);

                if (group != null)
                {
                    foreach (GameLiving living in group.GetMembersInTheGroup())
                    {
                        bool sameRegion = living.CurrentRegion == GameClient.Player.CurrentRegion;

                        pak.WriteByte(living.Level);
                        if (sameRegion)
                        {
                            pak.WriteByte(living.HealthPercentGroupWindow);
                            pak.WriteByte(living.ManaPercent);
                            pak.WriteByte(living.EndurancePercent); // new in 1.69

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

                            if (living is GamePlayer player && player.Client.ClientState == GameClient.eClientState.Linkdead)
                            {
                                playerStatus |= 0x10;
                            }

                            if (living.CurrentRegion != GameClient.Player.CurrentRegion)
                            {
                                playerStatus |= 0x20;
                            }

                            pak.WriteByte(playerStatus);

                            // 0x00 = Normal , 0x01 = Dead , 0x02 = Mezzed , 0x04 = Diseased ,
                            // 0x08 = Poisoned , 0x10 = Link Dead , 0x20 = In Another Region
                            pak.WriteShort((ushort)living.ObjectID);// or session id?
                        }
                        else
                        {
                            pak.WriteInt(0x20);
                            pak.WriteShort(0);
                        }

                        pak.WritePascalString(living.Name);
                        pak.WritePascalString(living is GamePlayer gamePlayer ? gamePlayer.CharacterClass.Name : "NPC");// classname
                    }
                }

                SendTCP(pak);
            }
        }

        public void SendGroupMemberUpdate(bool updateIcons, GameLiving living)
        {
            // 168
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

        protected virtual void WriteGroupMemberUpdate(GSTCPPacketOut pak, bool updateIcons, GameLiving living)
        {
            // 191
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
                pak.WriteByte(living.EndurancePercent); // new in 1.69

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

                if ((living as GamePlayer)?.Client.ClientState == GameClient.eClientState.Linkdead)
                {
                    playerStatus |= 0x10;
                }

                if (living.DebuffCategory[(int)eProperty.SpellRange] != 0 || living.DebuffCategory[(int)eProperty.ArcheryRange] != 0)
                {
                    playerStatus |= 0x40;
                }

                pak.WriteByte(playerStatus);

                // 0x00 = Normal , 0x01 = Dead , 0x02 = Mezzed , 0x04 = Diseased ,
                // 0x08 = Poisoned , 0x10 = Link Dead , 0x20 = In Another Region, 0x40 - NS
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
                                pak.WriteByte(0);
                                pak.WriteShort(effect.Icon);
                            }
                        }
                    }
                }

                WriteGroupMemberMapUpdate(pak, living);
            }
            else
            {
                pak.WriteInt(0x20);
                if (updateIcons)
                {
                    pak.WriteByte((byte)(0x80 | living.GroupIndex));
                    pak.WriteByte(0);
                }
            }
        }

        protected virtual void WriteGroupMemberMapUpdate(GSTCPPacketOut pak, GameLiving living)
        {
            // 174
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

        public void SendGroupMembersUpdate(bool updateIcons)
        {
            // 168
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

        public virtual void SendInventoryItemsUpdate(ICollection<InventoryItem> itemsToUpdate)
        {
            SendInventoryItemsUpdate(eInventoryWindowType.Update, itemsToUpdate);
        }

        public virtual void SendInventorySlotsUpdate(ICollection<int> slots)
        {
            // 168
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

        /// <summary>
        /// Legacy inventory update. This handler silently
        /// assumes that a slot on the client matches a slot on the server.
        /// </summary>
        /// <param name="slots"></param>
        /// <param name="preAction"></param>
        protected virtual void SendInventorySlotsUpdateRange(ICollection<int> slots, eInventoryWindowType windowType)
        {
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.InventoryUpdate)))
            {
                GameVault houseVault = GameClient.Player.ActiveInventoryObject as GameVault;

                pak.WriteByte((byte)(slots?.Count ?? 0));
                pak.WriteByte(0); // CurrentSpeed & 0xFF (not used for player, only for NPC)
                pak.WriteByte((byte)((GameClient.Player.IsCloakInvisible ? 0x01 : 0x00) | (GameClient.Player.IsHelmInvisible ? 0x02 : 0x00))); // new in 189b+, cloack/helm visibility

                if (windowType == eInventoryWindowType.HouseVault && houseVault != null)
                {
                    pak.WriteByte((byte)(houseVault.Index + 1));    // Add the vault number to the window caption
                }
                else
                {
                    pak.WriteByte((byte)((GameClient.Player.IsCloakHoodUp ? 0x01 : 0x00) | (int)GameClient.Player.ActiveQuiverSlot)); // bit0 is hood up bit4 to 7 is active quiver
                }

                pak.WriteByte(GameClient.Player.VisibleActiveWeaponSlots);
                pak.WriteByte((byte)windowType);

                if (slots != null)
                {
                    foreach (int updatedSlot in slots)
                    {
                        if (updatedSlot >= (int)eInventorySlot.Consignment_First && updatedSlot <= (int)eInventorySlot.Consignment_Last)
                        {
                            Log.Error("PacketLib198:SendInventorySlotsUpdateBase - GameConsignmentMerchant inventory is no longer cached with player.  Use a Dictionary<int, InventoryItem> instead.");
                            pak.WriteByte((byte)(updatedSlot - (int)eInventorySlot.Consignment_First + (int)eInventorySlot.HousingInventory_First));
                        }
                        else
                        {
                            pak.WriteByte((byte)updatedSlot);
                        }

                        WriteItemData(pak, GameClient.Player.Inventory.GetItem((eInventorySlot)updatedSlot));
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendInventoryItemsUpdate(eInventoryWindowType windowType, ICollection<InventoryItem> itemsToUpdate)
        {
            // 168
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

        /// <summary>
        /// New inventory update handler. This handler takes into account that
        /// a slot on the client isn't necessarily the same as a slot on the
        /// server, e.g. house vaults.
        /// </summary>
        /// <param name="updateItems"></param>
        /// <param name="windowType"></param>
        public virtual void SendInventoryItemsUpdate(IDictionary<int, InventoryItem> updateItems, eInventoryWindowType windowType)
        {
            // 189
            if (GameClient.Player == null)
            {
                return;
            }

            if (updateItems == null)
            {
                updateItems = new Dictionary<int, InventoryItem>();
            }

            if (updateItems.Count <= ServerProperties.Properties.MAX_ITEMS_PER_PACKET)
            {
                SendInventoryItemsPartialUpdate(updateItems, windowType);
                return;
            }

            var items = new Dictionary<int, InventoryItem>(ServerProperties.Properties.MAX_ITEMS_PER_PACKET);
            foreach (var item in updateItems)
            {
                items.Add(item.Key, item.Value);
                if (items.Count >= ServerProperties.Properties.MAX_ITEMS_PER_PACKET)
                {
                    SendInventoryItemsPartialUpdate(items, windowType);
                    items.Clear();
                    windowType = eInventoryWindowType.Update;
                }
            }

            if (items.Count > 0)
            {
                SendInventoryItemsPartialUpdate(items, windowType);
            }
        }
        
        /// <summary>
         /// Sends inventory items to the client.  If windowType is one of the client inventory windows then the client
         /// will display the window.  Once the window is displayed to the client all handling of items in the window
         /// is done in the move item request handlers
         /// </summary>
         /// <param name="items"></param>
         /// <param name="windowType"></param>
        protected virtual void SendInventoryItemsPartialUpdate(IDictionary<int, InventoryItem> items, eInventoryWindowType windowType)
        {
            // 189
            // ChatUtil.SendDebugMessage(m_gameClient, string.Format("SendItemsPartialUpdate: windowType: {0}, {1}", windowType, items == null ? "nothing" : items[0].Name));
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.InventoryUpdate)))
            {
                GameVault houseVault = GameClient.Player.ActiveInventoryObject as GameVault;
                pak.WriteByte((byte)items.Count);
                pak.WriteByte(0x00); // new in 189b+, show shield in left hand
                pak.WriteByte((byte)((GameClient.Player.IsCloakInvisible ? 0x01 : 0x00) | (GameClient.Player.IsHelmInvisible ? 0x02 : 0x00))); // new in 189b+, cloack/helm visibility
                if (windowType == eInventoryWindowType.HouseVault && houseVault != null)
                {
                    pak.WriteByte((byte)(houseVault.Index + 1));    // Add the vault number to the window caption
                }
                else
                {
                    pak.WriteByte((byte)((GameClient.Player.IsCloakHoodUp ? 0x01 : 0x00) | (int)GameClient.Player.ActiveQuiverSlot)); // bit0 is hood up bit4 to 7 is active quiver
                }

                // ^ in 1.89b+, 0 bit - showing hooded cloack, if not hooded not show cloack at all ?
                pak.WriteByte(GameClient.Player.VisibleActiveWeaponSlots);
                pak.WriteByte((byte)windowType);
                foreach (var entry in items)
                {
                    pak.WriteByte((byte)entry.Key);
                    WriteItemData(pak, entry.Value);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendDoorState(Region region, IDoor door)
        {
            // 168
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
            // 168
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
                                    Log.Error($"Merchant item template \'{((MerchantItem)itemsInPage[page * MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS + i]).ItemTemplateID}\' not found, abort!!!");
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
            if (GameClient.Player?.TradeWindow == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TradeWindow)))
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
                            WriteItemData(pak, item);
                        }
                    }

                    pak.WritePascalString(GameClient.Player.TradeWindow.Partner != null
                        ? $"Trading with {GameClient.Player.GetName(GameClient.Player.TradeWindow.Partner)}"
                        : "Selfcrafting");

                    SendTCP(pak);
                }
            }
        }

        public virtual void SendCloseTradeWindow()
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TradeWindow)))
            {
                pak.Fill(0x00, 40);
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerDied(GamePlayer killedPlayer, GameObject killer)
        {
            // 168
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
            // 168
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
            // 190
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
                pak.WriteShort(player.CurrentZone.ZoneSkinID);

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

                // Write Remainings.
                pak.WriteByte(player.ManaPercent);
                pak.WriteByte(player.EndurancePercent);
                pak.FillString(player.CharacterClass.Name, 32);
                pak.WriteByte((byte)(player.RPFlag ? 1 : 0));
                pak.WriteByte(0); // send last byte for 190+ packets

                SendUDP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(player.CurrentRegionID, (ushort)player.ObjectID)] = GameTimer.GetTickCount();
        }

        public virtual void SendUpdatePlayer()
        {
            // 179
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

        public virtual void SendUpdatePlayerSkills()
        {
            // 180
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
                        if ((index >= byte.MaxValue) || ((pak.Length + 8 + usableSkills[index].Item1.Name.Length) > 1400))
                        {
                            break;
                        }

                        // Enter Packet Values !! Format Level - Type - SpecialField - Bonus - Icon - Name
                        Skill skill = usableSkills[index].Item1;
                        Skill skillrelated = usableSkills[index].Item2;

                        if (skill is Specialization spec)
                        {
                            pak.WriteByte((byte)Math.Max(1, spec.Level));
                            pak.WriteByte((byte)spec.SkillType);
                            pak.WriteShort(0);
                            pak.WriteByte((byte)(GameClient.Player.GetModifiedSpecLevel(spec.KeyName) - spec.Level)); // bonus
                            pak.WriteShort(spec.Icon);
                            pak.WritePascalString(spec.Name);
                        }
                        else if (skill is Ability ab)
                        {
                            pak.WriteByte(0);
                            pak.WriteByte((byte)ab.SkillType);
                            pak.WriteShort(0);
                            pak.WriteByte(0);
                            pak.WriteShort(ab.Icon);
                            pak.WritePascalString(ab.Name);
                        }
                        else if (skill is Spell spell)
                        {
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
                        else if (skill is Style style)
                        {
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

            // reset trainer cache
            GameClient.TrainerSkillCache = null;
        }

        public virtual void SendUpdateWeaponAndArmorStats()
        {
            // 168
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

        public virtual void SendCustomTextWindow(string caption, IList<string> text)
        {
            // 181
            if (text == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                pak.WriteByte(0); // new in 1.75
                pak.WriteByte(0); // new in 1.81
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

        protected void WriteCustomTextWindowData(GSTCPPacketOut pak, IList<string> text)
        {
            // 168
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

        public virtual void SendPlayerTitles()
        {
            // 168
            var titles = GameClient.Player.Titles;
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                pak.WriteByte(1); // new in 1.75
                pak.WriteByte(0); // new in 1.81
                pak.WritePascalString("Player Statistics"); // window caption

                byte line = 1;
                foreach (string str in GameClient.Player.FormatStatistics())
                {
                    pak.WriteByte(line++);
                    pak.WritePascalString(str);
                }

                pak.WriteByte(200);
                long titlesCountPos = pak.Position;
                pak.WriteByte(0); // length of all titles part
                pak.WriteByte((byte)titles.Count);
                line = 0;
                foreach (IPlayerTitle title in titles)
                {
                    pak.WriteByte(line++);
                    pak.WritePascalString(title.GetDescription(GameClient.Player));
                }

                long titlesLen = pak.Position - titlesCountPos - 1; // include titles count
                if (titlesLen > byte.MaxValue)
                {
                    Log.Warn($"Titles block is too long! {titlesLen} (player: {GameClient.Player})");
                }

                // Trailing Zero!
                pak.WriteByte(0);

                // Set titles length
                pak.Position = titlesCountPos;
                pak.WriteByte((byte)titlesLen); // length of all titles part
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerTitleUpdate(GamePlayer player)
        {
            // 175
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(0x0B); // subcode
                IPlayerTitle title = player.CurrentTitle;
                if (title == PlayerTitleMgr.ClearTitle)
                {
                    pak.WriteByte(0); // flag
                    pak.WriteInt(0); // unk1 + str len
                }
                else
                {
                    pak.WriteByte(1); // flag
                    string val = GameServer.ServerRules.GetPlayerTitle(GameClient.Player, player);
                    pak.WriteShort((ushort)val.Length);
                    pak.WriteShort(0); // unk1
                    pak.WriteStringBytes(val);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendEncumberance()
        {
            // 168
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

        public virtual void SendAddFriends(string[] friendNames)
        {
            // 168
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
            // 168
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
            // 168
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
            // 168
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
            // 168
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
            // 168
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
            // 1105
            if (GameClient?.Player == null)
            {
                return;
            }

            GamePlayer player = GameClient.Player;

            List<Specialization> specs = GameClient.Player.GetSpecList().Where(it => it.Trainable).ToList();
            IList<string> autotrains = player.CharacterClass.GetAutotrainableSkills();

            // Send Trainer Window with Trainable Specs
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {
                pak.WriteByte((byte)specs.Count);
                pak.WriteByte((byte)player.SkillSpecialtyPoints);
                pak.WriteByte(0); // Spec code
                pak.WriteByte(0);

                int i = 0;
                foreach (Specialization spec in specs)
                {
                    pak.WriteByte((byte)i++);
                    pak.WriteByte((byte)Math.Min(player.MaxLevel, spec.Level));
                    pak.WriteByte((byte)(Math.Min(player.MaxLevel, spec.Level) + 1));
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
                pak.WriteByte((byte)player.RealmSpecialtyPoints);
                pak.WriteByte(1); // RA Code
                pak.WriteByte(0);

                int i = 0;
                foreach (RealmAbility ra in raList)
                {
                    int level = player.GetAbilityLevel(ra.KeyName);
                    pak.WriteByte((byte)i++);
                    pak.WriteByte((byte)level);
                    pak.WriteByte((byte)ra.CostForUpgrade(level));
                    bool canBeUsed = ra.CheckRequirement(player);
                    pak.WritePascalString(canBeUsed ? ra.Name : $"[{ra.Name}]");
                }

                SendTCP(pak);
            }

            // Send Name Index for each spec.
            // Get ALL skills for player, ordered by spec key.
            List<Tuple<Specialization, List<Tuple<int, int, Skill>>>> skillDictCache;

            // get from cache
            if (GameClient.TrainerSkillCache == null)
            {
                skillDictCache = new List<Tuple<Specialization, List<Tuple<int, int, Skill>>>>();

                foreach (Specialization spec in specs)
                {
                    var toAdd = new List<Tuple<int, int, Skill>>();

                    foreach (Ability ab in spec.PretendAbilitiesForLiving(player, player.MaxLevel))
                    {
                        toAdd.Add(new Tuple<int, int, Skill>(5, ab.InternalID, ab));
                    }

                    foreach (KeyValuePair<SpellLine, List<Skill>> ls in spec.PretendLinesSpellsForLiving(player, player.MaxLevel).Where(k => !k.Key.IsBaseLine))
                    {
                        foreach (Skill sk in ls.Value)
                        {
                            toAdd.Add(new Tuple<int, int, Skill>((int)sk.SkillType, sk.InternalID, sk));
                        }
                    }

                    foreach (Style st in spec.PretendStylesForLiving(player, player.MaxLevel))
                    {
                        toAdd.Add(new Tuple<int, int, Skill>((int)st.SkillType, st.InternalID, st));
                    }

                    skillDictCache.Add(new Tuple<Specialization, List<Tuple<int, int, Skill>>>(spec, toAdd.OrderBy(e => (e.Item3 as Ability)?.SpecLevelRequirement ?? ((e.Item3 as Style)?.SpecLevelRequirement ?? e.Item3.Level)).ToList()));
                }

                // save to cache
                GameClient.TrainerSkillCache = skillDictCache;
            }

            skillDictCache = GameClient.TrainerSkillCache;

            // Send Names first
            int index = 0;
            for (int skindex = 0; skindex < skillDictCache.Count; skindex++)
            {
                using (GSTCPPacketOut pakindex = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
                {
                    pakindex.WriteByte((byte)skillDictCache[skindex].Item2.Count); // size
                    pakindex.WriteByte((byte)player.SkillSpecialtyPoints);
                    pakindex.WriteByte(4); // name index code
                    pakindex.WriteByte(0);
                    pakindex.WriteByte((byte)index); // start index

                    foreach (Skill sk in skillDictCache[skindex].Item2.Select(e => e.Item3))
                    {
                        // send name
                        pakindex.WritePascalString(sk.Name);
                        index++;
                    }

                    SendTCP(pakindex);
                }
            }

            // Send Skill Secondly
            using (GSTCPPacketOut pakskill = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {

                pakskill.WriteByte((byte)skillDictCache.Count); // size we send for all specs
                pakskill.WriteByte((byte)player.SkillSpecialtyPoints);
                pakskill.WriteByte(3); // Skill description code
                pakskill.WriteByte(0);
                pakskill.WriteByte(0); // unk ?

                // Fill out an array that tells the client how many spec points are available at each of
                // this characters levels.  This seems to only be used for the 'Minimum Level' display on
                // the new trainer window.  I've changed the calls below to use AdjustedSpecPointsMultiplier
                // to enable servers that allow levels > 50 to train properly by modifying points available per level. - Tolakram

                // There is a bug here that is calculating too few spec points and causing level 50 players to
                // be unable to train RA.  Setting this to max for now to disable 'Minimum Level' feature on train window.
                // I think bug is that auto train points must be added to this calculation.
                // -Tolakram
                for (byte i = 2; i <= 50; i++)
                {
                    // int specpoints = 0;

                    // if (i <= 5)
                    //    specpoints = i;

                    // if (i > 5)
                    //    specpoints = i * m_gameClient.Player.CharacterClass.AdjustedSpecPointsMultiplier / 10;

                    // if (i > 40 && i != 50)
                    //    specpoints += i * m_gameClient.Player.CharacterClass.AdjustedSpecPointsMultiplier / 20;

                    // paksub.WriteByte((byte)specpoints);
                    pakskill.WriteByte(255);
                }

                for (int skindex = 0; skindex < skillDictCache.Count; skindex++)
                {

                    byte autotrain = 0;
                    if (autotrains.Contains(specs[skindex].KeyName))
                    {
                        autotrain = (byte)Math.Floor((double)GameClient.Player.BaseLevel / 4);
                    }

                    if (pakskill.Length >= 2045)
                    {
                        break;
                    }

                    // Skill Index Header
                    pakskill.WriteByte((byte)skindex); // skill index
                    pakskill.WriteByte((byte)skillDictCache[skindex].Item2.Count); // Count
                    pakskill.WriteByte(autotrain); // autotrain byte

                    foreach (Tuple<int, int, Skill> sk in skillDictCache[skindex].Item2)
                    {
                        if (pakskill.Length >= 2040)
                        {
                            break;
                        }

                        if (sk.Item3 is Ability ab)
                        {
                            // skill description
                            pakskill.WriteByte((byte)ab.SpecLevelRequirement); // level
                            // tooltip
                            pakskill.WriteShort(ab.Icon); // icon
                            pakskill.WriteByte((byte)sk.Item1); // skill page
                            pakskill.WriteByte(0); //
                            pakskill.WriteByte(0xFD); // line
                            pakskill.WriteShort((ushort)sk.Item2); // ID
                        }
                        else if (sk.Item3 is Spell)
                        {
                            Spell sp = (Spell)sk.Item3;

                            // skill description
                            pakskill.WriteByte((byte)sp.Level); // level
                            // tooltip
                            pakskill.WriteShort(sp.InternalIconID > 0 ? sp.InternalIconID : sp.Icon); // icon
                            pakskill.WriteByte((byte)sk.Item1); // skill page
                            pakskill.WriteByte(0); //
                            pakskill.WriteByte((byte)(sp.SkillType == eSkillPage.Songs ? 0xFF : 0xFE)); // line
                            pakskill.WriteShort((ushort)sk.Item2); // ID
                        }
                        else if (sk.Item3 is Style)
                        {
                            Style st = (Style)sk.Item3;
                            pakskill.WriteByte((byte)Math.Min(player.MaxLevel, st.SpecLevelRequirement));

                            // tooltip
                            pakskill.WriteShort(st.Icon);
                            pakskill.WriteByte((byte)sk.Item1);
                            pakskill.WriteByte((byte)st.OpeningRequirementType);
                            pakskill.WriteByte((byte)st.OpeningRequirementValue);
                            pakskill.WriteShort((ushort)sk.Item2);
                        }
                        else
                        {
                            // ??
                            pakskill.WriteByte((byte)sk.Item3.Level);

                            // tooltip
                            pakskill.WriteShort(sk.Item3.Icon);
                            pakskill.WriteByte((byte)sk.Item1);
                            pakskill.WriteByte(0);
                            pakskill.WriteByte(0);
                            pakskill.WriteShort((ushort)sk.Item2);
                        }
                    }
                }

                SendTCP(pakskill);
            }

            // type 5 (realm abilities)
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.TrainerWindow)))
            {
                pak.WriteByte((byte)raList.Count);
                pak.WriteByte((byte)player.RealmSpecialtyPoints);
                pak.WriteByte(5);
                pak.WriteByte(0);

                foreach (RealmAbility ra in raList)
                {
                    pak.WriteByte((byte)player.GetAbilityLevel(ra.KeyName));

                    pak.WriteByte(0);
                    pak.WriteByte((byte)ra.MaxLevel);

                    for (int i = 0; i < ra.MaxLevel; i++)
                    {
                        pak.WriteByte((byte)ra.CostForUpgrade(i));
                    }

                    pak.WritePascalString(ra.CheckRequirement(GameClient.Player) ? ra.KeyName : $"[{ra.Name}]");
                }

                SendTCP(pak);
            }
        }

        public virtual void SendInterruptAnimation(GameLiving living)
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.InterruptSpellCast)))
            {
                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteShort(1);
                SendTCP(pak);
            }
        }

        public virtual void SendDisableSkill(ICollection<Tuple<Skill, int>> skills)
        {
            // 168
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

                        for (int i = 0; i < countskill; i++)
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

                            for (int i = 0; i < total; i++)
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

        // 190
        public byte Icons { get; protected set; }

        public virtual void SendUpdateIcons(IList changedEffects, ref int lastUpdateEffectsCount)
        {
            // 190
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.UpdateIcons)))
            {
                long initPos = pak.Position;

                int fxcount = 0;
                int entriesCount = 0;
                lock (GameClient.Player.EffectList)
                {
                    pak.WriteByte(0);   // effects count set in the end
                    pak.WriteByte(0);   // unknown
                    pak.WriteByte(Icons);   // unknown
                    pak.WriteByte(0);   // unknown
                    foreach (IGameEffect effect in GameClient.Player.EffectList)
                    {
                        if (effect.Icon != 0)
                        {
                            fxcount++;
                            if (changedEffects != null && !changedEffects.Contains(effect))
                            {
                                continue;
                            }

                            // Log.DebugFormat("adding [{0}] '{1}'", fxcount-1, effect.Name);
                            pak.WriteByte((byte)(fxcount - 1)); // icon index
                            pak.WriteByte((effect is GameSpellEffect) ? (byte)(fxcount - 1) : (byte)0xff);

                            byte immunByte = 0;
                            if (effect is GameSpellEffect gsp && gsp.IsDisabled)
                            {
                                immunByte = 1;
                            }

                            pak.WriteByte(immunByte); // new in 1.73; if non zero says "protected by" on right click

                            // bit 0x08 adds "more..." to right click info
                            pak.WriteShort(effect.Icon);

                            // pak.WriteShort(effect.IsFading ? (ushort)1 : (ushort)(effect.RemainingTime / 1000));
                            pak.WriteShort((ushort)(effect.RemainingTime / 1000));
                            pak.WriteShort(effect.InternalID);      // reference for shift+i or cancel spell
                            byte flagNegativeEffect = 0;
                            if (effect is StaticEffect staticEffect)
                            {
                                if (staticEffect.HasNegativeEffect)
                                {
                                    flagNegativeEffect = 1;
                                }
                            }
                            else if (effect is GameSpellEffect)
                            {
                                if (!((GameSpellEffect)effect).SpellHandler.HasPositiveEffect)
                                {
                                    flagNegativeEffect = 1;
                                }
                            }

                            pak.WriteByte(flagNegativeEffect);
                            pak.WritePascalString(effect.Name);
                            entriesCount++;
                        }
                    }

                    int oldCount = lastUpdateEffectsCount;
                    lastUpdateEffectsCount = fxcount;
                    while (oldCount > fxcount)
                    {
                        pak.WriteByte((byte)(fxcount++));
                        pak.Fill(0, 10);
                        entriesCount++;

                        // Log.DebugFormat("adding [{0}] (empty)", fxcount-1);
                    }

                    changedEffects?.Clear();

                    if (entriesCount == 0)
                    {
                        return; // nothing changed - no update is needed
                    }

                    pak.Position = initPos;
                    pak.WriteByte((byte)entriesCount);
                    pak.Seek(0, SeekOrigin.End);

                    SendTCP(pak);
                }
            }
        }

        public virtual void SendLevelUpSound()
        {
            // 168
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
            // 168
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
            // 168
            if (GameClient.Account.PrivLevel > (int)ePrivLevel.Player || ServerProperties.Properties.ENABLE_DEBUG)
            {
                SendMessage(string.Format("[DEBUG] " + format, parameters), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }

        public virtual void SendDebugPopupMessage(string format, params object[] parameters)
        {
            // 168
            if (GameClient.Account.PrivLevel > (int)ePrivLevel.Player || ServerProperties.Properties.ENABLE_DEBUG)
            {
                SendMessage(string.Format("[DEBUG] " + format, parameters), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }

        public virtual void SendEmblemDialogue()
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.EmblemDialogue)))
            {
                pak.Fill(0x00, 4);
                SendTCP(pak);
            }
        }

        // FOR GM to test param and see min and max of each param
        public virtual void SendWeather(uint x, uint width, ushort speed, ushort fogdiffusion, ushort intensity)
        {
            // 168
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
            // 168
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
            // 168
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

        public virtual void SendObjectUpdate(GameObject obj)
        {
            // 168
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

        public virtual void SendQuestListUpdate()
        {
            // 187
            if (GameClient?.Player == null)
            {
                return;
            }

            SendTaskInfo();

            int questIndex = 1;
            lock (GameClient.Player.QuestList)
            {
                foreach (AbstractQuest quest in GameClient.Player.QuestList)
                {
                    SendQuestPacket((quest.Step == -1) ? null : quest, questIndex++);
                }
            }

            while (questIndex <= 25)
            {
                SendQuestPacket(null, questIndex++);
            }
        }

        protected virtual void SendTaskInfo()
        {
            // 184
            string name = BuildTaskString();

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry)))
            {
                pak.WriteByte(0); // index
                pak.WriteShortLowEndian((ushort)name.Length);
                pak.WriteByte(0);
                pak.WriteByte(0);
                pak.WriteByte(0);
                pak.WriteStringBytes(name); // Write Quest Name without trailing 0
                pak.WriteStringBytes(string.Empty); // Write Quest Description without trailing 0
                SendTCP(pak);
            }
        }

        protected string BuildTaskString()
        {
            // 168
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

        protected virtual void SendQuestPacket(AbstractQuest q, int index)
        {
            // 187
            if (!(q is RewardQuest))
            {
                // 184
                using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry)))
                {
                    pak.WriteByte((byte)index);
                    if (q == null)
                    {
                        pak.WriteByte(0);
                        pak.WriteByte(0);
                        pak.WriteByte(0);
                        pak.WriteByte(0);
                        pak.WriteByte(0);
                    }
                    else
                    {
                        string name = $"{q.Name} (Level {q.Level})";
                        string desc = $"[Step #{q.Step}]: {q.Description}";
                        if (name.Length > byte.MaxValue)
                        {
                            if (Log.IsWarnEnabled)
                            {
                                Log.Warn($"{q.GetType()}: name is too long for 1.68+ clients ({name.Length}) \'{name}\'");
                            }

                            name = name.Substring(0, byte.MaxValue);
                        }

                        if (desc.Length > byte.MaxValue)
                        {
                            if (Log.IsWarnEnabled)
                            {
                                Log.Warn($"{q.GetType()}: description is too long for 1.68+ clients ({desc.Length}) \'{desc}\'");
                            }

                            desc = desc.Substring(0, byte.MaxValue);
                        }

                        pak.WriteByte((byte)name.Length);
                        pak.WriteShortLowEndian((ushort)desc.Length);
                        pak.WriteByte(0); // Quest Zone ID ?
                        pak.WriteByte(0);
                        pak.WriteStringBytes(name); // Write Quest Name without trailing 0
                        pak.WriteStringBytes(desc); // Write Quest Description without trailing 0
                    }

                    SendTCP(pak);
                }
                return;
            }

            RewardQuest quest = (RewardQuest)q;
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.QuestEntry)))
            {
                pak.WriteByte((byte)index);
                pak.WriteByte((byte)quest.Name.Length);
                pak.WriteShort(0x00); // unknown
                pak.WriteByte((byte)quest.Goals.Count);
                pak.WriteByte((byte)quest.Level);
                pak.WriteStringBytes(quest.Name);
                pak.WritePascalString(quest.Description);
                int goalindex = 0;
                foreach (RewardQuest.QuestGoal goal in quest.Goals)
                {
                    goalindex++;
                    string goalDesc = $"{goal.Description}\r";
                    pak.WriteShortLowEndian((ushort)goalDesc.Length);
                    pak.WriteStringBytes(goalDesc);
                    pak.WriteShortLowEndian((ushort)goal.ZoneId2);
                    pak.WriteShortLowEndian((ushort)goal.XOffset2);
                    pak.WriteShortLowEndian((ushort)goal.YOffset2);
                    pak.WriteShortLowEndian(0x00);  // unknown
                    pak.WriteShortLowEndian((ushort)goal.Type);
                    pak.WriteShortLowEndian(0x00);  // unknown
                    pak.WriteShortLowEndian((ushort)goal.ZoneId1);
                    pak.WriteShortLowEndian((ushort)goal.XOffset1);
                    pak.WriteShortLowEndian((ushort)goal.YOffset1);
                    pak.WriteByte((byte)(goal.IsAchieved ? 0x01 : 0x00));
                    if (goal.QuestItem == null)
                    {
                        pak.WriteByte(0x00);
                    }
                    else
                    {
                        pak.WriteByte((byte)goalindex);
                        WriteTemplateData(pak, goal.QuestItem, 1);
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendQuestUpdate(AbstractQuest quest)
        {
            // 173
            int questIndex = 1;

            // add check for null due to LD
            if (GameClient?.Player?.QuestList != null)
            {
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
        }

        public virtual void SendConcentrationList()
        {
            // 191
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ConcentrationList)))
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
                        pak.WritePascalString(effect.Name.Length > 14
                            ? $"{effect.Name.Substring(0, 12)}.."
                            : effect.Name);

                        pak.WritePascalString(effect.OwnerName.Length > 14
                            ? $"{effect.OwnerName.Substring(0, 12)}.."
                            : effect.OwnerName);
                    }
                }

                SendTCP(pak);
            }

            SendStatusUpdate(); // send status update for convinience, mostly the conc has changed
        }

        public virtual void SendUpdateCraftingSkills()
        {
            // 168
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

        public void SendChangeTarget(GameObject newTarget)
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ChangeTarget)))
            {
                pak.WriteShort((ushort)(newTarget?.ObjectID ?? 0));
                pak.WriteShort(0); // unknown
                SendTCP(pak);
            }
        }

        public void SendChangeGroundTarget(Point3D newTarget)
        {
            // 168
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
            // 181
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PetWindow)))
            {
                pak.WriteShort((ushort)(pet?.ObjectID ?? 0));
                pak.WriteByte(0x00); // unused
                pak.WriteByte(0x00); // unused
                switch (windowAction) // 0-released, 1-normal, 2-just charmed? | Roach: 0-close window, 1-update window, 2-create window
                {
                    case ePetWindowAction.Open: pak.WriteByte(2); break;
                    case ePetWindowAction.Update: pak.WriteByte(1); break;
                    default: pak.WriteByte(0); break;
                }

                switch (aggroState) // 1-aggressive, 2-defensive, 3-passive
                {
                    case eAggressionState.Aggressive: pak.WriteByte(1); break;
                    case eAggressionState.Defensive: pak.WriteByte(2); break;
                    case eAggressionState.Passive: pak.WriteByte(3); break;
                    default: pak.WriteByte(0); break;
                }

                switch (walkState) // 1-follow, 2-stay, 3-goto, 4-here
                {
                    case eWalkState.Follow: pak.WriteByte(1); break;
                    case eWalkState.Stay: pak.WriteByte(2); break;
                    case eWalkState.GoTarget: pak.WriteByte(3); break;
                    case eWalkState.ComeHere: pak.WriteByte(4); break;
                    default: pak.WriteByte(0); break;
                }

                pak.WriteByte(0x00); // unused

                if (pet != null)
                {
                    lock (pet.EffectList)
                    {
                        ArrayList icons = new ArrayList();
                        foreach (IGameEffect effect in pet.EffectList)
                        {
                            if (icons.Count >= 8)
                            {
                                break;
                            }

                            if (effect.Icon == 0)
                            {
                                continue;
                            }

                            icons.Add(effect.Icon);
                        }

                        pak.WriteByte((byte)icons.Count); // effect count
                        // 0x08 - null terminated - (byte) list of shorts - spell icons on pet
                        foreach (ushort icon in icons)
                        {
                            pak.WriteShort(icon);
                        }
                    }
                }
                else
                {
                    pak.WriteByte(0); // effect count
                }

                SendTCP(pak);
            }
        }

        public virtual void SendPlaySound(eSoundType soundType, ushort soundId)
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.PlaySound)))
            {
                pak.WriteShort((ushort)soundType);
                pak.WriteShort(soundId);
                pak.Fill(0x00, 8);
                SendTCP(pak);
            }
        }

        public virtual void SendNPCsQuestEffect(GameNPC npc, eQuestIndicator indicator)
        {
            // 173
            if (GameClient.Player == null || npc == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {

                pak.WriteShort((ushort)npc.ObjectID);
                pak.WriteByte(0x7); // Quest visual effect
                pak.WriteByte((byte)indicator);
                pak.WriteInt(0);

                SendTCP(pak);
            }
        }

        public virtual void SendMasterLevelWindow(byte ml)
        {
            // 190
            if (GameClient?.Player == null)
            {
                return;
            }

            // If required ML=0 then send current player ML data
            byte mlToSend = (byte)(ml == 0 ? (GameClient.Player.MLLevel == 0 ? 1 : GameClient.Player.MLLevel) : ml);

            if (mlToSend > GamePlayer.ML_MAX_LEVEL)
            {
                mlToSend = GamePlayer.ML_MAX_LEVEL;
            }

            double mlXpPercent;
            double mlStepPercent = 0;

            if (GameClient.Player.MLLevel < 10)
            {
                mlXpPercent = 100.0 * GameClient.Player.MLExperience / GameClient.Player.GetMLExperienceForLevel(GameClient.Player.MLLevel + 1);
                if (GameClient.Player.GetStepCountForML((byte)(GameClient.Player.MLLevel + 1)) > 0)
                {
                    mlStepPercent = 100.0 * GameClient.Player.GetCountMLStepsCompleted((byte)(GameClient.Player.MLLevel + 1)) / GameClient.Player.GetStepCountForML((byte)(GameClient.Player.MLLevel + 1));
                }
            }
            else
            {
                mlXpPercent = 100.0; // ML10 has no MLXP, so always 100%
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut((byte)eServerPackets.MasterLevelWindow))
            {
                pak.WriteByte((byte)mlXpPercent); // MLXP (blue bar)
                pak.WriteByte((byte)Math.Min(mlStepPercent, 100)); // Step percent (red bar)
                pak.WriteByte((byte)(GameClient.Player.MLLevel + 1)); // ML level + 1
                pak.WriteByte(0);
                pak.WriteShort(0); // exp1 ? new in 1.90
                pak.WriteShort(0); // exp2 ? new in 1.90
                pak.WriteByte(ml);

                // ML level completion is displayed client side for Step 11
                for (int i = 1; i < 11; i++)
                {
                    string description = GameClient.Player.GetMLStepDescription(mlToSend, i);
                    pak.WritePascalString(description);
                }

                pak.WriteByte(0);
                SendTCP(pak);
            }
        }

        public virtual void SendHexEffect(GamePlayer player, byte effect1, byte effect2, byte effect3, byte effect4, byte effect5)
        {
            // 173
            if (player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(0x3); // show Hex
                pak.WriteByte(effect1);
                pak.WriteByte(effect2);
                pak.WriteByte(effect3);
                pak.WriteByte(effect4);
                pak.WriteByte(effect5);

                SendTCP(pak);
            }
        }

        public virtual void SendRvRGuildBanner(GamePlayer player, bool show)
        {
            // 176
            if (player == null)
            {
                return;
            }

            // cannot show banners for players that have no guild.
            if (show && player.Guild == null)
            {
                return;
            }

            GSTCPPacketOut pak = new GSTCPPacketOut((byte)eServerPackets.VisualEffect);
            pak.WriteShort((ushort)player.ObjectID);
            pak.WriteByte(0xC); // show Banner
            pak.WriteByte((byte)(show ? 0 : 1)); // 0-enable, 1-disable
            int newEmblemBitMask = ((player.Guild.Emblem & 0x010000) << 8) | (player.Guild.Emblem & 0xFFFF);
            pak.WriteInt((uint)newEmblemBitMask);
            SendTCP(pak);
        }

        public virtual void SendSiegeWeaponAnimation(GameSiegeWeapon siegeWeapon)
        {
            // 168
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
            // 168
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

        public virtual void SendSiegeWeaponCloseInterface()
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SiegeWeaponInterface)))
            {
                pak.WriteShort(0);
                pak.WriteShort(1);
                pak.Fill(0, 13);
                SendTCP(pak);
            }
        }

        public virtual void SendSiegeWeaponInterface(GameSiegeWeapon siegeWeapon, int time)
        {
            // 173
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.SiegeWeaponInterface)))
            {
                ushort flag = (ushort)((siegeWeapon.EnableToMove ? 1 : 0) | siegeWeapon.AmmoType << 8);
                pak.WriteShort(flag); // byte Ammo,  byte SiegeMoving(1/0)
                pak.WriteByte(0);
                pak.WriteByte(0); // Close interface(1/0)
                pak.WriteByte((byte)time);// time in 100ms
                pak.WriteByte((byte)siegeWeapon.Ammo.Count); // external ammo count
                pak.WriteByte((byte)siegeWeapon.SiegeWeaponTimer.CurrentAction);
                pak.WriteByte((byte)siegeWeapon.AmmoSlot);
                pak.WriteShort(siegeWeapon.Effect);
                pak.WriteShort(0); // SiegeHelperTimer ?
                pak.WriteShort(0); // SiegeTimer ?
                pak.WriteShort((ushort)siegeWeapon.ObjectID);

                string name = siegeWeapon.Name;

                LanguageDataObject translation = GameClient.GetTranslation(siegeWeapon);
                if (translation != null)
                {
                    if (!Util.IsEmpty(((DBLanguageNPC)translation).Name))
                    {
                        name = ((DBLanguageNPC)translation).Name;
                    }
                }

                pak.WritePascalString($"{name} ({siegeWeapon.CurrentState})");
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
                        pak.WritePascalString($"{item.Count} {item.Name}");
                    }
                    else
                    {
                        pak.WritePascalString(item.Name);
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendLivingDataUpdate(GameLiving living, bool updateStrings)
        {
            // 171
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ObjectDataUpdate)))
            {
                pak.WriteShort((ushort)living.ObjectID);
                pak.WriteByte(0);
                pak.WriteByte(living.Level);
                if (living is GamePlayer player)
                {
                    pak.WritePascalString(GameServer.ServerRules.GetPlayerGuildName(GameClient.Player, player));
                    pak.WritePascalString(GameServer.ServerRules.GetPlayerLastName(GameClient.Player, player));
                }
                else if (!updateStrings)
                {
                    pak.WriteByte(0xFF);
                }
                else
                {
                    pak.WritePascalString(living.GuildName);
                    pak.WritePascalString(living.Name);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendSoundEffect(ushort soundId, ushort zoneId, ushort x, ushort y, ushort z, ushort radius)
        {
            // 168
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

        public virtual void SendKeepInfo(IGameKeep keep)
        {
            // 173
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepInfo)))
            {

                pak.WriteShort(keep.KeepID);
                pak.WriteShort(0);
                pak.WriteInt((uint)keep.X);
                pak.WriteInt((uint)keep.Y);
                pak.WriteShort(keep.Heading);
                pak.WriteByte((byte)keep.Realm);
                pak.WriteByte(keep.Level);// level
                pak.WriteShort(0);// unk
                pak.WriteByte(0x52);// model
                pak.WriteByte(0);// unk

                SendTCP(pak);
            }
        }

        public virtual void SendKeepRealmUpdate(IGameKeep keep)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepRealmUpdate)))
            {

                pak.WriteShort(keep.KeepID);
                pak.WriteByte((byte)keep.Realm);
                pak.WriteByte(keep.Level);
                SendTCP(pak);
            }
        }

        public virtual void SendKeepRemove(IGameKeep keep)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepRemove)))
            {

                pak.WriteShort(keep.KeepID);
                SendTCP(pak);
            }
        }

        public virtual void SendKeepComponentInfo(IGameKeepComponent keepComponent)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentInfo)))
            {

                pak.WriteShort(keepComponent.Keep.KeepID);
                pak.WriteShort((ushort)keepComponent.ID);
                pak.WriteInt((uint)keepComponent.ObjectID);
                pak.WriteByte((byte)keepComponent.Skin);
                pak.WriteByte((byte)keepComponent.ComponentX);// relative to keep
                pak.WriteByte((byte)keepComponent.ComponentY);// relative to keep
                pak.WriteByte((byte)keepComponent.ComponentHeading);
                pak.WriteByte((byte)keepComponent.Height);
                pak.WriteByte(keepComponent.HealthPercent);
                byte flag = keepComponent.Status;
                if (keepComponent.IsRaized) // Only for towers
                {
                    flag |= 0x04;
                }

                if (flag == 0x00 && keepComponent.Climbing)
                {
                    flag = 0x02;
                }

                pak.WriteByte(flag);
                pak.WriteByte(0x00); // unk
                SendTCP(pak);
            }
        }

        public virtual void SendKeepComponentDetailUpdate(IGameKeepComponent keepComponent)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentDetailUpdate)))
            {
                pak.WriteShort(keepComponent.Keep.KeepID);
                pak.WriteShort((ushort)keepComponent.ID);
                pak.WriteByte((byte)keepComponent.Height);
                pak.WriteByte(keepComponent.HealthPercent);
                byte flag = keepComponent.Status;

                if (keepComponent.IsRaized) // Only for towers
                {
                    flag |= 0x04;
                }

                if (flag == 0x00 && keepComponent.Climbing)
                {
                    flag = 0x02;
                }

                pak.WriteByte(flag);
                pak.WriteByte(0x00);// unk
                SendTCP(pak);
            }
        }

        public virtual void SendKeepComponentRemove(IGameKeepComponent keepComponent)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentRemove)))
            {
                pak.WriteShort(keepComponent.Keep.KeepID);
                pak.WriteShort((ushort)keepComponent.ID);
                SendTCP(pak);
            }
        }

        public virtual void SendKeepClaim(IGameKeep keep, byte flag)
        {
            // 170
            if (GameClient.Player == null || keep == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepClaim)))
            {

                pak.WriteShort(keep.KeepID);
                pak.WriteByte(flag);// 0-Info,1-KeepTargetLevel,2-KeepLordType,4-Release
                pak.WriteByte(1); // Keep Lord Type: always melee, type is no longer used
                pak.WriteByte((byte)ServerProperties.Properties.MAX_KEEP_LEVEL);
                pak.WriteByte(keep.Level);
                SendTCP(pak);
            }
        }


        public virtual void SendKeepComponentUpdate(IGameKeep keep, bool levelUp)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentUpdate)))
            {

                pak.WriteShort(keep.KeepID);
                pak.WriteByte((byte)keep.Realm);
                pak.WriteByte(keep.Level);
                pak.WriteByte((byte)keep.SentKeepComponents.Count);
                foreach (IGameKeepComponent component in keep.SentKeepComponents)
                {
                    byte flag = (byte)component.Height;
                    if (component.Status == 0 && component.Climbing)
                    {
                        flag |= 0x80;
                    }

                    if (component.IsRaized) // Only for towers
                    {
                        flag |= 0x10;
                    }

                    if (levelUp)
                    {
                        flag |= 0x20;
                    }

                    if (!component.IsAlive)
                    {
                        flag |= 0x40;
                    }

                    pak.WriteByte(flag);
                }

                pak.WriteByte(0);// unk
                SendTCP(pak);
            }
        }

        public virtual void SendKeepComponentInteract(IGameKeepComponent component)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentInteractResponse)))
            {
                pak.WriteShort(component.Keep.KeepID);
                pak.WriteByte((byte)component.Keep.Realm);
                pak.WriteByte(component.HealthPercent);

                pak.WriteByte(component.Keep.EffectiveLevel(component.Keep.Level));
                pak.WriteByte(component.Keep.EffectiveLevel((byte)ServerProperties.Properties.MAX_KEEP_LEVEL));

                // guild
                pak.WriteByte(1); // Keep Type: always melee here, type is no longer used

                if (component.Keep.Guild != null)
                {
                    pak.WriteString(component.Keep.Guild.Name);
                }

                pak.WriteByte(0);
                SendTCP(pak);
            }
        }

        public virtual void SendKeepComponentHookPoint(IGameKeepComponent component, int selectedHookPointIndex)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentHookpointUpdate)))
            {
                pak.WriteShort(component.Keep.KeepID);
                pak.WriteShort((ushort)component.ID);
                ArrayList freeHookpoints = new ArrayList();
                foreach (GameKeepHookPoint hookPt in component.HookPoints.Values)
                {
                    if (hookPt.IsFree)
                    {
                        freeHookpoints.Add(hookPt);
                    }
                }

                pak.WriteByte((byte)freeHookpoints.Count);
                pak.WriteByte((byte)selectedHookPointIndex);
                foreach (GameKeepHookPoint hookPt in freeHookpoints) // have to sort by index?
                {
                    pak.WriteByte((byte)hookPt.ID);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendClearKeepComponentHookPoint(IGameKeepComponent component, int selectedHookPointIndex)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentHookpointUpdate)))
            {
                pak.WriteShort(component.Keep.KeepID);
                pak.WriteShort((ushort)component.ID);
                pak.WriteByte(0);
                pak.WriteByte((byte)selectedHookPointIndex);
                SendTCP(pak);
            }
        }

        public virtual void SendHookPointStore(GameKeepHookPoint hookPoint)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.KeepComponentHookpointStore)))
            {
                pak.WriteShort(hookPoint.Component.AbstractKeep.KeepID);
                pak.WriteShort((ushort)hookPoint.Component.ID);
                pak.WriteShort((ushort)hookPoint.ID);
                pak.Fill(0x01, 3);
                HookPointInventory inventory;
                if (hookPoint.ID > 0x80)
                {
                    inventory = HookPointInventory.YellowHPInventory; // oil
                }
                else if (hookPoint.ID > 0x60)
                {
                    inventory = HookPointInventory.GreenHPInventory;// big siege
                }
                else if (hookPoint.ID > 0x40)
                {
                    inventory = HookPointInventory.LightGreenHPInventory; // small siege
                }
                else if (hookPoint.ID > 0x20)
                {
                    inventory = HookPointInventory.BlueHPInventory;// npc
                }
                else
                {
                    inventory = HookPointInventory.RedHPInventory;// guard
                }

                pak.WriteByte((byte)inventory.GetAllItems().Count);// count
                pak.WriteShort(0);
                int i = 0;
                foreach (HookPointItem item in inventory.GetAllItems())
                {
                    // TODO : must be quite like the merchant item.
                    // the problem is to change how it is done maybe make the hookpoint item inherit from an interface in common with itemtemplate. have to think to that.
                    pak.WriteByte((byte)i);
                    i++;
                    if (item.GameObjectType == "GameKeepGuard") // TODO: hack wrong must think how design thing to have merchante of gameobject(living or item)
                    {
                        pak.WriteShort(0);
                    }
                    else
                    {
                        pak.WriteShort(item.Flag);
                    }

                    pak.WriteShort(0);
                    pak.WriteShort(0);
                    pak.WriteShort(0);
                    pak.WriteInt((uint)item.Gold);
                    pak.WriteShort(item.Icon);
                    pak.WritePascalString(item.Name);// item sell
                }

                SendTCP(pak);
            }
        }

        public virtual void SendWarmapUpdate(ICollection<IGameKeep> list)
        {
            // 170
            if (GameClient.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.WarMapClaimedKeeps)))
            {
                int keepCount = 0;
                int towerCount = 0;
                foreach (var gameKeep in list)
                {
                    var keep = (AbstractGameKeep)gameKeep;
                    if (keep is GameKeep)
                    {
                        keepCount++;
                    }
                    else
                    {
                        towerCount++;
                    }
                }

                pak.WriteShort(0);
                pak.WriteByte((byte)keepCount);
                pak.WriteByte((byte)towerCount);
                byte albStr = 0;
                byte hibStr = 0;
                byte midStr = 0;
                byte albMagic = 0;
                byte hibMagic = 0;
                byte midMagic = 0;
                foreach (GameRelic relic in RelicMgr.getNFRelics())
                {
                    switch (relic.OriginalRealm)
                    {
                        case eRealm.Albion:
                            if (relic.RelicType == eRelicType.Strength)
                            {
                                albStr = (byte)relic.Realm;
                            }

                            if (relic.RelicType == eRelicType.Magic)
                            {
                                albMagic = (byte)relic.Realm;
                            }

                            break;
                        case eRealm.Hibernia:
                            if (relic.RelicType == eRelicType.Strength)
                            {
                                hibStr = (byte)relic.Realm;
                            }

                            if (relic.RelicType == eRelicType.Magic)
                            {
                                hibMagic = (byte)relic.Realm;
                            }

                            break;
                        case eRealm.Midgard:
                            if (relic.RelicType == eRelicType.Strength)
                            {
                                midStr = (byte)relic.Realm;
                            }

                            if (relic.RelicType == eRelicType.Magic)
                            {
                                midMagic = (byte)relic.Realm;
                            }

                            break;
                    }
                }

                pak.WriteByte(albStr);
                pak.WriteByte(midStr);
                pak.WriteByte(hibStr);
                pak.WriteByte(albMagic);
                pak.WriteByte(midMagic);
                pak.WriteByte(hibMagic);
                foreach (var gameKeep in list)
                {
                    var keep = (AbstractGameKeep)gameKeep;
                    int keepId = keep.KeepID;

                    /*if (ServerProperties.Properties.USE_NEW_KEEPS == 1)
                    {
                        keepId -= 12;
                        if ((keep.KeepID > 74 && keep.KeepID < 114) || (keep.KeepID > 330 && keep.KeepID < 370) || (keep.KeepID > 586 && keep.KeepID < 626)
                            || (keep.KeepID > 842 && keep.KeepID < 882) || (keep.KeepID > 1098 && keep.KeepID < 1138))
                            keepId += 5;
                    }*/

                    int id = keepId & 0xFF;
                    int tower = keep.KeepID >> 8;
                    int map = (id - 25) / 25;
                    int index = id - (map * 25 + 25);
                    int flag = (byte)keep.Realm; // 3 bits
                    Guild guild = keep.Guild;
                    string name = string.Empty;
                    pak.WriteByte((byte)((map << 6) | (index << 3) | tower));
                    if (guild != null)
                    {
                        flag |= (byte)eRealmWarmapKeepFlags.Claimed;
                        name = guild.Name;
                    }

                    // Teleport
                    if (GameClient.Account.PrivLevel > (int)ePrivLevel.Player)
                    {
                        flag |= (byte)eRealmWarmapKeepFlags.Teleportable;
                    }
                    else
                    {
                        if (GameServer.KeepManager.FrontierRegionsList.Contains(GameClient.Player.CurrentRegionID) && GameClient.Player.Realm == keep.Realm)
                        {
                            if (keep is GameKeep theKeep)
                            {
                                if (theKeep.OwnsAllTowers && !theKeep.InCombat)
                                {
                                    flag |= (byte)eRealmWarmapKeepFlags.Teleportable;
                                }
                            }
                        }
                    }

                    if (keep.InCombat)
                    {
                        flag |= (byte)eRealmWarmapKeepFlags.UnderSiege;
                    }

                    pak.WriteByte((byte)flag);
                    pak.WritePascalString(name);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendWarmapDetailUpdate(List<List<byte>> fights, List<List<byte>> groups)
        {
            // 170
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.WarMapDetailUpdate)))
            {
                pak.WriteByte((byte)fights.Count);// count - Fights (Byte)
                pak.WriteByte((byte)groups.Count);// count - Groups (Byte)
                // order first fights after than groups

                // zoneid  - byte // zoneid from zones.xml
                //          - A7 - Mid  - left          -   10100111        Map: Midgard
                //          - A8 - Mid  - middle        -   10101000            | X |
                //          - A9 - Mid  - right         -   10101001        |x    x     x   |
                //          - AA - Mid  - middle  - top -   10101010
                //
                //          - AB - Hib  - top           -   10101011    171     Map: Hibernia
                //          - AC - Hib  - middle        -   10101100                |X      |
                //          - AD - Hib  - middle -left  -   10101101            |x   x      |
                //          - AE - Hib  - bottom        -   10101110                |x      |

                // - AF - Alb  - bottom        -   10101111            Map: Albion
                //          - B0 - Alb  - middle -right -   10110000            |X  |
                //          - B1 - Alb  - middle -left  -   10110001            |x   x  |
                //          - B2 - Alb  - top           -   10110010    178     |X  |

                // position   x/y offset  x<<4,y
                foreach (List<byte> obj in fights)
                {
                    pak.WriteByte(obj[0]);// zoneid
                    pak.WriteByte((byte)((obj[1] << 4) | (obj[2] & 0x0f))); // position
                    pak.WriteByte(obj[3]);// color - ( Fights:  0x00 - Grey , 0x01 - RedBlue , 0x02 - RedGreen , 0x03 - GreenBlue )
                    pak.WriteByte(obj[4]);// type  - ( Fights:  Size 0x00 - small  0x01 - medium  0x02 - big 0x03 - huge )
                }

                foreach (List<byte> obj in groups)
                {
                    pak.WriteByte(obj[0]);// zoneid
                    pak.WriteByte((byte)(obj[1] << 4 | obj[2])); // position
                    byte realm = obj[3];

                    pak.WriteByte((byte)((realm == 3) ? 0x04 : (realm == 2) ? 0x02 : 0x01));// color   ( Groups:  0x01 - Alb  , 0x02 - Mid , 0x04 - Hib
                    switch ((eRealm)obj[3])
                    {
                        // type    ( Groups:   Alb:    type       0x03,0x02,0x01   & 0x03
                        //                      Mid:    type << 2  0x0C,0x08,0x04   & 0x03
                        //                      Hib:    type << 4  0x30,0x20,0x10   & 0x03  )
                        default:
                            pak.WriteByte(obj[4]);
                            break;
                        case eRealm.Midgard:
                            pak.WriteByte((byte)(obj[4] << 2));
                            break;
                        case eRealm.Hibernia:
                            pak.WriteByte((byte)(obj[4] << 4));
                            break;
                    }
                }

                SendTCP(pak);
            }
        }

        public virtual void SendWarmapBonuses()
        {
            // 174
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

        public virtual void SendHouse(House house)
        {
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseCreate)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort((ushort)house.Z);
                pak.WriteInt((uint)house.X);
                pak.WriteInt((uint)house.Y);
                pak.WriteShort(house.Heading);
                pak.WriteShort((ushort)house.PorchRoofColor);
                int flagPorchAndGuildEmblem = (house.Emblem & 0x010000) >> 13;// new Guild Emblem
                if (house.Porch)
                {
                    flagPorchAndGuildEmblem |= 1;
                }

                if (house.OutdoorGuildBanner)
                {
                    flagPorchAndGuildEmblem |= 2;
                }

                if (house.OutdoorGuildShield)
                {
                    flagPorchAndGuildEmblem |= 4;
                }

                pak.WriteShort((ushort)flagPorchAndGuildEmblem);
                pak.WriteShort((ushort)house.Emblem);
                pak.WriteShort(0); // new in 1.89b+ (scheduled for resposession XXX hourses ago)
                pak.WriteByte((byte)house.Model);
                pak.WriteByte((byte)house.RoofMaterial);
                pak.WriteByte((byte)house.WallMaterial);
                pak.WriteByte((byte)house.DoorMaterial);
                pak.WriteByte((byte)house.TrussMaterial);
                pak.WriteByte((byte)house.PorchMaterial);
                pak.WriteByte((byte)house.WindowMaterial);
                pak.WriteByte(0);
                pak.WriteShort(0); // new in 1.89b+
                pak.WritePascalString(house.Name);

                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray[new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber)] = GameTimer.GetTickCount();
        }

        public virtual void SendHouseOccupied(House house, bool flagHouseOccuped)
        {
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseChangeGarden)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort(0); // sheduled for repossession (in hours) new in 1.89b+
                pak.WriteByte(0x00);
                pak.WriteByte((byte)(flagHouseOccuped ? 1 : 0));

                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray.UpdateIfExists(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber), GameTimer.GetTickCount());
        }

        public virtual void SendRemoveHouse(House house)
        {
            // 168
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

        public virtual void SendGarden(House house)
        {
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseChangeGarden)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort(0); // sheduled for repossession (in hours) new in 1.89b+
                pak.WriteByte((byte)house.OutdoorItems.Count);
                pak.WriteByte(0x80);

                foreach (var entry in house.OutdoorItems.OrderBy(entry => entry.Key))
                {
                    OutdoorItem item = entry.Value;
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
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseChangeGarden)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort(0); // sheduled for repossession (in hours) new in 1.89b+
                pak.WriteByte(0x01);
                pak.WriteByte(0x00); // update
                OutdoorItem item = house.OutdoorItems[i];
                pak.WriteByte((byte)i);
                pak.WriteShort((ushort)item.Model);
                pak.WriteByte((byte)item.Position);
                pak.WriteByte((byte)item.Rotation);
                SendTCP(pak);
            }

            // Update cache
            GameClient.HouseUpdateArray.UpdateIfExists(new Tuple<ushort, ushort>(house.RegionID, (ushort)house.HouseNumber), GameTimer.GetTickCount());
        }

        public virtual void SendEnterHouse(House house)
        {
            // 189
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseEnter)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteShort(25000);         // constant!
                pak.WriteInt((uint)house.X);
                pak.WriteInt((uint)house.Y);
                pak.WriteShort(house.Heading); // useless/ignored by client.
                pak.WriteByte(0x00);
                pak.WriteByte((byte)(house.GetGuildEmblemFlags() | (house.Emblem & 0x010000) >> 14));// new Guild Emblem
                pak.WriteShort((ushort)house.Emblem);   // emblem
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
                pak.WriteByte(0x00); // houses codemned ?
                pak.WriteShort(0); // 0xFFBF = condemned door model
                pak.WriteByte(0x00);

                SendTCP(pak);
            }
        }

        public virtual void SendExitHouse(House house, ushort unknown = 0)
        {
            // 168
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

        protected virtual void WriteHouseFurniture(GSTCPPacketOut pak, IndoorItem item, int index)
        {
            // 176
            pak.WriteByte((byte)index);
            byte type = 0;
            if (item.Emblem > 0)
            {
                item.Color = item.Emblem;
            }

            if (item.Color > 0)
            {
                if (item.Color <= 0xFF)
                {
                    type |= 1; // colored
                }
                else if (item.Color <= 0xFFFF)
                {
                    type |= 2; // old emblem
                }
                else
                {
                    type |= 6; // new emblem
                }
            }

            if (item.Size != 0)
            {
                type |= 8; // have size
            }

            pak.WriteByte(type);
            pak.WriteShort((ushort)item.Model);
            if ((type & 1) == 1)
            {
                pak.WriteByte((byte)item.Color);
            }
            else if ((type & 6) == 2)
            {
                pak.WriteShort((ushort)item.Color);
            }
            else if ((type & 6) == 6)
            {
                pak.WriteShort((ushort)(item.Color & 0xFFFF));
            }

            pak.WriteShort((ushort)item.X);
            pak.WriteShort((ushort)item.Y);
            pak.WriteShort((ushort)item.Rotation);
            if ((type & 8) == 8)
            {
                pak.WriteByte((byte)item.Size);
            }

            pak.WriteByte((byte)item.Position);
            pak.WriteByte((byte)(item.PlacementMode - 2));
        }

        public virtual void SendFurniture(House house, int i)
        {
            // 168
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

        public virtual void SendHousePayRentDialog(string title)
        {
            // 168
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

        public virtual void SendToggleHousePoints(House house)
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HouseTogglePoints)))
            {
                pak.WriteShort((ushort)house.HouseNumber);
                pak.WriteByte(0x04);
                pak.WriteByte(0x00);

                SendTCP(pak);
            }
        }

        public virtual void SendRentReminder(House house)
        {
            // 168
            // 0:00:58.047 S=>C 0xF7 show help window (topicIndex:106 houseLot?:4281)
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HelpWindow)))
            {
                pak.WriteShort(106); // short index
                pak.WriteShort((ushort)house.HouseNumber); // short lot
                SendTCP(pak);
            }
        }


        public virtual void SendMarketExplorerWindow(IList<InventoryItem> items, byte page, byte maxpage)
        {
            // 168
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

        public virtual void SendMarketExplorerWindow()
        {
            // 168
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MarketExplorerWindow)))
            {
                pak.WriteByte(255);
                pak.Fill(0, 3);
                SendTCP(pak);
            }
        }

        public virtual void SendConsignmentMerchantMoney(long money)
        {
            // 168
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

        public virtual void SendHouseUsersPermissions(House house)
        {
            // 168
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

        public virtual void SendStarterHelp()
        {
            // 168
            // * 0:00:57.984 S=>C 0xF7 show help window (topicIndex:1 houseLot?:0)
            using (var pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.HelpWindow)))
            {
                pak.WriteShort(1); // short index
                pak.WriteShort(0); // short lot
                SendTCP(pak);
            }
        }

        public virtual void SendPlayerFreeLevelUpdate()
        {
            // 171
            GamePlayer player = GameClient.Player;
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(0x09); // subcode

                byte flag = player.FreeLevelState;

                TimeSpan t = new TimeSpan(DateTime.Now.Ticks - player.LastFreeLeveled.Ticks);

                ushort time = 0;

                // time is in minutes
                switch (player.Realm)
                {
                    case eRealm.Albion:
                        time = (ushort)((ServerProperties.Properties.FREELEVEL_DAYS_ALBION * 24 * 60) - t.TotalMinutes);
                        break;
                    case eRealm.Midgard:
                        time = (ushort)((ServerProperties.Properties.FREELEVEL_DAYS_MIDGARD * 24 * 60) - t.TotalMinutes);
                        break;
                    case eRealm.Hibernia:
                        time = (ushort)((ServerProperties.Properties.FREELEVEL_DAYS_HIBERNIA * 24 * 60) - t.TotalMinutes);
                        break;
                }

                // flag 1 = above level, 2 = elligable, 3= time until, 4 = level and time until, 5 = level until
                pak.WriteByte(flag); // flag
                pak.WriteShort(0); // unknown
                pak.WriteShort(time); // time
                SendTCP(pak);
            }
        }

        public virtual void SendMovingObjectCreate(GameMovingObject obj)
        {
            // 168
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

        public virtual void SendSetControlledHorse(GamePlayer player)
        {
            // 180
            if (player == null || player.ObjectState != GameObject.eObjectState.Active)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ControlledHorse)))
            {

                if (player.HasHorse)
                {
                    pak.WriteShort(0); // for set self horse OID must be zero
                    pak.WriteByte(player.ActiveHorse.ID);
                    if (player.ActiveHorse.BardingColor == 0 && player.ActiveHorse.Barding != 0 && player.Guild != null)
                    {
                        int newGuildBitMask = (player.Guild.Emblem & 0x010000) >> 9;
                        pak.WriteByte((byte)(player.ActiveHorse.Barding | newGuildBitMask));
                        pak.WriteShort((ushort)player.Guild.Emblem);
                    }
                    else
                    {
                        pak.WriteByte(player.ActiveHorse.Barding);
                        pak.WriteShort(player.ActiveHorse.BardingColor);
                    }

                    pak.WriteByte(player.ActiveHorse.Saddle);
                    pak.WriteByte(player.ActiveHorse.SaddleColor);
                    pak.WriteByte(player.ActiveHorse.Slots);
                    pak.WriteByte(player.ActiveHorse.Armor);
                    pak.WritePascalString(player.ActiveHorse.Name ?? string.Empty);
                }
                else
                {
                    pak.Fill(0x00, 8);
                }

                SendTCP(pak);
            }
        }

        public virtual void SendControlledHorse(GamePlayer player, bool flag)
        {
            // 180
            if (player == null || player.ObjectState != GameObject.eObjectState.Active)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.ControlledHorse)))
            {
                if (!flag || !player.HasHorse)
                {
                    pak.WriteShort((ushort)player.ObjectID);
                    pak.Fill(0x00, 6);
                }
                else
                {
                    pak.WriteShort((ushort)player.ObjectID);
                    pak.WriteByte(player.ActiveHorse.ID);
                    if (player.ActiveHorse.BardingColor == 0 && player.ActiveHorse.Barding != 0 && player.Guild != null)
                    {
                        int newGuildBitMask = (player.Guild.Emblem & 0x010000) >> 9;
                        pak.WriteByte((byte)(player.ActiveHorse.Barding | newGuildBitMask));
                        pak.WriteShort((ushort)player.Guild.Emblem);
                    }
                    else
                    {
                        pak.WriteByte(player.ActiveHorse.Barding);
                        pak.WriteShort(player.ActiveHorse.BardingColor);
                    }

                    pak.WriteByte(player.ActiveHorse.Saddle);
                    pak.WriteByte(player.ActiveHorse.SaddleColor);
                }

                SendTCP(pak);
            }
        }

        public virtual void CheckLengthHybridSkillsPacket(ref GSTCPPacketOut pak, ref int maxSkills, ref int first)
        {
            // 180
            if (pak.Length > 1500)
            {
                pak.Position = 4;
                pak.WriteByte((byte)(maxSkills - first));
                pak.WriteByte((byte)(first == 0 ? 99 : 0x03)); // subtype
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

        public virtual void SendNonHybridSpellLines()
        {
            SendNonHybridSpellLines_168();

            // 181
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VariousUpdate)))
            {
                pak.WriteByte(0x02); // subcode
                pak.WriteByte(0x00);
                pak.WriteByte(99); // subtype (new subtype 99 in 1.80e)
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        private void SendNonHybridSpellLines_168()
        {
            // 168
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

        public virtual void SendCrash(string str)
        {
            // 168
            using (var pak = new GSTCPPacketOut(0x86))
            {
                pak.WriteByte(0xFF);
                pak.WritePascalString(str);
                SendTCP(pak);
            }
        }

        public virtual void SendRegionColorScheme()
        {
            SendRegionColorScheme(GameServer.ServerRules.GetColorHandling(GameClient));
        }

        public virtual void SendRegionColorScheme(byte color)
        {
            // 171
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                pak.WriteShort(0); // not used
                pak.WriteByte(0x05); // subcode
                pak.WriteByte(color);
                pak.WriteInt(0); // not used
                SendTCP(pak);
            }
        }

        public virtual void SendVampireEffect(GameLiving living, bool show)
        {
            // 174
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

        public virtual void SendXFireInfo(byte flag)
        {
            // 188
            if (GameClient?.Player == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut((byte)eServerPackets.XFire))
            {
                pak.WriteShort((ushort)GameClient.Player.ObjectID);
                pak.WriteByte(flag);
                pak.WriteByte(0x00);
                SendTCP(pak);
            }
        }

        public virtual void SendMinotaurRelicMapRemove(byte id)
        {
            // 186
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.MinotaurRelicMapRemove)))
            {
                pak.WriteIntLowEndian(id);
                SendTCP(pak);
            }
        }

        public virtual void SendMinotaurRelicMapUpdate(byte id, ushort region, int x, int y, int z)
        {
            // 186
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

        public virtual void SendMinotaurRelicWindow(GamePlayer player, int effect, bool flag)
        {
            // 186
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

        public virtual void SendMinotaurRelicBarUpdate(GamePlayer player, int xp)
        {
            // 186
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

        public virtual void SendBlinkPanel(byte flag)
        {
            // 193
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.VisualEffect)))
            {
                GamePlayer player = GameClient.Player;

                pak.WriteShort((ushort)player.ObjectID);
                pak.WriteByte(8);
                pak.WriteByte(flag);
                pak.WriteByte(0);

                SendTCP(pak);
            }
        }


        protected static int MaxNameLength = 55;

        /// <summary>
        /// 1.109 items have an additional byte prior to item.Weight
        /// </summary>
        /// <param name="pak"></param>
        /// <param name="item"></param>
        protected virtual void WriteItemData(GSTCPPacketOut pak, InventoryItem item)
        {
            if (item == null)
            {
                pak.Fill(0x00, 20); // 1.109 +1 byte
                return;
            }

            // Create a GameInventoryItem so item will display correctly in inventory window
            item = GameInventoryItem.Create(item);

            pak.WriteByte((byte)item.Level);

            int value1; // some object types use this field to display count
            int value2; // some object types use this field to display count
            switch (item.Object_Type)
            {
                case (int)eObjectType.GenericItem:
                    value1 = item.Count & 0xFF;
                    value2 = (item.Count >> 8) & 0xFF;
                    break;
                case (int)eObjectType.Arrow:
                case (int)eObjectType.Bolt:
                case (int)eObjectType.Poison:
                    value1 = item.Count;
                    value2 = item.SPD_ABS;
                    break;
                case (int)eObjectType.Thrown:
                    value1 = item.DPS_AF;
                    value2 = item.Count;
                    break;
                case (int)eObjectType.Instrument:
                    value1 = item.DPS_AF == 2 ? 0 : item.DPS_AF;
                    value2 = 0;
                    break; // unused
                case (int)eObjectType.Shield:
                    value1 = item.Type_Damage;
                    value2 = item.DPS_AF;
                    break;
                case (int)eObjectType.AlchemyTincture:
                case (int)eObjectType.SpellcraftGem:
                    value1 = 0;
                    value2 = 0;
                    /*
                    must contain the quality of gem for spell craft and think same for tincture
                    */
                    break;
                case (int)eObjectType.HouseWallObject:
                case (int)eObjectType.HouseFloorObject:
                case (int)eObjectType.GardenObject:
                    value1 = 0;
                    value2 = item.SPD_ABS;
                    /*
                    Value2 byte sets the width, only lower 4 bits 'seem' to be used (so 1-15 only)

                    The byte used for "Hand" (IE: Mini-delve showing a weapon as Left-Hand
                    usabe/TwoHanded), the lower 4 bits store the height (1-15 only)
                    */
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

            pak.Fill(0x00, 1); // 1.109, +1 byte, no clue what this is  - Tolakram

            pak.WriteShort((ushort)item.Weight);
            pak.WriteByte(item.ConditionPercent); // % of con
            pak.WriteByte(item.DurabilityPercent); // % of dur
            pak.WriteByte((byte)item.Quality); // % of qua
            pak.WriteByte((byte)item.Bonus); // % bonus
            pak.WriteShort((ushort)item.Model);
            pak.WriteByte(item.Extension);
            int flag = 0;
            if (item.Emblem != 0)
            {
                pak.WriteShort((ushort)item.Emblem);
                flag |= (item.Emblem & 0x010000) >> 16; // = 1 for newGuildEmblem
            }
            else
            {
                pak.WriteShort((ushort)item.Color);
            }

            // flag |= 0x01; // newGuildEmblem
            flag |= 0x02; // enable salvage button
            AbstractCraftingSkill skill = CraftingMgr.getSkillbyEnum(GameClient.Player.CraftingPrimarySkill);
            if (skill is AdvancedCraftingSkill)
            {
                flag |= 0x04; // enable craft button
            }

            ushort icon1 = 0;
            ushort icon2 = 0;
            string spellName1 = string.Empty;
            string spellName2 = string.Empty;
            if (item.Object_Type != (int)eObjectType.AlchemyTincture)
            {
                SpellLine chargeEffectsLine = SkillBase.GetSpellLine(GlobalSpellsLines.Item_Effects);

                if (chargeEffectsLine != null)
                {
                    if (item.SpellID > 0)
                    {
                        Spell spell = SkillBase.FindSpell(item.SpellID, chargeEffectsLine);
                        if (spell != null)
                        {
                            flag |= 0x08;
                            icon1 = spell.Icon;
                            spellName1 = spell.Name; // or best spl.Name ?
                        }
                    }

                    if (item.SpellID1 > 0)
                    {
                        Spell spell = SkillBase.FindSpell(item.SpellID1, chargeEffectsLine);
                        if (spell != null)
                        {
                            flag |= 0x10;
                            icon2 = spell.Icon;
                            spellName2 = spell.Name; // or best spl.Name ?
                        }
                    }
                }
            }

            pak.WriteByte((byte)flag);
            if ((flag & 0x08) == 0x08)
            {
                pak.WriteShort(icon1);
                pak.WritePascalString(spellName1);
            }

            if ((flag & 0x10) == 0x10)
            {
                pak.WriteShort(icon2);
                pak.WritePascalString(spellName2);
            }

            pak.WriteByte((byte)item.Effect);
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
                    name += $"[{Money.GetShortString(item.SellPrice)}]";
                }
            }

            if (name.Length > MaxNameLength)
            {
                name = name.Substring(0, MaxNameLength);
            }

            pak.WritePascalString(name);
        }

        protected virtual void WriteTemplateData(GSTCPPacketOut pak, ItemTemplate template, int count)
        {
            if (template == null)
            {
                pak.Fill(0x00, 20);  // 1.109 +1 byte
                return;
            }

            pak.WriteByte((byte)template.Level);

            int value1;
            int value2;

            switch (template.Object_Type)
            {
                case (int)eObjectType.Arrow:
                case (int)eObjectType.Bolt:
                case (int)eObjectType.Poison:
                case (int)eObjectType.GenericItem:
                    value1 = count; // Count
                    value2 = template.SPD_ABS;
                    break;
                case (int)eObjectType.Thrown:
                    value1 = template.DPS_AF;
                    value2 = count; // Count
                    break;
                case (int)eObjectType.Instrument:
                    value1 = template.DPS_AF == 2 ? 0 : template.DPS_AF;
                    value2 = 0;
                    break;
                case (int)eObjectType.Shield:
                    value1 = template.Type_Damage;
                    value2 = template.DPS_AF;
                    break;
                case (int)eObjectType.AlchemyTincture:
                case (int)eObjectType.SpellcraftGem:
                    value1 = 0;
                    value2 = 0;
                    /*
                    must contain the quality of gem for spell craft and think same for tincture
                    */
                    break;
                case (int)eObjectType.GardenObject:
                    value1 = 0;
                    value2 = template.SPD_ABS;
                    /*
                    Value2 byte sets the width, only lower 4 bits 'seem' to be used (so 1-15 only)

                    The byte used for "Hand" (IE: Mini-delve showing a weapon as Left-Hand
                    usabe/TwoHanded), the lower 4 bits store the height (1-15 only)
                    */
                    break;

                default:
                    value1 = template.DPS_AF;
                    value2 = template.SPD_ABS;
                    break;
            }

            pak.WriteByte((byte)value1);
            pak.WriteByte((byte)value2);

            if (template.Object_Type == (int)eObjectType.GardenObject)
            {
                pak.WriteByte((byte)template.DPS_AF);
            }
            else
            {
                pak.WriteByte((byte)(template.Hand << 6));
            }

            pak.WriteByte((byte)((template.Type_Damage > 3
                ? 0
                : template.Type_Damage << 6) | template.Object_Type));
            pak.Fill(0x00, 1); // 1.109, +1 byte, no clue what this is  - Tolakram
            pak.WriteShort((ushort)template.Weight);
            pak.WriteByte(template.BaseConditionPercent);
            pak.WriteByte(template.BaseDurabilityPercent);
            pak.WriteByte((byte)template.Quality);
            pak.WriteByte((byte)template.Bonus);
            pak.WriteShort((ushort)template.Model);
            pak.WriteByte(template.Extension);
            if (template.Emblem != 0)
            {
                pak.WriteShort((ushort)template.Emblem);
            }
            else
            {
                pak.WriteShort((ushort)template.Color);
            }

            pak.WriteByte(0); // Flag
            pak.WriteByte((byte)template.Effect);
            pak.WritePascalString(count > 1 ? $"{count} {template.Name}" : template.Name);
        }
    }
}
