using System.Numerics;

using Darkages.Object;
using Darkages.Sprites;

namespace Darkages.Types
{
    public class TileGrid : ObjectManager
    {
        private readonly Area _map;
        private readonly int _x;
        private readonly int _y;
        public readonly bool Impassable;
        public bool HasBeenUsed, IsViewable;
        public float FScore;
        public readonly float Cost;
        public float CurrentDist;
        public Vector2 Parent, Pos;

        public TileGrid(Area map, int x, int y)
        {
            _map = map;
            _x = x;
            _y = y;
            Impassable = false;
            HasBeenUsed = false;
            IsViewable = false;
            Cost = 1.0f;
        }

        public TileGrid(float cost)
        {
            Cost = cost;
            HasBeenUsed = false;
            IsViewable = false;
        }

        public TileGrid(Vector2 pos, float cost, bool filled, float fScore)
        {
            Cost = cost;
            Impassable = filled;
            HasBeenUsed = false;
            IsViewable = false;

            Pos = pos;
            FScore = fScore;
        }

        public void SetNode(Vector2 parent, float fScore, float currentDist)
        {
            Parent = parent;
            FScore = fScore;
            CurrentDist = currentDist;
        }

        public IEnumerable<Sprite> Sprites => GetObjects(_map, o => (int)o.Pos.X == _x && (int)o.Pos.Y == _y && o.Alive, Get.Monsters | Get.Mundanes | Get.Aislings);

        public IEnumerable<Sprite> PlayersToAttack => GetObjects(_map, o => (int)o.Pos.X == _x && (int)o.Pos.Y == _y && o.Alive, Get.MonsterDamage);

        public bool IsPassable(Sprite sprite, bool isAisling)
        {
            var length = 0;

            foreach (var obj in Sprites.Where(obj => obj != null))
            {
                if (obj.Serial == sprite.Serial)
                {
                    if (!isAisling) continue;

                    return true;
                }

                if (obj is Monster { Summoner: { } }) return true;

                if (obj.Pos == sprite.Pos) continue;

                if (obj is not Monster && obj is not Aisling && obj is not Mundane) continue;

                length++;
            }

            if (!isAisling)
                return length == 0;

            var updates = 0;

            foreach (var s in Sprites.Where(s => s != null).Where(s => s is not Aisling))
            {
                s.UpdateAddAndRemove();
                updates++;
            }

            if (updates > 0)
            {
                (sprite as Aisling)?.Client.UpdateDisplay();
                (sprite as Aisling)?.Client.ClientRefreshed();
            }

            return length == 0;
        }
    }
}