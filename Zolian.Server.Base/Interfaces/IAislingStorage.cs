using System.Data;
using Darkages.Sprites;

namespace Darkages.Interfaces
{
    public interface IAislingStorage
    {
        Task<Aisling> LoadAisling(string name);
        /// <summary>
        /// Save method for password attempts & password change
        /// </summary>
        Task<bool> PasswordSave(Aisling obj);
        /// <summary>
        /// Save method for properties that change often
        /// </summary>
        void QuickSave(Aisling obj);
        /// <summary>
        /// Save method used to store properties that rarely change
        /// </summary>
        Task<bool> Save(Aisling obj);
        Task<bool> SaveSkills(Aisling obj);
        Task<bool> SaveSpells(Aisling obj);
        Task<bool> CheckIfPlayerExists(string name);
        Task<Aisling> CheckPassword(string name);
        void Create(Aisling obj);
        DataTable MappedDataTablePlayersSecurity(DataTable dataTable, Aisling obj);
        DataTable MappedDataTablePlayersQuickSave(DataTable dataTable, Aisling obj);
        DataTable MappedDataTablePlayers(DataTable dataTable, Aisling obj);
        DataTable MappedDataTablePlayersSkills(DataTable dataTable, Aisling obj);
        DataTable MappedDataTablePlayersSpells(DataTable dataTable, Aisling obj);
    }
}