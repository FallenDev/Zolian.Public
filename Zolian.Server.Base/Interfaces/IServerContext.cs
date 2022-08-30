using Microsoft.Extensions.Logging;

namespace Darkages.Interfaces
{
    public interface IServerContext
    {
        void InitFromConfig(string storagePath, string serverIp);
        void Start(IServerConstants config, ILogger<ServerSetup> logger);
        void Startup();
        void LoadAndCacheStorage(bool contentOnly);
        void EmptyCacheCollectors();
        void LoadWorldMapTemplates();
        void BindTemplates();
        void CacheCommunityAssets();
        void LoadMetaDatabase();
        void LoadExtensions();
        void CacheBuffs();
        void CacheDebuffs();
        void StartServers();
        void CommandHandler();
    }
}