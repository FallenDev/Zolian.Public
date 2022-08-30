using System.Text;

using Darkages.Enums;
using Darkages.Sprites;

namespace Darkages.Network.Formats.Models.ServerFormats
{
    public class ServerFormat39 : NetworkFormat
    {
        /// <summary>
        /// Self Profile
        /// </summary>
        /// <param name="aisling"></param>
        public ServerFormat39(Aisling aisling) : this() => Aisling = aisling;

        private ServerFormat39()
        {
            Encrypted = true;
            Command = 0x39;
        }

        private Aisling Aisling { get; }

        public override void Serialize(NetworkPacketReader reader) { }

        public override void Serialize(NetworkPacketWriter packet)
        {
            if (Aisling.Abyss) return;

            packet.Write(Aisling.PlayerNation.NationId);
            packet.WriteStringA(Aisling.Clan);

            packet.Write((byte) 0x07);
            packet.Write((byte) 0x0);
            packet.Write((byte) 0x0);
            packet.Write((byte) 0x0);
            packet.Write((byte) 0x0);
            packet.Write((byte) 0x0);
            packet.Write((byte) 0x0);
            packet.Write((byte) 0x1);

            var isGrouped = Aisling.GroupParty?.PartyMembers != null && Aisling.GroupParty != null && Aisling.GroupParty.PartyMembers.Count(i => i.Serial != Aisling.Serial) > 0;
            var groupedCount = Aisling.GroupParty?.PartyMembers?.Count.ToString();

            if (!isGrouped)
            {
                packet.WriteStringA("Solo Hunting");
            }
            else
            {
                var sb = new StringBuilder("그룹구성원\n");
                foreach (var player in Aisling.GroupParty?.PartyMembers!)
                    sb.Append($"{(string.Equals(player.Username, Aisling.GroupParty?.LeaderName, StringComparison.CurrentCultureIgnoreCase) ? "*" : " ")} {player.Username}\n");

                sb.Append($"총 {groupedCount}명");
                packet.WriteStringA(sb.ToString());
            }

            packet.Write((byte) Aisling.PartyStatus);
            packet.Write((byte) 0x00);
            packet.Write((byte) Aisling.Path);
            packet.Write(Aisling.PlayerNation.NationId);
            packet.Write((byte) 0x01);
            packet.WriteStringA(Convert.ToString(Aisling.Stage != ClassStage.Class ? Aisling.Stage.ToString() : Aisling.Path.ToString()));
            packet.WriteStringA(Aisling.Clan);

            var legendSubjects = from subject in Aisling.LegendBook.LegendMarks
                group subject by subject
                into g
                let count = g.Count()
                orderby count descending
                select new
                {
                    Value = Aisling.LegendBook.LegendMarks.Find(i => i.Value == g.Key.Value),
                    Count = Aisling.LegendBook.LegendMarks.Count(i => i.Value == g.Key.Value)
                };

            var legendList = legendSubjects.ToList();
            var exactCount = legendList.Distinct().Count();
            packet.Write((byte) exactCount);

            foreach (var obj in legendList.Distinct())
            {
                var legend = obj.Value;
                packet.Write(legend!.Icon);
                packet.Write((byte)LegendColorConverter.ColorToInt(legend.Color));
                packet.WriteStringA(legend.Category);
                packet.WriteStringA($"{legend.Value} {(obj.Count > 1 ? "(" + obj.Count + ")" : "")}");
            }

            packet.Write((byte) 0x00);
            packet.Write((ushort) Aisling.Display);
            packet.Write((byte) 0x02);
            packet.Write((uint) 0x00);
            packet.Write((byte) 0x00);
        }
    }
}