namespace Darkages.Network.Formats.Models.ServerFormats
{
    public class ServerFormat3B : NetworkFormat
    {
        private DateTime _ping;

        /// <summary>
        /// PING A
        /// </summary>
        public ServerFormat3B()
        {
            Encrypted = true;
            Command = 0x3B;
        }

        public override void Serialize(NetworkPacketReader reader) { }

        public override void Serialize(NetworkPacketWriter writer)
        {
            var time = DateTime.UtcNow;
            _ping = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");

            writer.Write((ushort)0x0001);
        }
    }
}