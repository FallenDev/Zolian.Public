using System.Collections.Concurrent;
using System.Data;
using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Network.Client;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Object;
using Darkages.Sprites;
using Darkages.Templates;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Types
{
    public class Inventory : ObjectManager
    {
        private const int LENGTH = 59;

        public readonly ConcurrentDictionary<int, Item> Items = new();

        public Inventory()
        {
            for (var i = 0; i < LENGTH; i++) Items[i + 1] = null;
        }

        public IEnumerable<byte> BankList => (Items.Where(i => i.Value is {Template: { }} && i.Value.Template.Flags.HasFlag(ItemFlags.Bankable))).Select(i => i.Value.InventorySlot);

        public int Length => Items.Count;
        
        public bool CanPickup(Aisling player, Item LpItem)
        {
            if (player == null || LpItem == null)
                return false;

            if (LpItem.Template == null)
                return false;

            return player.CurrentWeight + LpItem.Template.CarryWeight < player.MaximumWeight &&
                   FindEmpty() != byte.MaxValue;
        }

        public byte FindEmpty()
        {
            byte idx = 1;

            foreach (var slot in Items)
            {
                if (slot.Value == null)
                    return idx;

                idx++;
            }

            return byte.MaxValue;
        }

        public Item FindInSlot(int Slot)
        {
            if (Items.ContainsKey(Slot)) return Items[Slot];

            return null;
        }

        public new IEnumerable<Item> Get(Predicate<Item> prediate)
        {
            return Items.Values.Where(i => i != null && prediate(i)).ToArray();
        }

        public List<Item> HasMany(Predicate<Item> predicate)
        {
            return Items.Values.Where(i => i != null && predicate(i)).ToList();
        }

        public Item Has(Predicate<Item> prediate)
        {
            return Items.Values.FirstOrDefault(i => i != null && prediate(i));
        }

        public int Has(Template templateContext)
        {
            var items = Items.Where(i => i.Value != null && i.Value.Template.Name == templateContext.Name)
                .Select(i => i.Value).ToList();

            var anyItem = items.FirstOrDefault();

            if (anyItem?.Template == null)
                return 0;

            var result = anyItem.Template.CanStack ? items.Sum(i => i.Stacks) : items.Count;

            return result;
        }

        public int HasCount(Template templateContext)
        {
            var items = Items.Where(i => i.Value != null && i.Value.Template.Name == templateContext.Name)
                .Select(i => i.Value).ToList();

            return items.Count;
        }

        public void Remove(GameClient client, Item item)
        {
            if (item == null)
                return;

            if (Remove(item.InventorySlot) != null) client.Send(new ServerFormat10(item.InventorySlot));
            client.SendStats(StatusFlags.StructA);
            item.DeleteFromAislingDb();
        }

        public Item Remove(byte movingFrom)
        {
            if (Items.ContainsKey(movingFrom))
            {
                var copy = Items[movingFrom];
                Items[movingFrom] = null;
                return copy;
            }

            return null;
        }

        private static void RemoveFromInventory(GameClient client, Item item, bool handleWeight = false)
        {
            if (item != null && client.Aisling.Inventory.Remove(item.InventorySlot) == null) return;
            if (item == null) return;

            client.Send(new ServerFormat10(item.InventorySlot));

            if (handleWeight)
            {
                client.Aisling.CurrentWeight -= item.Template.CarryWeight;
                if (client.Aisling.CurrentWeight < 0)
                    client.Aisling.CurrentWeight = 0;
            }

            client.LastItemDropped = item;
            client.SendStats(StatusFlags.StructA);
            item.DeleteFromAislingDb();
        }

        public static void RemoveRange(GameClient client, Item item, int range)
        {
            var remaining = Math.Abs(item.Stacks - range);

            if (remaining == 0)
            {
                RemoveFromInventory(client, item, true);
                item.Remove();
            }
            else
            {
                item.Stacks = (ushort) remaining;
                client.Aisling.Inventory.Set(item);
                client.Send(new ServerFormat10(item.InventorySlot));
                UpdateSlot(client, item);
            }
        }

        public void Set(Item s)
        {
            if (s == null) return;

            if (Items.ContainsKey(s.InventorySlot)) Items[s.InventorySlot] = s;
        }

        public static void UpdateSlot(GameClient client, Item item)
        {
            UpdateInventory(client.Aisling);
            client.Send(new ServerFormat0F(item));
        }

        private static async void UpdateInventory(Aisling obj)
        {
            if (obj?.Inventory == null) return;

            var dataTable = new DataTable();
            dataTable = MappedDataTablePlayersInventory(dataTable, obj);
            var tableNumber = Generator.GenerateNumber();
            var table = $"TmpTable{tableNumber}";

            try
            {
                await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await using var cmd = new SqlCommand("", sConn);
                await sConn.OpenAsync();
                cmd.CommandTimeout = 5;
                cmd.CommandText = $"CREATE TABLE {table}([ItemId] INT, [Name] VARCHAR(45), [Serial] INT, [Color] INT, [Cursed] BIT, [Durability] INT, [Identified] BIT, [ItemVariance] VARCHAR(15), [WeapVariance] VARCHAR(15), [ItemQuality] VARCHAR(10), [OriginalQuality] VARCHAR(10), [InventorySlot] INT, [Stacks] INT, [Enchantable] BIT)";
                await cmd.ExecuteNonQueryAsync();

                using var bulkCopy = new SqlBulkCopy(sConn);
                bulkCopy.BulkCopyTimeout = 5;
                bulkCopy.DestinationTableName = table;
                await bulkCopy.WriteToServerAsync(dataTable);
                bulkCopy.Close();

                cmd.CommandText =
                    "BEGIN TRAN; " +
                    "UPDATE P SET P.[Color] = T.[Color], P.[Cursed] = T.[Cursed], P.[Durability] = T.[Durability], " +
                    "P.[Identified] = T.[Identified], P.[ItemVariance] = T.[ItemVariance], P.[WeapVariance] = T.[WeapVariance], " +
                    "P.[ItemQuality] = T.[ItemQuality], P.[OriginalQuality] = T.[OriginalQuality], P.[InventorySlot] = T.[InventorySlot], " +
                    "P.[Stacks] = T.[Stacks], P.[Enchantable] = T.[Enchantable] " +
                    $"FROM ZolianPlayers.dbo.PlayersInventory AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[ItemId] = T.[ItemId]; DROP TABLE {table}; " +
                    "COMMIT;";
                await cmd.ExecuteNonQueryAsync();
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

        private static DataTable MappedDataTablePlayersInventory(DataTable dataTable, Aisling obj)
        {
            dataTable.Clear();

            dataTable.Columns.Add("ItemId");
            dataTable.Columns.Add("Name");
            dataTable.Columns.Add("Serial");
            dataTable.Columns.Add("Color");
            dataTable.Columns.Add("Cursed");
            dataTable.Columns.Add("Durability");
            dataTable.Columns.Add("Identified");
            dataTable.Columns.Add("ItemVariance");
            dataTable.Columns.Add("WeapVariance");
            dataTable.Columns.Add("ItemQuality");
            dataTable.Columns.Add("OriginalQuality");
            dataTable.Columns.Add("InventorySlot");
            dataTable.Columns.Add("Stacks");
            dataTable.Columns.Add("Enchantable");

            foreach (var item in obj.Inventory.Items.Values.Where(i => i != null && i.InventorySlot != 0))
            {
                var row = dataTable.NewRow();
                row["ItemId"] = item.ItemId;
                row["Name"] = item.Template.Name;
                row["Serial"] = obj.Serial;
                row["Color"] = item.Color;
                row["Cursed"] = item.Cursed;
                row["Durability"] = item.Durability;
                row["Identified"] = item.Identified;
                row["ItemVariance"] = item.ItemVariance;
                row["WeapVariance"] = item.WeapVariance;
                row["ItemQuality"] = item.ItemQuality;
                row["OriginalQuality"] = item.OriginalQuality;
                row["InventorySlot"] = item.InventorySlot;
                row["Stacks"] = item.Stacks;
                row["Enchantable"] = item.Enchantable;

                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }
}