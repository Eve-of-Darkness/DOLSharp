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
using DOL.GS.PlayerTitles;
using log4net;
using DOL.GS.Housing;

namespace DOL.GS.PacketHandler
{
    [PacketLib(175, GameClient.eClientVersion.Version175)]
    public class PacketLib175 : PacketLib174
    {
        /// <summary>
        /// Defines a logger for this class.
        /// </summary>
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Constructs a new PacketLib for Version 1.75 clients
        /// </summary>
        /// <param name="client">the gameclient this lib is associated with</param>
        public PacketLib175(GameClient client) : base(client)
        {
        }

        public override void SendCustomTextWindow(string caption, IList<string> text)
        {
            if (text == null)
            {
                return;
            }

            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                pak.WriteByte(0); // new in 1.75
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

        public override void SendPlayerTitles()
        {
            var titles = GameClient.Player.Titles;
            using (GSTCPPacketOut pak = new GSTCPPacketOut(GetPacketCode(eServerPackets.DetailWindow)))
            {
                pak.WriteByte(1); // new in 1.75
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

        public override void SendPlayerTitleUpdate(GamePlayer player)
        {
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
                pak.WriteByte(0x0e); // number of entry
                pak.WriteByte(0x00); // subtype
                pak.WriteByte(0x00); // unk
                // entry :
                pak.WriteByte(player.GetDisplayLevel(GameClient.Player)); // level
                pak.WritePascalString(player.Name);

                pak.WriteByte((byte)(player.MaxHealth >> 8)); // maxhealth high byte ?
                pak.WritePascalString(player.CharacterClass.Name); // class name
                pak.WriteByte((byte)(player.MaxHealth & 0xFF)); // maxhealth low byte ?

                pak.WritePascalString(/*"The "+*/player.CharacterClass.Profession); // Profession

                pak.WriteByte(0x00); // unk

                pak.WritePascalString(player.CharacterClass.GetTitle(player, player.Level));

                // todo make function to calcule realm rank
                // client.Player.RealmPoints
                // todo i think it s realmpoint percent not realrank
                pak.WriteByte((byte)player.RealmLevel); // urealm rank
                pak.WritePascalString(player.RealmRankTitle(player.Client.Account.Language));
                pak.WriteByte((byte)player.RealmSpecialtyPoints); // realm skill points

                pak.WritePascalString(player.CharacterClass.BaseName); // base class

                pak.WriteByte((byte)(HouseMgr.GetHouseNumberByPlayer(player) >> 8)); // personal house high byte
                pak.WritePascalString(player.GuildName);
                pak.WriteByte((byte)(HouseMgr.GetHouseNumberByPlayer(player) & 0xFF)); // personal house low byte

                pak.WritePascalString(player.LastName);

                pak.WriteByte(0x0); // ML Level
                pak.WritePascalString(player.RaceName);

                pak.WriteByte(0x0);
                pak.WritePascalString(player.GuildRank?.Title ?? string.Empty);

                pak.WriteByte(0x0);

                AbstractCraftingSkill skill = CraftingMgr.getSkillbyEnum(player.CraftingPrimarySkill);
                pak.WritePascalString(skill?.Name ?? "None");

                pak.WriteByte(0x0);
                pak.WritePascalString(player.CraftTitle.GetValue(player, player)); // crafter title: legendary alchemist

                pak.WriteByte(0x0);
                pak.WritePascalString("None"); // ML title

                // new in 1.75
                pak.WriteByte(0x0);
                string title = "None";
                if (player.CurrentTitle != PlayerTitleMgr.ClearTitle)
                {
                    title = GameServer.ServerRules.GetPlayerTitle(player, player);
                }

                pak.WritePascalString(title); // new in 1.74
                SendTCP(pak);
            }
        }

        public override void SendCharStatsUpdate()
        {
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

        public override void SendCharResistsUpdate()
        {
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

                // Dinberg:Instances - as with all objects, we need to use a zoneSkinID for clientside positioning.
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
                pak.WriteByte(0x00); // new in 1.75
                SendTCP(pak);
            }

            // Update Cache
            GameClient.GameObjectUpdateArray[new Tuple<ushort, ushort>(playerToCreate.CurrentRegionID, (ushort)playerToCreate.ObjectID)] = GameTimer.GetTickCount();

            SendObjectGuildID(playerToCreate, playerToCreate.Guild); // used for nearest friendly/enemy object buttons and name colors on PvP server
        }

        public override void SendLoginGranted(byte color)
        {
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

        public override void SendLoginGranted()
        {
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
    }
}
