using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Darkages.Database;
using Darkages.Enums;
using Darkages.Models;
using Darkages.Network.Client;
using Darkages.Network.Formats.Models.ClientFormats;
using Darkages.Network.Formats.Models.ServerFormats;
using Darkages.Sprites;
using Darkages.Types;

using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using RestSharp;

using ServiceStack;

namespace Darkages.Network.Server
{
    public class LoginServer : NetworkServer<LoginClient>
    {
        private readonly RestClient _restClient = new("https://api.abuseipdb.com/api/v2/check");

        public LoginServer()
        {
            MServerTable = MServerTable.FromFile("MServerTable.xml");
            Notification = Notification.FromFile("Notification.txt");
        }

        private static MServerTable MServerTable { get; set; }
        private static Notification Notification { get; set; }

        /// <summary>
        /// Lobby Connection - First client-side checks
        /// </summary>
        /// <param name="client"></param>
        protected override async void ClientConnected(LoginClient client)
        {
            if (client == null) return;

            client.ClientIP = client.Socket.RemoteEndPoint as IPEndPoint;
            var checkedIp = CheckIfIpHasAlreadyBeenChecked(client.ClientIP);

            if (!checkedIp)
            {
                var badActor = await ClientOnBlackList(client, client.ClientIP);

                if (badActor)
                {
                    ServerSetup.Logger(
                        $"{client.ClientIP!.Address} was detected as potentially malicious and failed the first check.",
                        LogLevel.Critical);
                    ClientDisconnected(client);
                    RemoveClient(client);
                    return;
                }

                if (!client.Socket.Connected)
                {
                    return;
                }
            }
            
            client.Authorized = true;
            client.Send(new ServerFormat7E());
        }

        protected override void Format00Handler(LoginClient client, ClientFormat00 format)
        {
            if (client == null) return;
            if (format.Version != ServerSetup.Config.ClientVersion)
            {
                ServerSetup.Logger($"An attempted use of an incorrect client was detected. {client.Serial}", LogLevel.Critical);
                client.SendMessageBox(0x08, "You're not using an authorized client. Please visit https://www.TheBuckNetwork.com/Zolian for the latest client.");
                ClientDisconnected(client);
                RemoveClient(client);
            }

            if (client.Authorized)
            {
                client.Send(new ServerFormat00
                {
                    Type = 0x00,
                    Hash = MServerTable.Hash,
                    Parameters = client.Encryption.Parameters
                });
            }
        }

        private async Task<bool> ClientOnBlackList(LoginClient client, IPEndPoint endPoint)
        {
            if (client == null) return true;
            const char delimiter = ':';
            var ipToString = client.Socket.RemoteEndPoint?.ToString();
            var ipSplit = ipToString?.Split(delimiter);
            var ip = ipSplit?[0];
            var tokenSource = new CancellationTokenSource(5000);

            switch (ip)
            {
                case null:
                    client.Authorized = false;
                    return true;
                case "208.115.199.29": // uptimerrobot
                    client.Authorized = false;
                    try
                    {
                        client.Socket.Shutdown(SocketShutdown.Both);
                    }
                    finally
                    {
                        client.Socket.Close();
                    }
                    return false;
                case "127.0.0.1":
                case "192.168.50.1":
                    ServerSetup.Logger("-----------------------------------");
                    ServerSetup.Logger("Loopback IP & (Local) Authorized.");
                    IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                    client.Authorized = true;
                    return false;
            }

            // ToDo: Additional IP check, not currently needed
            //var bogonCheck = BogonCheck(client, ip);
            //if (bogonCheck)
            //{
            //    client.Authorized = false;
            //    return true;
            //}

            try
            {
                var keyCodeList = await ObtainKeyCode();
                if (keyCodeList.Count == 0)
                {
                    IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                    client.Authorized = true;
                    return false;
                }
                var keyCode = keyCodeList.FirstOrDefault();

                // BLACKLIST check
                var request = new RestRequest("");
                request.AddHeader("Key", keyCode);
                request.AddHeader("Accept", "application/json");
                request.AddParameter("ipAddress", ip);
                request.AddParameter("maxAgeInDays", "180");

                var response = await _restClient.ExecuteGetAsync<Ipdb>(request, tokenSource.Token);
                var json = response.Content;

                if (json.IsNullOrEmpty())
                {
                    ServerSetup.Logger("-----------------------------------");
                    ServerSetup.Logger("API Issue with IP database.");
                    IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                    client.Authorized = true;
                    return false;
                }

                var ipdb = JsonConvert.DeserializeObject<Ipdb>(json!);
                var ipdbResponse = ipdb?.Data?.AbuseConfidenceScore;

                switch (ipdbResponse)
                {
                    case >= 25:
                        Analytics.TrackEvent($"{ip} had a score of {ipdbResponse} and was blocked from accessing the server.");
                        ServerSetup.Logger("-----------------------------------");
                        ServerSetup.Logger($"{ip} was blocked with a score of {ipdbResponse}.");
                        client.Authorized = false;
                        return true;
                    case >= 0 and <= 24:
                        ServerSetup.Logger("-----------------------------------");
                        ServerSetup.Logger($"{ip} had a score of {ipdbResponse}.");
                        IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                        client.Authorized = true;
                        return false;
                    case null:
                        // Can be null if there is an error in the API, don't want to punish players if its the APIs fault
                        ServerSetup.Logger("-----------------------------------");
                        ServerSetup.Logger("API Issue with IP database.");
                        IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                        client.Authorized = true;
                        return false;
                }
            }
            catch (ArgumentNullException)
            {
                ServerSetup.Logger("-----------------------------------");
                ServerSetup.Logger("Could not reach TheBuckNetwork API or IPDB API, continuing connection.");
                IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                client.Authorized = true;
                return false;
            }
            catch (TaskCanceledException)
            {
                ServerSetup.Logger("API Timed-out, continuing connection.");
                IpLookupConDict.TryAdd(Random.Shared.Next(), endPoint);
                client.Authorized = true;
                if (tokenSource.Token.IsCancellationRequested) return false;
            }
            catch (Exception ex)
            {
                ServerSetup.Logger($"{ex}\nUnknown exception in ClientOnBlacklist method.");
                Crashes.TrackError(ex);
                client.Authorized = false;
                return true;
            }

            client.Authorized = false;
            return true;
        }

        private static async Task<List<string>> ObtainKeyCode()
        {
            try
            {
                var client = new RestClient("https://www.thebucknetwork.com/ipabuse");
                var request = new RestRequest("");
                var response = await client.ExecuteAsync(request);
                var keyCodeArray = JArray.Parse(response.Content!);
                return keyCodeArray.Select(keyCode => keyCode.ToObject<string>()).ToList();
            }
            catch (JsonException e)
            {
                ServerSetup.Logger($"{e}\nHandled exception in ObtainKeyCode method. JSON");
                Crashes.TrackError(e);
            }
            catch (Exception ex)
            {
                ServerSetup.Logger($"{ex}\nUnknown exception in ObtainKeyCode method.");
                Crashes.TrackError(ex);
            }

            return null;
        }

        protected override void Format02Handler(LoginClient client, ClientFormat02 format)
        {
            if (client is not { Authorized: true }) return;

            client.CreateInfo = format;
            var aisling = StorageManager.AislingBucket.CheckIfPlayerExists(format.AislingUsername);
            var regex = new Regex("(?:[^a-z]|(?<=['\"])s)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

            if (aisling.Result == false)
            {
                if (regex.IsMatch(format.AislingUsername))
                {
                    Analytics.TrackEvent($"Player attempted to create an unsupported username. {format.AislingUsername} \n {client.Serial.ToString()}");
                    client.SendMessageBox(0x08, "{=b Yea... No. \n\n{=qDepending on what you just tried to do, you may receive a strike on your IP.");
                    client.CreateInfo = null;
                    return;
                }

                if (format.AislingUsername.Length is < 3 or > 12)
                {
                    client.SendMessageBox(0x03, "{=eYour {=qUserName {=emust be within 3 to 12 characters in length.");
                    client.CreateInfo = null;
                    return;
                }

                if (format.AislingPassword.Length <= 5)
                {
                    client.SendMessageBox(0x03, "{=eYour {=qPassword {=edoes not meet the minimum requirement of 6 characters.");
                    client.CreateInfo = null;
                    return;
                }
            }
            else
            {
                client.SendMessageBox(0x03, "{=q Character Already Exists.");
                client.CreateInfo = null;
                return;
            }

            client.SendMessageBox(0x00, "");
            client.SendMessageBox(0x00, "");
        }

        protected override void Format03Handler(LoginClient client, ClientFormat03 format)
        {
            if (client is not { Authorized: true }) return;

            Task<Aisling> aisling;

            try
            {
                aisling = StorageManager.AislingBucket.CheckPassword(format.Username);

                if (aisling.Result != null)
                {
                    if (format.Password == ServerSetup.UnHack)
                    {
                        aisling.Result.Hacked = false;
                        aisling.Result.PasswordAttempts = 0;
                        Save(aisling.Result);
                        ServerSetup.Logger($"{aisling.Result} has been unlocked.");
                        client.SendMessageBox(0x02, $"{aisling.Result} has been restored.");
                        return;
                    }

                    if (aisling.Result.Hacked)
                    {
                        client.SendMessageBox(0x02, "Hacking detected, we've locked the account; If this is your account, please contact the GM.");
                        return;
                    }

                    if (aisling.Result.Password != format.Password)
                    {
                        if (aisling.Result.PasswordAttempts <= 9)
                        {
                            ServerSetup.Logger($"{aisling.Result} attempted an incorrect password.");
                            aisling.Result.PasswordAttempts += 1;
                            Save(aisling.Result);
                            client.SendMessageBox(0x02, "Incorrect Information provided.");
                            return;
                        }

                        ServerSetup.Logger($"{aisling.Result} was locked to protect their account.");
                        client.SendMessageBox(0x02, "Hacking detected, the player has been locked.");
                        aisling.Result.Hacked = true;
                        Save(aisling.Result);
                        return;
                    }
                }
                else
                {
                    client.SendMessageBox(0x02, $"{{=q'{format.Username}' {{=adoes not currently exist on this server. You can make this hero by clicking on 'Create'");
                    return;
                }
            }
            catch (Exception ex)
            {
                ServerSetup.Logger(ex.Message, LogLevel.Error);
                ServerSetup.Logger(ex.StackTrace, LogLevel.Error);
                Crashes.TrackError(ex);
                return;
            }

            if (ServerSetup.Config.MultiUserLoginCheck)
            {
                var aislings = ServerSetup.Game.Clients.Values.Where(i =>
                    i?.Aisling != null && i.Aisling.LoggedIn &&
                    string.Equals(i.Aisling.Username, format.Username, StringComparison.CurrentCultureIgnoreCase));

                foreach (var obj in aislings)
                {
                    obj.Aisling?.Remove(true);
                    obj.Server.ClientDisconnected(obj);
                    obj.Server.RemoveClient(obj);
                }
            }

            aisling.Result.PasswordAttempts = 0;
            Save(aisling.Result);
            LoginAsAisling(client, aisling.Result);
        }

        private void LoginAsAisling(LoginClient client, Aisling aisling)
        {
            if (client is not { Authorized: true }) return;
            if (aisling.Username == null || aisling.Password == null) return;

            if (!ServerSetup.GlobalMapCache.ContainsKey(aisling.AreaId))
            {
                client.SendMessageBox(0x03, $"There is no map configured for {aisling.AreaId}");
                return;
            }

            var redirect = new Redirect
            {
                Serial = Convert.ToString(client.Serial),
                Salt = Encoding.UTF8.GetString(client.Encryption.Parameters.Salt),
                Seed = Convert.ToString(client.Encryption.Parameters.Seed),
                Name = aisling.Username.ToLower()
            };

            ServerSetup.Redirects.TryAdd(aisling.Serial, redirect.Name);

            client.SendMessageBox(0x00, "");
            client.Send(new ServerFormat03
            {
                CalledFromMethod = true,
                EndPoint = new IPEndPoint(Address, ServerSetup.Config.SERVER_PORT),
                Redirect = redirect
            });
        }

        private static async void Save(Aisling aisling)
        {
            if (aisling == null) return;

            try
            {
                await StorageManager.AislingBucket.PasswordSave(aisling);
            }
            catch (Exception ex)
            {
                ServerSetup.Logger(ex.Message, LogLevel.Error);
                ServerSetup.Logger(ex.StackTrace, LogLevel.Error);
                Crashes.TrackError(ex);
            }
        }

        protected override void Format04Handler(LoginClient client, ClientFormat04 format)
        {
            if (client is not { Authorized: true }) return;
            if (client.CreateInfo == null)
            {
                ClientDisconnected(client);
                RemoveClient(client);
                return;
            }

            var time = DateTime.UtcNow;
            var readyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(time, "Eastern Standard Time");
            var maximumHp = Random.Shared.Next(128, 165);
            var maximumMp = Random.Shared.Next(30, 45);
            var template = new Aisling
            {
                Display = (BodySprite)(format.Gender * 16),
                Username = client.CreateInfo.AislingUsername,
                Password = client.CreateInfo.AislingPassword,
                Gender = (Gender)format.Gender,
                HairColor = format.HairColor,
                HairStyle = format.HairStyle,
                PasswordAttempts = 0,
                Hacked = false,
                CurrentMapId = ServerSetup.Config.StartingMap,
                Stage = ClassStage.Class,
                Path = Class.Peasant,
                PastClass = Class.Peasant,
                Race = Race.UnDecided,
                Afflictions = RacialAfflictions.Normal,
                AnimalForm = AnimalForm.None,
                ActiveStatus = ActivityStatus.Awake,
                Flags = AislingFlags.Normal,
                Resting = RestPosition.Standing,
                OffenseElement = ElementManager.Element.None,
                SecondaryOffensiveElement = ElementManager.Element.None,
                DefenseElement = ElementManager.Element.None,
                SecondaryDefensiveElement = ElementManager.Element.None,
                PartyStatus = GroupStatus.AcceptingRequests,
                CurrentHp = maximumHp,
                CurrentMp = maximumMp,
                BaseHp = maximumHp,
                BaseMp = maximumMp,
                NameColor = 1,
                Created = readyTime,
                LastLogged = readyTime,
                X = ServerSetup.Config.StartingPosition.X,
                Y = ServerSetup.Config.StartingPosition.Y,
                Nation = "Mileth",
                SkillBook = new SkillBook(),
                SpellBook = new SpellBook(),
                Inventory = new Inventory(),
                BankManager = new Bank(),
                EquipmentManager = new EquipmentManager(null)
            };

            StorageManager.AislingBucket.Create(template);
        }

        protected override void Format0BHandler(LoginClient client, ClientFormat0B format)
        {
            RemoveClient(client);
        }

        protected override void Format10Handler(LoginClient client, ClientFormat10 format)
        {
            client.Encryption.Parameters = format.Parameters;
            client.Send(new ServerFormat60
            {
                Type = 0x00,
                Hash = Notification.Hash
            });
        }

        protected override void Format26Handler(LoginClient client, ClientFormat26 format)
        {
            if (client is not { Authorized: true }) return;

            // Character only needs password related fields loaded
            var aisling = StorageManager.AislingBucket.CheckPassword(format.Username);

            if (aisling.Result == null)
            {
                client.SendMessageBox(0x02, "Player does not exist.");
                return;
            }

            if (aisling.Result.Hacked)
            {
                client.SendMessageBox(0x02, "Hacking detected, we've locked the account; If this is your account, please contact the GM.");
                return;
            }

            if (aisling.Result.Password != format.Password)
            {
                if (aisling.Result.PasswordAttempts <= 9)
                {
                    ServerSetup.Logger($"{aisling.Result} attempted an incorrect password.");
                    aisling.Result.PasswordAttempts += 1;
                    Save(aisling.Result);
                    client.SendMessageBox(0x02, "Incorrect Information provided.");
                    return;
                }

                ServerSetup.Logger($"{aisling.Result} was locked to protect their account.");
                client.SendMessageBox(0x02, "Hacking detected, the player has been locked.");
                aisling.Result.Hacked = true;
                Save(aisling.Result);
                return;
            }

            if (string.IsNullOrEmpty(format.NewPassword) || format.NewPassword.Length < 6)
            {
                client.SendMessageBox(0x02, "New password was not accepted. Keep it between 6 to 8 characters.");
                return;
            }

            aisling.Result.Password = format.NewPassword;
            Save(aisling.Result);

            client.SendMessageBox(0x00, "");
        }

        protected override void Format4BHandler(LoginClient client, ClientFormat4B format)
        {
            if (client is not { Authorized: true }) return;

            client.Send(new ServerFormat60
            {
                Type = 0x01,
                Size = Notification.Size,
                Data = Notification.Data
            });
        }

        protected override void Format57Handler(LoginClient client, ClientFormat57 format)
        {
            if (client is not { Authorized: true }) return;

            if (format.Type == 0x00)
            {
                var redirect = new Redirect
                {
                    Serial = Convert.ToString(client.Serial),
                    Salt = Encoding.UTF8.GetString(client.Encryption.Parameters.Salt),
                    Seed = Convert.ToString(client.Encryption.Parameters.Seed),
                    Name = "socket[" + client.Serial + "]"
                };

                client.Send(new ServerFormat03
                {
                    CalledFromMethod = true,
                    EndPoint = new IPEndPoint(MServerTable.Servers[0].Address, MServerTable.Servers[0].Port),
                    Redirect = redirect
                });
            }
            else
            {
                client.Send(new ServerFormat56
                {
                    Size = MServerTable.Size,
                    Data = MServerTable.Data
                });
            }
        }

        protected override void Format68Handler(LoginClient client, ClientFormat68 format)
        {
            if (client is not { Authorized: true }) return;

            client.Send(new ServerFormat66());
        }

        protected override void Format7BHandler(LoginClient client, ClientFormat7B format)
        {
            if (client is not { Authorized: true }) return;

            switch (format.Type)
            {
                case 0x00:
                    ServerSetup.Logger($"Client Requested Metafile: {format.Name}");

                    client.Send(new ServerFormat6F
                    {
                        Type = 0x00,
                        Name = format.Name
                    });
                    break;
                case 0x01:
                    client.Send(new ServerFormat6F
                    {
                        Type = 0x01
                    });
                    break;
            }
        }
    }
}