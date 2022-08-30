using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using Darkages.Database;
using Darkages.Interfaces;
using Darkages.Models;
using Darkages.Network.Server;
using Darkages.Scripting;
using Darkages.Sprites;
using Darkages.Systems;
using Darkages.Systems.CLI;
using Darkages.Templates;
using Darkages.Types;

using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Darkages
{
    public class ServerSetup : IServerContext
    {
        public static readonly object SyncLock = new();
        public static readonly ConcurrentDictionary<int, string> Redirects = new();
        private static List<Metafile> _globalMetaCache = new();

        // Map
        public static ConcurrentDictionary<int, WorldMapTemplate> GlobalWorldMapTemplateCache = new();
        public static ConcurrentDictionary<int, Area> GlobalMapCache = new();
        public static ConcurrentDictionary<int, WarpTemplate> GlobalWarpTemplateCache = new();

        // Player
        public static ConcurrentDictionary<string, SkillTemplate> GlobalSkillTemplateCache = new();
        public static ConcurrentDictionary<string, SpellTemplate> GlobalSpellTemplateCache = new();
        public static ConcurrentDictionary<string, ItemTemplate> GlobalItemTemplateCache = new();
        public static ConcurrentDictionary<string, NationTemplate> GlobalNationTemplateCache = new();
        public static ConcurrentDictionary<string, Buff> GlobalBuffCache = new();
        public static ConcurrentDictionary<string, Debuff> GlobalDeBuffCache = new();
        public static ConcurrentDictionary<string, List<Board>> GlobalBoardCache = new();
        public static readonly ConcurrentDictionary<int, Party> GlobalGroupCache = new();

        // Monster
        public static ConcurrentDictionary<string, MonsterTemplate> GlobalMonsterTemplateCache = new();
        public static readonly ConcurrentDictionary<string, MonsterScript> GlobalMonsterScriptCache = new();
        public static ConcurrentDictionary<int, Monster> GlobalMonsterCache = new();

        // NPC
        public static ConcurrentDictionary<string, MundaneTemplate> GlobalMundaneTemplateCache = new();
        public static readonly ConcurrentDictionary<string, MundaneScript> GlobalMundaneScriptCache = new();
        public static readonly ConcurrentDictionary<int, Mundane> GlobalMundaneCache = new();

        private static Board[] _huntingToL = new Board[1];
        private static Board[] _trashTalk = new Board[1];
        private static Board[] _arenaUpdates = new Board[1];
        public static Board[] PersonalBoards = new Board[3];
        private static Board[] _serverUpdates = new Board[1];
        private static ILogger<ServerSetup> _log;
        public static IOptions<ServerOptions> ServerOptions;
        public static bool Running;
        public static IServerConstants Config;
        public static GameServer Game;
        private static LoginServer _lobby;

        #region Properties

        public static CommandParser Parser { get; set; }
        public static string StoragePath { get; private set; }
        public static string KeyCode { get; private set; }
        public static string UnHack { get; private set; }
        public static IPAddress IpAddress { get; private set; }

        #endregion

        public ServerSetup(IOptions<ServerOptions> options)
        {
            ServerOptions = options;
            StoragePath = options.Value.Location;
            KeyCode = options.Value.KeyCode;
            UnHack = options.Value.UnHack;
        }

        public static void Logger(string logMessage, LogLevel logLevel = LogLevel.Information)
        {
            lock (SyncLock)
            {
                _log?.Log(logLevel, "{logMessage}", logMessage);
            }
        }

        public void InitFromConfig(string storagePath, string ipAddress)
        {
            IpAddress = IPAddress.Parse(ipAddress);
            StoragePath = storagePath;

            if (StoragePath != null && !Directory.Exists(StoragePath))
                Directory.CreateDirectory(StoragePath);
        }

        public void Start(IServerConstants config, ILogger<ServerSetup> logger)
        {
            Config = config;
            _log = logger;

            Commander.CompileCommands();

            Startup();
            CommandHandler();
        }

        public void Startup()
        {
            try
            {
                LoadAndCacheStorage();
                StartServers();
            }
            catch (Exception ex)
            {
                Logger(ex.Message, LogLevel.Error);
                Logger(ex.StackTrace, LogLevel.Error);
                Crashes.TrackError(ex);
            }
        }

        public void LoadAndCacheStorage(bool contentOnly = false)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;

            lock (SyncLock)
            {
                EmptyCacheCollectors();
                AreaStorage.CacheFromDatabase();
                StorageManager.NationBucket.CacheFromDatabase(new NationTemplate());
                StorageManager.SkillBucket.CacheFromDatabase(new SkillTemplate());
                StorageManager.SpellBucket.CacheFromDatabase(new SpellTemplate());
                StorageManager.ItemBucket.CacheFromDatabase(new ItemTemplate());
                StorageManager.MonsterBucket.CacheFromDatabase(new MonsterTemplate());
                StorageManager.MundaneBucket.CacheFromDatabase(new MundaneTemplate());
                StorageManager.WarpBucket.CacheFromDatabase(new WarpTemplate());

                LoadWorldMapTemplates();
                CacheCommunityAssets();

                if (contentOnly) return;

                BindTemplates();
                LoadMetaDatabase();
                LoadExtensions();
            }
        }

        public void EmptyCacheCollectors()
        {
            GlobalMapCache = new ConcurrentDictionary<int, Area>();
            _globalMetaCache = new List<Metafile>();
            GlobalItemTemplateCache = new ConcurrentDictionary<string, ItemTemplate>();
            GlobalNationTemplateCache = new ConcurrentDictionary<string, NationTemplate>();
            GlobalMonsterTemplateCache = new ConcurrentDictionary<string, MonsterTemplate>();
            GlobalMonsterCache = new ConcurrentDictionary<int, Monster>();
            GlobalMundaneTemplateCache = new ConcurrentDictionary<string, MundaneTemplate>();
            GlobalSkillTemplateCache = new ConcurrentDictionary<string, SkillTemplate>();
            GlobalSpellTemplateCache = new ConcurrentDictionary<string, SpellTemplate>();
            GlobalWarpTemplateCache = new ConcurrentDictionary<int, WarpTemplate>();
            GlobalWorldMapTemplateCache = new ConcurrentDictionary<int, WorldMapTemplate>();
            GlobalBuffCache = new ConcurrentDictionary<string, Buff>();
            GlobalDeBuffCache = new ConcurrentDictionary<string, Debuff>();
            GlobalBoardCache = new ConcurrentDictionary<string, List<Board>>();
        }

        #region Template Building

        public void LoadWorldMapTemplates()
        {
            StorageManager.WorldMapBucket.CacheFromStorage();
            Logger($"World Map Templates Loaded: {GlobalWorldMapTemplateCache.Count}");
        }

        public void BindTemplates()
        {
            foreach (var spell in GlobalSpellTemplateCache.Values)
                spell.Prerequisites?.AssociatedWith(spell);
            foreach (var skill in GlobalSkillTemplateCache.Values)
                skill.Prerequisites?.AssociatedWith(skill);
        }

        #endregion

        public void CacheCommunityAssets()
        {
            if (PersonalBoards == null) return;
            var dirs = Directory.GetDirectories(Path.Combine(StoragePath, "Community\\Boards"));
            var tmpBoards = new Dictionary<string, List<Board>>();

            foreach (var dir in dirs.Select(i => new DirectoryInfo(i)))
            {
                var boards = Board.CacheFromStorage(dir.FullName);

                if (boards == null) continue;

                if (dir.Name == "Personal")
                    if (boards.Find(i => i.Index == 0) == null)
                        boards.Add(new Board("Mail", 0, true));

                if (!tmpBoards.ContainsKey(dir.Name)) tmpBoards[dir.Name] = new List<Board>();

                tmpBoards[dir.Name].AddRange(boards);
            }

            PersonalBoards = tmpBoards["Personal"].OrderBy(i => i.Index).ToArray();
            _huntingToL = tmpBoards["Hunting"].OrderBy(i => i.Index).ToArray();
            _arenaUpdates = tmpBoards["Arena Updates"].OrderBy(i => i.Index).ToArray();
            _trashTalk = tmpBoards["Trash Talk"].OrderBy(i => i.Index).ToArray();
            _serverUpdates = tmpBoards["Server Updates"].OrderBy(i => i.Index).ToArray();

            foreach (var (key, value) in tmpBoards)
            {
                if (!GlobalBoardCache.ContainsKey(key)) GlobalBoardCache[key] = new List<Board>();

                GlobalBoardCache[key].AddRange(value);
            }
        }

        public void LoadMetaDatabase()
        {
            try
            {
                var files = MetafileManager.GetMetaFiles();
                if (files.Any()) _globalMetaCache.AddRange(files);
            }
            catch (Exception ex)
            {
                Logger(ex.Message, LogLevel.Error);
                Logger(ex.StackTrace, LogLevel.Error);
                Crashes.TrackError(ex);
            }
        }

        public void LoadExtensions()
        {
            CacheBuffs();
            Logger($"Building Buff Cache: {GlobalBuffCache.Count} Loaded.");
            CacheDebuffs();
            Logger($"Building Debuff Cache: {GlobalDeBuffCache.Count} Loaded.");
        }

        public void CacheBuffs()
        {
            var listOfBuffs = from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                               from assemblyType in domainAssembly.GetTypes()
                               where typeof(Buff).IsAssignableFrom(assemblyType)
                               select assemblyType;

            foreach (var buff in listOfBuffs)
            {
                if (GlobalBuffCache != null)
                    GlobalBuffCache[buff.Name] = Activator.CreateInstance(buff) as Buff;
            }
        }

        public void CacheDebuffs()
        {
            var listOfDebuffs = from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                                 from assemblyType in domainAssembly.GetTypes()
                                 where typeof(Debuff).IsAssignableFrom(assemblyType)
                                 select assemblyType;

            foreach (var debuff in listOfDebuffs)
            {
                if (GlobalDeBuffCache != null)
                    GlobalDeBuffCache[debuff.Name] = Activator.CreateInstance(debuff) as Debuff;
            }
        }

        public void StartServers()
        {
            try
            {
                Game = new GameServer(Config.ConnectionCapacity);
                Game.Start(Config.SERVER_PORT);
                _lobby = new LoginServer();
                _lobby.Start(Config.LOGIN_PORT);

                Console.ForegroundColor = ConsoleColor.Green;
                Logger("Server is now online.");
            }
            catch (SocketException ex)
            {
                Logger(ex.Message, LogLevel.Error);
                Logger(ex.StackTrace, LogLevel.Error);
                Crashes.TrackError(ex);
            }
        }

        public void CommandHandler()
        {
            Console.WriteLine("\n");
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("GM Commands");

            foreach (var command in Parser.Commands)
            {
                Logger(command.ShowHelp(), LogLevel.Warning);
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
        }

        public static void SaveCommunityAssets()
        {
            lock (SyncLock)
            {
                var tmp = new List<Board>(_arenaUpdates);
                var tmp1 = new List<Board>(_huntingToL);
                var tmp2 = new List<Board>(PersonalBoards);
                var tmp3 = new List<Board>(_serverUpdates);
                var tmp4 = new List<Board>(_trashTalk);

                foreach (var asset in tmp)
                {
                    asset.Save("Arena Updates");
                }

                foreach (var asset in tmp1)
                {
                    asset.Save("Hunting");
                }

                foreach (var asset in tmp2)
                {
                    asset.Save("Personal");
                }

                foreach (var asset in tmp3)
                {
                    asset.Save("Server Updates");
                }

                foreach (var asset in tmp4)
                {
                    asset.Save("Trash Talk");
                }
            }
        }

        public static void Shutdown()
        {
            lock (SyncLock)
            {
                DisposeGame();
            }
        }

        private static void DisposeGame()
        {
            Game?.Abort();
            _lobby?.Abort();

            Game = null;
            _lobby = null;
            Running = false;
        }
    }
}