using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Numerics;
using Dapper;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.GameScripts.Formulas;
using Darkages.Infrastructure;
using Darkages.Models;
using Darkages.Network.Formats;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Object;
using Darkages.Scripting;
using Darkages.Sprites;
using Darkages.Templates;
using Darkages.Types;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Network.Client
{
    public partial class GameClient : NetworkClient
    {
        public readonly ObjectManager ObjectHandlers;
        public bool MapUpdating;
        public bool Chaos;
        private readonly GameServerTimer _chaos;
        public readonly GameServerTimer SkillSpellTimer;
        private readonly GameServerTimer _dayDreamingTimer;
        private int _chaosCountdown;
        private readonly object _syncClient = new();

        public GameClient()
        {
            SkillSpellTimer = new GameServerTimer(TimeSpan.FromSeconds(1));
            _dayDreamingTimer = new GameServerTimer(TimeSpan.FromSeconds(5));
            _chaos = new GameServerTimer(TimeSpan.FromSeconds(60));
            _chaosCountdown = 5;
            ObjectHandlers = new ObjectManager();
        }

        public bool SerialSent { get; set; }
        public Aisling Aisling { get; set; }
        public bool Authenticated { get; set; }
        public DateTime BoardOpened { get; set; }
        public DialogSession DlgSession { get; set; }

        public bool CanSendLocation
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastLocationSent < new TimeSpan(0, 0, 0, 2);
            }
        }

        public bool IsRefreshing
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastClientRefresh < new TimeSpan(0, 0, 0, 0, ServerSetup.Config.RefreshRate);
            }
        }

        public bool CanRefresh
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastClientRefresh > new TimeSpan(0, 0, 0, 0, 500);
            }
        }

        public bool IsEquipping
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastEquip > new TimeSpan(0, 0, 0, 0, 500);
            }
        }

        public bool IsDayDreaming
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastMessageFromClientNot0X45 > new TimeSpan(0, 0, 2, 0, 0);
            }
        }

        public bool IsMoving
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastMovement > new TimeSpan(0, 0, 0, 0, 850);
            }
        }

        public bool IsWarping
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastWarp < new TimeSpan(0, 0, 0, 0, ServerSetup.Config.WarpCheckRate);
            }
        }

        public bool WasUpdatingMapRecently
        {
            get
            {
                var time = DateTime.UtcNow;
                var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                return readyTime - LastMapUpdated < new TimeSpan(0, 0, 0, 0, 100);
            }
        }

        public readonly Stack<CastInfo> CastStack = new();
        public DateTime LastAssail { get; set; }
        public DateTime LastClientRefresh { get; set; }
        public DateTime LastWarp { get; set; }
        public Item LastItemDropped { get; set; }
        public DateTime LastLocationSent { get; set; }
        public DateTime LastMapUpdated { get; set; }
        public DateTime LastMessageSent { get; set; }
        public DateTime LastMovement { get; set; }
        public DateTime LastEquip { get; set; }
        public DateTime LastPing { get; set; }
        public DateTime LastPingResponse { get; set; }
        public DateTime LastSave { get; set; }
        public DateTime LastWhisperMessageSent { get; set; }
        public PendingBuy PendingBuySessions { get; set; }
        public PendingSell PendingItemSessions { get; set; }
        public PendingBanked PendingBankedSession { get; set; }
        public Server.GameServer Server { get; set; }
        public bool ShouldUpdateMap { get; set; }
        public DateTime LastNodeClicked { get; set; }
        public WorldPortal PendingNode { get; set; }
        public Position LastKnownPosition { get; set; }
        public int MapClicks { get; set; }

        public void BuildSettings()
        {
            Aisling.GameSettings = new List<ClientGameSettings>();

            foreach (var settings in ServerSetup.Config.Settings)
                Aisling.GameSettings.Add(new ClientGameSettings(settings.SettingOff, settings.SettingOn,
                    settings.Enabled));
        }

        public bool CheckReqs(GameClient client, Item item)
        {
            if (client.Aisling.GameMaster)
            {
                if (item.Durability > 1)
                {
                    return true;
                }
            }

            if (client.Aisling.ExpLevel < item.Template.LevelRequired)
            {
                client.SendMessage(0x02, "This item is too powerful for me.");
                return false;
            }

            if (item.Durability <= 0 && item.Template.Flags.HasFlag(ItemFlags.Equipable))
            {
                client.SendMessage(0x02, "I'll need to repair this item before I can use it.");
                return false;
            }

            if (!item.Template.Class.HasFlag(client.Aisling.Path | client.Aisling.PastClass) && item.Template.Class != Class.Peasant)
            {
                client.SendMessage(0x02, "This doesn't quite fit me.");
                return false;
            }

            if (client.Aisling.ExpLevel >= item.Template.LevelRequired
                && (client.Aisling.Path == item.Template.Class || client.Aisling.PastClass == item.Template.Class || item.Template.Class == Class.Peasant))
            {
                if (item.Template.Gender == Gender.Both)
                {
                    return true;
                }

                if (item.Template.Gender == client.Aisling.Gender)
                {
                    return true;
                }

                client.SendMessage(0x02, "I'll only wear this on Tuesday nights.");
                return false;
            }

            client.SendMessage(0x02, "Yea, this isn't going to work.");
            return false;
        }

        public void CloseDialog()
        {
            Send(new ServerFormat30());
        }

        public void DoUpdate(TimeSpan elapsedTime)
        {
            PreventMultiLogging();
            DispatchCasts();
            CheckChaos(elapsedTime);
            HandleBadTrades();
            DeathStatusCheck();
            UpdateStatusBarAndThreat(elapsedTime);
            UpdateSkillSpellCooldown(elapsedTime);
        }

        public void PreventMultiLogging()
        {
            var clones = ObjectHandlers.GetObjects<Aisling>(null, p => string.Equals(p.Username, Aisling.Username, StringComparison.CurrentCultureIgnoreCase)
                                                                       && p.Serial != Aisling.Serial).ToArray();

            if (clones.Length <= 0) return;

            foreach (var aisling in clones)
            {
                if (Aisling != null && aisling != null)
                {
                    aisling.HideFrom(Aisling);
                    Aisling.HideFrom(aisling);
                }

                if (aisling?.Client == null) continue;
                aisling.Remove(true);
                Server.ClientDisconnected(aisling.Client);
                Server.RemoveClient(aisling.Client);
            }

            ObjectHandlers.DelObjects(clones);
        }

        public void CheckChaos(TimeSpan elapsedTime)
        {
            if (!_chaos.Update(elapsedTime)) return;
            if (!ServerSetup.Running || ServerSetup.Game.Clients == null) return;
            if (!Chaos) return;

            _chaosCountdown--;
            var tick = _chaosCountdown.ToString();

            switch (_chaosCountdown)
            {
                case 0:
                    return;
                case 1:
                    SendMessage(0x0C, $"{{=qChaos is nearing in {{=b{tick} {{=qminute.");
                    SendAnimation(49, Aisling, Aisling);
                    return;
                default:
                    SendMessage(0x0C, $"{{=qChaos is nearing in {{=b{tick} {{=qminutes.");
                    SendAnimation(160, Aisling, Aisling, 200);
                    break;
            }
        }

        public void HandleBadTrades()
        {
            if (Aisling.Exchange?.Trader == null) return;

            if (!Aisling.Exchange.Trader.LoggedIn
                || !Aisling.WithinRangeOf(Aisling.Exchange.Trader))
                Aisling.CancelExchange();
        }

        public GameClient Insert(bool update, bool delete)
        {
            var obj = ObjectHandlers.GetObject<Aisling>(null, aisling => aisling.Serial == Aisling.Serial 
                                                                         || string.Equals(aisling.Username, Aisling.Username, StringComparison.CurrentCultureIgnoreCase));

            if (obj == null)
            {
                ObjectHandlers.AddObject(Aisling);
            }
            else
            {
                obj.Remove(update, delete);
                ObjectHandlers.AddObject(Aisling);
            }

            return this;
        }

        public void Interrupt()
        {
            Network.Server.GameServer.CancelIfCasting(this);
            SendLocation();
        }

        public GameClient LeaveArea(bool update = false, bool delete = false)
        {
            if (Aisling.LastMapId == short.MaxValue) Aisling.LastMapId = Aisling.CurrentMapId;

            Aisling.Remove(update, delete);

            if (Aisling.LastMapId != Aisling.CurrentMapId && Aisling.Map.Scripts.Any())
                foreach (var script in Aisling.Map.Scripts.Values)
                {
                    script.OnMapExit(this);
                }

            Aisling.View.Clear();
            return this;
        }

        public GameClient Load()
        {
            if (Aisling == null || Aisling.AreaId == 0) return null;
            if (!ServerSetup.GlobalMapCache.ContainsKey(Aisling.AreaId)) return null;

            SetAislingStartupVariables();

            lock (_syncClient)
            {
                try
                {
                    LoadSkillBook().LoadSpellBook().
                    LoadEquipment().LoadInventory().
                    SendStats(StatusFlags.All);
                    DisableShade().
                    SendSerial().
                    InitDiscoveredMaps().Enter().
                    InitSpellBar().InitIgnoreList().InitLegend().
                    SendProfileUpdate();

                    if (Aisling.IsDead())
                        AislingToGhostForm();
                }
                catch (Exception ex)
                {
                    ServerSetup.Logger($"Unhandled Exception in {nameof(Load)}.");
                    ServerSetup.Logger(ex.Message, LogLevel.Error);
                    ServerSetup.Logger(ex.StackTrace, LogLevel.Error);
                    Crashes.TrackError(ex);
                }
            }

            return null;
        }

        public GameClient SetAislingStartupVariables()
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            LastSave = readyTime;
            LastPingResponse = readyTime;
            PendingItemSessions = null;
            LastLocationSent = readyTime;
            LastMovement = readyTime;
            LastClientRefresh = readyTime;
            LastMessageSent = readyTime;
            BoardOpened = readyTime;
            Aisling.Client = this;
            Aisling.StoneSmithing = 0;
            Aisling.BonusAc = 0;
            Aisling.Exchange = null;
            Aisling.LastMapId = short.MaxValue;
            Aisling.Aegis = 0;
            Aisling.Bleeding = 0;
            Aisling.Rending = 0;
            Aisling.Spikes = 0;
            Aisling.Reaping = 0;
            Aisling.Vampirism = 0;
            Aisling.Haste = 0;
            Aisling.Gust = 0;
            Aisling.Quake = 0;
            Aisling.Rain = 0;
            Aisling.Flame = 0;
            Aisling.Dusk = 0;
            Aisling.Dawn = 0;
            Aisling.Hacked = false;
            Aisling.PasswordAttempts = 0;
            Aisling.MonsterKillCounters = new ConcurrentDictionary<string, KillRecord>();
            Aisling.Loading = true;

            // ToDo: Fix BuildSettings
            //BuildSettings();
            return this;
        }

        public GameClient LoadSkillBook()
        {
            try
            {
                const string procedure = "[SelectSkills]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var skillList = sConn.Query<Skill>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                foreach (var skill in skillList.Where(s => s is { SkillName: { } }))
                {
                    if (!ServerSetup.GlobalSkillTemplateCache.ContainsKey(skill.SkillName)) continue;

                    var skillName = skill.SkillName;
                    var template = ServerSetup.GlobalSkillTemplateCache[skillName];
                    {
                        skill.Template = template;
                    }

                    var newSkill = new Skill
                    {
                        SkillId = skill.SkillId,
                        Icon = skill.Template.Icon,
                        Level = skill.Level,
                        Slot = skill.Slot,
                        SkillName = skill.SkillName,
                        Uses = skill.Uses,
                        CurrentCooldown = skill.CurrentCooldown,
                        Template = skill.Template
                    };

                    Aisling.SkillBook.Skills[skill.Slot] = newSkill;
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            var skillsAvailable = Aisling.SkillBook.Skills.Values.Where(i => i?.Template != null);
            var hasAssail = false;

            foreach (var skill in skillsAvailable)
            {
                switch (skill.SkillName)
                {
                    case null:
                        continue;
                    case "Assail":
                        hasAssail = true;
                        break;
                }

                Send(new ServerFormat2C(skill.Slot, skill.Icon, skill.Name));

                if (skill.CurrentCooldown < skill.Template.Cooldown && skill.CurrentCooldown != 0)
                {
                    Aisling.UsedSkill(skill);
                }

                Skill.AttachScript(skill);
                {
                    Aisling.SkillBook.Set(skill);
                }
            }

            if (hasAssail) return this;

            Skill.GiveTo(Aisling, "Assail", 1);

            return this;
        }

        public GameClient LoadSpellBook()
        {
            try
            {
                const string procedure = "[SelectSpells]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var spellList = sConn.Query<Spell>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                foreach (var spell in spellList.Where(s => s is { SpellName: { } }))
                {
                    if (!ServerSetup.GlobalSpellTemplateCache.ContainsKey(spell.SpellName)) continue;

                    var spellName = spell.SpellName;
                    var template = ServerSetup.GlobalSpellTemplateCache[spellName];
                    {
                        spell.Template = template;
                    }

                    var newSpell = new Spell()
                    {
                        SpellId = spell.SpellId,
                        Icon = spell.Template.Icon,
                        Level = spell.Level,
                        Slot = spell.Slot,
                        SpellName = spell.SpellName,
                        Casts = spell.Casts,
                        CurrentCooldown = spell.CurrentCooldown,
                        Template = spell.Template
                    };

                    Aisling.SpellBook.Spells[spell.Slot] = newSpell;
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            var spellsAvailable = Aisling.SpellBook.Spells.Values.Where(i => i?.Template != null);

            foreach (var spell in spellsAvailable)
            {
                if (spell.SpellName == null) continue;

                Send(new ServerFormat17(spell));
                spell.Lines = spell.Template.BaseLines;

                if (spell.CurrentCooldown < spell.Template.Cooldown && spell.CurrentCooldown != 0)
                {
                    Send(new ServerFormat3F(0, spell.Slot, spell.CurrentCooldown));
                }

                Spell.AttachScript(spell);
                {
                    Aisling.SpellBook.Set(spell);
                }
            }

            return this;
        }

        public GameClient LoadInventory()
        {
            try
            {
                const string procedure = "[SelectInventory]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var itemList = sConn.Query<Item>(procedure, values, commandType: CommandType.StoredProcedure).OrderBy(s => s.InventorySlot);
                foreach (var item in itemList)
                {
                    if (!ServerSetup.GlobalItemTemplateCache.ContainsKey(item.Name)) continue;
                    if (item.InventorySlot is 0 or 60) continue;

                    var itemName = item.Name;
                    var template = ServerSetup.GlobalItemTemplateCache[itemName];
                    {
                        item.Template = template;
                    }

                    if (Aisling.Inventory.Items[item.InventorySlot] != null)
                    {
                        var itemCheckCount = 0;

                        for (byte i = 1; i < 60; i++)
                        {
                            itemCheckCount++;

                            if (Aisling.Inventory.Items[i] == null)
                            {
                                item.InventorySlot = i;
                            }

                            if (itemCheckCount == 59) break;
                        }
                    }

                    var color = (byte)ItemColors.ItemColorsToInt(item.Template.Color);

                    var newItem = new Item
                    {
                        Template = item.Template,
                        ItemId = item.ItemId,
                        Image = item.Template.Image,
                        DisplayImage = item.Template.DisplayImage,
                        Durability = item.Durability,
                        Owner = item.Serial,
                        Identified = item.Identified,
                        InventorySlot = item.InventorySlot,
                        Stacks = item.Stacks,
                        ItemQuality = item.ItemQuality,
                        OriginalQuality = item.OriginalQuality,
                        ItemVariance = item.ItemVariance,
                        WeapVariance = item.WeapVariance,
                        Enchantable = item.Template.Enchantable,
                        Color = color
                    };

                    ItemQualityVariance.SetMaxItemDurability(newItem, newItem.ItemQuality);
                    newItem.GetDisplayName();
                    newItem.NoColorGetDisplayName();

                    Aisling.Inventory.Items[item.InventorySlot] = newItem;
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            var itemsAvailable = Aisling.Inventory.Items.Values;

            foreach (var item in itemsAvailable)
            {
                if (item == null) continue;
                if (string.IsNullOrEmpty(item.Template.Name)) continue;

                item.Scripts = ScriptManager.Load<ItemScript>(item.Template.ScriptName, item);

                if (!string.IsNullOrEmpty(item.Template.WeaponScript))
                    item.WeaponScripts = ScriptManager.Load<WeaponScript>(item.Template.WeaponScript, item);

                if (Aisling.CurrentWeight + item.Template.CarryWeight <= Aisling.MaximumWeight)
                {
                    Aisling.Inventory.Set(item);
                    Inventory.UpdateSlot(Aisling.Client, item);
                    Aisling.CurrentWeight += item.Template.CarryWeight;
                }
                else
                {
                    item.Release(Aisling, Aisling.Position);
                    SendMessage(0x02, string.Format(CultureInfo.CurrentCulture, "You stumble and drop {0}", item.Template.Name));
                }
            }

            return this;
        }

        public GameClient LoadEquipment()
        {
            try
            {
                const string procedure = "[SelectEquipped]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var itemList = sConn.Query<Item>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                var aislingEquipped = Aisling.EquipmentManager.Equipment;

                foreach (var item in itemList.Where(s => s is { Name: { } }))
                {
                    if (!ServerSetup.GlobalItemTemplateCache.ContainsKey(item.Name)) continue;

                    var equip = new EquipmentSlot(item.Slot, item);
                    var itemName = item.Name;
                    var template = ServerSetup.GlobalItemTemplateCache[itemName];
                    {
                        equip.Item.Template = template;
                    }

                    var color = (byte)ItemColors.ItemColorsToInt(item.Template.Color);

                    var newGear = new Item
                    {
                        Template = equip.Item.Template,
                        ItemId = item.ItemId,
                        Slot = item.Slot,
                        Image = item.Template.Image,
                        DisplayImage = item.Template.DisplayImage,
                        Durability = item.Durability,
                        Owner = item.Serial,
                        ItemQuality = item.ItemQuality,
                        OriginalQuality = item.OriginalQuality,
                        ItemVariance = item.ItemVariance,
                        WeapVariance = item.WeapVariance,
                        Enchantable = item.Template.Enchantable,
                        Color = color
                    };

                    ItemQualityVariance.SetMaxItemDurability(newGear, newGear.ItemQuality);
                    newGear.GetDisplayName();
                    newGear.NoColorGetDisplayName();

                    aislingEquipped[newGear.Slot] = new EquipmentSlot(newGear.Slot, newGear);
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            foreach (var (equipmentKey, equipment) in Aisling.EquipmentManager.Equipment)
            {
                if (equipment?.Item?.Template == null) continue;
                equipment.Item.Scripts = ScriptManager.Load<ItemScript>(equipment.Item.Template.ScriptName, equipment.Item);

                if (!string.IsNullOrEmpty(equipment.Item.Template.WeaponScript))
                    equipment.Item.WeaponScripts = ScriptManager.Load<WeaponScript>(equipment.Item.Template.WeaponScript, equipment.Item);

                if (equipment.Item.CanCarry(Aisling))
                {
                    Aisling.CurrentWeight += equipment.Item.Template.CarryWeight;
                    Aisling.Client.Send(new ServerFormat37(equipment.Item, (byte)equipmentKey));
                    var script = equipment.Item.Scripts.Values.FirstOrDefault();
                    if (script == null) continue;
                    script.Equipped(Aisling, (byte)equipmentKey);
                }
                else
                {
                    equipment.Item.ReleaseFromEquipped(Aisling, Aisling.Position);
                    SendMessage(0x02, string.Format(CultureInfo.CurrentCulture, "{0} is too heavy to hold.", equipment.Item.Template.Name));
                }
            }

            return this;
        }

        public void LoadBank()
        {
            Aisling.BankManager = new Bank();

            try
            {
                const string procedure = "[SelectBanked]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var itemList = sConn.Query<Item>(procedure, values, commandType: CommandType.StoredProcedure).OrderBy(s => s.InventorySlot);
                foreach (var item in itemList)
                {
                    if (!ServerSetup.GlobalItemTemplateCache.ContainsKey(item.Name)) continue;

                    var itemName = item.Name;
                    var template = ServerSetup.GlobalItemTemplateCache[itemName];
                    {
                        item.Template = template;
                    }

                    var color = (byte)ItemColors.ItemColorsToInt(item.Template.Color);

                    var newItem = new Item
                    {
                        Template = item.Template,
                        ItemId = item.ItemId,
                        Image = item.Template.Image,
                        DisplayImage = item.Template.DisplayImage,
                        Durability = item.Durability,
                        Owner = item.Serial,
                        Identified = item.Identified,
                        Stacks = item.Stacks,
                        ItemQuality = item.ItemQuality,
                        OriginalQuality = item.OriginalQuality,
                        ItemVariance = item.ItemVariance,
                        WeapVariance = item.WeapVariance,
                        Enchantable = item.Template.Enchantable,
                        Color = color
                    };

                    ItemQualityVariance.SetMaxItemDurability(newItem, newItem.ItemQuality);
                    newItem.GetDisplayName();
                    newItem.NoColorGetDisplayName();

                    Aisling.BankManager.Items[newItem.ItemId] = newItem;
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
        }

        public GameClient InitSpellBar()
        {
            return InitBuffs()
                .InitDeBuffs();
        }

        public GameClient InitBuffs()
        {
            try
            {
                const string procedure = "[SelectBuffs]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var buffs = sConn.Query<Buff>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                var orderedBuffs = buffs.OrderBy(b => b.TimeLeft);

                foreach (var buff in orderedBuffs.Where(s => s is { Name: { } }))
                {
                    var buffCheck = false;
                    Buff buffTemplate = null;

                    foreach (var buffCache in ServerSetup.GlobalBuffCache.Values.Where(buffCache => buffCache.Name == buff.Name))
                    {
                        buffCheck = true;
                        buffTemplate = buffCache;
                    }

                    if (!buffCheck) continue;

                    var newBuff = new Buff()
                    {
                        Animation = buffTemplate.Animation,
                        BuffSpell = buffTemplate.BuffSpell,
                        Icon = buffTemplate.Icon,
                        Name = buff.Name,
                        Cancelled = buffTemplate.Cancelled,
                        Length = buffTemplate.Length,
                        TimeLeft = buff.TimeLeft
                    };

                    Aisling.Buffs[buffTemplate.Name] = newBuff;
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            foreach (var buff in Aisling.Buffs.Select(i => i.Value)) buff.OnDurationUpdate(Aisling, buff);

            return this;
        }

        public GameClient InitDeBuffs()
        {
            try
            {
                const string procedure = "[SelectDeBuffs]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                var deBuffs = sConn.Query<Debuff>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                var orderedDebuffs = deBuffs.OrderBy(d => d.TimeLeft);

                foreach (var deBuff in orderedDebuffs.Where(s => s is { Name: { } }))
                {
                    var debuffCheck = false;
                    Debuff debuffTemplate = null;

                    foreach (var debuffCache in ServerSetup.GlobalDeBuffCache.Values.Where(debuffCache => debuffCache.Name == deBuff.Name))
                    {
                        debuffCheck = true;
                        debuffTemplate = debuffCache;
                    }

                    if (!debuffCheck) continue;

                    var newDeBuff = new Debuff()
                    {
                        Animation = debuffTemplate.Animation,
                        DebuffSpell = debuffTemplate.DebuffSpell,
                        Icon = debuffTemplate.Icon,
                        Name = deBuff.Name,
                        Cancelled = debuffTemplate.Cancelled,
                        Length = debuffTemplate.Length,
                        TimeLeft = deBuff.TimeLeft
                    };

                    Aisling.Debuffs[debuffTemplate.Name] = newDeBuff;
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            foreach (var debuff in Aisling.Debuffs.Select(i => i.Value)) debuff.OnDurationUpdate(Aisling, debuff);

            return this;
        }

        public GameClient InitLegend()
        {
            try
            {
                const string procedure = "[SelectLegends]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();

                var legends = sConn.Query<Legend.LegendItem>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                foreach (var legend in legends.Where(s => s is not null).OrderBy(s => s.Time))
                {
                    var newLegend = new Legend.LegendItem()
                    {
                        LegendId = legend.LegendId,
                        Category = legend.Category,
                        Time = legend.Time,
                        Color = legend.Color,
                        Icon = legend.Icon,
                        Value = legend.Value
                    };

                    Aisling.LegendBook.LegendMarks.Add(newLegend);
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            Aisling.Loading = false;
            return this;
        }

        public GameClient InitDiscoveredMaps()
        {
            try
            {
                const string procedure = "[SelectDiscoveredMaps]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();

                var discovered = sConn.Query<DiscoveredMap>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                foreach (var map in discovered.Where(s => s is not null))
                {
                    var temp = new DiscoveredMap()
                    {
                        DmId = map.DmId,
                        Serial = map.Serial,
                        MapId = map.MapId
                    };

                    Aisling.DiscoveredMaps.Add(temp.MapId);
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            return this;
        }

        public GameClient InitIgnoreList()
        {
            try
            {
                const string procedure = "[SelectIgnoredPlayers]";
                var values = new { Aisling.Serial };
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();

                var ignoredRecords = sConn.Query<IgnoredRecord>(procedure, values, commandType: CommandType.StoredProcedure).ToList();
                foreach (var ignored in ignoredRecords.Where(s => s is not null))
                {
                    if (ignored.PlayerIgnored is null) continue;

                    var temp = new IgnoredRecord()
                    {
                        Id = ignored.Id,
                        Serial = ignored.Serial,
                        PlayerIgnored = ignored.PlayerIgnored
                    };

                    Aisling.IgnoredList.Add(temp.PlayerIgnored);
                }
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }

            return this;
        }

        public GameClient SendProfileUpdate()
        {
            Send(new ServerFormat49());
            return this;
        }

        public void SendMapMusic()
        {
            if (Aisling.Map == null) return;
            Send(new ServerFormat19(Aisling.Client, (byte)Aisling.Map.Music));
        }

        public GameClient SendStats(StatusFlags flags)
        {
            Send(new ServerFormat08(Aisling, flags));

            return this;
        }

        public GameClient PayItemPrerequisites(LearningPredicate prerequisites)
        {
            if (prerequisites.ItemsRequired is not { Count: > 0 }) return this;

            foreach (var retainer in prerequisites.ItemsRequired)
            {
                var item = Aisling.Inventory.Get(i => i.Template.Name == retainer.Item);

                foreach (var i in item)
                {
                    if (!i.Template.Flags.HasFlag(ItemFlags.Stackable))
                    {
                        Aisling.EquipmentManager.RemoveFromInventory(i, i.Template.CarryWeight > 0);
                        break;
                    }

                    Inventory.RemoveRange(Aisling.Client, i, retainer.AmountRequired);
                    break;
                }
            }

            return this;
        }

        public bool PayPrerequisites(LearningPredicate prerequisites)
        {
            if (prerequisites == null) return false;

            PayItemPrerequisites(prerequisites);
            {
                if (prerequisites.GoldRequired > 0)
                {
                    Aisling.GoldPoints -= prerequisites.GoldRequired;
                    if (Aisling.GoldPoints <= 0)
                        Aisling.GoldPoints = 0;
                }

                SendStats(StatusFlags.StructC);
                return true;
            }
        }

        public void ClientRefreshed()
        {
            if (!CanRefresh) return;

            var cluster = new List<NetworkFormat>
            {
                new ServerFormat15(Aisling.Map),
                new ServerFormat04(Aisling)
            };

            Send(cluster.ToArray());
            cluster.Clear();

            UpdateDisplay(true);

            var objects = ObjectHandlers.GetObjects(Aisling.Map, s => s.WithinRangeOf(Aisling), ObjectManager.Get.AllButAislings).ToList();

            if (objects.Any())
                cluster.Add(new ServerFormat07(objects));

            cluster.Add(new ServerFormat58());
            cluster.Add(new ServerFormat33(Aisling));
            cluster.Add(new ServerFormat22());

            Send(cluster.ToArray());

            if (Aisling.Blind == 0x08)
                Aisling.Client.SendStats(StatusFlags.StructD);

            var time = DateTime.UtcNow;
            Aisling.Client.LastMapUpdated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            Aisling.Client.LastLocationSent = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            Aisling.Client.LastClientRefresh = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
        }

        public GameClient RefreshMap(bool updateView = false)
        {
            MapUpdating = true;

            if (Aisling.Blind == 0x08)
                Aisling.Client.SendStats(StatusFlags.StructD);

            var cluster = new List<NetworkFormat>
            {
                new ServerFormat67(),
                new ServerFormat15(Aisling.Map),
                new ServerFormat04(Aisling)
            };

            Send(cluster.ToArray());

            if (Aisling.Map is not { Scripts: null }) return this;

            if (!string.IsNullOrEmpty(Aisling.Map.ScriptKey))
            {
                Aisling.Map.Scripts = ScriptManager.Load<AreaScript>(Aisling.Map.ScriptKey, Aisling.Map);
            }

            return this;
        }

        public GameClient DisableShade()
        {
            const byte shade = 0x005;
            var format20 = new ServerFormat20 { Shade = shade };

            foreach (var client in Server.Clients.Values.Where(client => client != null))
            {
                client.Send(format20);
            }

            return this;
        }

        public async Task<GameClient> Save()
        {
            if (Aisling == null) return this;

            var saved = await StorageManager.AislingBucket.Save(Aisling);

            if (!saved) return this;
            var time = DateTime.UtcNow;
            LastSave = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            return this;
        }

        public void Say(string message, byte type = 0x00)
        {
            var response = new ServerFormat0D
            {
                Serial = Aisling.Serial,
                Type = type,
                Text = message
            };

            Aisling.Show(Scope.NearbyAislings, response);
        }

        public void SendAnimation(ushort animation, Sprite to, Sprite from, byte speed = 100)
        {
            ServerFormat29 format;

            if (from is Aisling aisling)
                format = new ServerFormat29((uint)aisling.Serial, (uint)to.Serial, animation, 0, speed);
            else
                format = new ServerFormat29((uint)from.Serial, (uint)to.Serial, animation, 0, speed);

            Aisling.Show(Scope.NearbyAislings, format);
        }

        public void SendItemSellDialog(Mundane mundane, string text, ushort step, IEnumerable<byte> items)
        {
            Send(new ServerFormat2F(mundane, text, new ItemSellData(step, items)));
        }

        public void SendItemShopDialog(Mundane mundane, string text, ushort step, IEnumerable<ItemTemplate> items)
        {
            Send(new ServerFormat2F(mundane, text, new ItemShopData(step, items)));
        }

        public GameClient SendLocation()
        {
            Send(new ServerFormat04(Aisling));
            var time = DateTime.UtcNow;
            LastLocationSent = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            return this;
        }

        public GameClient SendMessage(byte type, string text)
        {
            Send(new ServerFormat0A(type, text));
            var time = DateTime.UtcNow;
            LastMessageSent = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            return this;
        }

        public GameClient SendMessage(string text)
        {
            Send(new ServerFormat0A(0x02, text));
            var time = DateTime.UtcNow;
            LastMessageSent = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            return this;
        }

        public void SendMessage(Scope scope, byte type, string text)
        {
            var nearby = ObjectHandlers.GetObjects<Aisling>(Aisling.Map, i => i.WithinRangeOf(Aisling));

            switch (scope)
            {
                case Scope.Self:
                    SendMessage(type, text);
                    break;

                case Scope.NearbyAislings:
                    {
                        foreach (var obj in nearby)
                            obj.Client.SendMessage(type, text);
                    }
                    break;

                case Scope.NearbyAislingsExludingSelf:
                    {
                        foreach (var obj in nearby)
                        {
                            if (obj.Serial == Aisling.Serial)
                                continue;

                            obj.Client.SendMessage(type, text);
                        }
                    }
                    break;

                case Scope.AislingsOnSameMap:
                    {
                        foreach (var obj in nearby.Where(n => n.CurrentMapId == Aisling.CurrentMapId))
                            obj.Client.SendMessage(type, text);
                    }
                    break;

                case Scope.All:
                    {
                        var allAislings = ObjectHandlers.GetObjects<Aisling>(null, i => i.WithinRangeOf(Aisling));
                        foreach (var obj in allAislings.Where(n => n.LoggedIn))
                            obj.Client.SendMessage(type, text);
                    }
                    break;
            }
        }

        public void SendOptionsDialog(Mundane mundane, string text, params OptionsDataItem[] options)
        {
            Send(new ServerFormat2F(mundane, text, new OptionsData(options)));
        }

        public void SendOptionsDialog(Mundane mundane, string text, string args, params OptionsDataItem[] options)
        {
            Send(new ServerFormat2F(mundane, text, new OptionsPlusArgsData(options, args)));
        }

        public void SendPopupDialog(Mundane popup, string text, params OptionsDataItem[] options)
        {
            Send(new PopupFormat(popup, text, new OptionsData(options)));
        }

        public GameClient SendSerial()
        {
            Send(new ServerFormat05(Aisling));
            SerialSent = true;

            return this;
        }

        public void SendSkillForgetDialog(Mundane mundane, string text, ushort step)
        {
            Send(new ServerFormat2F(mundane, text, new SkillForfeitData(step)));
        }

        public void SendSkillLearnDialog(Mundane mundane, string text, ushort step, IEnumerable<SkillTemplate> skills)
        {
            Send(new ServerFormat2F(mundane, text, new SkillAcquireData(step, skills)));
        }

        public GameClient SendSound(byte sound, Scope scope = Scope.Self)
        {
            Aisling.Show(scope, new ServerFormat19(sound));
            return this;
        }

        public GameClient SendSurroundingSound(byte sound, Scope scope = Scope.NearbyAislings)
        {
            Aisling.Show(scope, new ServerFormat19(sound));
            return this;
        }

        public GameClient SendMapWideSound(byte sound, Scope scope = Scope.AislingsOnSameMap)
        {
            Aisling.Show(scope, new ServerFormat19(sound));
            return this;
        }

        public void SendSpellForgetDialog(Mundane mundane, string text, ushort step)
        {
            Send(new ServerFormat2F(mundane, text, new SpellForfeitData(step)));
        }

        public void SendSpellLearnDialog(Mundane mundane, string text, ushort step, IEnumerable<SpellTemplate> spells)
        {
            Send(new ServerFormat2F(mundane, text, new SpellAcquireData(step, spells)));
        }

        public void DeathStatusCheck()
        {
            var proceed = false;

            if (Aisling.CurrentHp <= 0)
            {
                Aisling.CurrentHp = 1;
                proceed = true;
            }

            if (!proceed) return;
            SendStats(StatusFlags.StructB);

            if (Aisling.Map.Flags.HasFlag(MapFlags.PlayerKill))
            {
                for (var i = 0; i < 2; i++)
                    Aisling.RemoveBuffsAndDebuffs();

                // ToDo: Create a different method for player kills
                Aisling.CastDeath();

                var target = Aisling.Target;

                if (target != null)
                {
                    if (target is Aisling aisling)
                        SendMessage(Scope.NearbyAislings, 0x02, $"{Aisling.Username} has been killed by {aisling.Username}.");
                }
                else
                {
                    SendMessage(Scope.NearbyAislings, 0x02, $"{Aisling.Username} has died.");
                }

                return;
            }

            if (Aisling.CurrentMapId == ServerSetup.Config.DeathMap) return;
            if (Aisling.Skulled) return;

            var debuff = new debuff_reaping();
            {
                debuff.OnApplied(Aisling, debuff);
            }
        }

        public GameClient SystemMessage(string lpmessage)
        {
            SendMessage(0x03, lpmessage);
            return this;
        }

        public GameClient TransitionToMap(Area area, Position position)
        {
            if (area == null) return this;

            if (area.ID != Aisling.CurrentMapId)
            {
                LeaveArea(true, true);

                Aisling.LastPosition = new Position(Aisling.Pos);
                Aisling.Pos = new Vector2(position.X, position.Y);
                Aisling.CurrentMapId = area.ID;

                Enter();
            }
            else
            {
                WarpTo(position, false);
            }

            // ToDo: Logic to only play this if a menu is opened.
            //CloseDialog();

            return this;
        }

        public GameClient TransitionToMap(int area, Position position)
        {
            if (!ServerSetup.GlobalMapCache.ContainsKey(area)) return this;
            var target = ServerSetup.GlobalMapCache[area];
            if (target == null) return this;

            if (Aisling.LastMapId != target.ID)
            {
                LeaveArea(true, true);

                Aisling.LastPosition = new Position(Aisling.Pos);
                Aisling.Pos = new Vector2(position.X, position.Y);
                Aisling.CurrentMapId = target.ID;

                Enter();
            }
            else
            {
                Aisling.LastPosition = new Position(Aisling.Pos);
                Aisling.Pos = new Vector2(position.X, position.Y);
                Aisling.CurrentMapId = target.ID;

                WarpTo(new Position(Aisling.Pos), false);
            }

            return this;
        }

        public void Update(TimeSpan elapsedTime)
        {
            if (Aisling is not { LoggedIn: true }) return;
            
            // ToDo: Summoned Pets not yet implemented
            //lock (_syncClient)
            //{
            //    Aisling.SummonObjects?.Update(elapsedTime);

            //    if (Aisling.SummonObjects != null && !Aisling.SummonObjects.Spawns.Any())
            //    {
            //        Aisling.SummonObjects = null;
            //    }
            //}

            // Check Traps on PvP maps for activation
            lock (Trap.Traps)
            {
                foreach (var trap in Trap.Traps.Select(i => i.Value))
                {
                    if (trap == null) continue;
                    trap.Update();

                    if (trap.Owner != null &&
                        trap.Owner.Serial != Aisling.Serial &&
                        Aisling.X == trap.Location.X &&
                        Aisling.Y == trap.Location.Y && Aisling.Map.Flags.HasFlag(MapFlags.PlayerKill))
                        Trap.Activate(trap, Aisling);
                }
            }

            // Logic based on player's set ActiveStatus
            switch (Aisling.ActiveStatus)
            {
                case ActivityStatus.Awake:
                    DaydreamingRoutine(elapsedTime);
                    break;
                case ActivityStatus.DoNotDisturb:
                    break;
                case ActivityStatus.DayDreaming:
                    if (_dayDreamingTimer.Update(elapsedTime) & Aisling.Direction is 1 or 2)
                    {
                        Aisling.Show(Scope.NearbyAislings, new ServerFormat1A(Aisling.Serial, 16, 120));
                        SendAnimation(32, Aisling, Aisling, 200);
                    }
                    break;
                case ActivityStatus.NeedGroup:
                    DaydreamingRoutine(elapsedTime);
                    break;
                case ActivityStatus.Grouped:
                    DaydreamingRoutine(elapsedTime);
                    break;
                case ActivityStatus.LoneHunter:
                    DaydreamingRoutine(elapsedTime);
                    break;
                case ActivityStatus.GroupHunter:
                    DaydreamingRoutine(elapsedTime);
                    break;
                case ActivityStatus.NeedHelp:
                    DaydreamingRoutine(elapsedTime);
                    break;
            }

            // Normal players without ghost walking, check for walls
            if (!Aisling.GameMaster)
            {
                if (Aisling.Map.IsAStarWall(Aisling, Aisling.X, Aisling.Y))
                {
                    if (LastKnownPosition != null)
                    {
                        Aisling.X = LastKnownPosition.X;
                        Aisling.Y = LastKnownPosition.Y;
                    }

                    SendLocation();
                }
                else
                {
                    LastKnownPosition = new Position(Aisling.X, Aisling.Y);
                }
            }
            
            // Lag disconnector and routine update for client
            VariableLagDisconnector(45);
            DoUpdate(elapsedTime);
        }

        public void DaydreamingRoutine(TimeSpan elapsedTime)
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            if (Aisling.ActiveStatus == ActivityStatus.DayDreaming) return;
            if (!((readyTime - LastMessageFromClientNot0X45).TotalMinutes > 2)) return;
            if (!(_dayDreamingTimer.Update(elapsedTime) & Aisling.Direction is 1 or 2)) return;
            if (!Socket.Connected || !IsDayDreaming) return;

            Aisling.Show(Scope.NearbyAislings, new ServerFormat1A(Aisling.Serial, 16, 120));
            SendAnimation(32, Aisling, Aisling, 200);
        }

        public void VariableLagDisconnector(int delay)
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            if (!((readyTime - LastMessageFromClient).TotalSeconds > delay)) return;
            Aisling?.Remove(true);
            Server.ClientDisconnected(this);
            Server.RemoveClient(this);
        }

        public void DispatchCasts()
        {
            if (!CastStack.Any()) return;

            while (CastStack.Any())
            {
                if (CastStack.Count == 0) break;

                CastStack.TryPeek(out var stack);
                if (stack == null) return;

                var spell = Aisling.SpellBook.GetSpells(i => i.Slot == stack.Slot).FirstOrDefault();

                if (spell == null) continue;
                if (stack.Target == 0 && spell.Template.TargetType != SpellTemplate.SpellUseType.NoTarget)
                {
                    if (CastStack.Count <= 0) return;
                    CastStack.TryPop(out _);
                    continue;
                }

                if (!spell.Ready)
                {
                    try
                    {
                        if (CastStack.Count <= 0) return;
                        CastStack.TryPop(out _);
                        continue;
                    }
                    catch
                    {
                        return;
                    }
                }

                Aisling.CastSpell(spell);
            }
        }

        public GameClient UpdateDisplay(bool excludeSelf = false)
        {
            var response = new ServerFormat33(Aisling);

            if (!excludeSelf) Aisling.Show(Scope.Self, response);

            var nearbyAislings = Aisling.AislingsNearby();

            if (!nearbyAislings.Any()) return this;

            var self = Aisling;

            foreach (var nearby in nearbyAislings)
            {
                if (nearby is null) return this;
                if (self.Serial == nearby.Serial) continue;

                if (self.CanSeeSprite(nearby))
                    nearby.ShowTo(self);
                else
                    nearby.HideFrom(self);

                if (nearby.CanSeeSprite(self))
                    self.ShowTo(nearby);
                else
                    self.HideFrom(nearby);
            }

            return this;
        }

        public void UpdateStatusBarAndThreat(TimeSpan elapsedTime)
        {
            lock (_syncClient)
            {
                Aisling.UpdateBuffs(elapsedTime);
                Aisling.UpdateDebuffs(elapsedTime);
                Aisling.ThreatGeneratedSubsided(Aisling, elapsedTime);
            }
        }

        public void UpdateSkillSpellCooldown(TimeSpan elapsedTime)
        {
            if (!SkillSpellTimer.Update(elapsedTime)) return;

            foreach (var skill in Aisling.SkillBook.Skills.Values)
            {
                if (skill == null) continue;
                skill.CurrentCooldown--;
                if (skill.CurrentCooldown < 0)
                    skill.CurrentCooldown = 0;
            }

            foreach (var spell in Aisling.SpellBook.Spells.Values)
            {
                if (spell == null) continue;
                spell.CurrentCooldown--;
                if (spell.CurrentCooldown < 0)
                    spell.CurrentCooldown = 0;
            }
        }

        public void WarpToAdjacentMap(WarpTemplate warps)
        {
            if (warps.WarpType == WarpType.World) return;

            if (!Aisling.GameMaster)
            {
                if (warps.LevelRequired > 0 && Aisling.ExpLevel < warps.LevelRequired)
                {
                    var msgTier = Math.Abs(Aisling.ExpLevel - warps.LevelRequired);

                    SendMessage(0x03, msgTier <= 15
                        ? $"You're too afraid to enter. {{=c{warps.LevelRequired} REQ"
                        : $"{{=bNightmarish visions of your own death repel you. {{=c{warps.LevelRequired} REQ)");

                    return;
                }
            }

            if (Aisling.Map.ID != warps.To.AreaID)
            {
                TransitionToMap(warps.To.AreaID, warps.To.Location);
            }
            else
            {
                WarpTo(warps.To.Location, false);
            }
        }

        public void WarpTo(Position position, bool overrideRefresh)
        {
            Aisling.Pos = new Vector2(position.X, position.Y);
            if (overrideRefresh) return;
            ClientRefreshed();
        }

        public GameClient Enter()
        {
            Insert(true, false);
            RefreshMap();
            UpdateDisplay(true);
            CompleteMapTransition();

            var time = DateTime.UtcNow;
            Aisling.Client.LastMapUpdated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            Aisling.Client.LastLocationSent = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            foreach (var script in Aisling.Map.Scripts.Values)
            {
                script.OnMapEnter(this);
            }

            return this;
        }

        public void CompleteMapTransition()
        {
            var oldMap = new Area();
            var newMap = new Area();

            foreach (var (_, area) in ServerSetup.GlobalMapCache)
            {
                if (area.ID == Aisling.CurrentMapId)
                {
                    var onMap = Aisling.Map.IsLocationOnMap(Aisling);

                    if (!onMap)
                    {
                        TransitionToMap(136, new Position(5, 7));
                        SendMessage(0x02, "Something grabs your hand...");
                    }

                    newMap = area;

                    if (area.ID == 7000 && oldMap != newMap)
                    {
                        Aisling.Client.SendMessage(0x08,
                            "{=bLife{=a, all that you know, love, and cherish. Everything, and the very fabric of their being. \n\nThe aisling spark, creativity, passion. All of that lives within you." +
                            "\n\nThis story begins shortly after Anaman Pact successfully revives {=bChadul{=a. \n\n-{=cYou feel a sense of unease come over you{=a-");
                    }
                }

                if (area.ID == Aisling.LastMapId)
                {
                    oldMap = area;
                }
            }

            if (Aisling.CurrentMapId == Aisling.LastMapId) return;
            Aisling.LastMapId = Aisling.CurrentMapId;

            if (Aisling.DiscoveredMaps.All(i => i != Aisling.CurrentMapId))
                AddDiscoveredMapToDb();

            var objects = ObjectHandlers.GetObjects(Aisling.Map, s => s.WithinRangeOf(Aisling), ObjectManager.Get.AllButAislings).ToList();
            var cluster = new List<NetworkFormat>();

            if (objects.Any())
                cluster.Add(new ServerFormat07(objects));

            cluster.Add(new ServerFormat1F());

            if (oldMap.Music != newMap.Music)
            {
                cluster.Add(new ServerFormat19(Aisling.Client, (byte)Aisling.Map.Music));
            }

            cluster.Add(new ServerFormat58());
            cluster.Add(new ServerFormat33(Aisling));

            Send(cluster.ToArray());
            MapUpdating = false;
        }

        public void AddDiscoveredMapToDb()
        {
            try
            {
                Aisling.DiscoveredMaps.Add(Aisling.CurrentMapId);

                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();

                sConn.Open();
                var discovered = Generator.GenerateNumber();
                var playerDiscoveredMaps = "INSERT INTO ZolianPlayers.dbo.PlayersDiscoveredMaps (DiscoveredId, Serial, MapId) VALUES " +
                                           $"('{discovered}','{Aisling.Serial}','{Aisling.CurrentMapId}')";
                var cmd2 = new SqlCommand(playerDiscoveredMaps, sConn);
                cmd2.CommandTimeout = 5;

                adapter.InsertCommand = cmd2;
                adapter.InsertCommand.ExecuteNonQuery();
            }
            catch (SqlException e)
            {
                if (e.Message.Contains("PK__Players"))
                {
                    foreach (var client in ServerSetup.Game.Clients.Values)
                    {
                        if (!e.Message.Contains(Aisling.Serial.ToString())) continue;
                        if (client.Aisling.Serial != Aisling.Serial) continue;
                        client.SendMessage(0x03, "Saving new discovery failed. Alert GM");
                        Crashes.TrackError(e);
                        return;
                    }
                }

                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
        }

        public void AddToIgnoreListDb(string ignored)
        {
            try
            {
                Aisling.IgnoredList.Add(ignored);

                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();

                sConn.Open();
                var ignoreId = Generator.GenerateNumber();
                var playerIgnored = "INSERT INTO ZolianPlayers.dbo.PlayersIgnoreList (Id, Serial, PlayerIgnored) VALUES " +
                                           $"('{ignoreId}','{Aisling.Serial}','{ignored}')";
                var cmd2 = new SqlCommand(playerIgnored, sConn);
                cmd2.CommandTimeout = 5;

                adapter.InsertCommand = cmd2;
                adapter.InsertCommand.ExecuteNonQuery();
            }
            catch (SqlException e)
            {
                if (e.Message.Contains("PK__Players"))
                {
                    foreach (var client in ServerSetup.Game.Clients.Values)
                    {
                        if (!e.Message.Contains(Aisling.Serial.ToString())) continue;
                        if (client.Aisling.Serial != Aisling.Serial) continue;
                        client.SendMessage(0x03, "Player wasn't added to your list. Please try again.");
                        Crashes.TrackError(e);
                        return;
                    }
                }

                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
        }

        public void RemoveFromIgnoreListDb(string ignored)
        {
            try
            {
                Aisling.IgnoredList.Remove(ignored);

                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                const string playerIgnored = "DELETE FROM ZolianPlayers.dbo.PlayersIgnoreList WHERE Serial = @Serial AND PlayerIgnored = @ignored";
                sConn.Execute(playerIgnored, new
                {
                    Aisling.Serial,
                    ignored
                });
                sConn.Close();
            }
            catch (SqlException e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
                Crashes.TrackError(e);
            }
        }
    }
}