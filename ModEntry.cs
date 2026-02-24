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
    /// <summary>Configuration model for config.json.</summary>
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

    /// <summary>Represents an active challenge.</summary>
    public class Challenge
    {
        public string Description { get; set; }
        public string Type { get; set; }
        public int Target { get; set; }
        public int RewardMoney { get; set; }
        public bool Completed { get; set; }
    }

    /// <summary>Main entry point — single-file SMAPI mod.</summary>
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private SButton PhoneKey;
        private List<Challenge> ActiveChallenges = new();
        private readonly List<string> UsageLog = new();
        private bool GodModeUnlocked = false;
        private readonly Random Rng = new();

        // Pending shop deliveries: item ID → quantity
        private readonly Dictionary<string, int> PendingDeliveries = new();

        /*======================================================================
         *  SMAPI ENTRY
         *====================================================================*/
        public override void Entry(IModHelper helper)
        {
            Config = helper.ReadConfig<ModConfig>();

            if (!Enum.TryParse(Config.OpenPhoneKey, true, out PhoneKey))
                PhoneKey = SButton.U;

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;

            Monitor.Log("PhoneFarm loaded! Press " + PhoneKey + " to open.", LogLevel.Info);
        }

        /*======================================================================
         *  EVENTS
         *====================================================================*/
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (e.Button == PhoneKey)
                ShowMainMenu();
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Reset daily limit
            Config.DailyUsesLeft = Config.DailyLimit;

            // Bank interest
            if (Config.BankBalance > 0)
            {
                int interest = (int)(Config.BankBalance * Config.BankInterestRate);
                Config.BankBalance += interest;
                Log($"Bank interest: +{interest}g (balance {Config.BankBalance}g)");
            }

            // Loan interest
            if (Config.LoanAmount > 0)
            {
                int loanInterest = (int)(Config.LoanAmount * Config.LoanInterestRate);
                Config.LoanAmount += loanInterest;
                Log($"Loan interest: +{loanInterest}g (debt {Config.LoanAmount}g)");
            }

            // Deliver shop purchases
            DeliverPendingItems();

            // Generate daily challenge
            GenerateDailyChallenge();

            SaveConfig();
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            SaveConfig();
        }

        /*======================================================================
         *  MAIN MENU
         *====================================================================*/
        private void ShowMainMenu()
        {
            var options = new List<Response>();

            if (Config.EnableAIApp)
                options.Add(new Response("ai", "🤖 ИИ Помощник"));
            if (Config.EnableResourceApp)
                options.Add(new Response("resources", "📦 Выдача ресурсов"));
            if (Config.EnableUpgradeApp)
                options.Add(new Response("upgrade", "🛠 Прокачка"));
            if (Config.EnableShopApp)
                options.Add(new Response("shop", "🛒 Онлайн магазин"));
            if (Config.EnableBankApp)
                options.Add(new Response("bank", "🏦 Банк"));
            if (Config.EnableStatsApp)
                options.Add(new Response("stats", "📊 Статистика"));
            if (Config.EnableChallengesApp)
                options.Add(new Response("challenges", "🎯 Челленджи"));
            options.Add(new Response("settings", "⚙ Настройки"));
            options.Add(new Response("close", "❌ Закрыть"));

            Game1.currentLocation.createQuestionDialogue(
                "📱 SmartFarm Phone (Lv." + Config.PhoneLevel + ")",
                options.ToArray(),
                HandleMainMenu
            );
        }

        private void HandleMainMenu(Farmer who, string key)
        {
            switch (key)
            {
                case "ai": ShowAIMenu(); break;
                case "resources": ShowResourceMenu(); break;
                case "upgrade": ShowUpgradeMenu(); break;
                case "shop": ShowShopMenu(); break;
                case "bank": ShowBankMenu(); break;
                case "stats": ShowStats(); break;
                case "challenges": ShowChallenges(); break;
                case "settings": ShowSettings(); break;
                case "close": break;
            }
        }

        /*======================================================================
         *  1. AI ASSISTANT
         *====================================================================*/
        private void ShowAIMenu()
        {
            var options = new List<Response>
            {
                new Response("season", "🌿 Анализ сезона"),
                new Response("profit", "💰 Анализ прибыли"),
                new Response("crops", "🌾 Совет по культурам"),
                new Response("mine", "⛏ Совет по шахте"),
                new Response("npc", "👤 Подсказки по NPC"),
                new Response("birthday", "🎂 Дни рождения"),
                new Response("farm", "🏡 Анализ фермы"),
                new Response("forecast", "🔮 Прогноз"),
                new Response("levelup", "⬆ Повысить ИИ (100 AIPoints)"),
                new Response("back", "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                $"🤖 ИИ Помощник (Ур.{Config.AILevel}  AIPoints:{Config.AIPoints})",
                options.ToArray(),
                HandleAIMenu
            );
        }

        private void HandleAIMenu(Farmer who, string key)
        {
            switch (key)
            {
                case "season": AISeasonAnalysis(); break;
                case "profit": AIProfitAnalysis(); break;
                case "crops": AICropAdvice(); break;
                case "mine": AIMineAdvice(); break;
                case "npc": AINPCInfo(); break;
                case "birthday": AIBirthday(); break;
                case "farm": AIFarmAnalysis(); break;
                case "forecast": AIForecast(); break;
                case "levelup": AILevelUp(); break;
                case "back": ShowMainMenu(); break;
            }
        }

        private void AISeasonAnalysis()
        {
            string season = Game1.currentSeason;
            int day = Game1.dayOfMonth;
            int daysLeft = 28 - day;

            string advice = season switch
            {
                "spring" => $"Весна, день {day}. Осталось {daysLeft} дн.\n" +
                            "Лучшие культуры: Клубника, Картофель, Цветная капуста.\n" +
                            (day <= 10 ? "Ещё можно посадить долгоросы!" : "Сажайте быстрорастущие!"),
                "summer" => $"Лето, день {day}. Осталось {daysLeft} дн.\n" +
                            "Лучшие: Черника, Звёздочка, Красная капуста.\n" +
                            (day <= 7 ? "Идеальное время для посадки!" : "Фокус на повторные урожаи."),
                "fall"   => $"Осень, день {day}. Осталось {daysLeft} дн.\n" +
                            "Лучшие: Клюква, Тыква, Виноград.\n" +
                            (day <= 10 ? "Тыквы ещё успеют вырасти." : "Только быстрые культуры!"),
                "winter" => $"Зима, день {day}.\nКультуры не растут на открытом грунте.\n" +
                            "Займитесь шахтой, рыбалкой, теплицей.",
                _ => "Неизвестный сезон."
            };

            if (Config.AILevel >= 2)
                advice += $"\n\n💰 Ваше золото: {who().Money}g";
            if (Config.AILevel >= 3)
                advice += "\n📈 Совет: диверсифицируйте культуры для стабильности.";

            Msg(advice);
            GainAIPoints(5);
        }

        private void AIProfitAnalysis()
        {
            if (Config.AILevel < 2)
            {
                Msg("Нужен ИИ ур.2+ для финансового анализа.");
                return;
            }

            var farmer = who();
            int money = farmer.Money;
            int totalEarned = (int)farmer.totalMoneyEarned.Value;

            string msg = $"💰 Финансовый отчёт\n" +
                         $"Сейчас: {money}g\n" +
                         $"Заработано за всё время: {totalEarned}g\n" +
                         $"В банке: {Config.BankBalance}g\n" +
                         $"Долг: {Config.LoanAmount}g";

            if (Config.AILevel >= 4)
            {
                double dailyAvg = totalEarned / Math.Max(1.0,
                    (Game1.year - 1) * 112 + GetSeasonIndex() * 28 + Game1.dayOfMonth);
                msg += $"\nСредний доход/день: {dailyAvg:F0}g";
            }

            Msg(msg);
            GainAIPoints(10);
        }

        private void AICropAdvice()
        {
            string season = Game1.currentSeason;
            int daysLeft = 28 - Game1.dayOfMonth;

            var crops = season switch
            {
                "spring" => new[] {
                    ("Картофель", 6, 80),
                    ("Цветная капуста", 12, 175),
                    ("Клубника", 8, 120),
                    ("Пастернак", 4, 35)
                },
                "summer" => new[] {
                    ("Черника", 13, 150),
                    ("Звёздочка", 9, 750),
                    ("Дыня", 12, 250),
                    ("Красная капуста", 9, 260)
                },
                "fall" => new[] {
                    ("Клюква", 7, 130),
                    ("Тыква", 13, 320),
                    ("Виноград", 10, 100),
                    ("Амарант", 7, 150)
                },
                _ => new (string, int, int)[] { ("Ничего — зима", 0, 0) }
            };

            string msg = $"🌾 Рекомендации на {daysLeft} дн.:\n";
            foreach (var (name, days, price) in crops)
            {
                string status = daysLeft >= days ? "✅" : "❌";
                msg += $"{status} {name}: {days} дн., ~{price}g\n";
            }

            if (Config.AILevel >= 3)
                msg += "\n📊 Совет: выбирайте повторные урожаи для макс. дохода.";

            Msg(msg);
            GainAIPoints(5);
        }

        private void AIMineAdvice()
        {
            double luck = Game1.player.DailyLuck;
            string luckStr = luck > 0.07 ? "🍀 Отличная!" :
                             luck > 0 ? "👍 Хорошая" :
                             luck > -0.07 ? "😐 Нейтральная" : "💀 Плохая!";

            string msg = $"⛏ Удача сегодня: {luckStr} ({luck:+0.00;-0.00})\n";

            if (luck > 0.04)
                msg += "Рекомендую глубокие уровни — лут будет хороший!\nБольше шанс на лестницы и руду.";
            else if (luck > 0)
                msg += "Нормальный день для шахты.";
            else
                msg += "Лучше займитесь фермой. Шахта будет сложнее.";

            if (Config.AILevel >= 3)
            {
                int mineLevel = who().deepestMineLevel;
                msg += $"\n\nГлубина рекорд: {mineLevel}\n";
                if (mineLevel < 40) msg += "Цель: уровень 40 (замороженные этажи).";
                else if (mineLevel < 80) msg += "Цель: уровень 80 (огненные этажи).";
                else if (mineLevel < 120) msg += "Цель: уровень 120 (Skull Key).";
                else msg += "Шахта пройдена! Попробуйте Skull Cavern.";
            }

            Msg(msg);
            GainAIPoints(5);
        }

        private void AINPCInfo()
        {
            var options = new List<Response>();
            foreach (var npc in Utility.getAllCharacters()
                .Where(c => c.IsVillager)
                .OrderBy(c => c.Name)
                .Take(10))
            {
                options.Add(new Response(npc.Name, npc.displayName));
            }
            options.Add(new Response("back", "⬅ Назад"));

            Game1.currentLocation.createQuestionDialogue(
                "👤 Выберите NPC:",
                options.ToArray(),
                (f, k) =>
                {
                    if (k == "back") { ShowAIMenu(); return; }
                    var npc = Game1.getCharacterFromName(k);
                    if (npc == null) { Msg("NPC не найден."); return; }

                    int hearts = who().getFriendshipHeartLevelForNPC(k);
                    string loc = npc.currentLocation?.Name ?? "неизвестно";

                    string info = $"👤 {npc.displayName}\n" +
                                  $"❤ Сердца: {hearts}/10\n" +
                                  $"📍 Сейчас: {loc}\n" +
                                  $"🎂 ДР: {npc.Birthday_Season} {npc.Birthday_Day}";

                    Msg(info);
                    GainAIPoints(3);
                }
            );
        }

        private void AIBirthday()
        {
            string season = Game1.currentSeason;
            int day = Game1.dayOfMonth;

            var birthdays = new List<string>();
            foreach (var npc in Utility.getAllCharacters().Where(c => c.IsVillager))
            {
                if (npc.Birthday_Season == season && npc.Birthday_Day >= day && npc.Birthday_Day <= day + 7)
                    birthdays.Add($"🎂 {npc.displayName} — {npc.Birthday_Season} {npc.Birthday_Day}");
            }

            string msg = birthdays.Count > 0
                ? "Ближайшие дни рождения (7 дней):\n" + string.Join("\n", birthdays)
                : "Нет дней рождения в ближайшие 7 дней.";

            Msg(msg);
            GainAIPoints(3);
        }

        private void AIFarmAnalysis()
        {
            var farm = Game1.getFarm();
            int objects = farm.Objects.Count();
            int terrainFeatures = farm.terrainFeatures.Count();

            string msg = $"🏡 Анализ фермы\n" +
                         $"Объекты: {objects}\n" +
                         $"Terrain features: {terrainFeatures}\n" +
                         $"Животные: {farm.getAllFarmAnimals().Count}\n" +
                         $"Здания: {farm.buildings.Count}";

            if (Config.AILevel >= 3)
                msg += "\n\n💡 Совет: автоматизируйте сбор спринклерами и бочками.";
            if (Config.AILevel >= 5)
                msg += "\n🔥 Режим «Почти чит»: Древние семена + теплица = бесконечный доход.";

            Msg(msg);
            GainAIPoints(5);
        }

        private void AIForecast()
        {
            if (Config.AILevel < 3)
            {
                Msg("Нужен ИИ ур.3+ для прогноза.");
                return;
            }

            int day = Game1.dayOfMonth;
            int daysLeft = 28 - day;
            string season = Game1.currentSeason;

            string msg = $"🔮 Прогноз на ближайшие дни\n" +
                         $"Сезон: {season}, осталось {daysLeft} дн.\n";

            if (daysLeft <= 3)
                msg += "⚠ Сезон заканчивается! Соберите урожай СЕЙЧАС!\n";
            if (daysLeft == 0)
                msg += "🚨 ПОСЛЕДНИЙ ДЕНЬ СЕЗОНА!\n";

            if (Config.AILevel >= 4)
            {
                double luck = Game1.player.DailyLuck;
                msg += $"\nУдача сегодня: {luck:+0.00;-0.00}";
                msg += luck > 0 ? " (хороший день для шахты)" : " (лучше на ферме)";
            }

            if (Config.AILevel >= 5)
                msg += "\n\n🧠 Стратегия: инвестируйте в бочки и кеги для x3 прибыли.";

            Msg(msg);
            GainAIPoints(8);
        }

        private void AILevelUp()
        {
            if (Config.AILevel >= 5)
            {
                Msg("ИИ уже максимального уровня (5).");
                return;
            }
            if (Config.AIPoints < 100)
            {
                Msg($"Недостаточно AIPoints: {Config.AIPoints}/100");
                return;
            }

            Config.AIPoints -= 100;
            Config.AILevel++;
            SaveConfig();
            Msg($"🤖 ИИ повышен до ур.{Config.AILevel}!\n" +
                $"Оставшиеся очки: {Config.AIPoints}");
        }

        private void GainAIPoints(int amount)
        {
            Config.AIPoints += amount;
            SaveConfig();
        }

        /*======================================================================
         *  2. RESOURCE SPAWNING
         *====================================================================*/
        private void ShowResourceMenu()
        {
            if (Config.DailyUsesLeft <= 0 && !Config.SandboxMode)
            {
                Msg("📦 Дневной лимит выдачи исчерпан!");
                return;
            }

            var options = new List<Response>
            {
                new Response("wood", "🌲 Дерево (x50)"),
                new Response("stone", "🪨 Камень (x50)"),
                new Response("coal", "⚫ Уголь (x20)"),
                new Response("seeds", "🌱 Семена сезона (x30)"),
                new Response("food", "🍲 Еда (x10)"),
                new Response("metal", "⚙ Металлы (x20)"),
            };

            if (Config.AllowRareItems)
                options.Add(new Response("rare", "💎 Редкие ресурсы (x5)"));

            options.Add(new Response("custom", "🔢 По ID предмета"));
            options.Add(new Response("back", "⬅ Назад"));

            Game1.currentLocation.createQuestionDialogue(
                $"📦 Выдача ресурсов (осталось: {Config.DailyUsesLeft})",
                options.ToArray(),
                HandleResourceMenu
            );
        }

        private void HandleResourceMenu(Farmer f, string key)
        {
            switch (key)
            {
                case "wood":  GiveItem("(O)388", 50, "Дерево"); break;
                case "stone": GiveItem("(O)390", 50, "Камень"); break;
                case "coal":  GiveItem("(O)382", 20, "Уголь"); break;
                case "seeds": GiveSeasonSeeds(); break;
                case "food":  GiveItem("(O)196", 10, "Салат"); break;
                case "metal": GiveMetals(); break;
                case "rare":  GiveRareItems(); break;
                case "custom": ShowCustomItemInput(); break;
                case "back":  ShowMainMenu(); break;
            }
        }

        private void GiveItem(string itemId, int amount, string name)
        {
            amount = Math.Min(amount, Config.MaxResourceAmount);
            var item = ItemRegistry.Create(itemId, amount);
            who().addItemByMenuIfNecessary(item);

            Config.DailyUsesLeft--;
            Config.TotalResourcesGiven += amount;
            Log($"Выдано: {name} x{amount}");
            SaveConfig();
            Msg($"✅ Выдано: {name} x{amount}");
        }

        private void GiveSeasonSeeds()
        {
            string seedId = Game1.currentSeason switch
            {
                "spring" => "(O)472", // Parsnip Seeds
                "summer" => "(O)487", // Melon Seeds (Corn)
                "fall"   => "(O)490", // Pumpkin Seeds (Yam)
                "winter" => "(O)495", // wheat seeds fallback
                _ => "(O)472"
            };
            GiveItem(seedId, 30, "Семена сезона");
        }

        private void GiveMetals()
        {
            var options = new List<Response>
            {
                new Response("copper", "Медная руда x20"),
                new Response("iron", "Железная руда x20"),
                new Response("gold", "Золотая руда x20"),
                new Response("iridium", "Иридиевая руда x10"),
                new Response("back", "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "⚙ Выберите металл:",
                options.ToArray(),
                (f, k) =>
                {
                    switch (k)
                    {
                        case "copper":  GiveItem("(O)378", 20, "Медная руда"); break;
                        case "iron":    GiveItem("(O)380", 20, "Железная руда"); break;
                        case "gold":    GiveItem("(O)384", 20, "Золотая руда"); break;
                        case "iridium": GiveItem("(O)386", 10, "Иридиевая руда"); break;
                        case "back":    ShowResourceMenu(); break;
                    }
                }
            );
        }

        private void GiveRareItems()
        {
            var options = new List<Response>
            {
                new Response("diamond",     "💎 Алмаз x5"),
                new Response("prismatic",   "🌈 Призматический осколок x1"),
                new Response("ancient",     "🏺 Древние семена x5"),
                new Response("stardrop",    "⭐ Звёздная капля x1"),
                new Response("back",        "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "💎 Редкие ресурсы:",
                options.ToArray(),
                (f, k) =>
                {
                    switch (k)
                    {
                        case "diamond":   GiveItem("(O)72", 5, "Алмаз"); break;
                        case "prismatic": GiveItem("(O)74", 1, "Призматический осколок"); break;
                        case "ancient":   GiveItem("(O)499", 5, "Древние семена"); break;
                        case "stardrop":  GiveItem("(O)434", 1, "Звёздная капля"); break;
                        case "back":      ShowResourceMenu(); break;
                    }
                }
            );
        }

        private void ShowCustomItemInput()
        {
            // SMAPI doesn't have native text input dialogs, so we use a numeric choice
            var options = new List<Response>
            {
                new Response("id_72",  "ID 72 — Алмаз"),
                new Response("id_74",  "ID 74 — Призм. осколок"),
                new Response("id_388", "ID 388 — Дерево"),
                new Response("id_390", "ID 390 — Камень"),
                new Response("id_337", "ID 337 — Иридиевый слиток"),
                new Response("id_645", "ID 645 — Инопланетное пугало"),
                new Response("back",   "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "🔢 Выберите предмет по ID:",
                options.ToArray(),
                (f, k) =>
                {
                    if (k == "back") { ShowResourceMenu(); return; }
                    string id = k.Replace("id_", "");
                    GiveItem($"(O){id}", 10, $"Предмет #{id}");
                }
            );
        }

        /*======================================================================
         *  3. UPGRADES
         *====================================================================*/
        private void ShowUpgradeMenu()
        {
            var options = new List<Response>();

            if (Config.AllowToolUpgrade)
                options.Add(new Response("tools", "🔨 Улучшить инструменты"));
            if (Config.AllowSkillMax)
                options.Add(new Response("skills", "📚 Прокачать навыки"));
            options.Add(new Response("energy", "⚡ Восстановить энергию"));
            options.Add(new Response("health", "❤ Восстановить здоровье"));
            if (Config.AllowMoneyGive)
                options.Add(new Response("money", "💰 Добавить золото"));
            if (Config.GodModeEnabled)
                options.Add(new Response("god", "😈 РЕЖИМ БОГА"));
            options.Add(new Response("back", "⬅ Назад"));

            Game1.currentLocation.createQuestionDialogue(
                "🛠 Прокачка",
                options.ToArray(),
                HandleUpgradeMenu
            );
        }

        private void HandleUpgradeMenu(Farmer f, string key)
        {
            switch (key)
            {
                case "tools":  ShowToolUpgrade(); break;
                case "skills": MaxAllSkills(); break;
                case "energy": RestoreEnergy(); break;
                case "health": RestoreHealth(); break;
                case "money":  AddMoney(); break;
                case "god":    TryGodMode(); break;
                case "back":   ShowMainMenu(); break;
            }
        }

        private void ShowToolUpgrade()
        {
            var options = new List<Response>
            {
                new Response("copper", "🟤 Медь"),
                new Response("steel",  "⚪ Сталь"),
                new Response("gold",   "🟡 Золото"),
                new Response("iridium","🟣 Иридий"),
                new Response("back",   "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "🔨 Уровень улучшения:",
                options.ToArray(),
                (f, k) =>
                {
                    if (k == "back") { ShowUpgradeMenu(); return; }
                    int level = k switch
                    {
                        "copper"  => 1,
                        "steel"   => 2,
                        "gold"    => 3,
                        "iridium" => 4,
                        _ => 0
                    };
                    UpgradeAllTools(level);
                }
            );
        }

        private void UpgradeAllTools(int level)
        {
            var farmer = who();
            foreach (var item in farmer.Items)
            {
                if (item is Tool tool && tool is not MeleeWeapon)
                {
                    tool.UpgradeLevel = level;
                }
            }
            Log($"Tools upgraded to level {level}");
            Msg($"✅ Все инструменты улучшены до ур.{level}!");
        }

        private void MaxAllSkills()
        {
            var farmer = who();
            farmer.FarmingLevel = 10;
            farmer.FishingLevel = 10;
            farmer.ForagingLevel = 10;
            farmer.MiningLevel = 10;
            farmer.CombatLevel = 10;
            farmer.experiencePoints[0] = 15000;
            farmer.experiencePoints[1] = 15000;
            farmer.experiencePoints[2] = 15000;
            farmer.experiencePoints[3] = 15000;
            farmer.experiencePoints[4] = 15000;
            Log("All skills maxed to 10");
            Msg("✅ Все навыки прокачаны до 10!");
        }

        private void RestoreEnergy()
        {
            who().Stamina = who().MaxStamina;
            Msg("⚡ Энергия восстановлена!");
        }

        private void RestoreHealth()
        {
            who().health = who().maxHealth;
            Msg("❤ Здоровье восстановлено!");
        }

        private void AddMoney()
        {
            var options = new List<Response>
            {
                new Response("1000",    "+1,000g"),
                new Response("10000",   "+10,000g"),
                new Response("100000",  "+100,000g"),
                new Response("1000000", "+1,000,000g"),
                new Response("back",    "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "💰 Сколько золота добавить?",
                options.ToArray(),
                (f, k) =>
                {
                    if (k == "back") { ShowUpgradeMenu(); return; }
                    int amount = int.Parse(k);
                    who().Money += amount;
                    Config.TotalMoneyEarned += amount;
                    Log($"Added {amount}g");
                    SaveConfig();
                    Msg($"✅ Добавлено {amount}g!");
                }
            );
        }

        private void TryGodMode()
        {
            if (!Config.GodModeEnabled)
            {
                Msg("😈 Режим Бога отключён в настройках.");
                return;
            }

            if (!GodModeUnlocked)
            {
                // Password check via dialogue choices (simplified)
                var options = new List<Response>
                {
                    new Response(Config.GodModePassword, "🔑 " + Config.GodModePassword),
                    new Response("wrong1", "🔑 letmein"),
                    new Response("wrong2", "🔑 password"),
                    new Response("back", "⬅ Назад")
                };

                // Shuffle first 3
                var shuffled = options.Take(3).OrderBy(_ => Rng.Next()).ToList();
                shuffled.Add(options.Last());

                Game1.currentLocation.createQuestionDialogue(
                    "🔐 Введите пароль Режима Бога:",
                    shuffled.ToArray(),
                    (f, k) =>
                    {
                        if (k == "back") { ShowUpgradeMenu(); return; }
                        if (k == Config.GodModePassword)
                        {
                            GodModeUnlocked = true;
                            ActivateGodMode();
                        }
                        else
                        {
                            Msg("❌ Неверный пароль!");
                        }
                    }
                );
            }
            else
            {
                ActivateGodMode();
            }
        }

        private void ActivateGodMode()
        {
            var farmer = who();

            // Max all skills
            MaxAllSkills();

            // Max tools
            UpgradeAllTools(4);

            // Money
            farmer.Money += 1000000;
            Config.TotalMoneyEarned += 1000000;

            // Resources
            GiveItemSilent("(O)388", 999); // Wood
            GiveItemSilent("(O)390", 999); // Stone
            GiveItemSilent("(O)382", 999); // Coal
            GiveItemSilent("(O)386", 999); // Iridium
            GiveItemSilent("(O)337", 999); // Iridium Bar

            // Full energy & health
            farmer.Stamina = farmer.MaxStamina;
            farmer.health = farmer.maxHealth;

            Config.GodModeUsed = true;
            Log("GOD MODE ACTIVATED");
            SaveConfig();

            Msg("😈 РЕЖИМ БОГА АКТИВИРОВАН!\n" +
                "✅ Навыки: 10\n✅ Инструменты: Иридий\n" +
                "✅ +1,000,000g\n✅ +999 ресурсов\n" +
                "✅ Полная энергия и здоровье");
        }

        private void GiveItemSilent(string itemId, int amount)
        {
            var item = ItemRegistry.Create(itemId, Math.Min(amount, Config.MaxResourceAmount));
            who().addItemByMenuIfNecessary(item);
            Config.TotalResourcesGiven += amount;
        }

        /*======================================================================
         *  4. ONLINE SHOP
         *====================================================================*/
        private void ShowShopMenu()
        {
            var options = new List<Response>
            {
                new Response("seeds",     "🌱 Семена (500g)"),
                new Response("resources", "📦 Ресурсы (1000g)"),
                new Response("rare",      "💎 Редкие предметы (5000g)"),
                new Response("status",    "📋 Статус доставки"),
                new Response("back",      "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "🛒 Онлайн магазин\n(Доставка на следующий день)",
                options.ToArray(),
                HandleShopMenu
            );
        }

        private void HandleShopMenu(Farmer f, string key)
        {
            switch (key)
            {
                case "seeds":
                    if (TryBuy(500))
                    {
                        string seedId = Game1.currentSeason switch
                        {
                            "spring" => "(O)472",
                            "summer" => "(O)487",
                            "fall"   => "(O)490",
                            _ => "(O)472"
                        };
                        AddPendingDelivery(seedId, 30);
                        Msg("✅ Семена куплены! Доставка завтра.");
                    }
                    break;
                case "resources":
                    if (TryBuy(1000))
                    {
                        AddPendingDelivery("(O)388", 100);
                        AddPendingDelivery("(O)390", 100);
                        AddPendingDelivery("(O)382", 50);
                        Msg("✅ Ресурсы куплены! Доставка завтра.");
                    }
                    break;
                case "rare":
                    if (TryBuy(5000))
                    {
                        AddPendingDelivery("(O)72", 5);
                        AddPendingDelivery("(O)386", 20);
                        Msg("✅ Редкие предметы куплены! Доставка завтра.");
                    }
                    break;
                case "status":
                    if (PendingDeliveries.Count == 0)
                        Msg("📋 Нет ожидающих доставок.");
                    else
                    {
                        string status = "📋 Ожидают доставки:\n";
                        foreach (var kv in PendingDeliveries)
                        {
                            var data = ItemRegistry.GetData(kv.Key);
                            string name = data?.DisplayName ?? kv.Key;
                            status += $"  • {name} x{kv.Value}\n";
                        }
                        Msg(status);
                    }
                    break;
                case "back":
                    ShowMainMenu();
                    break;
            }
        }

        private bool TryBuy(int cost)
        {
            if (who().Money >= cost)
            {
                who().Money -= cost;
                return true;
            }
            Msg($"❌ Недостаточно золота! Нужно: {cost}g");
            return false;
        }

        private void AddPendingDelivery(string itemId, int amount)
        {
            if (PendingDeliveries.ContainsKey(itemId))
                PendingDeliveries[itemId] += amount;
            else
                PendingDeliveries[itemId] = amount;
        }

        private void DeliverPendingItems()
        {
            if (PendingDeliveries.Count == 0) return;

            foreach (var kv in PendingDeliveries)
            {
                var item = ItemRegistry.Create(kv.Key, kv.Value);
                who().addItemByMenuIfNecessary(item);
                Config.TotalResourcesGiven += kv.Value;
            }

            int count = PendingDeliveries.Count;
            PendingDeliveries.Clear();
            Log($"Delivered {count} item types from shop");
            Msg($"📦 Доставка прибыла! ({count} типов предметов)");
        }

        /*======================================================================
         *  5. BANK
         *====================================================================*/
        private void ShowBankMenu()
        {
            var options = new List<Response>
            {
                new Response("deposit",  "💰 Положить на счёт"),
                new Response("withdraw", "💸 Снять со счёта"),
                new Response("loan",     "🏦 Взять кредит"),
                new Response("repay",    "💳 Погасить кредит"),
                new Response("balance",  "📊 Баланс"),
                new Response("back",     "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                $"🏦 Банк SmartFarm\n" +
                $"Баланс: {Config.BankBalance}g | Долг: {Config.LoanAmount}g",
                options.ToArray(),
                HandleBankMenu
            );
        }

        private void HandleBankMenu(Farmer f, string key)
        {
            switch (key)
            {
                case "deposit":
                    ShowAmountChoice("Сколько положить?", amount =>
                    {
                        if (who().Money >= amount)
                        {
                            who().Money -= amount;
                            Config.BankBalance += amount;
                            SaveConfig();
                            Msg($"✅ Внесено {amount}g\nБаланс: {Config.BankBalance}g\n" +
                                $"Ставка: {Config.BankInterestRate * 100}% в день");
                        }
                        else Msg("❌ Недостаточно золота!");
                    });
                    break;
                case "withdraw":
                    ShowAmountChoice("Сколько снять?", amount =>
                    {
                        if (Config.BankBalance >= amount)
                        {
                            Config.BankBalance -= amount;
                            who().Money += amount;
                            SaveConfig();
                            Msg($"✅ Снято {amount}g\nОстаток: {Config.BankBalance}g");
                        }
                        else Msg("❌ Недостаточно на счёте!");
                    });
                    break;
                case "loan":
                    ShowAmountChoice("Размер кредита?", amount =>
                    {
                        Config.LoanAmount += amount;
                        who().Money += amount;
                        SaveConfig();
                        Msg($"✅ Кредит: {amount}g\nОбщий долг: {Config.LoanAmount}g\n" +
                            $"Ставка: {Config.LoanInterestRate * 100}% в день");
                    });
                    break;
                case "repay":
                    ShowAmountChoice("Сколько погасить?", amount =>
                    {
                        amount = Math.Min(amount, Config.LoanAmount);
                        if (who().Money >= amount)
                        {
                            who().Money -= amount;
                            Config.LoanAmount -= amount;
                            SaveConfig();
                            Msg($"✅ Погашено: {amount}g\nОсталось: {Config.LoanAmount}g");
                        }
                        else Msg("❌ Недостаточно золота!");
                    });
                    break;
                case "balance":
                    Msg($"📊 Банковский отчёт\n" +
                        $"Баланс: {Config.BankBalance}g\n" +
                        $"Ставка: {Config.BankInterestRate * 100}%/день\n" +
                        $"Долг: {Config.LoanAmount}g\n" +
                        $"Ставка кредита: {Config.LoanInterestRate * 100}%/день");
                    break;
                case "back":
                    ShowMainMenu();
                    break;
            }
        }

        private void ShowAmountChoice(string title, Action<int> callback)
        {
            var options = new List<Response>
            {
                new Response("500",    "500g"),
                new Response("1000",   "1,000g"),
                new Response("5000",   "5,000g"),
                new Response("10000",  "10,000g"),
                new Response("50000",  "50,000g"),
                new Response("100000", "100,000g"),
                new Response("back",   "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                title,
                options.ToArray(),
                (f, k) =>
                {
                    if (k == "back") { ShowBankMenu(); return; }
                    callback(int.Parse(k));
                }
            );
        }

        /*======================================================================
         *  6. STATISTICS
         *====================================================================*/
        private void ShowStats()
        {
            var farmer = who();
            string msg = $"📊 Статистика PhoneFarm\n\n" +
                         $"💰 Общий доход: {farmer.totalMoneyEarned.Value}g\n" +
                         $"💵 Текущее золото: {farmer.Money}g\n" +
                         $"📦 Ресурсов выдано: {Config.TotalResourcesGiven}\n" +
                         $"💸 Денег добавлено модом: {Config.TotalMoneyEarned}g\n" +
                         $"🤖 Уровень ИИ: {Config.AILevel}\n" +
                         $"🧠 AI Points: {Config.AIPoints}\n" +
                         $"📱 Уровень телефона: {Config.PhoneLevel}\n" +
                         $"🏦 В банке: {Config.BankBalance}g\n" +
                         $"💳 Долг: {Config.LoanAmount}g\n" +
                         $"😈 Режим Бога: {(Config.GodModeUsed ? "Использован" : "Нет")}\n" +
                         $"📝 Записей в логе: {UsageLog.Count}";

            Msg(msg);
        }

        /*======================================================================
         *  7. CHALLENGES
         *====================================================================*/
        private void GenerateDailyChallenge()
        {
            if (ActiveChallenges.Count >= 3) return;

            var types = new[]
            {
                ("earn",   "Заработай {0}g",             new[] { 1000, 5000, 10000 }),
                ("mine",   "Дойди до {0} уровня шахты",  new[] { 40, 80, 120 }),
            };

            var (type, template, targets) = types[Rng.Next(types.Length)];
            int target = targets[Rng.Next(targets.Length)];
            int reward = target * (type == "earn" ? 1 : 100);

            ActiveChallenges.Add(new Challenge
            {
                Description = string.Format(template, target),
                Type = type,
                Target = target,
                RewardMoney = reward,
                Completed = false
            });
        }

        private void ShowChallenges()
        {
            CheckChallengeCompletion();

            if (ActiveChallenges.Count == 0)
            {
                Msg("🎯 Нет активных челленджей.\nНовый появится завтра!");
                return;
            }

            string msg = "🎯 Челленджи:\n\n";
            for (int i = 0; i < ActiveChallenges.Count; i++)
            {
                var c = ActiveChallenges[i];
                string status = c.Completed ? "✅" : "⬜";
                msg += $"{status} {c.Description}\n   Награда: {c.RewardMoney}g\n\n";
            }

            // Collect completed
            int collected = 0;
            foreach (var c in ActiveChallenges.Where(c => c.Completed).ToList())
            {
                who().Money += c.RewardMoney;
                GainAIPoints(20);
                collected += c.RewardMoney;
                ActiveChallenges.Remove(c);
            }

            if (collected > 0)
                msg += $"\n🎉 Получено: {collected}g + 20 AIPoints!";

            Msg(msg);
        }

        private void CheckChallengeCompletion()
        {
            var farmer = who();
            foreach (var c in ActiveChallenges.Where(c => !c.Completed))
            {
                switch (c.Type)
                {
                    case "earn":
                        if ((int)farmer.totalMoneyEarned.Value >= c.Target)
                            c.Completed = true;
                        break;
                    case "mine":
                        if (farmer.deepestMineLevel >= c.Target)
                            c.Completed = true;
                        break;
                }
            }
        }

        /*======================================================================
         *  8. SETTINGS
         *====================================================================*/
        private void ShowSettings()
        {
            var options = new List<Response>
            {
                new Response("god_toggle",  $"😈 Режим Бога: {(Config.GodModeEnabled ? "ВКЛ" : "ВЫКЛ")}"),
                new Response("rare_toggle", $"💎 Редкие предметы: {(Config.AllowRareItems ? "ВКЛ" : "ВЫКЛ")}"),
                new Response("sandbox",     $"🏖 Sandbox: {(Config.SandboxMode ? "ВКЛ" : "ВЫКЛ")}"),
                new Response("phone_up",    $"📱 Улучшить телефон (Ур.{Config.PhoneLevel}, 5000g)"),
                new Response("reset",       "🔄 Сброс статистики"),
                new Response("viewlog",     "📝 Просмотр лога"),
                new Response("back",        "⬅ Назад")
            };

            Game1.currentLocation.createQuestionDialogue(
                "⚙ Настройки PhoneFarm",
                options.ToArray(),
                HandleSettings
            );
        }

        private void HandleSettings(Farmer f, string key)
        {
            switch (key)
            {
                case "god_toggle":
                    Config.GodModeEnabled = !Config.GodModeEnabled;
                    GodModeUnlocked = false;
                    SaveConfig();
                    Msg($"😈 Режим Бога: {(Config.GodModeEnabled ? "ВКЛ" : "ВЫКЛ")}");
                    break;
                case "rare_toggle":
                    Config.AllowRareItems = !Config.AllowRareItems;
                    SaveConfig();
                    Msg($"💎 Редкие предметы: {(Config.AllowRareItems ? "ВКЛ" : "ВЫКЛ")}");
                    break;
                case "sandbox":
                    Config.SandboxMode = !Config.SandboxMode;
                    SaveConfig();
                    Msg($"🏖 Sandbox: {(Config.SandboxMode ? "ВКЛ (нет лимитов)" : "ВЫКЛ")}");
                    break;
                case "phone_up":
                    if (Config.PhoneLevel >= 5)
                    {
                        Msg("📱 Телефон уже максимального уровня!");
                        break;
                    }
                    int cost = 5000 * Config.PhoneLevel;
                    if (who().Money >= cost)
                    {
                        who().Money -= cost;
                        Config.PhoneLevel++;
                        Config.DailyLimit += 2;
                        SaveConfig();
                        Msg($"📱 Телефон улучшен до ур.{Config.PhoneLevel}!\n" +
                            $"Дневной лимит: {Config.DailyLimit}");
                    }
                    else Msg($"❌ Нужно {cost}g!");
                    break;
                case "reset":
                    Config.TotalResourcesGiven = 0;
                    Config.TotalMoneyEarned = 0;
                    Config.GodModeUsed = false;
                    UsageLog.Clear();
                    SaveConfig();
                    Msg("🔄 Статистика сброшена!");
                    break;
                case "viewlog":
                    if (UsageLog.Count == 0)
                        Msg("📝 Лог пуст.");
                    else
                    {
                        var last10 = UsageLog.TakeLast(10);
                        Msg("📝 Последние действия:\n" + string.Join("\n", last10));
                    }
                    break;
                case "back":
                    ShowMainMenu();
                    break;
            }
        }

        /*======================================================================
         *  UTILITIES
         *====================================================================*/
        private Farmer who() => Game1.player;

        private void Msg(string text)
        {
            Game1.activeClickableMenu = new DialogueBox(text);
        }

        private void Log(string text)
        {
            string entry = $"[{Game1.currentSeason} {Game1.dayOfMonth}] {text}";
            UsageLog.Add(entry);
            Monitor.Log(entry, LogLevel.Info);
            if (UsageLog.Count > 100)
                UsageLog.RemoveAt(0);
        }

        private void SaveConfig()
        {
            Helper.WriteConfig(Config);
        }

        private int GetSeasonIndex()
        {
            return Game1.currentSeason switch
            {
                "spring" => 0,
                "summer" => 1,
                "fall" => 2,
                "winter" => 3,
                _ => 0
            };
        }
    }
}
