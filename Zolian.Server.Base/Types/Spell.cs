using System.Collections.Concurrent;
using System.ComponentModel;
using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Network.Client;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Scripting;
using Darkages.Sprites;
using Darkages.Templates;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Types
{
    public class Spell
    {
        public int SpellId { get; init; }
        public byte Icon { get; set; }
        public bool InUse { get; set; }
        public byte Level { get; set; }
        public int Lines { get; set; }
        [Browsable(false)] public string Name => $"{Template.Name} (Lev:{Level}/{Template.MaxLevel})";
        public string SpellName { get; init; }
        public int CurrentCooldown { get; set; }
        public bool Ready => CurrentCooldown == 0;

        public ConcurrentDictionary<string, SpellScript> Scripts { get; set; }

        public byte Slot { get; set; }
        public SpellTemplate Template { get; set; }
        public int Casts { get; set; }

        public static void AttachScript(Spell spell)
        {
            spell.Scripts = ScriptManager.Load<SpellScript>(spell.Template.ScriptName, spell);
        }

        public static Spell Create(int slot, SpellTemplate spellTemplate)
        {
            var spellID = Generator.GenerateNumber();
            var obj = new Spell
            {
                Template = spellTemplate,
                SpellId = spellID,
                Level = 1,
                Slot = (byte)slot,
                Icon = spellTemplate.Icon,
                Lines = spellTemplate.BaseLines
            };

            return obj;
        }

        public static Task<bool> GiveTo(GameClient client, string args)
        {
            if (!client.Aisling.LoggedIn) return Task.FromResult(false);
            if (!ServerSetup.GlobalSpellTemplateCache.ContainsKey(args)) return Task.FromResult(false);

            var spellTemplate = ServerSetup.GlobalSpellTemplateCache[args];

            if (spellTemplate == null) return Task.FromResult(false);
            if (client.Aisling.SpellBook.Has(spellTemplate)) return Task.FromResult(false);
            var slot = client.Aisling.SpellBook.FindEmpty(spellTemplate.Pane == Pane.Spells ? 0 : 72);

            if (slot <= 0) return Task.FromResult(false);

            var spell = Create(slot, spellTemplate);
            {
                AttachScript(spell);
                {
                    client.Aisling.SpellBook.Set(spell);

                    client.Send(new ServerFormat17(spell));
                    client.Aisling.SendAnimation(22, client.Aisling, client.Aisling);
                }
            }

            try
            {
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();
                sConn.Open();
                var spellId = Generator.GenerateNumber();
                var spellNameReplaced = spell.Template.ScriptName.Replace("'", "''");
                var playerSpellBook = "INSERT INTO ZolianPlayers.dbo.PlayersSpellBook (SpellId, Serial, Level, Slot, SpellName, Casts, CurrentCooldown) VALUES " +
                                      $"('{spellId}','{client.Aisling.Serial}','{0}','{spell.Slot}','{spellNameReplaced}','{0}','{0}')";
                var cmd = new SqlCommand(playerSpellBook, sConn);
                cmd.CommandTimeout = 5;
                adapter.InsertCommand = cmd;
                adapter.InsertCommand.ExecuteNonQuery();
                sConn.Close();
            }
            catch (SqlException e)
            {
                if (e.Message.Contains("PK__Players"))
                {
                    if (!e.Message.Contains(client.Aisling.Serial.ToString())) return Task.FromResult(false);
                    client.SendMessage(0x03, "Issue saving spell on issue. Contact GM");
                    Crashes.TrackError(e);
                    return Task.FromResult(false);
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

            return Task.FromResult(true);
        }

        public static Task<bool> GiveTo(Aisling aisling, string spellName, int level = 100)
        {
            if (!aisling.LoggedIn) return Task.FromResult(false);
            if (!ServerSetup.GlobalSpellTemplateCache.ContainsKey(spellName)) return Task.FromResult(false);

            var spellTemplate = ServerSetup.GlobalSpellTemplateCache[spellName];

            if (spellTemplate == null) return Task.FromResult(false);
            if (aisling.SpellBook.Has(spellTemplate)) return Task.FromResult(false);

            var slot = aisling.SpellBook.FindEmpty(spellTemplate.Pane == Pane.Spells ? 0 : 72);

            if (slot <= 0) return Task.FromResult(false);

            var spell = Create(slot, spellTemplate);
            {
                AttachScript(spell);
                {
                    aisling.SpellBook.Set(spell);

                    aisling.Show(Scope.Self, new ServerFormat17(spell));
                    aisling.SendAnimation(22, aisling, aisling);
                }
            }

            try
            {
                using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();
                sConn.Open();
                var spellId = Generator.GenerateNumber();
                var spellNameReplaced = spell.Template.ScriptName.Replace("'", "''");
                var playerSpellBook = "INSERT INTO ZolianPlayers.dbo.PlayersSpellBook (SpellId, Serial, Level, Slot, SpellName, Casts, CurrentCooldown) VALUES " +
                                      $"('{spellId}','{aisling.Serial}','{0}','{spell.Slot}','{spellNameReplaced}','{0}','{0}')";
                var cmd = new SqlCommand(playerSpellBook, sConn);
                cmd.CommandTimeout = 5;
                adapter.InsertCommand = cmd;
                adapter.InsertCommand.ExecuteNonQuery();
                sConn.Close();
            }
            catch (SqlException e)
            {
                if (e.Message.Contains("PK__Players"))
                {
                    if (!e.Message.Contains(aisling.Serial.ToString())) return Task.FromResult(false);
                    aisling.Client.SendMessage(0x03, "Issue saving spell on issue. Contact GM");
                    Crashes.TrackError(e);
                    return Task.FromResult(false);
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

            return Task.FromResult(true);
        }

        public bool CanUse()
        {
            return Ready;
        }

        internal static Sprite SpellReflect(Sprite enemy, Sprite damageDealingSprite)
        {
            if (enemy == null) return null;
            if (!enemy.SpellReflect) return enemy;

            var reflect = Generator.RandNumGen100();

            if (reflect > 60) return enemy;
            (_, enemy) = (enemy, damageDealingSprite);

            return enemy;
        }
    }
}