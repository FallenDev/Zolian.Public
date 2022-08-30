using Darkages.Types;

namespace Darkages.Network.Formats.Models.ServerFormats
{
    public class ServerFormat6F : NetworkFormat
    {
        public string Name;
        public byte Type;

        /// <summary>
        /// Metadata Load
        /// </summary>
        public ServerFormat6F()
        {
            Encrypted = true;
            Command = 0x6F;
        }

        public override void Serialize(NetworkPacketReader reader) { }

        public override void Serialize(NetworkPacketWriter writer)
        {
            writer.Write(Type);

            if (Type == 0x00)
                if (Name != null)
                {
                    if (!Name.Contains("Class")) writer.Write(MetafileManager.GetMetaFile(Name));

                    var file = MetafileManager.GetMetaFile(Name);

                    switch (file.Name)
                    {
                        case "SClass1":
                            file.Name = "SClass1";
                            writer.Write(file);
                            break;
                        case "SClass2":
                            file.Name = "SClass2";
                            writer.Write(file);
                            break;
                        case "SClass3":
                            file.Name = "SClass3";
                            writer.Write(file);
                            break;
                        case "SClass4":
                            file.Name = "SClass4";
                            writer.Write(file);
                            break;
                        case "SClass5":
                            file.Name = "SClass5";
                            writer.Write(file);
                            break;
                        case "SClass6":
                            file.Name = "SClass6";
                            writer.Write(file);
                            break;
                        case "SClass7":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass7";
                            break;
                        case "SClass8":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass8";
                            break;
                        case "SClass9":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass9";
                            break;
                        case "SClass10":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass10";
                            break;
                        case "SClass11":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass11";
                            break;
                        case "SClass12":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass12";
                            break;
                        case "SClass13":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass13";
                            break;
                        case "SClass14":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass14";
                            break;
                        case "SClass15":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass15";
                            break;
                        case "SClass16":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass16";
                            break;
                        case "SClass17":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass17";
                            break;
                        case "SClass18":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass18";
                            break;
                        case "SClass19":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass19";
                            break;
                        case "SClass20":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass20";
                            break;
                        case "SClass21":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass21";
                            break;
                        case "SClass22":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass22";
                            break;
                        case "SClass23":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass23";
                            break;
                        case "SClass24":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass24";
                            break;
                        case "SClass25":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass25";
                            break;
                        case "SClass26":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass26";
                            break;
                        case "SClass27":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass27";
                            break;
                        case "SClass28":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass28";
                            break;
                        case "SClass29":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass29";
                            break;
                        case "SClass30":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass30";
                            break;
                        case "SClass31":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass31";
                            break;
                        case "SClass32":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass32";
                            break;
                        case "SClass33":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass33";
                            break;
                        case "SClass34":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass34";
                            break;
                        case "SClass35":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass35";
                            break;
                        case "SClass36":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass36";
                            break;
                        case "SClass37":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass37";
                            break;
                        case "SClass38":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass38";
                            break;
                        case "SClass39":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass39";
                            break;
                        case "SClass40":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass40";
                            break;
                        case "SClass41":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass41";
                            break;
                        case "SClass42":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass42";
                            break;
                        case "SClass43":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass43";
                            break;
                        case "SClass44":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass44";
                            break;
                        case "SClass45":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass45";
                            break;
                        case "SClass46":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass46";
                            break;
                        case "SClass47":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass47";
                            break;
                        case "SClass48":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass48";
                            break;
                        case "SClass49":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass49";
                            break;
                        case "SClass50":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass50";
                            break;
                        case "SClass51":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass51";
                            break;
                        case "SClass52":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass52";
                            break;
                        case "SClass53":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass53";
                            break;
                        case "SClass54":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass54";
                            break;
                        case "SClass55":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass55";
                            break;
                        case "SClass56":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass56";
                            break;
                        case "SClass57":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass57";
                            break;
                        case "SClass58":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass58";
                            break;
                        case "SClass59":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass59";
                            break;
                        case "SClass60":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass60";
                            break;
                        case "SClass61":
                            file.Name = "SClass1";
                            writer.Write(file);
                            file.Name = "SClass61";
                            break;
                        case "SClass62":
                            file.Name = "SClass2";
                            writer.Write(file);
                            file.Name = "SClass62";
                            break;
                        case "SClass63":
                            file.Name = "SClass3";
                            writer.Write(file);
                            file.Name = "SClass63";
                            break;
                        case "SClass64":
                            file.Name = "SClass4";
                            writer.Write(file);
                            file.Name = "SClass64";
                            break;
                        case "SClass65":
                            file.Name = "SClass5";
                            writer.Write(file);
                            file.Name = "SClass65";
                            break;
                        case "SClass66":
                            file.Name = "SClass6";
                            writer.Write(file);
                            file.Name = "SClass66";
                            break;
                    }
                }

            if (Type == 0x01)
                writer.Write(MetafileManager.GetMetaFiles());
        }
    }
}