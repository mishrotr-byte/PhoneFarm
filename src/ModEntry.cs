using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace PhoneFarm
{
    public class ModConfig
    {
        public string OpenPhoneKey { get; set; } = "U";
        public bool GodModeEnabled { get; set; } = false;
        public string GodModePassword { get; set; } = "farm123";
        public bool AllowToolUpgrade { get; set; } = true;
        public bool AllowSkillMax { get; set; } = true;
        public bool AllowMoneyGive { get; set; } = true;
        public int MaxResourceAmount { get; set; } = 999;
        public int DailyLimit { get; set; } = 5;
        public bool AllowRareItems { get; set; } = false;
        public bool DisableAchievementsInGodMode { get; set; } = true;
        public int AIPoints { get; set; } = 0;
        public int AILevel { get; set; } = 1;
        public int BankBalance { get; set; } = 0;
        public double BankInterestRate { get; set; } = 0.05;
        public int LoanAmount { get; set; } = 0;
        public double LoanInterestRate { get; set; } = 0.1;
        public bool EnableAIApp { get; set; } = true;
        public bool EnableResourceApp { get; set; } = true;
        public bool EnableUpgradeApp { get; set; } = true;
        public bool EnableShopApp { get; set; } = true;
        public bool EnableBankApp { get; set; } = true;
        public bool EnableStatsApp { get; set; } = true;
        public bool EnableChallengesApp { get; set; } = true;
        public bool SandboxMode { get; set; } = false;
        public int PhoneLevel { get; set; } = 1;
        public int TotalResourcesGiven { get; set; } = 0;
        public int TotalMoneyEarned { get; set; } = 0;
        public bool GodModeUsed { get; set; } = false;
        public int DailyUsesLeft { get; set; } = 5;
    }

    public class Challenge
    {
        public string Description { get; set; }
        public string Type { get; set; }
        public int Target { get; set; }
        public int RewardMoney { get; set; }
        public bool Completed { get; set; }
    }

    public class ModEntry : Mod
    {
        private ModConfig Config;
        private SButton PhoneKey;
        private readonly List<Challenge> ActiveChallenges = new();
        private readonly List<string> UsageLog = new();
        private bool GodModeUnlocked = false;
        private readonly Random Rng = new();
        private readonly Dictionary<string, int> PendingDeliveries = new();

        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            if (!Enum.TryParse(Config.OpenPhoneKey, true, out PhoneKey))
                PhoneKey = SButton.U;

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;

            Monitor.Log($"PhoneFarm loaded! Press {PhoneKey} to open.", LogLevel.Info);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || e.Button != PhoneKey) return;
            ShowMainMenu();
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            Config.DailyUsesLeft = Config.DailyLimit;

            if (Config.BankBalance > 0)
            {
                int interest = (int)(Config.BankBalance * Config.BankInterestRate);
                Config.BankBalance += interest;
                LogAction($"Bank interest +{interest}g");
            }
            if (Config.LoanAmount > 0)
            {
                int li = (int)(Config.LoanAmount * Config.LoanInterestRate);
                Config.LoanAmount += li;
                LogAction($"Loan interest +{li}g");
            }

            DeliverPendingItems();
            GenerateDailyChallenge();
            SaveConfig();
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            SaveConfig();
        }

        /*=== MAIN MENU ===*/

        private void ShowMainMenu()
        {
            var o = new List<Response>();
            if (Config.EnableAIApp)         o.Add(R("ai",   "AI Assistant"));
            if (Config.EnableResourceApp)   o.Add(R("res",  "Resources"));
            if (Config.EnableUpgradeApp)    o.Add(R("upg",  "Upgrades"));
            if (Config.EnableShopApp)       o.Add(R("shop", "Online Shop"));
            if (Config.EnableBankApp)       o.Add(R("bank", "Bank"));
            if (Config.EnableStatsApp)      o.Add(R("stat", "Statistics"));
            if (Config.EnableChallengesApp) o.Add(R("chal", "Challenges"));
            o.Add(R("set",   "Settings"));
            o.Add(R("close", "Close"));
            Ask($"SmartFarm Phone (Lv.{Config.PhoneLevel})", o, HandleMain);
        }

        private void HandleMain(Farmer f, string k)
        {
            switch (k)
            {
                case "ai":   ShowAI(); break;
                case "res":  ShowResources(); break;
                case "upg":  ShowUpgrades(); break;
                case "shop": ShowShop(); break;
                case "bank": ShowBank(); break;
                case "stat": ShowStats(); break;
                case "chal": ShowChallengesMenu(); break;
                case "set":  ShowSettings(); break;
            }
        }

        /*=== AI ===*/

        private void ShowAI()
        {
            Ask($"AI (Lv.{Config.AILevel} | {Config.AIPoints}pts)", new List<Response>
            {
                R("season","Season Analysis"), R("profit","Profit Analysis"),
                R("crops","Crop Advice"),      R("mine","Mine Advice"),
                R("npc","NPC Info"),            R("bday","Birthdays"),
                R("farm","Farm Analysis"),      R("forecast","Forecast"),
                R("lvl","Level Up AI (100pts)"),R("back","Back")
            }, HandleAI);
        }

        private void HandleAI(Farmer f, string k)
        {
            switch (k)
            {
                case "season":   AISeason(); break;
                case "profit":   AIProfit(); break;
                case "crops":    AICrops(); break;
                case "mine":     AIMine(); break;
                case "npc":      AINpc(); break;
                case "bday":     AIBirthdays(); break;
                case "farm":     AIFarm(); break;
                case "forecast": AIForecast(); break;
                case "lvl":      AILevelUp(); break;
                case "back":     ShowMainMenu(); break;
            }
        }

        private void AISeason()
        {
            int left = 28 - Game1.dayOfMonth;
            string m = Game1.currentSeason switch
            {
                "spring" => $"Spring day {Game1.dayOfMonth}, {left} left.\n" +
                    (left > 12 ? "Plant cauliflower!" : "Fast crops only!"),
                "summer" => $"Summer day {Game1.dayOfMonth}, {left} left.\nBest: blueberry, starfruit.",
                "fall"   => $"Fall day {Game1.dayOfMonth}, {left} left.\nBest: cranberry, pumpkin.",
                "winter" => "Winter. No outdoor crops.\nFocus on mining/fishing.",
                _ => "Unknown season."
            };
            if (Config.AILevel >= 2) m += $"\nGold: {P().Money}g";
            if (Config.AILevel >= 3) m += "\nTip: diversify crops for stability.";
            Msg(m); EarnAI(5);
        }

        private void AIProfit()
        {
            if (Config.AILevel < 2) { Msg("Need AI Lv.2+"); return; }
            var p = P();
            string m = $"Finance Report:\nCurrent: {p.Money}g\n" +
                $"Total earned: {p.totalMoneyEarned.Value}g\n" +
                $"Bank: {Config.BankBalance}g\nDebt: {Config.LoanAmount}g";
            if (Config.AILevel >= 4)
            {
                double days = Math.Max(1, (Game1.year - 1) * 112 + SeasonIdx() * 28 + Game1.dayOfMonth);
                m += $"\nAvg income/day: {p.totalMoneyEarned.Value / days:F0}g";
            }
            Msg(m); EarnAI(10);
        }

        private void AICrops()
        {
            int left = 28 - Game1.dayOfMonth;
            var crops = Game1.currentSeason switch
            {
                "spring" => new[] { ("Potato", 6), ("Cauliflower", 12), ("Strawberry", 8) },
                "summer" => new[] { ("Blueberry", 13), ("Melon", 12), ("Red Cabbage", 9) },
                "fall"   => new[] { ("Cranberry", 7), ("Pumpkin", 13), ("Grape", 10) },
                _ => new[] { ("No crops in winter", 0) }
            };
            string m = $"Crops ({left} days left):\n";
            foreach (var (n, d) in crops)
                m += $"{(left >= d ? "[OK]" : "[X]")} {n}: {d} days\n";
            Msg(m); EarnAI(5);
        }

        private void AIMine()
        {
            double luck = Game1.player.DailyLuck;
            string ls = luck > 0.07 ? "Excellent!" : luck > 0 ? "Good" :
                luck > -0.07 ? "Neutral" : "Bad!";
            string m = $"Mine — Luck: {ls} ({luck:+0.00;-0.00})\n";
            m += luck > 0.04 ? "Go mining — good loot today!" : "Better stay on the farm.";
            if (Config.AILevel >= 3) m += $"\nDeepest level: {P().deepestMineLevel}";
            Msg(m); EarnAI(5);
        }

        private void AINpc()
        {
            var npcs = Utility.getAllCharacters().Where(c => c.IsVillager).Take(8).ToList();
            if (!npcs.Any()) { Msg("No NPCs found."); return; }
            var opts = npcs.Select(n => R(n.Name, n.displayName ?? n.Name)).ToList();
            opts.Add(R("back", "Back"));
            Ask("Choose NPC:", opts, (f, k) =>
            {
                if (k == "back") { ShowAI(); return; }
                var npc = Game1.getCharacterFromName(k);
                if (npc == null) { Msg("Not found."); return; }
                int h = P().getFriendshipHeartLevelForNPC(k);
                Msg($"{npc.displayName}\nHearts: {h}/10\n" +
                    $"Location: {npc.currentLocation?.Name ?? "?"}\n" +
                    $"Birthday: {npc.Birthday_Season} {npc.Birthday_Day}");
                EarnAI(3);
            });
        }

        private void AIBirthdays()
        {
            string s = Game1.currentSeason;
            int d = Game1.dayOfMonth;
            var bdays = Utility.getAllCharacters()
                .Where(c => c.IsVillager && c is NPC n &&
                    n.Birthday_Season == s && n.Birthday_Day >= d && n.Birthday_Day <= d + 7)
                .Cast<NPC>()
                .Select(n => $"{n.displayName} — {n.Birthday_Season} {n.Birthday_Day}")
                .ToList();
            Msg(bdays.Count > 0
                ? "Birthdays (7 days):\n" + string.Join("\n", bdays)
                : "No birthdays in 7 days.");
            EarnAI(3);
        }

        private void AIFarm()
        {
            var farm = Game1.getFarm();
            if (farm == null) { Msg("Farm unavailable."); return; }
            string m = $"Farm Analysis:\nObjects: {farm.Objects.Count}\n" +
                $"Terrain: {farm.terrainFeatures.Count}\n" +
                $"Animals: {farm.getAllFarmAnimals().Count}\n" +
                $"Buildings: {farm.buildings.Count}";
            if (Config.AILevel >= 5) m += "\nTip: Ancient Seeds + greenhouse = infinite profit.";
            Msg(m); EarnAI(5);
        }

        private void AIForecast()
        {
            if (Config.AILevel < 3) { Msg("Need AI Lv.3+"); return; }
            int left = 28 - Game1.dayOfMonth;
            string m = $"Forecast: {left} days left.\n";
            if (left <= 3) m += "WARNING: Season ending! Harvest NOW!\n";
            if (Config.AILevel >= 4) m += $"Luck: {P().DailyLuck:+0.00;-0.00}\n";
            if (Config.AILevel >= 5) m += "Strategy: kegs and preserves jars for x3 profit.";
            Msg(m); EarnAI(8);
        }

        private void AILevelUp()
        {
            if (Config.AILevel >= 5) { Msg("AI already max (5)."); return; }
            if (Config.AIPoints < 100) { Msg($"Need 100 pts ({Config.AIPoints} now)"); return; }
            Config.AIPoints -= 100;
            Config.AILevel++;
            SaveConfig();
            Msg($"AI leveled to {Config.AILevel}! Remaining: {Config.AIPoints}pts");
        }

        private void EarnAI(int n) { Config.AIPoints += n; SaveConfig(); }

        /*=== RESOURCES ===*/

        private void ShowResources()
        {
            if (Config.DailyUsesLeft <= 0 && !Config.SandboxMode)
            { Msg("Daily limit reached!"); return; }

            var o = new List<Response>
            {
                R("wood","Wood x50"),    R("stone","Stone x50"),
                R("coal","Coal x20"),    R("seeds","Season Seeds x30"),
                R("food","Food x10"),    R("metal","Metals")
            };
            if (Config.AllowRareItems) o.Add(R("rare", "Rare Items"));
            o.Add(R("custom", "By Item ID"));
            o.Add(R("back", "Back"));
            Ask($"Resources ({Config.DailyUsesLeft} left)", o, HandleRes);
        }

        private void HandleRes(Farmer f, string k)
        {
            switch (k)
            {
                case "wood":  Give("(O)388", 50, "Wood"); break;
                case "stone": Give("(O)390", 50, "Stone"); break;
                case "coal":  Give("(O)382", 20, "Coal"); break;
                case "seeds": GiveSeeds(); break;
                case "food":  Give("(O)196", 10, "Salad"); break;
                case "metal": ShowMetals(); break;
                case "rare":  ShowRare(); break;
                case "custom":ShowCustom(); break;
                case "back":  ShowMainMenu(); break;
            }
        }

        private void Give(string id, int amt, string name)
        {
            amt = Math.Min(amt, Config.MaxResourceAmount);
            P().addItemByMenuIfNecessary(ItemRegistry.Create(id, amt));
            Config.DailyUsesLeft--;
            Config.TotalResourcesGiven += amt;
            LogAction($"Given {name} x{amt}");
            SaveConfig();
            Msg($"Given: {name} x{amt}");
        }

        private void GiveSeeds()
        {
            string id = Game1.currentSeason switch
            {
                "spring" => "(O)472", "summer" => "(O)487",
                "fall" => "(O)490", _ => "(O)472"
            };
            Give(id, 30, "Season Seeds");
        }

        private void ShowMetals()
        {
            Ask("Metals:", new List<Response>
            {
                R("cu","Copper x20"), R("fe","Iron x20"),
                R("au","Gold x20"),   R("ir","Iridium x10"),
                R("back","Back")
            }, (f, k) => {
                switch (k)
                {
                    case "cu": Give("(O)378", 20, "Copper Ore"); break;
                    case "fe": Give("(O)380", 20, "Iron Ore"); break;
                    case "au": Give("(O)384", 20, "Gold Ore"); break;
                    case "ir": Give("(O)386", 10, "Iridium Ore"); break;
                    case "back": ShowResources(); break;
                }
            });
        }

        private void ShowRare()
        {
            Ask("Rare:", new List<Response>
            {
                R("dia","Diamond x5"),          R("pri","Prismatic Shard x1"),
                R("anc","Ancient Seeds x5"),     R("star","Stardrop x1"),
                R("back","Back")
            }, (f, k) => {
                switch (k)
                {
                    case "dia":  Give("(O)72", 5, "Diamond"); break;
                    case "pri":  Give("(O)74", 1, "Prismatic Shard"); break;
                    case "anc":  Give("(O)499", 5, "Ancient Seeds"); break;
                    case "star": Give("(O)434", 1, "Stardrop"); break;
                    case "back": ShowResources(); break;
                }
            });
        }

        private void ShowCustom()
        {
            Ask("Item by ID:", new List<Response>
            {
                R("72","72 Diamond"),     R("74","74 Prismatic"),
                R("388","388 Wood"),      R("390","390 Stone"),
                R("337","337 Iridium Bar"), R("back","Back")
            }, (f, k) => {
                if (k == "back") { ShowResources(); return; }
                Give($"(O){k}", 10, $"Item#{k}");
            });
        }

        /*=== UPGRADES ===*/

        private void ShowUpgrades()
        {
            var o = new List<Response>();
            if (Config.AllowToolUpgrade) o.Add(R("tools", "Upgrade Tools"));
            if (Config.AllowSkillMax)    o.Add(R("skills","Max All Skills"));
            o.Add(R("energy", "Restore Energy"));
            o.Add(R("hp",     "Restore Health"));
            if (Config.AllowMoneyGive)   o.Add(R("money", "Add Gold"));
            if (Config.GodModeEnabled)   o.Add(R("god",   "GOD MODE"));
            o.Add(R("back", "Back"));
            Ask("Upgrades", o, HandleUpg);
        }

        private void HandleUpg(Farmer f, string k)
        {
            switch (k)
            {
                case "tools":  ShowToolLevel(); break;
                case "skills": MaxSkills(); break;
                case "energy": P().Stamina = P().MaxStamina; Msg("Energy restored!"); break;
                case "hp":     P().health = P().maxHealth; Msg("Health restored!"); break;
                case "money":  ShowMoneyAdd(); break;
                case "god":    TryGodMode(); break;
                case "back":   ShowMainMenu(); break;
            }
        }

        private void ShowToolLevel()
        {
            Ask("Tool level:", new List<Response>
            {
                R("1","Copper"), R("2","Steel"), R("3","Gold"), R("4","Iridium"),
                R("back","Back")
            }, (f, k) => {
                if (k == "back") { ShowUpgrades(); return; }
                int lv = int.Parse(k);
                foreach (var item in P().Items)
                    if (item is Tool t && t is not MeleeWeapon)
                        t.UpgradeLevel = lv;
                LogAction($"Tools -> lv{lv}");
                Msg($"All tools upgraded to level {lv}!");
            });
        }

        private void MaxSkills()
        {
            var p = P();
            p.FarmingLevel = p.FishingLevel = p.ForagingLevel = p.MiningLevel = p.CombatLevel = 10;
            for (int i = 0; i < 5; i++) p.experiencePoints[i] = 15000;
            LogAction("All skills maxed");
            Msg("All skills set to 10!");
        }

        private void ShowMoneyAdd()
        {
            Ask("How much?", new List<Response>
            {
                R("1000","1,000g"),     R("10000","10,000g"),
                R("100000","100,000g"), R("1000000","1,000,000g"),
                R("back","Back")
            }, (f, k) => {
                if (k == "back") { ShowUpgrades(); return; }
                int a = int.Parse(k);
                P().Money += a;
                Config.TotalMoneyEarned += a;
                LogAction($"+{a}g"); SaveConfig();
                Msg($"Added {a}g!");
            });
        }

        private void TryGodMode()
        {
            if (!Config.GodModeEnabled) { Msg("God Mode disabled in config."); return; }
            if (!GodModeUnlocked)
            {
                var opts = new List<Response>
                {
                    R(Config.GodModePassword, Config.GodModePassword),
                    R("wrong1", "letmein"),
                    R("wrong2", "password"),
                    R("back", "Back")
                };
                var shuffled = opts.Take(3).OrderBy(_ => Rng.Next()).Concat(opts.Skip(3)).ToList();
                Ask("Enter password:", shuffled, (f, k) => {
                    if (k == "back") { ShowUpgrades(); return; }
                    if (k == Config.GodModePassword) { GodModeUnlocked = true; ActivateGod(); }
                    else Msg("Wrong password!");
                });
            }
            else ActivateGod();
        }

        private void ActivateGod()
        {
            MaxSkills();
            foreach (var item in P().Items)
                if (item is Tool t && t is not MeleeWeapon)
                    t.UpgradeLevel = 4;
            P().Money += 1000000;
            Config.TotalMoneyEarned += 1000000;
            GiveSilent("(O)388", 999); GiveSilent("(O)390", 999);
            GiveSilent("(O)382", 999); GiveSilent("(O)386", 999);
            GiveSilent("(O)337", 999);
            P().Stamina = P().MaxStamina;
            P().health = P().maxHealth;
            Config.GodModeUsed = true;
            LogAction("GOD MODE ACTIVATED"); SaveConfig();
            Msg("GOD MODE ACTIVATED!\nSkills 10, Iridium tools,\n+1M gold, +999 resources, full energy.");
        }

        private void GiveSilent(string id, int amt)
        {
            amt = Math.Min(amt, Config.MaxResourceAmount);
            P().addItemByMenuIfNecessary(ItemRegistry.Create(id, amt));
            Config.TotalResourcesGiven += amt;
        }

        /*=== SHOP ===*/

        private void ShowShop()
        {
            Ask("Online Shop (next-day delivery)", new List<Response>
            {
                R("s","Seeds 500g"),     R("r","Resources 1000g"),
                R("x","Rare 5000g"),     R("d","Delivery Status"),
                R("back","Back")
            }, HandleShop);
        }

        private void HandleShop(Farmer f, string k)
        {
            switch (k)
            {
                case "s":
                    if (Buy(500))
                    {
                        string sid = Game1.currentSeason switch
                        { "spring"=>"(O)472","summer"=>"(O)487","fall"=>"(O)490",_=>"(O)472" };
                        AddDelivery(sid, 30);
                        Msg("Seeds purchased! Delivery tomorrow.");
                    }
                    break;
                case "r":
                    if (Buy(1000))
                    {
                        AddDelivery("(O)388",100); AddDelivery("(O)390",100);
                        AddDelivery("(O)382",50);
                        Msg("Resources purchased! Delivery tomorrow.");
                    }
                    break;
                case "x":
                    if (Buy(5000))
                    {
                        AddDelivery("(O)72",5); AddDelivery("(O)386",20);
                        Msg("Rare items purchased! Delivery tomorrow.");
                    }
                    break;
                case "d":
                    if (PendingDeliveries.Count == 0) Msg("No pending deliveries.");
                    else
                    {
                        string st = "Pending:\n";
                        foreach (var kv in PendingDeliveries) st += $"  {kv.Key}: x{kv.Value}\n";
                        Msg(st);
                    }
                    break;
                case "back": ShowMainMenu(); break;
            }
        }

        private bool Buy(int cost)
        {
            if (P().Money >= cost) { P().Money -= cost; return true; }
            Msg($"Not enough gold! Need {cost}g"); return false;
        }

        private void AddDelivery(string id, int amt)
        {
            PendingDeliveries[id] = PendingDeliveries.GetValueOrDefault(id) + amt;
        }

        private void DeliverPendingItems()
        {
            if (PendingDeliveries.Count == 0) return;
            foreach (var kv in PendingDeliveries)
            {
                P().addItemByMenuIfNecessary(ItemRegistry.Create(kv.Key, kv.Value));
                Config.TotalResourcesGiven += kv.Value;
            }
            int c = PendingDeliveries.Count;
            PendingDeliveries.Clear();
            LogAction($"Delivered {c} item types");
        }

        /*=== BANK ===*/

        private void ShowBank()
        {
            Ask($"Bank | Balance: {Config.BankBalance}g | Debt: {Config.LoanAmount}g",
                new List<Response>
                {
                    R("dep","Deposit"), R("wdr","Withdraw"),
                    R("loan","Take Loan"), R("pay","Repay Loan"),
                    R("bal","Balance"), R("back","Back")
                }, HandleBank);
        }

        private void HandleBank(Farmer f, string k)
        {
            switch (k)
            {
                case "dep":
                    AmountChoice("Deposit amount?", a => {
                        if (P().Money >= a)
                        {
                            P().Money -= a; Config.BankBalance += a; SaveConfig();
                            Msg($"Deposited {a}g. Balance: {Config.BankBalance}g");
                        }
                        else Msg("Not enough gold!");
                    }); break;
                case "wdr":
                    AmountChoice("Withdraw amount?", a => {
                        if (Config.BankBalance >= a)
                        {
                            Config.BankBalance -= a; P().Money += a; SaveConfig();
                            Msg($"Withdrawn {a}g. Remaining: {Config.BankBalance}g");
                        }
                        else Msg("Not enough in account!");
                    }); break;
                case "loan":
                    AmountChoice("Loan amount?", a => {
                        Config.LoanAmount += a; P().Money += a; SaveConfig();
                        Msg($"Loan: {a}g. Total debt: {Config.LoanAmount}g");
                    }); break;
                case "pay":
                    AmountChoice("Repay amount?", a => {
                        a = Math.Min(a, Config.LoanAmount);
                        if (P().Money >= a)
                        {
                            P().Money -= a; Config.LoanAmount -= a; SaveConfig();
                            Msg($"Repaid {a}g. Debt: {Config.LoanAmount}g");
                        }
                        else Msg("Not enough gold!");
                    }); break;
                case "bal":
                    Msg($"Balance: {Config.BankBalance}g ({Config.BankInterestRate*100}%/day)\n" +
                        $"Debt: {Config.LoanAmount}g ({Config.LoanInterestRate*100}%/day)");
                    break;
                case "back": ShowMainMenu(); break;
            }
        }

        private void AmountChoice(string title, Action<int> cb)
        {
            Ask(title, new List<Response>
            {
                R("500","500g"), R("1000","1,000g"), R("5000","5,000g"),
                R("10000","10,000g"), R("50000","50,000g"), R("back","Back")
            }, (f, k) => {
                if (k == "back") { ShowBank(); return; }
                cb(int.Parse(k));
            });
        }

        /*=== STATS ===*/

        private void ShowStats()
        {
            var p = P();
            Msg($"PhoneFarm Statistics\n\n" +
                $"Total earned: {p.totalMoneyEarned.Value}g\n" +
                $"Current gold: {p.Money}g\n" +
                $"Resources given: {Config.TotalResourcesGiven}\n" +
                $"Money added by mod: {Config.TotalMoneyEarned}g\n" +
                $"AI: Lv.{Config.AILevel} ({Config.AIPoints}pts)\n" +
                $"Phone: Lv.{Config.PhoneLevel}\n" +
                $"Bank: 
