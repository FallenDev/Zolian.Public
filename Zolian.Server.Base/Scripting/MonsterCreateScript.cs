using Darkages.Interfaces;
using Darkages.Sprites;
using Darkages.Templates;
using Darkages.Types;

namespace Darkages.Scripting
{
    public abstract class MonsterCreateScript : IScriptBase
    {
        public abstract Monster Create(MonsterTemplate template, Area map);
    }
}
