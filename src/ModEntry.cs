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

        private void OnDayEnding(object sender, DayEndingEventArgs e) => SaveConfig();

        private void ShowMainMenu()
        {
            var o = new List<Response>();
            if (Config.EnableAIApp) o.Add(R("ai", "AI Assistant"));
            if (Config.EnableResourceApp) o.Add(R("res", "Resources"));
            if (Config.EnableUpgradeApp) o.Add(R("upg", "Upgrades"));
            if (Config.EnableShopApp) o.Add(R("shop", "Online Shop"));
            if (Config.EnableBankApp) o.Add(R("bank", "Bank"));
            if (Config.EnableStatsApp) o.Add(R("stat", "Statistics"));
            if (Config.EnableChallengesApp) o.Add(R("chal", "Challenges"));
            o.Add(R("set", "Settings"));
            o.Add(R("close", "Close"));
            Ask($"SmartFarm Phone (Lv.{Config.PhoneLevel})", o, HandleMain);
        }

        private void HandleMain(Farmer f, string k)
        {
            switch (k)
            {
                case "ai": ShowAI(); break;
                case "res": ShowResources(); break;
                case "upg": ShowUpgrades(); break;
                case "shop": ShowShop(); break;
                case "bank": ShowBank(); break;
                case "stat": ShowStats(); break;
                case "chal": ShowChallengesMenu(); break;
                case "set": ShowSettings(); break;
            }
        }

        private void ShowAI()
        {
            Ask($"AI (Lv.{Config.AILevel} | {Config.AIPoints}pts)", new List<Response>
            {
                R("season","Season"), R("profit","Profit"), R("crops","Crops"),
                R("mine","Mine"), R("npc","NPC"), R("bday","Birthdays"),
                R("farm","Farm"), R("forecast","Forecast"),
                R("lvl","Level Up (100pts)"), R("back","Back")
            }, HandleAI);
        }

        private void HandleAI(Farmer f, string k)
        {
            switch (k)
            {
                case "season": AISeason(); break;
                case "profit": AIProfit(); break;
                case "crops": AICrops(); break;
                case "mine": AIMine(); break;
                case "npc": AINpc(); break;
                case "bday": AIBirthdays(); break;
                case "farm": AIFarm(); break;
                case "forecast": AIForecast(); break;
                case "lvl": AILevelUp(); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private void AISeason()
        {
            int left = 28 - Game1.dayOfMonth;
            string m = Game1.currentSeason switch
            {
                "spring" => $"Spring day {Game1.dayOfMonth}, {left} left. " + (left > 12 ? "Plant cauliflower!" : "Fast crops only!"),
                "summer" => $"Summer day {Game1.dayOfMonth}, {left} left. Best: blueberry, starfruit.",
                "fall" => $"Fall day {Game1.dayOfMonth}, {left} left. Best: cranberry, pumpkin.",
                "winter" => "Winter. No outdoor crops. Focus on mining.",
                _ => "Unknown season."
            };
            if (Config.AILevel >= 2) m += $" Gold: {P().Money}g";
            Msg(m); EarnAI(5);
        }

        private void AIProfit()
        {
            if (Config.AILevel < 2) { Msg("Need AI Lv.2+"); return; }
            var p = P();
            string m = $"Finance: Current {p.Money}g, Total earned {p.totalMoneyEarned.Value}g, Bank {Config.BankBalance}g, Debt {Config.LoanAmount}g";
            if (Config.AILevel >= 4)
            {
                double days = Math.Max(1, (Game1.year - 1) * 112 + SeasonIdx() * 28 + Game1.dayOfMonth);
                m += $", Avg/day {p.totalMoneyEarned.Value / days:F0}g";
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
                "fall" => new[] { ("Cranberry", 7), ("Pumpkin", 13), ("Grape", 10) },
                _ => new[] { ("No crops in winter", 0) }
            };
            string m = $"Crops ({left} days left): ";
            foreach (var (n, d) in crops) m += $"{(left >= d ? "OK" : "X")} {n}({d}d) ";
            Msg(m); EarnAI(5);
        }

        private void AIMine()
        {
            double luck = Game1.player.DailyLuck;
            string ls = luck > 0.07 ? "Excellent" : luck > 0 ? "Good" : luck > -0.07 ? "Neutral" : "Bad";
            string m = $"Mine luck: {ls} ({luck:+0.00;-0.00}). ";
            m += luck > 0.04 ? "Go mining!" : "Better stay on farm.";
            if (Config.AILevel >= 3) m += $" Deepest: {P().deepestMineLevel}";
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
                Msg($"{npc.displayName} Hearts:{P().getFriendshipHeartLevelForNPC(k)}/10 Location:{npc.currentLocation?.Name ?? "?"} Birthday:{npc.Birthday_Season} {npc.Birthday_Day}");
                EarnAI(3);
            });
        }

        private void AIBirthdays()
        {
            var bdays = Utility.getAllCharacters()
                .Where(c => c.IsVillager && c is NPC n && n.Birthday_Season == Game1.currentSeason && n.Birthday_Day >= Game1.dayOfMonth && n.Birthday_Day <= Game1.dayOfMonth + 7)
                .Cast<NPC>().Select(n => $"{n.displayName} {n.Birthday_Season} {n.Birthday_Day}").ToList();
            Msg(bdays.Count > 0 ? "Birthdays: " + string.Join(", ", bdays) : "No birthdays in 7 days.");
            EarnAI(3);
        }

        private void AIFarm()
        {
            var farm = Game1.getFarm();
            if (farm == null) { Msg("Farm unavailable."); return; }
            Msg($"Farm: Objects {farm.Objects.Count}, Terrain {farm.terrainFeatures.Count}, Animals {farm.getAllFarmAnimals().Count}, Buildings {farm.buildings.Count}");
            EarnAI(5);
        }

        private void AIForecast()
        {
            if (Config.AILevel < 3) { Msg("Need AI Lv.3+"); return; }
            int left = 28 - Game1.dayOfMonth;
            string m = $"Forecast: {left} days left. ";
            if (left <= 3) m += "SEASON ENDING! ";
            if (Config.AILevel >= 4) m += $"Luck: {P().DailyLuck:+0.00;-0.00} ";
            Msg(m); EarnAI(8);
        }

        private void AILevelUp()
        {
            if (Config.AILevel >= 5) { Msg("AI max (5)."); return; }
            if (Config.AIPoints < 100) { Msg($"Need 100pts ({Config.AIPoints} now)"); return; }
            Config.AIPoints -= 100; Config.AILevel++; SaveConfig();
            Msg($"AI -> Lv.{Config.AILevel}! Remaining: {Config.AIPoints}pts");
        }

        private void EarnAI(int n) { Config.AIPoints += n; SaveConfig(); }

        private void ShowResources()
        {
            if (Config.DailyUsesLeft <= 0 && !Config.SandboxMode) { Msg("Daily limit reached!"); return; }
            var o = new List<Response>
            {
                R("wood","Wood x50"), R("stone","Stone x50"), R("coal","Coal x20"),
                R("seeds","Seeds x30"), R("food","Food x10"), R("metal","Metals")
            };
            if (Config.AllowRareItems) o.Add(R("rare", "Rare"));
            o.Add(R("custom", "By ID")); o.Add(R("back", "Back"));
            Ask($"Resources ({Config.DailyUsesLeft} left)", o, HandleRes);
        }

        private void HandleRes(Farmer f, string k)
        {
            switch (k)
            {
                case "wood": Give("(O)388", 50, "Wood"); break;
                case "stone": Give("(O)390", 50, "Stone"); break;
                case "coal": Give("(O)382", 20, "Coal"); break;
                case "seeds": GiveSeeds(); break;
                case "food": Give("(O)196", 10, "Salad"); break;
                case "metal": ShowMetals(); break;
                case "rare": ShowRare(); break;
                case "custom": ShowCustom(); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private void Give(string id, int amt, string name)
        {
            amt = Math.Min(amt, Config.MaxResourceAmount);
            P().addItemByMenuIfNecessary(ItemRegistry.Create(id, amt));
            Config.DailyUsesLeft--; Config.TotalResourcesGiven += amt;
            LogAction($"Given {name} x{amt}"); SaveConfig();
            Msg($"Given: {name} x{amt}");
        }

        private void GiveSeeds()
        {
            string id = Game1.currentSeason switch { "spring" => "(O)472", "summer" => "(O)487", "fall" => "(O)490", _ => "(O)472" };
            Give(id, 30, "Seeds");
        }

        private void ShowMetals()
        {
            Ask("Metals:", new List<Response> { R("cu","Copper x20"), R("fe","Iron x20"), R("au","Gold x20"), R("ir","Iridium x10"), R("back","Back") },
            (f, k) => { switch (k) { case "cu": Give("(O)378",20,"Copper"); break; case "fe": Give("(O)380",20,"Iron"); break; case "au": Give("(O)384",20,"Gold"); break; case "ir": Give("(O)386",10,"Iridium"); break; case "back": ShowResources(); break; } });
        }

        private void ShowRare()
        {
            Ask("Rare:", new List<Response> { R("dia","Diamond x5"), R("pri","Prismatic x1"), R("anc","Ancient Seeds x5"), R("star","Stardrop x1"), R("back","Back") },
            (f, k) => { switch (k) { case "dia": Give("(O)72",5,"Diamond"); break; case "pri": Give("(O)74",1,"Prismatic"); break; case "anc": Give("(O)499",5,"Ancient Seeds"); break; case "star": Give("(O)434",1,"Stardrop"); break; case "back": ShowResources(); break; } });
        }

        private void ShowCustom()
        {
            Ask("By ID:", new List<Response> { R("72","Diamond"), R("74","Prismatic"), R("388","Wood"), R("390","Stone"), R("337","Iridium Bar"), R("back","Back") },
            (f, k) => { if (k == "back") { ShowResources(); return; } Give($"(O){k}", 10, $"Item#{k}"); });
        }

        private void ShowUpgrades()
        {
            var o = new List<Response>();
            if (Config.AllowToolUpgrade) o.Add(R("tools", "Upgrade Tools"));
            if (Config.AllowSkillMax) o.Add(R("skills", "Max Skills"));
            o.Add(R("energy", "Restore Energy")); o.Add(R("hp", "Restore Health"));
            if (Config.AllowMoneyGive) o.Add(R("money", "Add Gold"));
            if (Config.GodModeEnabled) o.Add(R("god", "GOD MODE"));
            o.Add(R("back", "Back"));
            Ask("Upgrades", o, HandleUpg);
        }

        private void HandleUpg(Farmer f, string k)
        {
            switch (k)
            {
                case "tools": ShowToolLevel(); break;
                case "skills": MaxSkills(); break;
                case "energy": P().Stamina = P().MaxStamina; Msg("Energy restored!"); break;
                case "hp": P().health = P().maxHealth; Msg("Health restored!"); break;
                case "money": ShowMoneyAdd(); break;
                case "god": TryGodMode(); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private void ShowToolLevel()
        {
            Ask("Level:", new List<Response> { R("1","Copper"), R("2","Steel"), R("3","Gold"), R("4","Iridium"), R("back","Back") },
            (f, k) => { if (k == "back") { ShowUpgrades(); return; } int lv = int.Parse(k); foreach (var item in P().Items) if (item is Tool t && t is not MeleeWeapon) t.UpgradeLevel = lv; Msg($"Tools -> lv{lv}!"); });
        }

        private void MaxSkills()
        {
            var p = P();
            p.FarmingLevel = p.FishingLevel = p.ForagingLevel = p.MiningLevel = p.CombatLevel = 10;
            for (int i = 0; i < 5; i++) p.experiencePoints[i] = 15000;
            Msg("All skills = 10!");
        }

        private void ShowMoneyAdd()
        {
            Ask("Amount:", new List<Response> { R("1000","1K"), R("10000","10K"), R("100000","100K"), R("1000000","1M"), R("back","Back") },
            (f, k) => { if (k == "back") { ShowUpgrades(); return; } int a = int.Parse(k); P().Money += a; Config.TotalMoneyEarned += a; SaveConfig(); Msg($"+{a}g!"); });
        }

        private void TryGodMode()
        {
            if (!GodModeUnlocked)
            {
                var opts = new List<Response> { R(Config.GodModePassword, Config.GodModePassword), R("wrong1","letmein"), R("wrong2","password"), R("back","Back") };
                var shuffled = opts.Take(3).OrderBy(_ => Rng.Next()).Concat(opts.Skip(3)).ToList();
                Ask("Password:", shuffled, (f, k) => { if (k == "back") { ShowUpgrades(); return; } if (k == Config.GodModePassword) { GodModeUnlocked = true; ActivateGod(); } else Msg("Wrong!"); });
            }
            else ActivateGod();
        }

        private void ActivateGod()
        {
            MaxSkills();
            foreach (var item in P().Items) if (item is Tool t && t is not MeleeWeapon) t.UpgradeLevel = 4;
            P().Money += 1000000; Config.TotalMoneyEarned += 1000000;
            GiveSilent("(O)388", 999); GiveSilent("(O)390", 999); GiveSilent("(O)382", 999); GiveSilent("(O)386", 999); GiveSilent("(O)337", 999);
            P().Stamina = P().MaxStamina; P().health = P().maxHealth;
            Config.GodModeUsed = true; SaveConfig();
            Msg("GOD MODE! Skills 10, Iridium tools, +1M gold, +999 resources, full energy.");
        }

        private void GiveSilent(string id, int amt)
        {
            P().addItemByMenuIfNecessary(ItemRegistry.Create(id, Math.Min(amt, Config.MaxResourceAmount)));
            Config.TotalResourcesGiven += amt;
        }

        private void ShowShop()
        {
            Ask("Shop (next-day delivery)", new List<Response> { R("s","Seeds 500g"), R("r","Resources 1000g"), R("x","Rare 5000g"), R("d","Status"), R("back","Back") }, HandleShop);
        }

        private void HandleShop(Farmer f, string k)
        {
            switch (k)
            {
                case "s": if (Buy(500)) { AddDel(Game1.currentSeason switch { "spring"=>"(O)472","summer"=>"(O)487","fall"=>"(O)490",_=>"(O)472" }, 30); Msg("Seeds ordered!"); } break;
                case "r": if (Buy(1000)) { AddDel("(O)388",100); AddDel("(O)390",100); AddDel("(O)382",50); Msg("Resources ordered!"); } break;
                case "x": if (Buy(5000)) { AddDel("(O)72",5); AddDel("(O)386",20); Msg("Rare ordered!"); } break;
                case "d": Msg(PendingDeliveries.Count == 0 ? "No deliveries." : "Pending: " + string.Join(", ", PendingDeliveries.Select(kv => $"{kv.Key}x{kv.Value}"))); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private bool Buy(int c) { if (P().Money >= c) { P().Money -= c; return true; } Msg($"Need {c}g!"); return false; }
        private void AddDel(string id, int a) { PendingDeliveries[id] = PendingDeliveries.GetValueOrDefault(id) + a; }

        private void DeliverPendingItems()
        {
            if (PendingDeliveries.Count == 0) return;
            foreach (var kv in PendingDeliveries) { P().addItemByMenuIfNecessary(ItemRegistry.Create(kv.Key, kv.Value)); Config.TotalResourcesGiven += kv.Value; }
            PendingDeliveries.Clear();
        }

        private void ShowBank()
        {
            Ask($"Bank: {Config.BankBalance}g | Debt: {Config.LoanAmount}g", new List<Response>
            { R("dep","Deposit"), R("wdr","Withdraw"), R("loan","Loan"), R("pay","Repay"), R("bal","Balance"), R("back","Back") }, HandleBank);
        }

        private void HandleBank(Farmer f, string k)
        {
            switch (k)
            {
                case "dep": Amt("Deposit?", a => { if (P().Money >= a) { P().Money -= a; Config.BankBalance += a; SaveConfig(); Msg($"Deposited {a}g"); } else Msg("Not enough!"); }); break;
                case "wdr": Amt("Withdraw?", a => { if (Config.BankBalance >= a) { Config.BankBalance -= a; P().Money += a; SaveConfig(); Msg($"Withdrawn {a}g"); } else Msg("Not enough!"); }); break;
                case "loan": Amt("Loan?", a => { Config.LoanAmount += a; P().Money += a; SaveConfig(); Msg($"Loan {a}g, debt {Config.LoanAmount}g"); }); break;
                case "pay": Amt("Repay?", a => { a = Math.Min(a, Config.LoanAmount); if (P().Money >= a) { P().Money -= a; Config.LoanAmount -= a; SaveConfig(); Msg($"Repaid {a}g"); } else Msg("Not enough!"); }); break;
                case "bal": Msg($"Balance: {Config.BankBalance}g ({Config.BankInterestRate*100}%/day), Debt: {Config.LoanAmount}g ({Config.LoanInterestRate*100}%/day)"); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private void Amt(string t, Action<int> cb)
        {
            Ask(t, new List<Response> { R("500","500"), R("1000","1K"), R("5000","5K"), R("10000","10K"), R("50000","50K"), R("back","Back") },
            (f, k) => { if (k == "back") { ShowBank(); return; } cb(int.Parse(k)); });
        }

        private void ShowStats()
        {
            var p = P();
            Msg($"Stats: Earned {p.totalMoneyEarned.Value}g, Gold {p.Money}g, Resources given {Config.TotalResourcesGiven}, Mod money {Config.TotalMoneyEarned}g, AI Lv.{Config.AILevel} ({Config.AIPoints}pts), Phone Lv.{Config.PhoneLevel}, Bank {Config.BankBalance}g, Debt {Config.LoanAmount}g, God {(Config.GodModeUsed?"Yes":"No")}");
        }

        private void GenerateDailyChallenge()
        {
            if (ActiveChallenges.Count >= 3) return;
            var types = new[] { ("earn", "Earn {0}g", new[] { 1000, 5000, 10000 }), ("mine", "Mine level {0}", new[] { 40, 80, 120 }) };
            var (t, tmpl, tgts) = types[Rng.Next(types.Length)];
            int tgt = tgts[Rng.Next(tgts.Length)];
            ActiveChallenges.Add(new Challenge { Description = string.Format(tmpl, tgt), Type = t, Target = tgt, RewardMoney = tgt * (t == "earn" ? 1 : 100) });
        }

        private void ShowChallengesMenu()
        {
            foreach (var c in ActiveChallenges.Where(c => !c.Completed))
            { if (c.Type == "earn" && P().totalMoneyEarned.Value >= c.Target) c.Completed = true; if (c.Type == "mine" && P().deepestMineLevel >= c.Target) c.Completed = true; }
            if (!ActiveChallenges.Any()) { Msg("No challenges. New one tomorrow!"); return; }
            string m = "Challenges: ";
            foreach (var c in ActiveChallenges) m += $"[{(c.Completed?"V":" ")}] {c.Description} ({c.RewardMoney}g) ";
            int col = 0;
            foreach (var c in ActiveChallenges.Where(c => c.Completed).ToList()) { P().Money += c.RewardMoney; EarnAI(20); col += c.RewardMoney; ActiveChallenges.Remove(c); }
            if (col > 0) m += $" Collected {col}g!";
            Msg(m);
        }

        private void ShowSettings()
        {
            Ask("Settings", new List<Response>
            {
                R("god",$"God:{(Config.GodModeEnabled?"ON":"OFF")}"), R("rare",$"Rare:{(Config.AllowRareItems?"ON":"OFF")}"),
                R("sand",$"Sandbox:{(Config.SandboxMode?"ON":"OFF")}"), R("pup",$"Phone Lv{Config.PhoneLevel} ({5000*Config.PhoneLevel}g)"),
                R("reset","Reset Stats"), R("log","View Log"), R("back","Back")
            }, HandleSettings);
        }

        private void HandleSettings(Farmer f, string k)
        {
            switch (k)
            {
                case "god": Config.GodModeEnabled = !Config.GodModeEnabled; GodModeUnlocked = false; SaveConfig(); Msg($"God: {(Config.GodModeEnabled?"ON":"OFF")}"); break;
                case "rare": Config.AllowRareItems = !Config.AllowRareItems; SaveConfig(); Msg($"Rare: {(Config.AllowRareItems?"ON":"OFF")}"); break;
                case "sand": Config.SandboxMode = !Config.SandboxMode; SaveConfig(); Msg($"Sandbox: {(Config.SandboxMode?"ON":"OFF")}"); break;
                case "pup":
                    if (Config.PhoneLevel >= 5) { Msg("Max!"); break; }
                    int cost = 5000 * Config.PhoneLevel;
                    if (P().Money >= cost) { P().Money -= cost; Config.PhoneLevel++; Config.DailyLimit += 2; SaveConfig(); Msg($"Phone Lv.{Config.PhoneLevel}! Limit:{Config.DailyLimit}"); }
                    else Msg($"Need {cost}g!"); break;
                case "reset": Config.TotalResourcesGiven = 0; Config.TotalMoneyEarned = 0; Config.GodModeUsed = false; UsageLog.Clear(); SaveConfig(); Msg("Reset!"); break;
                case "log": Msg(UsageLog.Count > 0 ? string.Join(", ", UsageLog.TakeLast(10)) : "Empty log."); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private Farmer P() => Game1.player;
        private void Msg(string t) => Game1.activeClickableMenu = new DialogueBox(t);
        private Response R(string k, string t) => new(k, t);
        private void Ask(string q, List<Response> o, GameLocation.afterQuestionBehavior cb) => Game1.currentLocation.createQuestionDialogue(q, o.ToArray(), cb);
        private void LogAction(string t) { var e = $"[{Game1.currentSeason} {Game1.dayOfMonth}] {t}"; UsageLog.Add(e); Monitor.Log(e, LogLevel.Info); if (UsageLog.Count > 100) UsageLog.RemoveAt(0); }
        private void SaveConfig() => Helper.WriteConfig(Config);
        private int SeasonIdx() => Game1.currentSeason switch { "spring" => 0, "summer" => 1, "fall" => 2, "winter" => 3, _ => 0 };
    }
}
