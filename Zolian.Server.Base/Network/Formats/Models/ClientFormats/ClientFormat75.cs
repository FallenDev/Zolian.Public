namespace Darkages.Network.Formats.Models.ClientFormats
{
    public class ClientFormat75 : NetworkFormat
    {
        /// <summary>
        /// Tick Synchronization
        /// </summary>
        public ClientFormat75()
        {
            Encrypted = true;
            Command = 0x75;
        }

        private long Tick { get; set; }

        public override void Serialize(NetworkPacketReader reader) => Tick = (long) (reader.ReadByte() >> 4) - 0x15;

        public override void Serialize(NetworkPacketWriter writer) { }
    }
}