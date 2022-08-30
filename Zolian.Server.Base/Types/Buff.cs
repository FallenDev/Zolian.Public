using System.Data;
using Dapper;

using Darkages.Common;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Infrastructure;
using Darkages.Interfaces;
using Darkages.Models;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Sprites;

using Microsoft.AppCenter.Crashes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Darkages.Types
{
    public class Buff
    {
        public Buff()
        {
            Timer = new GameServerTimer(TimeSpan.FromSeconds(1));
        }

        public ushort Animation { get; init; }
        public bool Cancelled { get; init; }
        public virtual byte Icon { get; init; }
        public virtual int Length { get; init; }
        public virtual string Name { get; init; }
        public int TimeLeft { get; set; }
        public GameServerTimer Timer { get; set; }
        public Buff BuffSpell { get; set; }

        public virtual void OnApplied(Sprite affected, Buff buff)
        {
            ObtainBuffName(affected, buff);
            BuffSpell.OnApplied(affected, buff);

            if (affected is not Aisling aisling) return;
            InsertBuff(aisling, buff);
        }

        public virtual void OnDurationUpdate(Sprite affected, Buff buff)
        {
            ObtainBuffName(affected, buff);
            BuffSpell.OnDurationUpdate(affected, buff);

            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public virtual void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }

        private void ObtainBuffName(Sprite affected, Buff buff)
        {
            if (affected is not Aisling) return;

            BuffSpell = buff.Name switch
            {
                "Dia Aite" => new buff_aite("Dia Aite", 3600, 17),
                "Aite" => new buff_aite("Aite", 600, 11),
                "Claw Fist" => new buff_clawfist(),
                "Ard Dion" => new buff_dion("Ard Dion", 120, 194),
                "Mor Dion" => new buff_dion("Mor Dion", 20, 53),
                "Dion" => new buff_dion("Dion", 8, 53),
                "Stone Skin" => new buff_StoneSkin(),
                "Iron Skin" => new buff_dion("Iron Skin", 16, 53),
                "Wings of Protection" => new buff_dion("Wings of Protection", 80, 194),
                "Perfect Defense" => new buff_PerfectDefense(),
                "Shadow Step" => new buff_hide(),
                "Asgall" => new buff_skill_reflect(),
                "Deireas Faileas" => new buff_spell_reflect(),
                "Spectral Shield" => new buff_SpectralShield(),
                "Defensive Stance" => new buff_DefenseUp(),
                "Adrenaline" => new buff_DexUp(),
                "Atlantean Weapon" => new buff_randWeaponElement(),
                "Elemental Bane" => new buff_ElementalBane(),
                _ => BuffSpell
            };
        }

        internal void Update(Sprite affected, TimeSpan elapsedTime)
        {
            if (Timer.Disabled) return;
            if (!Timer.Update(elapsedTime)) return;
            if (Length - Timer.Tick > 0)
                OnDurationUpdate(affected, this);
            else
            {
                OnEnded(affected, this);
                Timer.Tick = 0;
                return;
            }

            Timer.Tick++;
        }

        protected static async void InsertBuff(Aisling aisling, Buff buff)
        {
            var continueInsert = await CheckOnBuffAsync(aisling.Client, buff.Name);
            if (continueInsert) return;

            // Timer needed to be re-initiated here (do not refactor out)
            buff.Timer = new GameServerTimer(TimeSpan.FromSeconds(1));

            try
            {
                await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                var adapter = new SqlDataAdapter();
                await sConn.OpenAsync();
                var buffId = Generator.GenerateNumber();
                var buffNameReplaced = buff.Name.Replace("'", "''");
                var playerBuffs = "INSERT INTO ZolianPlayers.dbo.PlayersBuffs (BuffId, Serial, Name, TimeLeft) VALUES " +
                                    $"('{buffId}','{aisling.Serial}','{buffNameReplaced}','{buff.TimeLeft}')";
                var cmd = new SqlCommand(playerBuffs, sConn);
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
                        client.SendMessage(0x03, "Issue saving buff. Contact GM");
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

        protected static async void UpdateBuff(Aisling aisling)
        {
            var dataTable = new DataTable();
            dataTable = MappedDataTablePlayersBuffs(dataTable, aisling);
            var tableNumber = Generator.GenerateNumber();
            var table = $"TmpTable{tableNumber.ToString()}";

            try
            {
                await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await using var cmd = new SqlCommand("", sConn);
                await sConn.OpenAsync();
                cmd.CommandTimeout = 5;
                cmd.CommandText = $"CREATE TABLE {table}([Serial] INT,[Name] VARCHAR(30),[TimeLeft] INT)";
                await cmd.ExecuteNonQueryAsync();

                using var bulkCopy = new SqlBulkCopy(sConn);
                bulkCopy.BulkCopyTimeout = 5;
                bulkCopy.DestinationTableName = table;
                await bulkCopy.WriteToServerAsync(dataTable);
                bulkCopy.Close();

                cmd.CommandText =
                    "BEGIN TRAN; " +
                    "UPDATE P SET P.[Name] = T.[Name], P.[TimeLeft] = T.[TimeLeft] " +
                    $"FROM ZolianPlayers.dbo.PlayersBuffs AS P INNER JOIN {table} AS T ON P.[Serial] = T.[Serial] AND P.[Name] = T.[Name]; DROP TABLE {table}; " +
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

        public static async void DeleteBuff(Aisling aisling, Buff buff)
        {
            try
            {
                await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await sConn.OpenAsync();
                const string playerBuffs = "DELETE FROM ZolianPlayers.dbo.PlayersBuffs WHERE Serial = @Serial AND Name = @Name";
                await sConn.ExecuteAsync(playerBuffs, new
                {
                    aisling.Serial,
                    buff.Name
                });
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

        private static async Task<bool> CheckOnBuffAsync(IGameClient client, string name)
        {
            try
            {
                const string procedure = "[SelectBuffsCheck]";
                await using var sConn = new SqlConnection(AislingStorage.ConnectionString);
                await sConn.OpenAsync();
                var cmd = new SqlCommand(procedure, sConn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                cmd.CommandTimeout = 5;
                cmd.Parameters.Add("@Serial", SqlDbType.Int).Value = client.Aisling.Serial;
                cmd.Parameters.Add("@Name", SqlDbType.VarChar).Value = name;

                await using var reader = await cmd.ExecuteReaderAsync();

                while (reader.Read())
                {
                    var buffName = reader["Name"].ToString();
                    if (!string.Equals(buffName, name, StringComparison.CurrentCultureIgnoreCase)) continue;
                    return string.Equals(name, buffName, StringComparison.CurrentCultureIgnoreCase);
                }

                reader.Close();
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

            return false;
        }

        private static DataTable MappedDataTablePlayersBuffs(DataTable dataTable, Aisling obj)
        {
            dataTable.Clear();

            dataTable.Columns.Add("Serial");
            dataTable.Columns.Add("Name");
            dataTable.Columns.Add("TimeLeft");

            foreach (var buff in obj.Buffs.Values.Where(i => i is { Name: { } }))
            {
                var row = dataTable.NewRow();
                row["Serial"] = obj.Serial;
                row["Name"] = buff.Name;
                row["TimeLeft"] = buff.TimeLeft;
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }

    #region Armor
    public class buff_aite : Buff
    {
        public buff_aite() { }

        public buff_aite(string name, int length, byte icon)
        {
            Name = name;
            Length = length;
            Icon = icon;
        }

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is Aisling aisling)
                aisling
                    .Client
                    .SendMessage(0x02, "Aite has strengthened your resolve.");
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            if (affected is Aisling aisling)
                aisling
                    .Client
                    .SendMessage(0x02, "Your resolve returns to normal.");

            if (affected.Buffs.TryRemove(buff.Name, out _))
                (affected as Aisling)?.Client
                    .Send(new ServerFormat3A(Icon, byte.MinValue));

            if (affected is not Aisling) return;
            affected.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_SpectralShield : Buff
    {
        private static StatusOperator AcModifier => new(Operator.Add, 10);
        public override byte Icon => 149;
        public override int Length => 600;
        public override string Name => "Spectral Shield";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
                affected.BonusAc += AcModifier.Value;
            }

            if (affected is Aisling aisling)
            {
                aisling.Client.SendMessage(0x02, "Spectral Shield has strengthened your resolve.");
                aisling.Client.SendAnimation(262, aisling.Client.Aisling, aisling.Client.Aisling.Target ?? aisling.Client.Aisling);
                aisling.Show(Scope.NearbyAislings, new ServerFormat19(30));
                InsertBuff(aisling, buff);
                aisling.Client.SendStats(StatusFlags.StructD);
            }
            else
            {
                var nearby = affected.GetObjects<Aisling>(affected.Map, i => i.WithinRangeOf(affected));

                foreach (var near in nearby)
                {
                    near.Client.SendAnimation(262, affected, affected);
                    near.Show(Scope.NearbyAislings, new ServerFormat19(30));
                }
            }
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);
            affected.BonusAc -= AcModifier.Value;

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "Your resolve returns to normal.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.StructD);
        }
    }

    public class buff_DefenseUp : Buff
    {
        private static StatusOperator AcModifier => new(Operator.Add, 20);
        public override byte Icon => 0;
        public override int Length => 150;
        public override string Name => "Defensive Stance";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
                affected.BonusAc += AcModifier.Value;
            }

            if (affected is Aisling aisling)
            {
                aisling.Client.SendMessage(0x02, "You're now aware of your surroundings.");
                aisling.Client.SendAnimation(89, aisling.Client.Aisling, aisling.Client.Aisling.Target ?? aisling.Client.Aisling);
                aisling.Show(Scope.NearbyAislings, new ServerFormat19(83));
                InsertBuff(aisling, buff);
                aisling.Client.SendStats(StatusFlags.StructD);
            }
            else
            {
                var nearby = affected.GetObjects<Aisling>(affected.Map, i => i.WithinRangeOf(affected));

                foreach (var near in nearby)
                {
                    near.Client.SendAnimation(89, affected, affected);
                    near.Show(Scope.NearbyAislings, new ServerFormat19(83));
                }
            }
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);
            affected.BonusAc -= AcModifier.Value;

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "You've grown complacent.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.StructD);
        }
    }

    #endregion

    #region Enhancement

    public class buff_clawfist : Buff
    {
        public override byte Icon => 13;
        public override int Length => 30;
        public override string Name => "Claw Fist";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
                affected.ClawFistEmpowerment = true;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendMessage(0x02, "Your hands are empowered!")
                .SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);
            affected.ClawFistEmpowerment = false;

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "Your hands turn back to normal.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_dion : Buff
    {
        public override byte Icon => 53;
        public override int Length => 6;
        public override string Name => "Dion";

        public buff_dion() { }

        public buff_dion(string name, int length, byte icon)
        {
            Name = name;
            Length = length;
            Icon = icon;
        }

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendMessage(0x02, "You've become almost impervious.")
                .SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "You're no longer impervious.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_StoneSkin : Buff
    {
        public override byte Icon => 53;
        public override int Length => 15;
        public override string Name => "Stone Skin";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendMessage(0x02, "Your skin turns to stone!")
                .SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "Your skin turns back to normal.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_hide : Buff
    {
        public override byte Icon => 10;
        public override int Length => 10;
        public override string Name => "Hide";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is not Aisling aisling) return;
            var client = aisling.Client;
            if (client.Aisling == null || client.Aisling.Dead) return;

            client.Aisling.Invisible = true;
            client.SendMessage(0x02, "You blend in to the shadows.");
            aisling.Show(Scope.NearbyAislings, new ServerFormat19(43));
            client.UpdateDisplay();
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            affected.Show(Scope.NearbyAislings, new ServerFormat29((uint)affected.Serial, (uint)affected.Serial, 0, 0, 100));
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            var client = aisling.Client;

            client.Aisling.Invisible = false;

            aisling.Client.SendMessage(0x02, "You've emerged from the shadows.");
            aisling.Client.SendStats(StatusFlags.MultiStat);
            client.UpdateDisplay();
            DeleteBuff(aisling, buff);
        }
    }

    public class buff_DexUp : Buff
    {
        public override byte Icon => 148;
        public override int Length => 30;
        public override string Name => "Adrenaline";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
                affected.BonusDex += 15;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendMessage(0x02, "Adrenaline starts pumping!")
                .SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);
            affected.BonusDex -= 15;

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "You begin to come back down from your high.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_randWeaponElement : Buff
    {
        public override byte Icon => 110;
        public override int Length => 120;
        public override string Name => "Atlantean Weapon";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
                affected.SecondaryOffensiveElement = Generator.RandomEnumValue<ElementManager.Element>();
                if (affected.SecondaryOffensiveElement == ElementManager.Element.None)
                {
                    affected.SecondaryOffensiveElement = Generator.RandomEnumValue<ElementManager.Element>();
                }
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendMessage(0x02, $"Secondary Offensive element has changed {aisling.SecondaryOffensiveElement}")
                .SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);
            affected.SecondaryOffensiveElement = ElementManager.Element.None;

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "Element applied to your offense has worn off");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_ElementalBane : Buff
    {
        public override byte Icon => 17;
        public override int Length => 120;
        public override string Name => "Elemental Bane";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;

                affected.BonusFortitude += 100;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendMessage(0x02, $"Your resistance to all damage has been increased by 33%")
                .SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);
            affected.BonusFortitude -= 100;

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "You are no longer protected.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    #endregion

    #region Reflection
    public class buff_skill_reflect : Buff
    {
        public override byte Icon => 118;
        public override int Length => 12;
        public override string Name => "Asgall";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            aisling.Client.SendMessage(0x02, "Skills are no longer being reflected.");
            DeleteBuff(aisling, buff);
            aisling.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_spell_reflect : Buff
    {
        public override byte Icon => 54;
        public override int Length => 12;
        public override string Name => "Deireas Faileas";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            affected.Client.SendMessage(0x02, "Spells are no longer reflecting.");
            DeleteBuff(aisling, buff);
            affected.Client.SendStats(StatusFlags.MultiStat);
        }
    }

    public class buff_PerfectDefense : Buff
    {
        public override byte Icon => 178;
        public override int Length => 18;
        public override string Name => "Perfect Defense";

        public override void OnApplied(Sprite affected, Buff buff)
        {
            if (affected.Buffs.TryAdd(buff.Name, buff))
            {
                BuffSpell = buff;
                BuffSpell.TimeLeft = BuffSpell.Length;
            }

            if (affected is not Aisling aisling) return;
            aisling.Client.SendStats(StatusFlags.MultiStat);
            InsertBuff(aisling, buff);
        }

        public override void OnDurationUpdate(Sprite affected, Buff buff)
        {
            if (affected is not Aisling aisling) return;
            UpdateBuff(aisling);
        }

        public override void OnEnded(Sprite affected, Buff buff)
        {
            affected.Buffs.TryRemove(buff.Name, out _);

            if (affected is not Aisling aisling) return;
            aisling.Client.Send(new ServerFormat3A(Icon, byte.MinValue));
            affected.Client.SendMessage(0x02, "Spells are no longer being deflected.");
            DeleteBuff(aisling, buff);
            affected.Client.SendStats(StatusFlags.MultiStat);
        }
    }
    #endregion
}