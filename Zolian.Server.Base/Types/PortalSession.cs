using Darkages.Interfaces;
using Darkages.Network.Client;
using Darkages.Network.Formats.Models.ServerFormats;

namespace Darkages.Types
{
    public class PortalSession : IPortalSession
    {
        public void TransitionToMap(GameClient client, int destinationMap = 0)
        {
            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            client.LastWarp = readyTime.AddMilliseconds(100);
            client.ResetLocation(client);

            if (destinationMap == 0)
            {
                client.Aisling.Abyss = true;
                ShowFieldMap(client);
                client.Send(new ServerFormat19(client, 42));
            }

            client.Aisling.Abyss = false;
        }

        public void ShowFieldMap(GameClient client)
        {
            if (client.MapOpen) return;

            if (ServerSetup.GlobalWorldMapTemplateCache.ContainsKey(client.Aisling.World))
            {
                var portal = ServerSetup.GlobalWorldMapTemplateCache[client.Aisling.World];

                if (portal.Portals.Any(ports => !ServerSetup.GlobalMapCache.ContainsKey(ports.Destination.AreaID)))
                {
                    ServerSetup.Logger("No Valid Configured World Map.");
                    return;
                }
            }

            client.Send(new ServerFormat2E(client.Aisling));
        }
    }
}