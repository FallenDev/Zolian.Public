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
    public class SkillBook : ObjectManager
    {
        private const int SkillLength = 35 * 3;

        public readonly ConcurrentDictionary<int, Skill> Skills = new();

        public SkillBook()
        {
            for (var i = 0; i < SkillLength; i++)
            {
                Skills[i + 1] = null;
            }
        }

        public int Length => Skills.Count;

        public int FindEmpty(int start = 0)
        {
            for (var i = start; i < Length; i++)
                if (Skills[i + 1] == null)
                    return i + 1;

            return -1;
        }

        public Skill[] GetSkills(Predicate<Skill> predicate)
        {
            return Skills.Values.Where(i => i != null && predicate(i)).ToArray();
        }

        public bool Has(Skill s)
        {
            return Skills.Where(i => i.Value != null).Select(i => i.Value.Template)
                .FirstOrDefault(i => i.Name.Equals(s.Template.Name)) != null;
        }

        public bool Has(SkillTemplate s)
        {
            return Skills.Where(i => i.Value?.Template != null).Select(i => i.Value.Template)
                .FirstOrDefault(i => i.Name.Equals(s.Name)) != null;
        }

        public Skill Remove(byte movingFrom, bool skillDelete = false)
        {
            if (!Skills.ContainsKey(movingFrom)) return null;
            var copy = Skills[movingFrom];
            if (skillDelete)
            {
                DeleteFromAislingDb(copy);
            }

            Skills[movingFrom] = null;
            return copy;
        }

        public void Set(Skill s, bool clone = false)
        {
            Skills[s.Slot] = s;
        }

        private static async void DeleteFromAislingDb(Skill skill)
        {
            try
            {
                var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await sConn.OpenAsync();
                const string cmd = "DELETE FROM ZolianPlayers.dbo.PlayersSkillBook WHERE SkillId = @SkillId";
                await sConn.ExecuteAsync(cmd, new { skill.SkillId });
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