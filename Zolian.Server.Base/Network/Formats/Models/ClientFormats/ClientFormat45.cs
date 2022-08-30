namespace Darkages.Network.Formats.Models.ClientFormats
{
    public class ClientFormat45 : NetworkFormat
    {
        /// <summary>
        /// Client Heartbeat
        /// </summary>
        public ClientFormat45()
        {
            Encrypted = true;
            Command = 0x45;
        }

        private DateTime Ping { get; set; }

        public override void Serialize(NetworkPacketReader reader)
        {
            var time = DateTime.UtcNow;
            Ping = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
        }

        public override void Serialize(NetworkPacketWriter writer) { }
    }
}