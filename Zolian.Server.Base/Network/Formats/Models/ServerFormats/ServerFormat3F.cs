namespace Darkages.Network.Formats.Models.ServerFormats
{
    public class ServerFormat3F : NetworkFormat
    {
        private readonly byte _pane;
        private readonly byte _slot;
        private readonly int _time;

        /// <summary>
        /// Cooldown
        /// </summary>
        public ServerFormat3F(byte pane, byte slot, int time) : this()
        {
            _pane = pane;
            _slot = slot;
            _time = time;
        }

        private ServerFormat3F()
        {
            Encrypted = true;
            Command = 0x3F;
        }

        public override void Serialize(NetworkPacketReader reader) { }

        public override void Serialize(NetworkPacketWriter writer)
        {
            writer.Write(_pane);
            writer.Write(_slot);
            writer.Write(_time);
        }
    }
}