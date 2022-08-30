using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Network.Client;
using Darkages.Network.Formats;
using Darkages.Network.Formats.Models.ClientFormats;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Network.GameServer;
using Darkages.Network.GameServer.Components;
using Darkages.Object;
using Darkages.Scripting;
using Darkages.Sprites;
using Darkages.Systems;
using Darkages.Types;

using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.Logging;

using ServiceStack;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Numerics;
using System.Text;
using Darkages.GameScripts.Mundanes.Generic;
using Darkages.Models;

namespace Darkages.Network.Server
{
    public class GameServer : NetworkServer<GameClient>
    {
        public readonly ObjectService ObjectFactory = new();
        public readonly ObjectManager ObjectHandlers = new();
        private ConcurrentDictionary<Type, GameServerComponent> _serverComponents;
        private DateTime _fastGameTime;
        private DateTime _normalGameTime;
        private DateTime _slowGameTime;
        private const int FastGameSpeed = 40;
        private const int NormalGameSpeed = 80;
        private const int SlowGameSpeed = 120;

        public GameServer(int capacity)
        {
            RegisterServerComponents();
        }

        private void RegisterServerComponents()
        {
            lock (ServerSetup.SyncLock)
            {
                _serverComponents = new ConcurrentDictionary<Type, GameServerComponent>
                {
                    [typeof(InterestAndCommunityComponent)] = new InterestAndCommunityComponent(this),
                    [typeof(MessageClearComponent)] = new MessageClearComponent(this),
                    [typeof(MonolithComponent)] = new MonolithComponent(this),
                    [typeof(MundaneComponent)] = new MundaneComponent(this),
                    [typeof(ObjectComponent)] = new ObjectComponent(this),
                    [typeof(PingComponent)] = new PingComponent(this),
                    [typeof(PlayerRegenerationComponent)] = new PlayerRegenerationComponent(this),
                    [typeof(PlayerSaveComponent)] = new PlayerSaveComponent(this)
                };

                ServerSetup.Logger($"Server Components Loaded: {_serverComponents.Count}");
            }
        }

        public override void Start(int port)
        {
            base.Start(port);

            try
            {
                ServerSetup.Running = true;

                lock (ServerSetup.SyncLock)
                {
                    _ = UpdateComponentsFast();
                    _ = UpdateObjectsNormal();
                    _ = UpdateAreasSlow();
                }
            }
            catch (Exception ex)
            {
                ServerSetup.Logger(ex.Message, LogLevel.Error);
                ServerSetup.Logger(ex.StackTrace, LogLevel.Error);
                Crashes.TrackError(ex);
                ServerSetup.Running = false;
            }
        }

        #region Game Engine

        private async Task UpdateComponentsFast()
        {
            var time = DateTime.UtcNow;
            _fastGameTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            while (ServerSetup.Running)
            {
                var time2 = DateTime.UtcNow;
                var gTimeConvert = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time2, "Eastern Standard Time");
                var gameTime = gTimeConvert - _fastGameTime;

                UpdateComponents(gameTime);

                _fastGameTime += gameTime;

                await Task.Delay(FastGameSpeed).ConfigureAwait(false);
            }
        }

        private async Task UpdateObjectsNormal()
        {
            var time = DateTime.UtcNow;
            _normalGameTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            while (ServerSetup.Running)
            {
                var time2 = DateTime.UtcNow;
                var gTimeConvert = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time2, "Eastern Standard Time");
                var gameTime = gTimeConvert - _normalGameTime;

                UpdateClients(gameTime);
                UpdateMonsters(gameTime);

                _normalGameTime += gameTime;

                await Task.Delay(NormalGameSpeed).ConfigureAwait(false);
            }
        }

        private async Task UpdateAreasSlow()
        {
            var time = DateTime.UtcNow;
            _slowGameTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            while (ServerSetup.Running)
            {
                var time2 = DateTime.UtcNow;
                var gTimeConvert = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time2, "Eastern Standard Time");
                var gameTime = gTimeConvert - _slowGameTime;

                UpdateMundanes(gameTime);
                UpdateMaps(gameTime);

                _slowGameTime += gameTime;

                await Task.Delay(SlowGameSpeed).ConfigureAwait(false);
            }
        }

        private void UpdateComponents(TimeSpan elapsedTime)
        {
            try
            {
                var components = _serverComponents.Select(i => i.Value);

                foreach (var component in components)
                {
                    component?.Update(elapsedTime);
                }
            }
            catch (Exception ex)
            {
                ServerSetup.Logger(ex.Message, LogLevel.Error);
                Crashes.TrackError(ex);
            }
        }

        private void UpdateClients(TimeSpan elapsedTime)
        {
            foreach (var client in Clients.Values.Where(client => client == null))
            {
                ClientDisconnected(client);
                RemoveClient(client);
            }

            foreach (var client in Clients.Values.Where(client => client.Aisling != null))
            {
                try
                {
                    switch (client.IsWarping)
                    {
                        case false when !client.MapOpen:
                            client?.Update(elapsedTime);
                            break;
                        case true:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ServerSetup.Logger(ex.Message, LogLevel.Error);
                    ServerSetup.Logger(ex.StackTrace, LogLevel.Error);
                    Crashes.TrackError(ex);
                    client.Server.ClientDisconnected(client);
                    client.Server.RemoveClient(client);
                }
            }
        }

        private static void UpdateMonsters(TimeSpan elapsedTime)
        {
            foreach (var (_, area) in ServerSetup.GlobalMapCache)
            {
                var updateList = ServerSetup.GlobalMonsterCache.Where(m => m.Value.Map.ID == area.ID);

                foreach (var (_, monster) in updateList)
                {
                    if (monster.Scripts == null) continue;
                    if (monster.CurrentHp <= 0)
                    {
                        foreach (var script in monster.Scripts.Values.Where(_ => monster.Target?.Client != null))
                        {
                            script?.OnDeath(monster.Target.Client);
                        }

                        foreach (var script in monster.Scripts.Values.Where(_ => monster.Target?.Client == null))
                        {
                            script?.OnDeath();
                        }

                        UpdateKillCounters(monster);

                        monster.Skulled = true;
                    }

                    foreach (var script in monster.Scripts.Values)
                    {
                        script?.Update(elapsedTime);
                    }

                    if (monster.TrapsAreNearby())
                    {
                        var nextTrap = Trap.Traps.Select(i => i.Value)
                            .FirstOrDefault(i => i.Location.X == monster.X && i.Location.Y == monster.Y);

                        if (nextTrap != null)
                            Trap.Activate(nextTrap, monster);
                    }

                    monster.UpdateBuffs(elapsedTime);
                    monster.UpdateDebuffs(elapsedTime);

                    var timed = DateTime.UtcNow;
                    monster.LastUpdated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(timed, "Eastern Standard Time");
                }
            }
        }

        private static void UpdateKillCounters(Monster monster)
        {
            if (monster.Target is not Aisling aisling) return;
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            if (!aisling.MonsterKillCounters.ContainsKey(monster.Template.BaseName))
            {
                aisling.MonsterKillCounters[monster.Template.BaseName] =
                    new KillRecord
                    {
                        TotalKills = 1,
                        MonsterLevel = monster.Template.Level,
                        TimeKilled = readyTime
                    };
            }
            else
            {
                aisling.MonsterKillCounters[monster.Template.BaseName].TotalKills++;
                aisling.MonsterKillCounters[monster.Template.BaseName].TimeKilled = readyTime;
                aisling.MonsterKillCounters[monster.Template.BaseName].MonsterLevel = monster.Template.Level;
            }
        }

        private static void UpdateMundanes(TimeSpan elapsedTime)
        {
            foreach (var (_, area) in ServerSetup.GlobalMapCache)
            {
                var updateList = ServerSetup.GlobalMundaneCache.Where(m => m.Value.Map.ID == area.ID);

                foreach (var (_, mundane) in updateList)
                {
                    if (mundane == null) continue;

                    mundane.Update(elapsedTime);

                    var timed = DateTime.UtcNow;
                    mundane.LastUpdated = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(timed, "Eastern Standard Time");
                }
            }
        }

        private static void UpdateMaps(TimeSpan elapsedTime)
        {
            HashSet<Area> tmpAreas;

            lock (ServerSetup.SyncLock)
            {
                tmpAreas = new HashSet<Area>(ServerSetup.GlobalMapCache.Values);
            }

            foreach (var map in tmpAreas)
            {
                map.Update(elapsedTime);
            }
        }

        #endregion

        #region Client Handlers

        /// <summary>
        /// On Request Map Data
        /// </summary>
        protected override void Format05Handler(GameClient client, ClientFormat05 format)
        {
            if (client?.Aisling?.Map == null) return;
            if (client is not { Authenticated: true }) return;
            if (client.MapUpdating && client.Aisling.CurrentMapId != ServerSetup.Config.TransitionZone) return;

            client.MapUpdating = true;
            SendMapData(client);
            client.MapUpdating = false;
        }

        private static void SendMapData(GameClient client)
        {
            var cluster = new List<NetworkFormat>();

            for (var i = 0; i < client.Aisling.Map.Rows; i++)
            {
                var response = new ServerFormat3C
                {
                    Line = (ushort)i,
                    Data = client.Aisling.Map.GetRowData(i)
                };

                cluster.Add(response);
            }

            client.Send(cluster.ToArray());
            client.Aisling.Map.OnLoaded();
        }

        /// <summary>
        /// On Player Movement
        /// </summary>
        protected override void Format06Handler(GameClient client, ClientFormat06 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.Map is not { Ready: true }) return;

            if (!client.Aisling.CanMove)
            {
                client.SendMessage(0x03, "{=bYou cannot feel your legs...");
                client.SendLocation();

                return;
            }

            client.Aisling.CanReact = true;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();

                return;
            }

            if (client.IsRefreshing && ServerSetup.Config.CancelWalkingIfRefreshing) return;

            if (client.Aisling.IsCastingSpell && ServerSetup.Config.CancelCastingWhenWalking) CancelIfCasting(client);

            client.Aisling.Direction = format.Direction;

            var success = client.Aisling.Walk();

            if (success)
            {
                if (client.Aisling.AreaId == ServerSetup.Config.TransitionZone)
                {
                    var portal = new PortalSession();
                    portal.TransitionToMap(client);
                    return;
                }

                CheckWarpTransitions(client);

                if (client.Aisling.Map == null || !client.Aisling.Map.Scripts.Any()) return;

                foreach (var script in client.Aisling.Map.Scripts.Values)
                {
                    script.OnPlayerWalk(client, client.Aisling.LastPosition, client.Aisling.Position);
                }
            }
            else
            {
                client.ClientRefreshed();
                CheckWarpTransitions(client);
            }

            var time = DateTime.UtcNow;
            client.LastMovement = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
        }

        /// <summary>
        /// On Item Pickup from Map
        /// </summary>
        protected override void Format07Handler(GameClient client, ClientFormat07 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.IsDead()) return;

            var objs = ObjectHandlers.GetObjects(client.Aisling.Map, i => (int)i.Pos.X == format.Position.X
                                                                          && (int)i.Pos.Y == format.Position.Y, ObjectManager.Get.Items | ObjectManager.Get.Money);

            if (objs == null) return;

            foreach (var obj in objs)
            {
                if (obj?.CurrentMapId != client.Aisling.CurrentMapId) continue;

                if (!(client.Aisling.Position.DistanceFrom(obj.Position) <= ServerSetup.Config.ClickLootDistance)) continue;

                if (obj is Item item)
                {
                    if ((item.Template.Flags & ItemFlags.Trap) == ItemFlags.Trap) continue;

                    if (item.Cursed)
                    {
                        Sprite first = null;

                        if (item.AuthenticatedAislings != null)
                        {
                            foreach (var i in item.AuthenticatedAislings)
                            {
                                if (i.Serial != client.Aisling.Serial)
                                    continue;

                                first = i;
                                break;
                            }

                            if (item.AuthenticatedAislings != null && first == null)
                            {
                                client.SendMessage(0x02, $"{ServerSetup.Config.CursedItemMessage}");
                                break;
                            }
                        }

                        if (item.GiveTo(client.Aisling))
                        {
                            item.Remove();
                            break;
                        }

                        item.Pos = client.Aisling.Pos;
                        item.Show(Scope.NearbyAislings, new ServerFormat07(new[] { obj }));
                        break;
                    }

                    if (item.GiveTo(client.Aisling))
                    {
                        item.Remove();

                        if (item.Scripts != null)
                            foreach (var itemScript in item.Scripts?.Values)
                                itemScript?.OnPickedUp(client.Aisling, format.Position, client.Aisling.Map);

                        break;
                    }

                    item.Pos = client.Aisling.Pos;
                    item.Show(Scope.NearbyAislings, new ServerFormat07(new[] { obj }));

                    break;
                }

                if (obj is Money money)
                {
                    money.GiveTo(money.Amount, client.Aisling);
                }
            }
        }

        /// <summary>
        /// On Item dropped on Map
        /// </summary>
        protected override void Format08Handler(GameClient client, ClientFormat08 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.Map is not { Ready: true }) return;
            if (format.ItemAmount is > 1000 or < 0) return;

            Item item = null;
            if (client.Aisling.Inventory.Items.TryGetValue(format.ItemSlot, out var value))
            {
                item = value;
                item.Serial = Generator.GenerateNumber();
            }

            if (item == null) return;

            if (item.Stacks > 1)
            {
                if (format.ItemAmount > item.Stacks)
                {
                    client.SendMessage(0x02, "Wait.. how many did I have again?");
                    return;
                }
            }

            if (!item.Template.Flags.HasFlag(ItemFlags.Dropable))
            {
                client.SendMessage(Scope.Self, 0x02, $"{ServerSetup.Config.CantDropItemMsg}");
                return;
            }

            var itemPosition = new Position(format.X, format.Y);

            if (client.Aisling.Position.DistanceFrom(itemPosition.X, itemPosition.Y) > 4)
            {
                client.SendMessage(Scope.Self, 0x02, "I can not do that. Too far.");
                return;
            }

            if (client.Aisling.Map.IsWall(format.X, format.Y))
                if ((int)client.Aisling.Pos.X != format.X || (int)client.Aisling.Pos.Y != format.Y)
                {
                    client.SendMessage(Scope.Self, 0x02, "Something is in the way.");
                    return;
                }

            if (item.Template.Flags.HasFlag(ItemFlags.Stackable))
            {
                if (format.ItemAmount > item.Stacks)
                {
                    client.SendMessage(0x02, "Wait.. how many did I have again?");
                    return;
                }

                var remaining = item.Stacks - (ushort)format.ItemAmount;
                item.Dropping = format.ItemAmount;

                if (remaining == 0)
                {
                    if (client.Aisling.EquipmentManager.RemoveFromInventory(item, true))
                    {
                        item.Release(client.Aisling, new Position(format.X, format.Y));

                        // Mileth Altar 
                        if (client.Aisling.Map.ID == 500)
                        {
                            if (itemPosition.X == 31 && itemPosition.Y == 52 || itemPosition.X == 31 && itemPosition.Y == 53)
                                item.Remove();
                        }
                    }
                }
                else
                {
                    var temp = new Item
                    {
                        Slot = item.Slot,
                        Image = item.Image,
                        DisplayImage = item.DisplayImage,
                        Durability = item.Durability,
                        ItemVariance = item.ItemVariance,
                        WeapVariance = item.WeapVariance,
                        ItemQuality = item.ItemQuality,
                        OriginalQuality = item.OriginalQuality,
                        InventorySlot = format.ItemSlot,
                        Stacks = (ushort)format.ItemAmount,
                        Template = item.Template
                    };

                    temp.Release(client.Aisling, itemPosition);

                    // Mileth Altar 
                    if (client.Aisling.Map.ID == 500)
                    {
                        if (itemPosition.X == 31 && itemPosition.Y == 52 || itemPosition.X == 31 && itemPosition.Y == 53)
                            temp.Remove();
                    }

                    item.Stacks = (ushort)remaining;
                    client.Aisling.Inventory.Set(item);
                    client.Send(new ServerFormat10(item.InventorySlot));
                    Inventory.UpdateSlot(client, item);
                }
            }
            else
            {
                if (client.Aisling.EquipmentManager.RemoveFromInventory(item, true))
                {
                    item.Release(client.Aisling, new Position(format.X, format.Y));

                    // Mileth Altar 
                    if (client.Aisling.Map.ID == 500)
                    {
                        if (itemPosition.X == 31 && itemPosition.Y == 52 || itemPosition.X == 31 && itemPosition.Y == 53)
                            item.Remove();
                    }
                }
            }

            client.SendStats(StatusFlags.StructA);

            if (client.Aisling.Map != null && client.Aisling.Map.Scripts.Any())
            {
                foreach (var script in client.Aisling.Map.Scripts.Values)
                {
                    script.OnItemDropped(client, item, itemPosition);
                }
            }

            // ToDo: No items currently use OnDropped, can refactor this out if there is no future usage
            if (item.Scripts == null) return;
            foreach (var itemScript in (item.Scripts.Values))
            {
                itemScript?.OnDropped(client.Aisling, new Position(format.X, format.Y), client.Aisling.Map);
            }
        }

        /// <summary>
        /// On Client End-Game Request
        /// </summary>
        protected override void Format0BHandler(GameClient client, ClientFormat0B format)
        {
            if (client is not { Authenticated: true }) return;
            LeaveGame(client, format);
        }

        /// <summary>
        /// Display Object Request
        /// </summary>
        protected override void Format0CHandler(GameClient client, ClientFormat0C format) { }

        /// <summary>
        /// On Client End-Game Request Continued...
        /// </summary>
        private void LeaveGame(GameClient client, ClientFormat0B format)
        {
            if (client?.Aisling == null) return;

            Party.RemovePartyMember(client.Aisling);
            RemoveFromServer(client, format.Type);

            switch (format.Type)
            {
                case 1:
                    client.Send(new ServerFormat4C());
                    break;
                case 3:
                    {
                        var time = DateTime.UtcNow;
                        client.LastSave = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                        client.Aisling.Remove(true);
                        break;
                    }
            }
        }

        /// <summary>
        /// On Ignore Player - F9 Button
        /// </summary>
        protected override void Format0DHandler(GameClient client, ClientFormat0D format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;

            switch (format.IgnoreType)
            {
                case 1:
                    var ignored = string.Join(", ", client.Aisling.IgnoredList);
                    client.SendMessage(0x09, ignored);
                    break;
                case 2:
                    if (format.Target == null) break;
                    if (format.Target.EqualsIgnoreCase("Death")) break;
                    if (client.Aisling.IgnoredList.ListContains(format.Target)) break;
                    client.AddToIgnoreListDb(format.Target);
                    break;
                case 3:
                    if (format.Target == null) break;
                    if (format.Target.EqualsIgnoreCase("Death")) break;
                    if (!client.Aisling.IgnoredList.ListContains(format.Target)) break;
                    client.RemoveFromIgnoreListDb(format.Target);
                    break;
            }
        }

        /// <summary>
        /// On Public Message
        /// </summary>
        protected override void Format0EHandler(GameClient client, ClientFormat0E format)
        {
            bool ParseCommand()
            {
                if (!client.Aisling.GameMaster) return false;
                if (!format.Text.StartsWith("/")) return false;
                Commander.ParseChatMessage(client, format.Text);
                return true;
            }

            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;

            var response = new ServerFormat0D
            {
                Serial = client.Aisling.Serial,
                Type = format.Type,
                Text = string.Empty
            };

            IEnumerable<Aisling> audience;

            if (ParseCommand()) return;

            switch (format.Type)
            {
                case 0x00:
                    response.Text = $"{client.Aisling.Username}: {format.Text}";
                    audience = ObjectHandlers.GetObjects<Aisling>(client.Aisling.Map, n => client.Aisling.WithinRangeOf(n));
                    break;
                case 0x01:
                    response.Text = $"{client.Aisling.Username}! {format.Text}";
                    audience = ObjectHandlers.GetObjects<Aisling>(client.Aisling.Map, n => client.Aisling.CurrentMapId == n.CurrentMapId);
                    break;
                case 0x02:
                    response.Text = format.Text;
                    audience = ObjectHandlers.GetObjects<Aisling>(client.Aisling.Map, n => client.Aisling.WithinRangeOf(n, false));
                    break;
                default:
                    ClientDisconnected(client);
                    RemoveClient(client);
                    return;
            }

            var nearbyMundanes = client.Aisling.MundanesNearby();
            var playerMap = client.Aisling.Map;

            foreach (var npc in nearbyMundanes)
            {
                if (npc?.Scripts is null) continue;

                foreach (var script in npc.Scripts.Values)
                    script?.OnGossip(this, client, format.Text);
            }

            foreach (var action in playerMap.Scripts.Values)
            {
                action?.OnGossip(client, format.Text);
            }

            var playersToShowList = audience.Where(player => !player.IgnoredList.ListContains(client.Aisling.Username));
            client.Aisling.Show(Scope.DefinedAislings, response, playersToShowList);
        }

        /// <summary>
        /// Spell Use
        /// </summary>
        protected override void Format0FHandler(GameClient client, ClientFormat0F format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.IsDead()) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            var spellReq = client.Aisling.SpellBook.GetSpells(i => i != null && i.Slot == format.Index).FirstOrDefault();

            if (spellReq == null) return;

            //ToDo: Abort cast if spell is not "Ao Suain" or "Ao Pramh"
            if (!client.Aisling.CanCast)
                if (spellReq.Template.Name != "Ao Suain" && spellReq.Template.Name != "Ao Pramh")
                {
                    CancelIfCasting(client);
                    return;
                }

            var info = new CastInfo
            {
                Slot = format.Index,
                Target = format.Serial,
                Position = format.Point,
                Data = format.Data,
            };

            info.Position ??= new Position(client.Aisling.X, client.Aisling.Y);

            lock (client.CastStack)
            {
                client.CastStack?.Push(info);
            }
        }

        /// <summary>
        /// Client Redirect to GameServer from LoginServer
        /// </summary>
        protected override void Format10Handler(GameClient client, ClientFormat10 format)
        {
            if (client == null) return;
            if (format.Name.IsNullOrEmpty()) return;

            EnterGame(client, format);
            AuthenticateClient(client);

            if (client.Server.Clients.Values.Count <= 2000) return;
            client.SendMessage(0x08, $"Server Capacity Reached: {client.Server.Clients.Values.Count} \n Thank you, please come back later.");
            ClientDisconnected(client);
            RemoveClient(client);
        }

        /// <summary>
        /// Client Redirect to GameServer from LoginServer Continued...
        /// </summary>
        private void EnterGame(GameClient client, ClientFormat10 format)
        {
            var timeUtc = DateTime.UtcNow;
            var time = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(timeUtc, "Eastern Standard Time");
            client.Encryption.Parameters = format.Parameters;
            client.Server = this;

            var player = LoadPlayer(client, format.Name);

            if (player == null)
            {
                ClientDisconnected(client);
                RemoveClient(client);
                return;
            }

            player.GameMaster = ServerSetup.Config.GameMasters?.Any(n => string.Equals(n.ToLower(), player.Username.ToLower(), StringComparison.CurrentCulture)) ?? false;

            ServerSetup.Logger($"{player.Username} logged in at: {time}");
            client.LastPing = time;
        }

        /// <summary>
        /// Client Authentication
        /// </summary>
        private void AuthenticateClient(GameClient client)
        {
            if (ServerSetup.Redirects.ContainsKey(client.Aisling.Serial))
            {
                client.Aisling.Client.Authenticated = true;
                return;
            }

            ClientDisconnected(client);
            RemoveClient(client);
        }

        /// <summary>
        /// On Player Change Direction
        /// </summary>
        protected override void Format11Handler(GameClient client, ClientFormat11 format)
        {
            if (client.Aisling == null) return;
            if (client is not { Authenticated: true }) return;

            client.Aisling.Direction = format.Direction;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            client.Aisling.Show(Scope.NearbyAislings, new ServerFormat11
            {
                Direction = client.Aisling.Direction,
                Serial = client.Aisling.Serial
            });
        }

        /// <summary>
        /// Auto-Attack - Spacebar Button
        /// </summary>
        protected override void Format13Handler(GameClient client, ClientFormat13 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.IsDead()) return;
            if (ServerSetup.Config.AssailsCancelSpells) CancelIfCasting(client);

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (!client.Aisling.CanAttack)
            {
                client.Interrupt();
                return;
            }

            Assail(client);
        }

        private static void Assail(IGameClient lpClient)
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            var lastTemplate = string.Empty;

            foreach (var skill in lpClient.Aisling.GetAssails())
            {
                if (skill?.Template == null) continue;
                if (lastTemplate == skill.Template.Name) continue;
                if (skill.Scripts == null) continue;
                if (skill.Ready && !skill.InUse)
                    ExecuteAbility(lpClient, skill);
                skill.CurrentCooldown = skill.Template.Cooldown;
                lastTemplate = skill.Template.Name;
                lpClient.LastAssail = readyTime;
            }
        }

        /// <summary>
        /// On World list Request - (Who is Online)
        /// </summary>
        protected override void Format18Handler(GameClient client, ClientFormat18 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.IsRefreshing) return;

            client.Aisling.Show(Scope.Self, new ServerFormat36(client));
        }

        /// <summary>
        /// On Private Message (Guild, Group, Direct, World)
        /// </summary>
        protected override void Format19Handler(GameClient client, ClientFormat19 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (format == null) return;

            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            if (readyTime.Subtract(client.LastWhisperMessageSent).TotalSeconds < 0.30) return;
            if (format.Name.Length > 24) return;

            client.LastWhisperMessageSent = readyTime;

            switch (format.Name)
            {
                case "#" when client.Aisling.GameMaster:
                    foreach (var client2 in from client2 in ServerSetup.Game.Clients.Values where client2?.Aisling != null select client2)
                    {
                        if (client2 is null) return;
                        client2.SendMessage(0x0B, $"{{=c{client.Aisling.Username}: {format.Message}");
                    }
                    break;
                case "#" when client.Aisling.GameMaster != true:
                    client.SystemMessage("You cannot broadcast in this way.");
                    break;
                case "!" when !string.IsNullOrEmpty(client.Aisling.Clan):
                    foreach (var client2 in from client2 in ServerSetup.Game.Clients.Values
                                            where client2?.Aisling != null
                                            select client2)
                    {
                        if (client2 is null) return;
                        if (client2.Aisling.Clan == client.Aisling.Clan)
                        {
                            client2.SendMessage(0x0B, $"<!{client.Aisling.Username}> {format.Message}");
                        }
                    }
                    break;
                case "!" when string.IsNullOrEmpty(client.Aisling.Clan):
                    client.SystemMessage("{=eYou're not in a guild.");
                    return;
                case "!!" when client.Aisling.PartyMembers != null:
                    foreach (var client2 in from client2 in ServerSetup.Game.Clients.Values
                                            where client2?.Aisling != null
                                            select client2)
                    {
                        if (client2 is null) return;
                        if (client2.Aisling.GroupParty == client.Aisling.GroupParty)
                        {
                            client2.SendMessage(0x0C, $"[!{client.Aisling.Username}] {format.Message}");
                        }
                    }
                    break;
                case "!!" when client.Aisling.PartyMembers == null:
                    client.SystemMessage("{=eYou're not in a group or party.");
                    return;
            }

            var user = Clients.Values.FirstOrDefault(i => i?.Aisling != null && i.Aisling.LoggedIn && i.Aisling.Username.ToLower() ==
                                                   format.Name.ToLower(CultureInfo.CurrentCulture));

            if (format.Name != "!" && format.Name != "!!" && format.Name != "#" && user == null)
                client.SendMessage(0x02, string.Format(CultureInfo.CurrentCulture, "{0} is nowhere to be found.", format.Name));

            if (user == null) return;

            if (user.Aisling.IgnoredList.ListContains(client.Aisling.Username))
            {
                user.SendMessage(0x00, $"{client.Aisling.Username} tried to message you but it was intercepted.");
                client.SendMessage(0x00, "You cannot bother people this way.");
                return;
            }

            user.SendMessage(0x00, string.Format(CultureInfo.CurrentCulture, "{0}: {1}", client.Aisling.Username, format.Message));
            client.SendMessage(0x00, string.Format(CultureInfo.CurrentCulture, "{0}> {1}", user.Aisling.Username, format.Message));
        }

        /// <summary>
        /// On Client -Options Panel- Change - F4 Button
        /// </summary>
        protected override void Format1BHandler(GameClient client, ClientFormat1B format)
        {
            if (client is not { Authenticated: true }) return;
            if (client.Aisling.GameSettings == null) return;

            var settingKeys = client.Aisling.GameSettings.ToArray();

            if (settingKeys.Length == 0) return;

            var settingIdx = format.Index;

            if (settingIdx > 0)
            {
                settingIdx--;

                var setting = settingKeys[settingIdx];
                setting.Toggle();

                UpdateSettings(client);
            }
            else
            {
                UpdateSettings(client);
            }
        }

        private static void UpdateSettings(IGameClient client)
        {
            var msg = "\t";

            foreach (var setting in client.Aisling.GameSettings.Where(setting => setting != null))
            {
                msg += setting.Enabled ? setting.EnabledSettingStr : setting.DisabledSettingStr;
                msg += "\t";
            }

            client.SendMessage(0x07, msg);
        }

        /// <summary>
        /// On Item Usage
        /// </summary>
        protected override void Format1CHandler(GameClient client, ClientFormat1C format)
        {
            if (client?.Aisling?.Map == null || !client.Aisling.Map.Ready) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.Dead) return;
            if (!client.IsEquipping) return;

            var time = DateTime.UtcNow;
            client.LastEquip = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            var slot = format.Index;
            var item = client.Aisling.Inventory.Get(i => i != null && i.InventorySlot == slot).FirstOrDefault();

            if (item?.Template == null) return;
            var activated = false;

            // Run Scripts on item on use
            if (!string.IsNullOrEmpty(item.Template.ScriptName)) item.Scripts ??= ScriptManager.Load<ItemScript>(item.Template.ScriptName, item);
            if (!string.IsNullOrEmpty(item.Template.WeaponScript)) item.WeaponScripts ??= ScriptManager.Load<WeaponScript>(item.Template.WeaponScript, item);

            if (item.Scripts == null)
            {
                client.SendMessage(0x02, $"{ServerSetup.Config.CantUseThat}");
            }
            else
            {
                foreach (var script in item.Scripts.Values) script?.OnUse(client.Aisling, slot);
                activated = true;
            }

            if (!activated) return;
            if (!item.Template.Flags.HasFlag(ItemFlags.Stackable)) return;
            if (!item.Template.Flags.HasFlag(ItemFlags.Consumable)) return;

            var stack = item.Stacks - 1;

            if (stack > 0)
            {
                item.Stacks -= 1;
                client.Aisling.Inventory.Set(item);

                client.Send(new ServerFormat10(item.InventorySlot));
                Inventory.UpdateSlot(client, item);
            }
            else
            {
                client.Aisling.EquipmentManager.RemoveFromInventory(item, true);
            }
        }

        /// <summary>
        /// On Client using Emote
        /// </summary>
        protected override void Format1DHandler(GameClient client, ClientFormat1D format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.IsRefreshing) return;
            if (client.Aisling.IsDead()) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            var id = format.Number;

            if (id <= 35)
                client.Aisling.Show(Scope.NearbyAislings, new ServerFormat1A(client.Aisling.Serial, (byte)(id + 9), 120));
        }

        /// <summary>
        /// On Client Dropping Gold on Map
        /// </summary>
        protected override void Format24Handler(GameClient client, ClientFormat24 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.CanAttack) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (client.Aisling.GoldPoints >= format.GoldAmount)
            {
                client.Aisling.GoldPoints -= format.GoldAmount;
                if (client.Aisling.GoldPoints <= 0)
                    client.Aisling.GoldPoints = 0;

                client.SendMessage(Scope.Self, 0x02, $"{ServerSetup.Config.YouDroppedGoldMsg}");
                client.SendMessage(Scope.NearbyAislingsExludingSelf, 0x02, $"{ServerSetup.Config.UserDroppedGoldMsg.Replace("noname", client.Aisling.Username)}");

                Money.Create(client.Aisling, format.GoldAmount, new Position(format.X, format.Y));
                client.SendStats(StatusFlags.StructC);
            }
            else
            {
                client.SendMessage(0x02, $"{ServerSetup.Config.NotEnoughGoldToDropMsg}");
            }
        }

        /// <summary>
        /// On Item Dropped on Sprite
        /// </summary>
        protected override void Format29Handler(GameClient client, ClientFormat29 format)
        {
            if (client is not { Authenticated: true }) return;

            client.Send(new ServerFormat4B(format.ID, 0));
            client.Send(new ServerFormat4B(format.ID, 1, format.ItemSlot));
            var result = new List<Sprite>();
            var listA = client.Aisling.GetObjects<Monster>(client.Aisling.Map, i => i != null && i.WithinRangeOf(client.Aisling, ServerSetup.Config.WithinRangeProximity));
            var listB = client.Aisling.GetObjects<Mundane>(client.Aisling.Map, i => i != null && i.WithinRangeOf(client.Aisling, ServerSetup.Config.WithinRangeProximity));
            var listC = client.Aisling.GetObjects<Aisling>(client.Aisling.Map, i => i != null && i.WithinRangeOf(client.Aisling, ServerSetup.Config.WithinRangeProximity));
            result.AddRange(listA);
            result.AddRange(listB);
            result.AddRange(listC);

            foreach (var sprite in result.Where(sprite => sprite.Serial == format.ID))
            {
                switch (sprite)
                {
                    case Monster monster:
                        {
                            foreach (var (_, script) in monster.Scripts.Select(x => (x.Key, x.Value)))
                            {
                                var item = client.Aisling.Inventory.Items[format.ItemSlot];
                                client.Aisling.EquipmentManager.RemoveFromInventory(item, true);
                                script.OnItemDropped(client, item);
                            }

                            break;
                        }
                    case Mundane mundane:
                        {
                            foreach (var (_, script) in mundane.Scripts.Select(x => (x.Key, x.Value)))
                            {
                                var item = client.Aisling.Inventory.Items[format.ItemSlot];
                                script.OnItemDropped(client, item);
                            }

                            break;
                        }
                    case Aisling aisling:
                        {
                            if (format.ItemSlot == 0) return;
                            var item = client.Aisling.Inventory.Items[format.ItemSlot];

                            if (item.DisplayName.StringContains("deum"))
                            {
                                foreach (var (_, script) in item.Scripts.Select(x => (x.Key, x.Value)))
                                {
                                    Inventory.RemoveRange(client, item, 1);
                                    client.Aisling.ThrewHealingPot = true;

                                    var action = new ServerFormat1A
                                    {
                                        Serial = aisling.Serial,
                                        Number = 0x06,
                                        Speed = 50
                                    };

                                    script.OnUse(aisling, format.ItemSlot);
                                    client.Aisling.Show(Scope.NearbyAislings, action);
                                }
                            }

                            break;
                        }
                }
            }
        }

        /// <summary>
        /// On Gold Dropped on Sprite
        /// </summary>
        protected override void Format2AHandler(GameClient client, ClientFormat2A format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            var result = new List<Sprite>();
            var listA = client.Aisling.GetObjects<Monster>(client.Aisling.Map, i => i != null && i.WithinRangeOf(client.Aisling, ServerSetup.Config.WithinRangeProximity));
            var listB = client.Aisling.GetObjects<Mundane>(client.Aisling.Map, i => i != null && i.WithinRangeOf(client.Aisling, ServerSetup.Config.WithinRangeProximity));
            var listC = client.Aisling.GetObjects<Aisling>(client.Aisling.Map, i => i != null && i.WithinRangeOf(client.Aisling, ServerSetup.Config.WithinRangeProximity));

            result.AddRange(listA);
            result.AddRange(listB);
            result.AddRange(listC);

            foreach (var sprite in result.Where(sprite => sprite.Serial == format.ID))
            {
                switch (sprite)
                {
                    case Monster monster:
                        {
                            foreach (var (_, script) in monster.Scripts.Select(x => (x.Key, x.Value)))
                            {
                                if (client.Aisling.GoldPoints >= format.Gold)
                                {
                                    client.Aisling.GoldPoints -= format.Gold;
                                    client.SendStats(StatusFlags.WeightMoney);
                                }
                                else
                                    break;

                                script.OnGoldDropped(client, format.Gold);
                            }

                            break;
                        }
                    case Mundane mundane:
                        {
                            foreach (var (_, script) in mundane.Scripts.Select(x => (x.Key, x.Value)))
                            {
                                if (client.Aisling.GoldPoints >= format.Gold)
                                {
                                    client.Aisling.GoldPoints -= format.Gold;
                                    client.SendStats(StatusFlags.WeightMoney);
                                }
                                else
                                    break;

                                script.OnGoldDropped(client, format.Gold);
                            }

                            break;
                        }
                    case Aisling aisling:
                        {
                            var format4AReceiver = new ClientFormat4A
                            {
                                Id = (uint)client.Aisling.Serial,
                                Type = byte.MinValue,
                                Command = 74
                            };

                            var format4ASender = new ClientFormat4A
                            {
                                Gold = format.Gold,
                                Id = format.ID,
                                Type = 0x03,
                                Command = 74
                            };

                            Format4AHandler(aisling.Client, format4AReceiver);
                            Format4AHandler(client, format4ASender);
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// On Player Request Profile - Load Profile
        /// </summary>
        protected override void Format2DHandler(GameClient client, ClientFormat2D format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            client.Send(new ServerFormat39(client.Aisling));
        }

        /// <summary>
        /// On Party Join Request
        /// </summary>
        protected override void Format2EHandler(GameClient client, ClientFormat2E format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.IsRefreshing) return;
            if (client.Aisling.IsDead()) return;

            if (format.Type != 0x02) return;

            var player = ObjectHandlers.GetObject<Aisling>(client.Aisling.Map, i => string.Equals(i.Username, format.Name, StringComparison.CurrentCultureIgnoreCase)
                && i.WithinRangeOf(client.Aisling));

            if (player == null)
            {
                client.SendMessage(0x02, $"{ServerSetup.Config.BadRequestMessage}");
                return;
            }

            if (player.PartyStatus != GroupStatus.AcceptingRequests)
            {
                client.SendMessage(0x02, $"{ServerSetup.Config.GroupRequestDeclinedMsg.Replace("noname", player.Username)}");
                return;
            }

            if (Party.AddPartyMember(client.Aisling, player))
            {
                client.Aisling.PartyStatus = GroupStatus.AcceptingRequests;
                if (client.Aisling.GroupParty.PartyMembers.Any(other => other.Invisible))
                    client.UpdateDisplay();
                return;
            }

            Party.RemovePartyMember(player);
        }

        /// <summary>
        /// On Toggle Group Button
        /// </summary>
        protected override void Format2FHandler(GameClient client, ClientFormat2F format)
        {
            if (client is not { Authenticated: true }) return;

            var mode = client.Aisling.PartyStatus;

            mode = mode switch
            {
                GroupStatus.AcceptingRequests => GroupStatus.NotAcceptingRequests,
                GroupStatus.NotAcceptingRequests => GroupStatus.AcceptingRequests,
                _ => mode
            };

            client.Aisling.PartyStatus = mode;

            if (client.Aisling.PartyStatus == GroupStatus.NotAcceptingRequests)
            {
                if (client.Aisling.LeaderPrivileges)
                {
                    if (!ServerSetup.GlobalGroupCache.TryGetValue(client.Aisling.GroupId, out var group)) return;

                    if (group != null)
                        Party.DisbandParty(group);
                }

                Party.RemovePartyMember(client.Aisling);
                client.ClientRefreshed();
            }
        }

        /// <summary>
        /// Swapping Sprites within UI (Skills, Spells, Items)
        /// </summary>
        protected override void Format30Handler(GameClient client, ClientFormat30 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.IsRefreshing) return;
            CancelIfCasting(client);
            if (client.Aisling.IsDead()) return;

            if (!client.Aisling.CanMove || !client.Aisling.CanAttack || !client.Aisling.CanCast || client.Aisling.IsCastingSpell)
            {
                client.Interrupt();
                return;
            }

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            // ToDo: Moving UI items, skills, spells around
            switch (format.PaneType)
            {
                case Pane.Inventory:
                    {
                        if (format.MovingTo - 1 > client.Aisling.Inventory.Length) return;
                        if (format.MovingFrom - 1 > client.Aisling.Inventory.Length) return;
                        if (format.MovingTo - 1 < 0) return;
                        if (format.MovingFrom - 1 < 0) return;

                        client.Send(new ServerFormat10(format.MovingFrom));
                        client.Send(new ServerFormat10(format.MovingTo));

                        var a = client.Aisling.Inventory.Remove(format.MovingFrom);
                        var b = client.Aisling.Inventory.Remove(format.MovingTo);

                        if (a != null)
                        {
                            a.InventorySlot = format.MovingTo;
                            client.Aisling.Inventory.Set(a);
                            Inventory.UpdateSlot(client, a);
                        }

                        if (b != null)
                        {
                            b.InventorySlot = format.MovingFrom;
                            client.Aisling.Inventory.Set(b);
                            Inventory.UpdateSlot(client, b);
                        }
                    }
                    break;
                case Pane.Skills:
                    {
                        if (format.MovingTo - 1 > client.Aisling.SkillBook.Length) return;
                        if (format.MovingFrom - 1 > client.Aisling.SkillBook.Length) return;
                        if (format.MovingTo - 1 < 0) return;
                        if (format.MovingFrom - 1 < 0) return;

                        client.Send(new ServerFormat2D(format.MovingFrom));
                        client.Send(new ServerFormat2D(format.MovingTo));

                        var a = client.Aisling.SkillBook.Remove(format.MovingFrom);
                        var b = client.Aisling.SkillBook.Remove(format.MovingTo);

                        if (a != null)
                        {
                            a.Slot = format.MovingTo;
                            client.Send(new ServerFormat2C(a.Slot, a.Icon, a.Name));
                            client.Aisling.SkillBook.Set(a);
                        }

                        if (b != null)
                        {
                            b.Slot = format.MovingFrom;
                            client.Send(new ServerFormat2C(b.Slot, b.Icon, b.Name));
                            client.Aisling.SkillBook.Set(b);
                        }
                    }
                    break;
                case Pane.Spells:
                    {
                        if (format.MovingTo - 1 > client.Aisling.SpellBook.Length) return;
                        if (format.MovingFrom - 1 > client.Aisling.SpellBook.Length) return;
                        if (format.MovingTo - 1 < 0) return;
                        if (format.MovingFrom - 1 < 0) return;

                        client.Send(new ServerFormat18(format.MovingFrom));
                        client.Send(new ServerFormat18(format.MovingTo));

                        var a = client.Aisling.SpellBook.Remove(format.MovingFrom);
                        var b = client.Aisling.SpellBook.Remove(format.MovingTo);

                        if (a != null)
                        {
                            a.Slot = format.MovingTo;
                            client.Send(new ServerFormat17(a));
                            client.Aisling.SpellBook.Set(a, false);
                        }

                        if (b != null)
                        {
                            b.Slot = format.MovingFrom;
                            client.Send(new ServerFormat17(b));
                            client.Aisling.SpellBook.Set(b, false);
                        }
                    }
                    break;
                case Pane.Tools:
                    {
                        if (format.MovingTo - 1 > client.Aisling.SpellBook.Length) return;
                        if (format.MovingFrom - 1 > client.Aisling.SpellBook.Length) return;
                        if (format.MovingTo - 1 < 0) return;
                        if (format.MovingFrom - 1 < 0) return;

                        client.Send(new ServerFormat18(format.MovingFrom));
                        client.Send(new ServerFormat18(format.MovingTo));

                        var a = client.Aisling.SpellBook.Remove(format.MovingFrom);
                        var b = client.Aisling.SpellBook.Remove(format.MovingTo);

                        if (a != null)
                        {
                            a.Slot = format.MovingTo;
                            client.Send(new ServerFormat17(a));
                            client.Aisling.SpellBook.Set(a, false);
                        }

                        if (b != null)
                        {
                            b.Slot = format.MovingFrom;
                            client.Send(new ServerFormat17(b));
                            client.Aisling.SpellBook.Set(b, false);
                        }
                    }
                    break;
            }
        }

        protected override void Format32Handler(GameClient client, ClientFormat32 format)
        {
            Console.Write($"Format32HandlerDiscovery: {format.UnknownA}\n{format.UnknownB}\n{format.UnknownC}\n{format.UnknownD}\n");
        }

        /// <summary>
        /// On Client Refresh - F5 Button
        /// </summary>
        protected override void Format38Handler(GameClient client, ClientFormat38 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.IsRefreshing) return;

            client.ClientRefreshed();
        }

        /// <summary>
        /// Request Pursuit
        /// </summary>
        protected override void Format39Handler(GameClient client, ClientFormat39 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;

            CancelIfCasting(client);

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (!client.Aisling.CanCast || !client.Aisling.CanAttack)
            {
                client.Interrupt();
                return;
            }

            var npcs = ServerSetup.GlobalMundaneCache.Where(i => i.Key == format.Serial);

            foreach (var npc in npcs)
            {
                if (npc.Value?.Template?.ScriptKey == null) continue;

                var scriptObj = ServerSetup.GlobalMundaneScriptCache.FirstOrDefault(i => i.Key == npc.Value.Template.Name);
                scriptObj.Value?.OnResponse(this, client, format.Step, format.Args);
            }
        }

        /// <summary>
        /// NPC Input Response -- Story Building, Send 3A after OnResponse
        /// </summary>
        protected override void Format3AHandler(GameClient client, ClientFormat3A format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;

            CancelIfCasting(client);

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (!client.Aisling.CanCast || !client.Aisling.CanAttack)
            {
                client.Interrupt();
                return;
            }

            if (format.Step == 0 && format.ScriptId == ushort.MaxValue)
            {
                client.CloseDialog();
                return;
            }

            var objId = format.Serial;

            if (objId is > 0 and < int.MaxValue)
            {
                var npcs = ServerSetup.GlobalMundaneCache.Where(i => i.Key == format.Serial);

                foreach (var npc in npcs)
                {
                    if (npc.Value?.Template?.ScriptKey == null) continue;

                    var scriptObj = ServerSetup.GlobalMundaneScriptCache.FirstOrDefault(i => i.Key == npc.Value.Template.Name);
                    scriptObj.Value?.OnResponse(this, client, format.Step, format.Input);
                    return;
                }
            }

            if (format.ScriptId == ushort.MaxValue)
            {
                if (client.Aisling.ActiveReactor?.Decorators == null)
                    return;

                switch (format.Step)
                {
                    case 0:
                        foreach (var script in client.Aisling.ActiveReactor.Decorators.Values)
                            script.OnClose(client.Aisling);
                        break;

                    case 255:
                        foreach (var script in client.Aisling.ActiveReactor.Decorators.Values)
                            script.OnBack(client.Aisling);
                        break;

                    case 0xFFFF:
                        foreach (var script in client.Aisling.ActiveReactor.Decorators.Values)
                            script.OnBack(client.Aisling);
                        break;

                    case 2:
                        foreach (var script in client.Aisling.ActiveReactor.Decorators.Values)
                            script.OnClose(client.Aisling);
                        break;

                    case 1:
                        foreach (var script in client.Aisling.ActiveReactor.Decorators.Values)
                            script.OnNext(client.Aisling);
                        break;
                }
            }
            else
            {
                client.DlgSession?.Callback?.Invoke(this, client, format.Step, format.Input ?? string.Empty);
            }
        }

        /// <summary>
        /// Request Bulletin Board
        /// </summary>
        protected override void Format3BHandler(GameClient client, ClientFormat3B format)
        {
            if (client is not { Authenticated: true }) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (format.Type == 0x01)
            {
                client.Send(new BoardList(ServerSetup.PersonalBoards));
                return;
            }

            if (format.Type == 0x02)
            {
                if (format.BoardIndex == 0)
                {
                    var clone = ObjectHandlers.PersonalMailJsonConvert<Board>(ServerSetup.PersonalBoards[format.BoardIndex]);
                    {
                        clone.Client = client;
                        client.Send(clone);
                    }
                    return;
                }

                var boards = ServerSetup.GlobalBoardCache.Select(i => i.Value)
                    .SelectMany(i => i.Where(n => n.Index == format.BoardIndex))
                    .FirstOrDefault();

                if (boards != null)
                    client.Send(boards);

                return;
            }

            if (format.Type == 0x03)
            {
                var index = format.TopicIndex - 1;

                var boards = ServerSetup.GlobalBoardCache.Select(i => i.Value)
                    .SelectMany(i => i.Where(n => n.Index == format.BoardIndex))
                    .FirstOrDefault();

                if (boards != null &&
                    boards.Posts.Count > index)
                {
                    var post = boards.Posts[index];
                    if (!post.Read)
                    {
                        post.Read = true;
                    }

                    client.Send(post);
                    return;
                }

                client.Send(new ForumCallback("Unable to retrieve more.", 0x06, true));
                return;
            }

            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            if (format.Type == 0x06)
            {
                var boards = ServerSetup.GlobalBoardCache.Select(i => i.Value)
                    .SelectMany(i => i.Where(n => n.Index == format.BoardIndex))
                    .FirstOrDefault();

                if (boards == null) return;
                var np = new PostFormat(format.BoardIndex, format.TopicIndex)
                {
                    DatePosted = readyTime,
                    Message = format.Message,
                    Subject = format.Title,
                    Read = false,
                    Sender = client.Aisling.Username,
                    Recipient = format.To,
                    PostId = (ushort)(boards.Posts.Count + 1)
                };

                np.Associate(client.Aisling.Username);
                boards.Posts.Add(np);
                ServerSetup.SaveCommunityAssets();
                client.Send(new ForumCallback("Message Delivered.", 0x06, true));
                var recipient = ObjectHandlers.GetAislingForMailDeliveryMessage(Convert.ToString(format.To));

                if (recipient == null) return;
                recipient.Client.SendStats(StatusFlags.UnreadMail);
                recipient.Client.SendMessage(0x03, "{=cYou have new mail.");
                return;
            }

            if (format.Type == 0x04)
            {
                var boards = ServerSetup.GlobalBoardCache.Select(i => i.Value)
                    .SelectMany(i => i.Where(n => n.Index == format.BoardIndex))
                    .FirstOrDefault();

                if (boards == null) return;
                var np = new PostFormat(format.BoardIndex, format.TopicIndex)
                {
                    DatePosted = readyTime,
                    Message = format.Message,
                    Subject = format.Title,
                    Read = false,
                    Sender = client.Aisling.Username,
                    PostId = (ushort)(boards.Posts.Count + 1)
                };

                np.Associate(client.Aisling.Username);

                boards.Posts.Add(np);
                ServerSetup.SaveCommunityAssets();
                client.Send(new ForumCallback("Post Added.", 0x06, true));

                return;
            }

            if (format.Type == 0x05)
            {
                var community = ServerSetup.GlobalBoardCache.Select(i => i.Value)
                    .SelectMany(i => i.Where(n => n.Index == format.BoardIndex))
                    .FirstOrDefault();

                if (community == null || community.Posts.Count <= 0) return;
                try
                {
                    if ((format.BoardIndex == 0
                            ? community.Posts[format.TopicIndex - 1].Recipient
                            : community.Posts[format.TopicIndex - 1].Sender
                        ).Equals(client.Aisling.Username, StringComparison.OrdinalIgnoreCase) || client.Aisling.GameMaster)
                    {
                        client.Send(new ForumCallback("", 0x07, true));
                        client.Send(new BoardList(ServerSetup.PersonalBoards));
                        client.Send(new ForumCallback("Post Deleted.", 0x07, true));

                        community.Posts.RemoveAt(format.TopicIndex - 1);
                        ServerSetup.SaveCommunityAssets();

                        client.Send(new ForumCallback("Post Deleted.", 0x07, true));
                    }
                    else
                    {
                        client.Send(new ForumCallback(ServerSetup.Config.CantDoThat, 0x07, true));
                    }
                }
                catch (Exception ex)
                {
                    ServerSetup.Logger(ex.Message, LogLevel.Error);
                    ServerSetup.Logger(ex.StackTrace, LogLevel.Error);
                    Crashes.TrackError(ex);
                    client.Send(new ForumCallback(ServerSetup.Config.CantDoThat, 0x07, true));
                }
            }

            if (format.Type != 0x07) return;
            {
                client.Send(client.Aisling.GameMaster == false
                    ? new ForumCallback("You cannot perform this action.", 0x07, true)
                    : new ForumCallback("Action completed.", 0x07, true));

                if (format.BoardIndex == 0)
                {
                    var clone = ObjectHandlers.PersonalMailJsonConvert<Board>(ServerSetup.PersonalBoards[format.BoardIndex]);
                    {
                        clone.Client = client;
                        client.Send(clone);
                    }
                    return;
                }

                var boards = ServerSetup.GlobalBoardCache.Select(i => i.Value)
                    .SelectMany(i => i.Where(n => n.Index == format.BoardIndex))
                    .FirstOrDefault();

                if (!client.Aisling.GameMaster) return;
                if (boards == null) return;

                foreach (var ind in boards.Posts.Where(ind => ind.PostId == format.TopicIndex))
                {
                    if (ind.HighLighted)
                    {
                        ind.HighLighted = false;
                        client.SendMessage(0x08, $"Removed Highlight: {ind.Subject}");
                    }
                    else
                    {
                        ind.HighLighted = true;
                        client.SendMessage(0x08, $"Highlighted: {ind.Subject}");
                    }
                }

                client.Send(boards);
            }
        }

        /// <summary>
        /// Skill Use
        /// </summary>
        protected override void Format3EHandler(GameClient client, ClientFormat3E format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (client.Aisling.IsDead()) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (!client.Aisling.CanAttack)
            {
                client.Interrupt();
                return;
            }

            var skill = client.Aisling.SkillBook.GetSkills(i => i.Slot == format.Index).FirstOrDefault();
            if (skill?.Template == null || skill.Scripts == null) return;

            if (!skill.CanUse()) return;

            skill.InUse = true;

            foreach (var script in skill.Scripts.Values)
                script.OnUse(client.Aisling);

            skill.InUse = false;
        }

        /// <summary>
        /// World Map Click
        /// </summary>
        protected override void Format3FHandler(GameClient client, ClientFormat3F format)
        {
            if (client.Aisling is not { LoggedIn: true }) return;
            if (client is not { Authenticated: true, MapOpen: true }) return;

            if (ServerSetup.GlobalWorldMapTemplateCache.ContainsKey(client.Aisling.World))
            {
                var worldMap = ServerSetup.GlobalWorldMapTemplateCache[client.Aisling.World];
                client.PendingNode = worldMap?.Portals.Find(i => i.Destination.AreaID == format.Index);
            }

            TraverseWorldMap(client);
        }

        private static void TraverseWorldMap(GameClient client)
        {
            if (!client.MapOpen) return;
            var selectedPortalNode = client.PendingNode;
            if (selectedPortalNode == null) return;

            for (var i = 0; i < 1; i++)
            {
                client.Aisling.CurrentMapId = selectedPortalNode.Destination.AreaID;
                client.Aisling.Pos = new Vector2(selectedPortalNode.Destination.Location.X, selectedPortalNode.Destination.Location.Y);
                client.Aisling.X = selectedPortalNode.Destination.Location.X;
                client.Aisling.Y = selectedPortalNode.Destination.Location.Y;
                client.TransitionToMap(selectedPortalNode.Destination.AreaID, selectedPortalNode.Destination.Location);
            }

            client.PendingNode = null;
            client.MapOpen = false;
        }

        /// <summary>
        /// On (map, player, monster, npc) Click - F1 Button
        /// </summary>
        protected override void Format43Handler(GameClient client, ClientFormat43 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;

            foreach (var script in client.Aisling.Map.Scripts.Values)
            {
                script.OnMapClick(client, format.X, format.Y);
            }

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (format.Serial == ServerSetup.Config.HelperMenuId &&
                ServerSetup.GlobalMundaneTemplateCache.ContainsKey(ServerSetup.Config
                    .HelperMenuTemplateKey))
            {
                if (!client.Aisling.CanCast || !client.Aisling.CanAttack) return;

                if (format.Type != 0x01) return;

                var helper = new UserHelper(this, new Mundane
                {
                    Serial = ServerSetup.Config.HelperMenuId,
                    Template = ServerSetup.GlobalMundaneTemplateCache[
                        ServerSetup.Config.HelperMenuTemplateKey]
                });

                helper.OnClick(this, client);
                return;
            }

            if (format.Type != 1) return;
            {
                var isMonster = false;
                var isNpc = false;
                var monsterCheck = ServerSetup.GlobalMonsterCache.Where(i => i.Key == format.Serial);
                var npcCheck = ServerSetup.GlobalMundaneCache.Where(i => i.Key == format.Serial);

                foreach (var (_, monster) in monsterCheck)
                {
                    if (monster?.Template?.ScriptName == null) continue;
                    var scripts = monster.Scripts?.Values;
                    if (scripts != null)
                        foreach (var script in scripts)
                            script.OnClick(client);
                    isMonster = true;
                }

                if (isMonster) return;

                foreach (var (_, npc) in npcCheck)
                {
                    if (npc?.Template?.ScriptKey == null) continue;
                    var scripts = npc.Scripts?.Values;
                    if (scripts != null)
                        foreach (var script in scripts)
                            script.OnClick(this, client);
                    isNpc = true;
                }

                if (isNpc) return;

                var obj = ObjectHandlers.GetObject(client.Aisling.Map, i => i.Serial == format.Serial, ObjectManager.Get.Aislings);
                switch (obj)
                {
                    case null:
                        return;
                    case Aisling aisling:
                        client.Aisling.Show(Scope.Self, new ServerFormat34(aisling));
                        break;
                }
            }
        }

        /// <summary>
        /// Remove Equipment From Slot
        /// </summary>
        protected override void Format44Handler(GameClient client, ClientFormat44 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.Dead) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            if (!client.Aisling.CanCast || !client.Aisling.CanAttack) return;

            if (client.Aisling.EquipmentManager.Equipment.ContainsKey(format.Slot))
                client.Aisling.EquipmentManager?.RemoveFromExisting(format.Slot);
        }

        /// <summary>
        /// Client Ping - Heartbeat
        /// </summary>
        protected override void Format45Handler(GameClient client, ClientFormat45 format)
        {
            if (client is not { Authenticated: true }) return;

            var time = DateTime.UtcNow;
            client.LastPingResponse = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            AutoSave(client);
        }

        /// <summary>
        /// Stat Increase Buttons
        /// </summary>
        protected override void Format47Handler(GameClient client, ClientFormat47 format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.IsRefreshing) return;
            CancelIfCasting(client);

            if (!client.Aisling.CanCast || !client.Aisling.CanAttack) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            var attribute = (Stat)format.Stat;

            if (client.Aisling.StatPoints == 0)
            {
                client.SendMessage(0x02, "You do not have any stat points left to use.");
                return;
            }

            if ((attribute & Stat.Str) == Stat.Str)
            {
                if (client.Aisling._Str >= 255)
                {
                    client.SendMessage(0x02, "You've maxed this attribute.");
                    return;
                }
                client.Aisling._Str++;
                client.SendMessage(0x02, $"{ServerSetup.Config.StrAddedMessage}");
            }

            if ((attribute & Stat.Int) == Stat.Int)
            {
                if (client.Aisling._Int >= 255)
                {
                    client.SendMessage(0x02, "You've maxed this attribute.");
                    return;
                }
                client.Aisling._Int++;
                client.SendMessage(0x02, $"{ServerSetup.Config.IntAddedMessage}");
            }

            if ((attribute & Stat.Wis) == Stat.Wis)
            {
                if (client.Aisling._Wis >= 255)
                {
                    client.SendMessage(0x02, "You've maxed this attribute.");
                    return;
                }
                client.Aisling._Wis++;
                client.SendMessage(0x02, $"{ServerSetup.Config.WisAddedMessage}");
            }

            if ((attribute & Stat.Con) == Stat.Con)
            {
                if (client.Aisling._Con >= 255)
                {
                    client.SendMessage(0x02, "You've maxed this attribute.");
                    return;
                }
                client.Aisling._Con++;
                client.SendMessage(0x02, $"{ServerSetup.Config.ConAddedMessage}");
            }

            if ((attribute & Stat.Dex) == Stat.Dex)
            {
                if (client.Aisling._Dex >= 255)
                {
                    client.SendMessage(0x02, "You've maxed this attribute.");
                    return;
                }
                client.Aisling._Dex++;
                client.SendMessage(0x02, $"{ServerSetup.Config.DexAddedMessage}");
            }

            if (client.Aisling._Wis > ServerSetup.Config.StatCap)
                client.Aisling._Wis = ServerSetup.Config.StatCap;
            if (client.Aisling._Str > ServerSetup.Config.StatCap)
                client.Aisling._Str = ServerSetup.Config.StatCap;
            if (client.Aisling._Int > ServerSetup.Config.StatCap)
                client.Aisling._Int = ServerSetup.Config.StatCap;
            if (client.Aisling._Con > ServerSetup.Config.StatCap)
                client.Aisling._Con = ServerSetup.Config.StatCap;
            if (client.Aisling._Dex > ServerSetup.Config.StatCap)
                client.Aisling._Dex = ServerSetup.Config.StatCap;

            if (client.Aisling._Wis <= 0)
                client.Aisling._Wis = 0;
            if (client.Aisling._Str <= 0)
                client.Aisling._Str = 0;
            if (client.Aisling._Int <= 0)
                client.Aisling._Int = 0;
            if (client.Aisling._Con <= 0)
                client.Aisling._Con = 0;
            if (client.Aisling._Dex <= 0)
                client.Aisling._Dex = 0;

            if (!client.Aisling.GameMaster)
                client.Aisling.StatPoints--;

            if (client.Aisling.StatPoints < 0)
                client.Aisling.StatPoints = 0;

            client.Aisling.Show(Scope.Self, new ServerFormat08(client.Aisling, StatusFlags.StructA));
        }

        /// <summary>
        /// Client Trading
        /// </summary>
        protected override void Format4AHandler(GameClient client, ClientFormat4A format)
        {
            if (format == null) return;
            if (client == null || !client.Aisling.LoggedIn) return;
            if (client is not { Authenticated: true }) return;

            if (client.Aisling.Skulled)
            {
                client.SystemMessage(ServerSetup.Config.ReapMessageDuringAction);
                client.Interrupt();
                return;
            }

            var trader = ObjectHandlers.GetObject<Aisling>(client.Aisling.Map, i => i.Serial.Equals((int)format.Id));
            var player = client.Aisling;

            if (player == null || trader == null) return;
            if (!player.WithinRangeOf(trader)) return;

            switch (format.Type)
            {
                case 0x00:
                    {
                        if (player.ThrewHealingPot) break;

                        player.Exchange = new ExchangeSession(trader);
                        trader.Exchange = new ExchangeSession(player);

                        var packet = new NetworkPacketWriter();
                        packet.Write((byte)0x42);
                        packet.Write((byte)0x00);
                        packet.Write((byte)0x00);
                        packet.Write((uint)trader.Serial);
                        packet.WriteStringA(trader.Username);
                        client.Send(packet);

                        packet = new NetworkPacketWriter();
                        packet.Write((byte)0x42);
                        packet.Write((byte)0x00);
                        packet.Write((byte)0x00);
                        packet.Write((uint)player.Serial);
                        packet.WriteStringA(player.Username);
                        trader.Client.Send(packet);
                    }
                    break;
                case 0x01:
                    {
                        if (player.ThrewHealingPot)
                        {
                            player.ThrewHealingPot = false;
                            break;
                        }

                        var item = client.Aisling.Inventory.Items[format.ItemSlot];

                        if (player.Exchange == null) return;
                        if (trader.Exchange == null) return;
                        if (player.Exchange.Trader != trader) return;
                        if (trader.Exchange.Trader != player) return;
                        if (player.Exchange.Confirmed) return;
                        if (item?.Template == null) return;

                        if (trader.CurrentWeight + item.Template.CarryWeight < trader.MaximumWeight)
                        {
                            if (player.EquipmentManager.RemoveFromInventory(item, true))
                            {
                                player.Exchange.Items.Add(item);
                                player.Exchange.Weight += item.Template.CarryWeight;
                            }

                            var packet = new NetworkPacketWriter();
                            packet.Write((byte)0x42);
                            packet.Write((byte)0x00);

                            packet.Write((byte)0x02);
                            packet.Write((byte)0x00);
                            packet.Write((byte)player.Exchange.Items.Count);
                            packet.Write(item.DisplayImage);
                            packet.Write(item.Color);
                            packet.WriteStringA(item.NoColorDisplayName);
                            client.Send(packet);

                            packet = new NetworkPacketWriter();
                            packet.Write((byte)0x42);
                            packet.Write((byte)0x00);

                            packet.Write((byte)0x02);
                            packet.Write((byte)0x01);
                            packet.Write((byte)player.Exchange.Items.Count);
                            packet.Write(item.DisplayImage);
                            packet.Write(item.Color);
                            packet.WriteStringA(item.NoColorDisplayName);
                            trader.Client.Send(packet);
                        }
                        else
                        {
                            var packet = new NetworkPacketWriter();
                            packet.Write((byte)0x42);
                            packet.Write((byte)0x00);

                            packet.Write((byte)0x04);
                            packet.Write((byte)0x00);
                            packet.WriteStringA("They can't seem to lift that. The trade has been cancelled.");
                            client.Send(packet);

                            packet = new NetworkPacketWriter();
                            packet.Write((byte)0x42);
                            packet.Write((byte)0x00);

                            packet.Write((byte)0x04);
                            packet.Write((byte)0x01);
                            packet.WriteStringA("That item seems to be too heavy. The trade has been cancelled.");
                            trader.Client.Send(packet);
                            player.CancelExchange();
                        }
                    }
                    break;
                case 0x02:
                    break;
                case 0x03:
                    {
                        if (player.Exchange == null) return;
                        if (trader.Exchange == null) return;
                        if (player.Exchange.Trader != trader) return;
                        if (trader.Exchange.Trader != player) return;
                        if (player.Exchange.Confirmed) return;

                        var gold = format.Gold;

                        if (gold > player.GoldPoints) return;
                        if (player.Exchange.Gold != 0) return;

                        player.GoldPoints -= gold;
                        player.Exchange.Gold = gold;
                        player.Client.SendStats(StatusFlags.StructC);

                        var packet = new NetworkPacketWriter();
                        packet.Write((byte)0x42);
                        packet.Write((byte)0x00);

                        packet.Write((byte)0x03);
                        packet.Write((byte)0x00);
                        packet.Write((uint)gold);
                        client.Send(packet);

                        packet = new NetworkPacketWriter();
                        packet.Write((byte)0x42);
                        packet.Write((byte)0x00);

                        packet.Write((byte)0x03);
                        packet.Write((byte)0x01);
                        packet.Write(gold);
                        trader.Client.Send(packet);
                    }
                    break;
                case 0x04:
                    {
                        if (player.Exchange == null) return;
                        if (trader.Exchange == null) return;
                        if (player.Exchange.Trader != trader) return;
                        if (trader.Exchange.Trader != player) return;

                        player.CancelExchange();
                    }
                    break;

                case 0x05:
                    {
                        if (player.Exchange == null) return;
                        if (trader.Exchange == null) return;
                        if (player.Exchange.Trader != trader) return;
                        if (trader.Exchange.Trader != player) return;
                        if (player.Exchange.Confirmed) return;

                        player.Exchange.Confirmed = true;

                        if (trader.Exchange.Confirmed)
                            player.FinishExchange();

                        var packet = new NetworkPacketWriter();
                        packet.Write((byte)0x42);
                        packet.Write((byte)0x00);

                        packet.Write((byte)0x05);
                        packet.Write((byte)0x00);
                        packet.WriteStringA("Trade was completed.");
                        client.Send(packet);

                        packet = new NetworkPacketWriter();
                        packet.Write((byte)0x42);
                        packet.Write((byte)0x00);

                        packet.Write((byte)0x05);
                        packet.Write((byte)0x01);
                        packet.WriteStringA("Trade was completed.");
                        trader.Client.Send(packet);
                    }
                    break;
            }
        }

        /// <summary>
        /// Begin Casting - Spell Lines
        /// </summary>
        protected override void Format4DHandler(GameClient client, ClientFormat4D format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.IsDead()) return;

            client.Aisling.IsCastingSpell = true;

            var lines = format.Lines;

            if (lines <= 0)
            {
                CancelIfCasting(client);
                return;
            }

            if (!client.CastStack.Any()) return;
            var info = client.CastStack.Peek();

            if (info == null) return;
            info.SpellLines = lines;
            var time = DateTime.UtcNow;
            info.Started = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
        }

        /// <summary>
        /// Skill / Spell Lines - Chant Message
        /// </summary>
        protected override void Format4EHandler(GameClient client, ClientFormat4E format)
        {
            if (client?.Aisling == null) return;
            if (client is not { Authenticated: true }) return;
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.IsDead()) return;

            var chant = format.Message;
            var subject = chant.IndexOf(" Lev", StringComparison.Ordinal);

            if (subject > 0)
            {
                if (chant.Length <= subject) return;

                if (chant.Length > subject)
                {
                    client.Say(chant.Trim());
                }

                return;
            }

            client.Say(chant, 0x02);
        }

        /// <summary>
        /// Player Portrait & Profile Message
        /// </summary>
        protected override void Format4FHandler(GameClient client, ClientFormat4F format)
        {
            if (client is not { Authenticated: true }) return;

            client.Aisling.ProfileMessage = format.Words;
            client.Aisling.PictureData = format.Image;
        }

        /// <summary>
        /// Client Tick Sync
        /// </summary>
        protected override void Format75Handler(GameClient client, ClientFormat75 format)
        {
            if (client is not { Authenticated: true }) return;

            AutoSave(client);
        }

        /// <summary>
        /// Player Social Status
        /// </summary>
        protected override void Format79Handler(GameClient client, ClientFormat79 format)
        {
            if (client is not { Authenticated: true }) return;
            client.Aisling.ActiveStatus = format.Status;
        }

        /// <summary>
        /// Client Metafile Request
        /// </summary>
        protected override void Format7BHandler(GameClient client, ClientFormat7B format)
        {
            if (client is not { Authenticated: true }) return;


            switch (format.Type)
            {
                case 0x00:
                    {
                        if (!format.Name.Contains("Class"))
                        {
                            client.Send(new ServerFormat6F
                            {
                                Type = 0x00,
                                Name = format.Name
                            });
                            break;
                        }

                        var name = DecideOnSkillsToPull(client, format);

                        client.Send(new ServerFormat6F
                        {
                            Type = 0x00,
                            Name = name
                        });
                    }
                    break;
                case 0x01:
                    client.Send(new ServerFormat6F
                    {
                        Type = 0x01
                    });
                    break;
            }
        }

        private static string DecideOnSkillsToPull(IGameClient client, ClientFormat7B format)
        {
            if (client.Aisling == null) return null;

            switch (client.Aisling.Race)
            {
                case Race.UnDecided:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass1";
                        case "SClass2":
                            return "SClass2";
                        case "SClass3":
                            return "SClass3";
                        case "SClass4":
                            return "SClass4";
                        case "SClass5":
                            return "SClass5";
                        case "SClass6":
                            return "SClass6";
                    }
                    break;
                case Race.Human:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass1";
                        case "SClass2":
                            return "SClass2";
                        case "SClass3":
                            return "SClass3";
                        case "SClass4":
                            return "SClass4";
                        case "SClass5":
                            return "SClass5";
                        case "SClass6":
                            return "SClass6";
                    }
                    break;
                case Race.HalfElf:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass7";
                        case "SClass2":
                            return "SClass8";
                        case "SClass3":
                            return "SClass9";
                        case "SClass4":
                            return "SClass10";
                        case "SClass5":
                            return "SClass11";
                        case "SClass6":
                            return "SClass12";
                    }
                    break;
                case Race.HighElf:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass13";
                        case "SClass2":
                            return "SClass14";
                        case "SClass3":
                            return "SClass15";
                        case "SClass4":
                            return "SClass16";
                        case "SClass5":
                            return "SClass17";
                        case "SClass6":
                            return "SClass18";
                    }
                    break;
                case Race.DarkElf:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass19";
                        case "SClass2":
                            return "SClass20";
                        case "SClass3":
                            return "SClass21";
                        case "SClass4":
                            return "SClass22";
                        case "SClass5":
                            return "SClass23";
                        case "SClass6":
                            return "SClass24";
                    }
                    break;
                case Race.WoodElf:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass25";
                        case "SClass2":
                            return "SClass26";
                        case "SClass3":
                            return "SClass27";
                        case "SClass4":
                            return "SClass28";
                        case "SClass5":
                            return "SClass29";
                        case "SClass6":
                            return "SClass30";
                    }
                    break;
                case Race.Orc:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass31";
                        case "SClass2":
                            return "SClass32";
                        case "SClass3":
                            return "SClass33";
                        case "SClass4":
                            return "SClass34";
                        case "SClass5":
                            return "SClass35";
                        case "SClass6":
                            return "SClass36";
                    }
                    break;
                case Race.Dwarf:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass37";
                        case "SClass2":
                            return "SClass38";
                        case "SClass3":
                            return "SClass39";
                        case "SClass4":
                            return "SClass40";
                        case "SClass5":
                            return "SClass41";
                        case "SClass6":
                            return "SClass42";
                    }
                    break;
                case Race.Halfling:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass43";
                        case "SClass2":
                            return "SClass44";
                        case "SClass3":
                            return "SClass45";
                        case "SClass4":
                            return "SClass46";
                        case "SClass5":
                            return "SClass47";
                        case "SClass6":
                            return "SClass48";
                    }
                    break;
                case Race.Dragonkin:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass49";
                        case "SClass2":
                            return "SClass50";
                        case "SClass3":
                            return "SClass51";
                        case "SClass4":
                            return "SClass52";
                        case "SClass5":
                            return "SClass53";
                        case "SClass6":
                            return "SClass54";
                    }
                    break;
                case Race.HalfBeast:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass55";
                        case "SClass2":
                            return "SClass56";
                        case "SClass3":
                            return "SClass57";
                        case "SClass4":
                            return "SClass58";
                        case "SClass5":
                            return "SClass59";
                        case "SClass6":
                            return "SClass60";
                    }
                    break;
                case Race.Fish:
                    switch (format.Name)
                    {
                        case "SClass1":
                            return "SClass61";
                        case "SClass2":
                            return "SClass62";
                        case "SClass3":
                            return "SClass63";
                        case "SClass4":
                            return "SClass64";
                        case "SClass5":
                            return "SClass65";
                        case "SClass6":
                            return "SClass66";
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// Display Mask
        /// </summary>
        protected override void Format89Handler(GameClient client, ClientFormat89 format) { }

        #endregion

        private static async void AutoSave(IGameClient client)
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            if ((readyTime - client.LastSave).TotalSeconds > ServerSetup.Config.SaveRate) await client.Save();
        }

        public static void CancelIfCasting(GameClient client)
        {
            if (!client.Aisling.LoggedIn) return;
            if (client.Aisling.IsCastingSpell)
                client.Send(new ServerFormat48());

            client.CastStack.Clear();
            client.Aisling.IsCastingSpell = false;
        }

        private static void ExecuteAbility(IGameClient lpClient, Skill lpSkill, bool optExecuteScript = true)
        {
            lpSkill.InUse = true;

            if (lpSkill.Template.ScriptName == "Assail")
            {
                var itemScripts = lpClient.Aisling.EquipmentManager.Equipment[1]?.Item?.WeaponScripts;

                if (itemScripts != null)
                    foreach (var itemScript in itemScripts.Values.Where(itemScript => itemScript != null))
                        itemScript.OnUse(lpClient.Aisling);
            }

            if (optExecuteScript)
                foreach (var script in lpSkill.Scripts.Values)
                    script?.OnUse(lpClient.Aisling);

            lpSkill.InUse = false;
        }

        public static void CheckWarpTransitions(GameClient client)
        {
            foreach (var (_, value) in ServerSetup.GlobalWarpTemplateCache)
            {
                var breakOuterLoop = false;
                if (value.ActivationMapId != client.Aisling.CurrentMapId) continue;

                lock (ServerSetup.SyncLock)
                {
                    foreach (var _ in value.Activations.Where(o =>
                                 o.Location.X == (int)client.Aisling.Pos.X &&
                                 o.Location.Y == (int)client.Aisling.Pos.Y))
                    {
                        if (value.WarpType == WarpType.Map)
                        {
                            client.WarpToAdjacentMap(value);
                            breakOuterLoop = true;
                            break;
                        }

                        if (value.WarpType != WarpType.World) continue;
                        if (!ServerSetup.GlobalWorldMapTemplateCache.ContainsKey(value.To.PortalKey)) return;
                        if (client.Aisling.World != value.To.PortalKey) client.Aisling.World = value.To.PortalKey;

                        var portal = new PortalSession();
                        portal.TransitionToMap(client);
                        breakOuterLoop = true;
                        break;
                    }
                }

                if (breakOuterLoop) break;
            }
        }

        public void RemoveFromServer(GameClient client, byte type = 0)
        {
            if (client == null) return;

            try
            {
                if (client.Server == null) return;
                if (client.Server.Clients.Count == 0) return;
                if (client.Server.Clients.Values.Count == 0) return;
                if (!client.Server.Clients.Values.Contains(client)) return;

                if (type == 0)
                {
                    ExitGame(client);
                    return;
                }

                client.CloseDialog();
                if (client.Aisling == null) return;
                client.Aisling.CancelExchange();

                client.DlgSession = null;
                var time = DateTime.UtcNow;
                client.Aisling.LastLogged = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
                client.Aisling.ActiveReactor = null;
                client.Aisling.ActiveSequence = null;
                client.Aisling.Remove(true);
                client.Aisling.LoggedIn = false;
            }
            catch (Exception e)
            {
                ServerSetup.Logger(e.Message, LogLevel.Error);
                ServerSetup.Logger(e.StackTrace, LogLevel.Error);
            }
        }

        private async void ExitGame(GameClient client)
        {
            var redirect = new Redirect
            {
                Serial = Convert.ToString(client.Serial, CultureInfo.CurrentCulture),
                Salt = Encoding.UTF8.GetString(client.Encryption.Parameters.Salt),
                Seed = Convert.ToString(client.Encryption.Parameters.Seed, CultureInfo.CurrentCulture),
                Name = client.Aisling.Username,
                Type = "2"
            };

            if (client.Aisling == null) return;
            client.Aisling.LoggedIn = false;
            await client.Save();

            if (ServerSetup.Redirects.ContainsKey(client.Aisling.Serial))
                ServerSetup.Redirects.TryRemove(client.Aisling.Serial, out _);

            client.Send(new ServerFormat03
            {
                CalledFromMethod = true,
                EndPoint = new IPEndPoint(Address, 2610),
                Redirect = redirect
            });

            client.Send(new ServerFormat02(0x00, ""));
            RemoveClient(client);
            ServerSetup.Logger($"{client.Aisling.Username} either logged out or was removed from the server.");
            client.Dispose();
        }

        public override void ClientDisconnected(GameClient client)
        {
            if (client == null) return;
            if (client.Aisling?.GroupId != 0)
                Party.RemovePartyMember(client.Aisling);
            RemoveFromServer(client, 1);
            RemoveFromServer(client);
        }

        private Aisling LoadPlayer(GameClient client, string player)
        {
            if (client == null) return null;
            if (player.IsNullOrEmpty()) return null;

            var aisling = StorageManager.AislingBucket.LoadAisling(player);

            if (aisling.Result == null) return null;
            client.Aisling = aisling.Result;
            client.Aisling.Serial = aisling.Result.Serial;
            client.Aisling.Pos = new Vector2(aisling.Result.X, aisling.Result.Y);
            client.Aisling.Client = client;

            if (client.Aisling == null)
            {
                client.SendMessage(0x02, "Something happened. Please report this bug to Zolian staff.");
                Analytics.TrackEvent($"{client.Aisling.Username} failed to load character.");
                base.ClientDisconnected(client);
                RemoveClient(client);
                return null;
            }

            if (client.Aisling._Str <= 0 || client.Aisling._Int <= 0 || client.Aisling._Wis <= 0 || client.Aisling._Con <= 0 || client.Aisling._Dex <= 0)
            {
                client.SendMessage(0x02, "Your stats have has been corrupted. Please report this bug to Zolian staff.");
                Analytics.TrackEvent($"{client.Aisling.Username} has corrupted stats.");
                base.ClientDisconnected(client);
                RemoveClient(client);
                return null;
            }

            var dupedClients = Clients.Values.Where(i => i.Aisling != null && i.Aisling.Serial == aisling.Result.Serial && i.Serial != aisling.Result.Client.Serial).ToArray();

            if (!dupedClients.Any()) return CleanUpLoadPlayer(client);
            foreach (var dupedClient in dupedClients)
            {
                dupedClient.Aisling?.Remove(true);
                base.ClientDisconnected(dupedClient);
                RemoveClient(client);
            }

            return client.Aisling;
        }

        private static Aisling CleanUpLoadPlayer(GameClient client)
        {
            CheckOnLoad(client);

            if (client.Aisling.Map != null) client.Aisling.CurrentMapId = client.Aisling.Map.ID;
            client.Aisling.LoggedIn = false;
            client.Aisling.EquipmentManager.Client = client;
            client.Aisling.CurrentWeight = 0;
            client.Aisling.ActiveStatus = ActivityStatus.Awake;

            var ip = client.Socket.RemoteEndPoint as IPEndPoint;
            Analytics.TrackEvent($"{ip!.Address} logged in {client.Aisling.Username}");
            client.Flush();
            client.Load();
            client.SendMessage(0x02, $"{ServerSetup.Config.ServerWelcomeMessage}: {client.Aisling.Username}");
            client.SendStats(StatusFlags.All);
            client.LoggedIn(true);

            if (client.Aisling == null) return null;

            if (!client.Aisling.Dead) return client.Aisling;
            client.Aisling.Flags = AislingFlags.Ghost;
            client.Aisling.WarpToHell();

            return client.Aisling;
        }

        private static void CheckOnLoad(IGameClient client)
        {
            var aisling = client.Aisling;

            aisling.SkillBook ??= new SkillBook();
            aisling.SpellBook ??= new SpellBook();
            aisling.Inventory ??= new Inventory();
            aisling.BankManager ??= new Bank();
            aisling.EquipmentManager ??= new EquipmentManager(aisling.Client);
        }
    }
}