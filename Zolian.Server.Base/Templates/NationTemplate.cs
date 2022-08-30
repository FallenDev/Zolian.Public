using Darkages.Sprites;
using Darkages.Types;

namespace Darkages.Templates
{
    public class NationTemplate : Template
    {
        public int AreaId { get; set; }
        public Position MapPosition { get; set; }
        public byte NationId { get; init; }

        public bool PastCurfew(Aisling aisling)
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            return (readyTime - aisling.LastLogged).TotalHours > ServerSetup.Config.NationReturnHours;
        }
    }
}