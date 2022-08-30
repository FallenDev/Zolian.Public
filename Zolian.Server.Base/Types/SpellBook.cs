using System.Collections.Concurrent;
using Dapper;

using Darkages.Database;
using Darkages.Object;
using Darkages.Templates;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Types
{
    public class SpellBook : ObjectManager
    {
        private const int SpellLength = 35 * 3;

        public readonly ConcurrentDictionary<int, Spell> Spells = new();

        public SpellBook()
        {
            for (var i = 0; i < SpellLength; i++)
            {
                Spells[i + 1] = null;
            }
        }

        public int Length => Spells.Count;

        public int FindEmpty(int start = 0)
        {
            for (var i = start; i < Length; i++)
                if (Spells[i + 1] == null)
                    return i + 1;

            return -1;
        }

        public Spell FindInSlot(int slot)
        {
            Spell ret = null;

            if (Spells.ContainsKey(slot))
                ret = Spells[slot];

            return ret is { Template: { } } ? ret : null;
        }

        public Spell[] GetSpells(Predicate<Spell> predicate)
        {
            return Spells.Values.Where(i => i != null && predicate(i)).ToArray();
        }

        public bool Has(Spell s)
        {
            return Spells.Where(i => i.Value != null).Select(i => i.Value.Template)
                .FirstOrDefault(i => i.Name.Equals(s.Template.Name)) != null;
        }

        public bool Has(SpellTemplate s)
        {
            return Spells.Where(i => i.Value?.Template != null).Select(i => i.Value.Template)
                .FirstOrDefault(i => i.Name.Equals(s.Name)) != null;
        }

        public Spell Remove(byte movingFrom, bool spellDelete = false)
        {
            if (!Spells.ContainsKey(movingFrom)) return null;
            var copy = Spells[movingFrom];
            if (spellDelete)
            {
                DeleteFromAislingDb(copy);
            }

            Spells[movingFrom] = null;
            return copy;
        }

        public void Set(Spell s, bool clone = false)
        {
            Spells[s.Slot] = s;
        }

        private static async void DeleteFromAislingDb(Spell spell)
        {
            try
            {
                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await sConn.OpenAsync();
                const string cmd = "DELETE FROM ZolianPlayers.dbo.PlayersSpellBook WHERE SpellId = @SpellId";
                await sConn.ExecuteAsync(cmd, new { spell.SpellId });
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