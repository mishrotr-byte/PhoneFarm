using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Netcode
{
    public class NetIntDelta
    {
        public int Value;
    }
}

namespace StardewValley
{
    public class Game1
    {
        public static string currentSeason = "";
        public static int dayOfMonth;
        public static int year = 1;
        public static Farmer player;
        public static StardewValley.Menus.IClickableMenu activeClickableMenu;

        public static NPC getCharacterFromName(string n) => null;
        public static Farm getFarm() => null;
    }

    public class Item
    {
        public virtual string DisplayName { get; set; }
    }

    public class Object : Item
    {
        public Object() { }
        public Object(Vector2 v, string id, int stack = 1) { }
    }

    public class Farmer
    {
        public int Money;
        public Netcode.NetIntDelta totalMoneyEarned = new();
        public double DailyLuck;
        public float Stamina;
        public float MaxStamina = 270;
        public int health;
        public int maxHealth = 100;
        public int deepestMineLevel;
        public int FarmingLevel;
        public int FishingLevel;
        public int ForagingLevel;
        public int MiningLevel;
        public int CombatLevel;
        public int[] experiencePoints = new int[5];
        public IList<Item> Items { get; set; } = new List<Item>();

        public void addItemByMenuIfNecessary(Item i) { }
        public int getFriendshipHeartLevelForNPC(string n) => 0;
    }

    public class Character
    {
        public string Name;
        public string displayName;
        public GameLocation currentLocation;
        public virtual bool IsVillager => false;
    }

    public class NPC : Character
    {
        public string Birthday_Season;
        public int Birthday_Day;
        public override bool IsVillager => true;
    }

    public class GameLocation
    {
        public string Name;
        public delegate void afterQuestionBehavior(Farmer who, string key);

        public void createQuestionDialogue(
            string question,
            Response[] answers,
            afterQuestionBehavior callback) { }
    }

    public class Farm : GameLocation
    {
        public IDictionary<Vector2, Object> Objects =
            new Dictionary<Vector2, Object>();
        public IDictionary<Vector2, TerrainFeature> terrainFeatures =
            new Dictionary<Vector2, TerrainFeature>();
        public IList<Building> buildings = new List<Building>();

        public List<FarmAnimal> getAllFarmAnimals() => new();
    }

    public class TerrainFeature { }
    public class Building { }
    public class FarmAnimal { }

    public class Response
    {
        public Response(string key, string text) { }
    }

    public static class Utility
    {
        public static IEnumerable<NPC> getAllCharacters() => new List<NPC>();
    }

    public static class ItemRegistry
    {
        public static Item Create(string id, int amount = 1) => new Object();
        public static ItemMetadata GetData(string id) => null;
    }

    public class ItemMetadata
    {
        public string DisplayName;
    }
}

namespace StardewValley.Menus
{
    public interface IClickableMenu { }

    public class DialogueBox : IClickableMenu
    {
        public DialogueBox(string text) { }
    }
}

namespace StardewValley.Tools
{
    public class Tool : StardewValley.Item
    {
        public int UpgradeLevel;
    }

    public class MeleeWeapon : Tool { }
}
