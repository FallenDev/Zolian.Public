using Dapper;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Network.Client;
using Darkages.Sprites;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Types
{
    public class Legend
    {
        public readonly List<LegendItem> LegendMarks = new();

        public void AddLegend(LegendItem legend, GameClient client)
        {
            if (legend == null) return;
            if (client.Aisling == null) return;
            if (LegendMarks.Contains(legend)) return;
            LegendMarks.Add(legend);
            AddToAislingDb(client.Aisling, legend);
        }

        public bool Has(string lpVal)
        {
            return LegendMarks.Any(i => i.Value.Equals(lpVal));
        }

        public void Remove(LegendItem legend, GameClient client)
        {
            if (legend == null) return;
            if (client.Aisling == null) return;
            LegendMarks.Remove(legend);
            DeleteFromAislingDb(client.Aisling, legend);
        }

        public class LegendItem
        {
            public int LegendId { get; init; }
            public string Category { get; init; }
            public DateTime Time { get; init; }
            public LegendColor Color { get; init; }
            public byte Icon { get; init; }
            public string Value { get; init; }
        }

        private static async void AddToAislingDb(Aisling aisling, LegendItem legend)
        {
            try
            {
                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();
                await sConn.OpenAsync();
                var legendId = Generator.GenerateNumber();
                var player = "INSERT INTO ZolianPlayers.dbo.PlayersLegend (LegendId, Serial, Category, Time, Color, Icon, Value) VALUES " +
                                      $"('{legendId}','{aisling.Serial}','{legend.Category}','{legend.Time}','{legend.Color}','{legend.Icon}','{legend.Value}')";
                var cmd = new SqlCommand(player, sConn);
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
                        client.SendMessage(0x03, "Issue saving legend mark. Contact GM");
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

        private static async void DeleteFromAislingDb(Aisling aisling, LegendItem legend)
        {
            if (legend.LegendId == 0) return;

            try
            {
                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await sConn.OpenAsync();
                const string cmd = "DELETE FROM ZolianPlayers.dbo.PlayersLegend WHERE LegendId = @LegendId";
                await sConn.ExecuteAsync(cmd, new { legend.LegendId });
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