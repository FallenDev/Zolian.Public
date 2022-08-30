using System.Numerics;
using Dapper;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Models;
using Darkages.Sprites;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Types
{
    public class Death
    {
        private Vector2 Location { get; set; }
        private int MapId { get; set; }
        public Aisling Owner { get; set; }

        public async Task Reap(Aisling player)
        {
            Owner = player;
            if (Owner == null) return;

            Location = Owner.Pos;
            MapId = Owner.CurrentMapId;

            await ReapEquipment();
            await ReapInventory();
            await ReapGold();

            Owner.Client.SendMessage(0x02, $"{ServerSetup.Config.DeathReapingMessage}");
            Owner.Client.SendStats(StatusFlags.All);
            Owner.Client.UpdateDisplay();
        }

        private Task ReapInventory()
        {
            List<Item> inv;

            lock (Owner.Inventory.Items)
            {
                var batch = Owner.Inventory.Items.Select(i => i.Value).Where(i => i != null && i.Template.Flags.HasFlag(ItemFlags.Dropable) && !i.Template.Flags.HasFlag(ItemFlags.NonDropableQuest));
                inv = new List<Item>(batch);
            }

            foreach (var obj in inv.Where(obj => obj?.Template != null))
            {
                if (obj.Durability > 0 && obj.Template.Flags.HasFlag(ItemFlags.Equipable))
                {
                    obj.Durability -= obj.Durability * 10 / 100;
                }

                if (!obj.Template.Flags.HasFlag(ItemFlags.Dropable)) continue;

                Owner.EquipmentManager.RemoveFromInventory(obj, true);

                if (obj.Template.Flags.HasFlag(ItemFlags.Perishable))
                {
                    obj.ItemQuality = Item.Quality.Damaged;
                }
                
                ReleaseInventory(obj);
            }
            
            return Task.CompletedTask;
        }

        private Task ReapEquipment()
        {
            List<EquipmentSlot> inv;

            lock (Owner.EquipmentManager.Equipment)
            {
                var batch = Owner.EquipmentManager.Equipment.Where(i => i.Value != null && i.Value.Item.Template.Flags.HasFlag(ItemFlags.Dropable))
                    .Select(i => i.Value);

                inv = new List<EquipmentSlot>(batch);
            }

            foreach (var obj in from equipSlot in inv let obj = equipSlot.Item where obj?.Template != null where Owner.EquipmentManager.RemoveFromExisting(equipSlot.Slot, false) select obj)
            {
                obj.Durability -= obj.Durability * 10 / 100;

                if (obj.Durability <= 0) continue;
                if (obj.Template.Flags.HasFlag(ItemFlags.PerishIFEquipped) ||
                    obj.Template.Flags.HasFlag(ItemFlags.Perishable))
                {
                    obj.ItemQuality = Item.Quality.Damaged;
                }

                ReleaseEquipment(obj);
            }

            return Task.CompletedTask;
        }

        private Task ReapGold()
        {
            var gold = Owner.GoldPoints;
            {
                Money.Create(Owner, gold, Owner.Position);
                Owner.GoldPoints = 0;
            }

            return Task.CompletedTask;
        }

        private void ReleaseInventory(Item item)
        {
            item.Pos = Location;
            item.CurrentMapId = MapId;

            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            item.AbandonedDate = readyTime;
            item.Cursed = false;

            item.DeleteFromAislingDb();
            item.Serial = Generator.GenerateNumber();
            item.ItemId = Generator.GenerateNumber();

            item.AddObject(item);

            foreach (var player in item.AislingsNearby())
            {
                item.ShowTo(player);
            }
        }

        private void ReleaseEquipment(Item item)
        {
            item.Pos = Location;
            item.CurrentMapId = MapId;

            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            item.AbandonedDate = readyTime;
            item.Cursed = false;

            DeleteFromAislingDb(item);
            item.Serial = Generator.GenerateNumber();
            item.ItemId = Generator.GenerateNumber();

            item.AddObject(item);

            foreach (var player in item.AislingsNearby())
            {
                item.ShowTo(player);
            }
        }

        private static async void DeleteFromAislingDb(Item item)
        {
            var sConn = new SqlConnection(AislingStorage.ConnectionString);
            if (item.ItemId == 0) return;

            try
            {
                await sConn.OpenAsync();
                const string cmd = "DELETE FROM ZolianPlayers.dbo.PlayersEquipped WHERE ItemId = @ItemId";
                await sConn.ExecuteAsync(cmd, new { item.ItemId });
                await sConn.CloseAsync();
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