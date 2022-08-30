using Darkages.Enums;
using Darkages.Sprites;

namespace Darkages.Network.Formats.Models.ServerFormats
{
    public class ServerFormat33 : NetworkFormat
    {
        private ServerFormat33()
        {
            Encrypted = true;
            Command = 0x33;
        }

        /// <summary>
        /// Display Player
        /// </summary>
        /// <param name="aisling"></param>
        public ServerFormat33(Aisling aisling) : this() => Aisling = aisling;

        private Aisling Aisling { get; }

        public override void Serialize(NetworkPacketReader reader) { }

        public override void Serialize(NetworkPacketWriter writer)
        {
            if (Aisling.Abyss) return;

            writer.Write((ushort)Aisling.Pos.X);
            writer.Write((ushort)Aisling.Pos.Y);
            writer.Write(Aisling.Direction);
            writer.Write((uint)Aisling.Serial);

            if (Aisling.MonsterForm > 0)
            {
                writer.Write((byte)0xFF);
                writer.Write((byte)0xFF);
                writer.Write(Aisling.MonsterForm);
                writer.Write((byte)0x01);
                writer.Write((byte)0x3A);
                writer.Write(new byte[7]);
                writer.WriteStringA(Aisling.Username);
            }
            else
            {
                var displayFlag = Aisling.Gender == Gender.Male ? 0x10 : 0x20;

                if (Aisling.Dead)
                    displayFlag += 0x20;
                else if (Aisling.Invisible)
                    displayFlag += Aisling.Gender == Gender.Male ? 0x40 : 0x30;

                if (!Aisling.Invisible && !Aisling.Dead)
                {
                    switch (displayFlag)
                    {
                        //Hair Style
                        case 0x10 when Aisling.HelmetImg > 100 && !Aisling.Map.Flags.HasFlag(MapFlags.PlayerKill):
                            writer.Write((ushort)Aisling.HelmetImg);
                            break;
                        case 0x10:
                            writer.Write((ushort)Aisling.HairStyle);
                            break;
                        case 0x20 when Aisling.HelmetImg > 100 && !Aisling.Map.Flags.HasFlag(MapFlags.PlayerKill):
                            writer.Write((ushort)Aisling.HelmetImg);
                            break;
                        case 0x20:
                            writer.Write((ushort)Aisling.HairStyle);
                            break;
                    }
                }
                else
                {
                    writer.Write((ushort)0);
                }

                writer.Write((byte)(Aisling.Dead || Aisling.Invisible ? displayFlag : (byte)(Aisling.Display + Aisling.Pants)));

                if (!Aisling.Dead && !Aisling.Invisible)
                {
                    writer.Write((ushort)Aisling.ArmorImg);
                    writer.Write((byte)Aisling.BootsImg);
                    writer.Write((ushort)Aisling.ArmorImg);
                    writer.Write((byte)Aisling.ShieldImg);
                    writer.Write((byte)Aisling.WeaponImg);
                    writer.Write(Aisling.HairColor);
                    writer.Write(Aisling.BootColor);
                    writer.Write((ushort)Aisling.HeadAccessory1Img);
                    writer.Write(Aisling.Lantern);
                    writer.Write((byte)Aisling.HeadAccessory2Img);
                    writer.Write((byte)Aisling.HeadAccessory2Img);
                    writer.Write((byte)Aisling.Resting);
                    writer.Write((ushort)Aisling.OverCoatImg);
                    writer.Write(Aisling.OverCoatColor);
                }
                else
                {
                    writer.Write((ushort)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write(Aisling.HairColor);
                    writer.Write(Aisling.BootColor);
                    writer.Write((ushort)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)0);
                    writer.Write((byte)0);
                }
            }

            if (Aisling.Map is {Ready: true} && Aisling.LoggedIn)
            {
                if (Aisling.Map.Flags.HasFlag(MapFlags.PlayerKill))
                {
                    writer.Write((byte)NameDisplayStyle.RedAlwaysOn);
                }
                else
                {
                    writer.Write((byte)NameDisplayStyle.GreyHover);
                }
            }
            else
                writer.Write((byte)NameDisplayStyle.GreenHover);

            writer.WriteStringA(Aisling.Username ?? string.Empty);
        }
    }
}