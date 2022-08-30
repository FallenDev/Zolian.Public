using System.Data;

using Dapper;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Network.Client;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Sprites;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using ServiceStack;

namespace Darkages.Types
{
    public class Bank
    {
        public Bank()
        {
            Items = new Dictionary<int, Item>();
        }

        public Dictionary<int, Item> Items { get; }

        public static async void Deposit(GameClient client, Item item)
        {
            var temp = new Item
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Serial = item.Serial,
                Color = item.Color,
                Cursed = item.Cursed,
                Durability = item.Durability,
                Identified = item.Identified,
                ItemVariance = item.ItemVariance,
                WeapVariance = item.WeapVariance,
                ItemQuality = item.ItemQuality,
                OriginalQuality = item.OriginalQuality,
                Stacks = (ushort)client.PendingBankedSession.ArgsQuantity,
                Enchantable = item.Enchantable,
                Template = item.Template
            };

            if (temp.Template.CanStack)
                try
                {
                    const string procedure = "[CheckIfItemExists]";
                    await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                    await sConn.OpenAsync();

                    var cmd = new SqlCommand(procedure, sConn)
                    {
                        CommandType = CommandType.StoredProcedure
                    };
                    cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = temp.DisplayName;
                    cmd.Parameters.Add("Serial", SqlDbType.Int).Value = client.Aisling.Serial;
                    cmd.CommandTimeout = 5;

                    await using var reader = await cmd.ExecuteReaderAsync();
                    var itemName = "";
                    while (reader.Read())
                    {
                        itemName = reader["Name"].ToString();
                        var stacked = (int)reader["Stacks"];
                        temp.Stacks += (ushort)stacked;
                    }

                    reader.Close();
                    await sConn.CloseAsync();

                    if (itemName.IsNullOrEmpty())
                    {
                        AddToAislingDb(client.Aisling, temp);
                    }
                    else
                    {
                        UpdateBanked(client.Aisling, temp);
                    }

                    return;
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
            
            AddToAislingDb(client.Aisling, temp);
        }

        private static async void AddToAislingDb(ISprite aisling, Item item)
        {
            try
            {
                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();
                await sConn.OpenAsync();
                var color = ItemColors.ItemColorsToInt(item.Template.Color);
                var quality = ItemEnumConverters.QualityToString(item.ItemQuality);
                var orgQuality = ItemEnumConverters.QualityToString(item.OriginalQuality);
                var templateNameReplaced = item.Template.Name;

                if (item.Template.Name.Contains("'"))
                {
                    templateNameReplaced = item.Template.Name.Replace("'", "''");
                }

                var playerBanked = "INSERT INTO ZolianPlayers.dbo.PlayersBanked (ItemId, Name, Serial, Color, Cursed, Durability, Identified, ItemVariance, WeapVariance, ItemQuality, OriginalQuality, Stacks, Enchantable, Stackable) VALUES " +
                                      $"('{item.ItemId}','{templateNameReplaced}','{aisling.Serial}','{color}','{item.Cursed}','{item.Durability}','{item.Identified}','{item.ItemVariance}','{item.WeapVariance}','{quality}','{orgQuality}','{item.Stacks}','{item.Enchantable}','{item.Template.CanStack}')";

                var cmd = new SqlCommand(playerBanked, sConn);
                cmd.CommandTimeout = 5;
                adapter.InsertCommand = cmd;
                await adapter.InsertCommand.ExecuteNonQueryAsync();
                await sConn.CloseAsync();
            }
            catch (SqlException e)
            {
                if (e.Message.Contains("PK__Players"))
                {
                    foreach (var client in ServerSetup.Game.Clients.Values)
                    {
                        if (!e.Message.Contains(aisling.Serial.ToString())) continue;
                        if (client.Aisling.Serial != aisling.Serial) continue;
                        client.SendMessage(0x03, "Item did not save correctly to Bank. Contact GM");
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

        private static async void UpdateBanked(ISprite aisling, Item item)
        {
            if (item == null) return;

            var dataTable = new DataTable();
            dataTable = MappedDataTablePlayersInventory(dataTable, aisling, item);
            var tableNumber = Generator.GenerateNumber();
            var table = $"TmpTable{tableNumber}";

            try
            {
                await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await using var cmd = new SqlCommand("", sConn);
                await sConn.OpenAsync();
                cmd.CommandTimeout = 5;
                cmd.CommandText = $"CREATE TABLE {table}([ItemId] INT, [Name] VARCHAR(45), [Serial] INT, [Color] INT, [Cursed] BIT, [Durability] INT, [Identified] BIT, [ItemVariance] VARCHAR(15), [WeapVariance] VARCHAR(15), [ItemQuality] VARCHAR(10), [OriginalQuality] VARCHAR(10), [Stacks] INT, [Enchantable] BIT, [Stackable] BIT)";
                await cmd.ExecuteNonQueryAsync();

                using var bulkCopy = new SqlBulkCopy(sConn);
                bulkCopy.BulkCopyTimeout = 5;
                bulkCopy.DestinationTableName = table;
                await bulkCopy.WriteToServerAsync(dataTable);
                bulkCopy.Close();

                if (!item.Template.CanStack)
                {
                    cmd.CommandText =
                        "BEGIN TRAN; " +
                        "UPDATE P SET P.[Color] = T.[Color], P.[Cursed] = T.[Cursed], P.[Durability] = T.[Durability], " +
                        "P.[Identified] = T.[Identified], P.[ItemVariance] = T.[ItemVariance], P.[WeapVariance] = T.[WeapVariance], " +
                        "P.[ItemQuality] = T.[ItemQuality], P.[OriginalQuality] = T.[OriginalQuality], " +
                        "P.[Stacks] = T.[Stacks], P.[Enchantable] = T.[Enchantable], P.[Stackable] = T.[Stackable] " +
                        $"FROM ZolianPlayers.dbo.PlayersBanked AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[ItemId] = T.[ItemId]; DROP TABLE {table}; " +
                        "COMMIT;";
                }
                else
                {
                    cmd.CommandText =
                        "BEGIN TRAN; " +
                        "UPDATE P SET P.[Color] = T.[Color], P.[Cursed] = T.[Cursed], P.[Durability] = T.[Durability], " +
                        "P.[Identified] = T.[Identified], P.[ItemVariance] = T.[ItemVariance], P.[WeapVariance] = T.[WeapVariance], " +
                        "P.[ItemQuality] = T.[ItemQuality], P.[OriginalQuality] = T.[OriginalQuality], " +
                        "P.[Stacks] = T.[Stacks], P.[Enchantable] = T.[Enchantable], P.[Stackable] = T.[Stackable] " +
                        $"FROM ZolianPlayers.dbo.PlayersBanked AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[Name] = T.[Name]; DROP TABLE {table}; " +
                        "COMMIT;";
                }

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

        private static DataTable MappedDataTablePlayersInventory(DataTable dataTable, ISprite obj, Item item)
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
            dataTable.Columns.Add("Stacks");
            dataTable.Columns.Add("Enchantable");
            dataTable.Columns.Add("Stackable");

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
            row["Stacks"] = item.Stacks;
            row["Enchantable"] = item.Enchantable;
            row["Stackable"] = item.Template.CanStack;
            dataTable.Rows.Add(row);

            return dataTable;
        }

        public bool Withdraw(GameClient client, Mundane mundane)
        {
            #region Item Check

            if (client.PendingBankedSession.SelectedItem == null) return false;
            if (!ServerSetup.GlobalItemTemplateCache.ContainsKey(client.PendingBankedSession.SelectedItem.Template.Name)) return false;
            var stack = 1;
            if (client.PendingBankedSession.BankQuantity > 0)
                stack = client.PendingBankedSession.SelectedItem.Stacks;
            var pulledStacks = Math.Abs(stack - client.PendingBankedSession.ArgsQuantity);

            #endregion

            if (client.Aisling.CurrentWeight + client.PendingBankedSession.SelectedItem.Template.CarryWeight > client.Aisling.MaximumWeight)
            {
                mundane.Show(Scope.NearbyAislings, new ServerFormat0D
                {
                    Serial = mundane.Serial,
                    Text = $"{client.PendingBankedSession.SelectedItem.Template.Name} is too heavy for you.",
                    Type = 0x03
                });
                return false;
            }

            if (pulledStacks > client.PendingBankedSession.SelectedItem.Template.MaxStack && client.PendingBankedSession.SelectedItem.Template.CanStack)
            {
                mundane.Show(Scope.NearbyAislings, new ServerFormat0D
                {
                    Serial = mundane.Serial,
                    Text = "You can't have that many.",
                    Type = 0x03
                });
                return false;
            }

            if (pulledStacks > client.PendingBankedSession.SelectedItem.Stacks && client.PendingBankedSession.SelectedItem.Template.CanStack)
            {
                mundane.Show(Scope.NearbyAislings, new ServerFormat0D
                {
                    Serial = mundane.Serial,
                    Text = "You don't have that many banked with us.",
                    Type = 0x03
                });
                return false;
            }

            client.PendingBankedSession.SelectedItem.GiveTo(client.Aisling);
            client.Aisling.BankManager.Items.TryRemove(client.PendingBankedSession.ItemId, out _);
            DeleteFromAislingDb(client);
            return true;
        }

        private static void DeleteFromAislingDb(IGameClient client)
        {
            if (client.PendingBankedSession.ItemId == 0) return;

            try
            {
                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                sConn.Open();
                const string cmd = "DELETE FROM ZolianPlayers.dbo.PlayersBanked WHERE ItemId = @ItemId";
                sConn.Execute(cmd, new { client.PendingBankedSession.ItemId });
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

        public static void DepositGold(IGameClient client, uint gold)
        {
            client.Aisling.GoldPoints -= gold;
            client.Aisling.BankedGold += gold;
            client.SendStats(StatusFlags.StructC);
            client.Save();
        }

        public static void WithdrawGold(IGameClient client, uint gold)
        {
            client.Aisling.GoldPoints += gold;
            client.Aisling.BankedGold -= gold;
            client.SendStats(StatusFlags.StructC);
            client.Save();
        }
    }
}