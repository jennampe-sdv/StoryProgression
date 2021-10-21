using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
// using ContentPatcher.Framework.Tokens.ValueProviders.ModConvention;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using ContentPatcher;
using HarmonyLib;
using Netcode;
using StoryProgression.Patches;
using StoryProgression.Configs;
using StoryProgression.Calculations;
using System.IO;
using System.Linq;
using System.Reflection;
using StoryProgression.ContentPatcherTokens;
using StoryProgression.ManageData;
using StardewValley.Locations;

namespace ContentPatcher
{
    /// <summary>The Content Patcher API which other mods can access.</summary>
    public interface IContentPatcherAPI
    {
        /*********
        ** Methods
        *********/
        /// <summary>Register a simple token.</summary>
        /// <param name="mod">The manifest of the mod defining the token (see <see cref="Mod.ModManifest"/> on your entry class).</param>
        /// <param name="name">The token name. This only needs to be unique for your mod; Content Patcher will prefix it with your mod ID automatically, like <c>YourName.ExampleMod/SomeTokenName</c>.</param>
        /// <param name="getValue">A function which returns the current token value. If this returns a null or empty list, the token is considered unavailable in the current context and any patches or dynamic tokens using it are disabled.</param>
        void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);

        /// <summary>Register a complex token. This is an advanced API; only use this method if you've read the documentation and are aware of the consequences.</summary>
        /// <param name="mod">The manifest of the mod defining the token (see <see cref="Mod.ModManifest"/> on your entry class).</param>
        /// <param name="name">The token name. This only needs to be unique for your mod; Content Patcher will prefix it with your mod ID automatically, like <c>YourName.ExampleMod/SomeTokenName</c>.</param>
        /// <param name="token">An arbitrary class with one or more methods from <see cref="ConventionDelegates"/>.</param>
        void RegisterToken(IManifest mod, string name, object token);

    }
}

namespace StoryProgression
{



    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod, IAssetEditor, IAssetLoader
    {

        /*********
        ** Properties
        *********/
        /// <summary>The mod configuration from the player.</summary>
        private ModConfig Config;
        public static bool toddlerSchedules;
        public static bool dynamGifts;
        public static bool dynamPersonality;
        public static bool dynamSched;
        public static int maxChildrenAllowed;
        public static Dictionary<int, string> ageupBrackets = new Dictionary<int, string>() { }; // structure: position/index = stage number; key = first day of; string = stage friendly name

        public static Dictionary<string, string> parentDialogueFills;
        public static List<string> userModderPrefs = new List<string>();

        /*********
        ** /// MOD ENTRY POINT
        *********/
        public static IMonitor GeneralMonitor;

        public override void Entry(IModHelper helper)
        {
            // CONFIGURATION FILE
            this.Config = this.Helper.ReadConfig<ModConfig>();
            toddlerSchedules = this.Config.ToddlerSchedules;
            dynamGifts = this.Config.DynamicGiftTastes;
            dynamPersonality = this.Config.DynamicPersonalities;
            dynamSched = this.Config.DynamicSchedules;
            maxChildrenAllowed = this.Config.MaxChildren;
            
            parentDialogueFills = new Dictionary<string, string>()
            {
                { "player male", this.Config.PlayerParentNicknameMale },
                { "player female", this.Config.PlayerParentNicknameFemale },
                { "npc male", this.Config.SpouseParentNicknameMale },
                { "npc female", this.Config.SpouseParentNicknameFemale }
            };

            // note that the minimum allowed length for a given stage is 1 day
            int ageToNewborn = 0;
            int ageToBaby = ageToNewborn + 1 + (Math.Max(this.Config.stageLengthNewborn, 1));
            int ageToCrawler = ageToBaby + (Math.Max(this.Config.stageLengthBaby, 1));
            int ageToToddler = ageToCrawler + (Math.Max(this.Config.stageLengthCrawler, 1));
            int ageToChild = ageToToddler + (Math.Max(this.Config.stageLengthToddler, 1));

            ageupBrackets.Add(ageToNewborn, "newborn");
            ageupBrackets.Add(ageToBaby, "baby");
            ageupBrackets.Add(ageToCrawler, "crawler");
            ageupBrackets.Add(ageToToddler, "toddler");
            ageupBrackets.Add(ageToChild, "child");

            try
            {
                userModderPrefs = this.Config.ModderPreferences.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
            } catch (Exception ex)
            {
                this.Monitor.Log($"Could not parse config file 'ModderPreferences' entry:\n{ex}", LogLevel.Debug);
            }

            // HARMONY INJECTION
            ChildPatching.Initialize(this.Monitor);
            ChildPatching.InitializeScope(this.Helper);
            PatchMaster.Initialize(this.Monitor);
            PatchMaster.InitializeScope(this.Helper);

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // EVENT HOOKS
            helper.Events.GameLoop.GameLaunched += this.makeAPITokens; // register Content Patcher tokens
            helper.Events.GameLoop.SaveLoaded += this.onGameStartUp; // create custom Child modData if none is already present
            helper.Events.GameLoop.Saving += this.markModLoaded; // add custom Child data to save file
            helper.Events.GameLoop.DayStarted += this.onDayStart;
            helper.Events.GameLoop.DayEnding += this.onDayEnd;
            helper.Events.GameLoop.TimeChanged += doAt6AM;
            helper.Events.GameLoop.TimeChanged += confirmChildAssets;
        }

        /*********
        ** /// GLOBAL PARAMETERS
        *********/
        public static bool defaultToddlersCanSpeak = false; // toggles whether there are portraits for the default toddlers
        public static bool defaultToddlersCanSchedule = false; // toggles whether there are schedules for the default toddlers

        /*********
        ** ///  INITIALIZE MOD / TRACK IF HAS BEEN LOADED
        *********/
        //*** Track whether this is the first time loading the mod
        class StoryProgData
        {
            public bool loadedBefore { get; set; }
            public StoryProgData()
            {
                this.loadedBefore = true;
            }
        }

        public void markModLoaded(object sender, SavingEventArgs e)
        {
            this.Helper.Data.WriteSaveData("general", new StoryProgData());
        }

        /*********
        ** /// DAILY MAINTENANCE
        *********/
        public void moveChildToBed(Child child, bool sleeping)
        {
            // establish randomizer as needed
            // COPIED FROM VANILLA
            int parent_unique_id = (int)Game1.MasterPlayer.UniqueMultiplayerID;
            if (Game1.currentLocation is FarmHouse)
            {
                FarmHouse farm_house = Game1.currentLocation as FarmHouse;
                if (farm_house.owner != null)
                {
                    parent_unique_id = (int)farm_house.owner.UniqueMultiplayerID;
                }
            }
            Random r = new Random(Game1.Date.TotalDays + (int)Game1.uniqueIDForThisGame / 2 + parent_unique_id * 2);

            // find house
            Farmer parent = Game1.getFarmerMaybeOffline(long.Parse(child.modData[Configs.ConfigsMain.dataParent1ID]));
            if (parent == null)
            {
                parent = Game1.MasterPlayer;
            }
            FarmHouse inferredHome = Utility.getHomeOfFarmer(parent);

            // perform movements
            if (DataGetters.getChildStage(child) <= 1)
            {
                child.Position = DataGetters.childCribSpot;
            }
            else if (DataGetters.getChildStage(child) == 2)
            {
                child.speed = 1;
                Point p2 = inferredHome.getRandomOpenPointInHouse(r, 1, 200);
                if (!p2.Equals(Point.Zero))
                {
                    child.setTilePosition(p2);
                }
                else
                {
                    child.Position = DataGetters.childCribSpot;
                }
                child.Sprite.CurrentAnimation = null;
            }
            else if (DataGetters.getChildStage(child) >= 3)
            {
                child.speed = 2;

                StardewValley.Objects.BedFurniture myBed = ChildMethods.getNPCBed(child as NPC, inferredHome, StardewValley.Objects.BedFurniture.BedType.Child);

                if (sleeping & myBed != null)
                {
                    child.setTilePosition(myBed.GetBedSpot());
                    myBed.ReserveForNPC();
                } else
                {
                    Point p3 = inferredHome.getRandomOpenPointInHouse(r, 1, 200);
                    if (!p3.Equals(Point.Zero))
                    {
                        child.setTilePosition(p3);
                    }
                    else if (myBed != null)
                    {
                        child.setTilePosition(myBed.GetBedSpot());
                    }
                }                
            }

            child.reloadSprite();
        }
        public void setAgeVanilla(Child child) // turns .Age value back to the game's defined value (for uninstall compatibility)
        {
            // save vanilla-preferred age
            child.modData[ConfigsMain.dataModdedAge] = child.Age.ToString();

            // set to mod-preferred age
            if (child.modData.TryGetValue(ConfigsMain.dataVanillaAge, out string vanillaAge))
            {
                if (int.TryParse(vanillaAge, out int vanillaAgeNum))
                {
                    child.Age = vanillaAgeNum;
                }
            }
        }
        public void setAgeMod(Child child) // turns .Age value back to the value consistent with the mod
        {
            // save vanilla-preferred age
            child.modData[ConfigsMain.dataVanillaAge] = child.Age.ToString();

            // set to mod-preferred age
            if (child.modData.TryGetValue(ConfigsMain.dataModdedAge, out string moddedAge))
            {
                if (int.TryParse(moddedAge, out int moddedAgeNum))
                {
                    child.Age = moddedAgeNum; 
                }
            }
        }


        public static bool fullLoadComplete = false; // set up marker that full loading has occurred
        private void doAt6AM(object sender, TimeChangedEventArgs e)
        {
            fullLoadComplete = true;

            if (e.NewTime == 610)
            {
                List<string> logicPaths = new List<string>(); // initialize list for deleting dummy NPCs

                foreach (Child child in Game1.player.getChildren())
                {
                    // reset sprites
                    child.reloadSprite();

                    // add logic paths
                    foreach (string logicPathType in new string[] { "base", "texture", "dialogue", "schedule", "gifts" })
                    {
                        string currentLogicPath = DataGetters.getModderControl(child, logicPathType);

                        if (currentLogicPath != null && !DataGetters.isUsingDefault(child, logicPathType))
                        {
                            logicPaths.Add(currentLogicPath);
                        }
                    }
                }

                // delete dummy NPCs
                IEnumerable<string> deleteNPCs = logicPaths.Distinct();

                foreach (string deleteNPC in deleteNPCs)
                {
                    NPC targetNPC = Game1.getCharacterFromName(deleteNPC, false);
                    if (targetNPC != null) // we found this NPC
                    {
                        GameLocation targetLoc = targetNPC.currentLocation;

                        targetLoc.characters.Remove(targetNPC);
                    }
                }


                // detach from event handler: we only need to do this on the first day
                // for sprites reloading, other days will reload at day start
                Helper.Events.GameLoop.TimeChanged -= doAt6AM;
            }
        }
        public void onDayStart(object sender, DayStartedEventArgs e)
        {
            // check for new babies
            FileInfo file = new FileInfo(Path.Combine(this.Helper.DirectoryPath, "data", "baby.json"));
            if (file.Exists)
            {
                bool tryUpdateNewBaby = babyInfoCalcs.updateNewBaby(this.Helper);

                if (!tryUpdateNewBaby)
                {
                    this.Monitor.LogOnce("Attempted to update new baby, but failed. IMPORTANT: New baby may not have full mod data.", LogLevel.Error);
                }
            }

            foreach (Child child in Game1.player.getChildren())
            {
                // set ages back to mod-preferred age
                setAgeMod(child);

                int childStage = DataGetters.getChildStage(child);

                // TO DO: what is this supposed to do?
                if (childStage <= 2 || (childStage == 3 && !ModEntry.toddlerSchedules)) // Child object is "toddler" age or younger
                {
                    child.isSleeping.Value = true;
                }

                // set schedules
                if (childStage >= 3)
                {
                    child.Schedule = child.getSchedule(Game1.dayOfMonth);
                }

                // set wakeup/bed times
                if (!child.modData.ContainsKey(ConfigsMain.scheduleWakeUp))
                {
                    child.modData[ConfigsMain.scheduleWakeUp] = this.Config.defaultTimeWakeup.ToString();
                }
                if (!child.modData.ContainsKey(ConfigsMain.scheduleGoSleep))
                {
                    child.modData[ConfigsMain.scheduleGoSleep] = this.Config.defaultTimeBedtime.ToString();
                }

                // move to beds properly
                bool stillAsleep = true;
                if (int.TryParse(child.modData[ConfigsMain.scheduleWakeUp], out int integerWakeup))
                {
                    stillAsleep = (integerWakeup > 600);
                }
                moveChildToBed(child, stillAsleep);
            }

            Helper.Content.InvalidateCache("Data/NPCGiftTastes");
            Helper.Content.InvalidateCache("Data/NPCDispositions");
            Helper.Content.InvalidateCache("Characters/");
        }

        public void onDayEnd(object sender, DayEndingEventArgs e)
        {
            // age up children
            foreach (Child child in Game1.player.getChildren())
            {
                // check for age-up
                calculateAgeUp(child);

                // address mod uninstall compatibility
                // // warp to FarmHouse, just in case
                Farmer parent = Game1.getFarmerMaybeOffline(long.Parse(child.modData[ConfigsMain.dataParent1ID]));
                FarmHouse inferredHome = Utility.getHomeOfFarmer(parent);
                Game1.warpCharacter(child, inferredHome, new Vector2(0, 0));
                // // set age back to vanilla age
                setAgeVanilla(child);
            }

            // set age and stage to update
            tokenDataUpdated["BASE_AGE"] = true;
            tokenDataUpdated[ConfigsMain.dataChildStage] = true;

            // update this token, if not already
            fullLoadComplete = true;
        }
        public void onGameStartUp(object sender, SaveLoadedEventArgs e)
        {
            //deleteSprites(); // delete every Child's portrait and sprite textures... helps with case where user deleted a texture a Child was using

            List<string> loadedPaths = new List<string>(); // is this really necessary?
            // parse content packs
            foreach (IContentPack contentPack in this.Helper.ContentPacks.GetOwned())
            {
                string[] filesPresent = Directory.GetFiles(contentPack.DirectoryPath);
                foreach (string file in filesPresent)
                {
                    if (file.Contains("manifest")) { continue; }
                    List<string> result = ParseModderListing(contentPack.ReadJsonFile<ModderLogicShell>(file.Substring(contentPack.DirectoryPath.Length + 1)), this.Monitor, this.Config.MaxChildren);

                    if (result.Count != 0)
                    {
                        loadedPaths.AddRange(result);
                    }
                }
            }
            // ensure that entries for each possible gender/age category exist, even if blank
            validateAllCatsExist(this.Config.MaxChildren);

            // perform first time startup tasks, if necessary
            var loadedData = this.Helper.Data.ReadSaveData<StoryProgData>("general");
            if (loadedData == null) // this saved file has not been loaded yet with this mod
            {
                // organize children by birth order
                // // provides support for potential null parent -> 0/1 birth order results, even when the child is not 0/1 in order
                // // provides extensibility for 3+ children games
                List<Child> allChildren = Game1.player.getChildren();
                string[] allChildNames = Calculations.ChildSorting.getChildrenInOrder(Game1.player);

                // set modData for children
                foreach (var child in allChildren)
                {
                    int birthOrder = Array.IndexOf(allChildNames, child.Name);

                    var modAddSuccess = AddData.addChildModInfo(child, birthOrder, this.Monitor);
                    if (!modAddSuccess)
                    {
                        // something went wrong
                        this.Monitor.LogOnce($"Introducing mod data to child {child.Name}: failed. IMPORTANT: Child may not have full mod data.", LogLevel.Error);
                    }
                    else
                    {
                        this.Monitor.LogOnce($"Introducing mod data to child {child.Name}: successful.", LogLevel.Info);
                    }
                }

            }

            // update children lists and birth order for games that have already been loaded before
            if (loadedData != null)
            {
                DataGetters.setChildrenList(Game1.player, updateModData: true);
            }


            // now that everything is available, reload caches
            foreach (Child child in Game1.player.getChildren())
            {
                Helper.Content.InvalidateCache($"Characters/schedules/{child.Name}");
                child.reloadSprite();
                //Helper.Content.InvalidateCache($"Characters/{child.Name}");
                child.Schedule = child.getSchedule(Game1.dayOfMonth);

            }

            // need to reload caches that now have info for the children's modder logic controls
            Helper.Content.InvalidateCache("Data/NPCGiftTastes");
            Helper.Content.InvalidateCache("Data/NPCDispositions");
            Helper.Content.InvalidateCache("Characters/Dialogue/rainy");
            //Helper.Content.InvalidateCache("Data/animationDescriptions");


            // need to reload children taking advantage of mod data, now that it is available
            foreach (Child child in Game1.player.getChildren())
            {
                child.reloadData();

                // also, update their modder info
                foreach (string parameter in pairedLogicPaths.Keys)
                {
                    if (!DataGetters.isUsingDefault(child, parameter))
                    {
                        string modderControl = DataGetters.getModderControl(child, parameter);

                        if (modderControl != null)
                        {
                            pairedLogicPaths[parameter][modderControl] = child.Name;
                        }
                    }                    
                }

            }

            //foreach (string parameter in pairedLogicPaths.Keys)
            //{
            //    foreach (KeyValuePair<string, string> element in pairedLogicPaths[parameter])
            //    {
            //        Monitor.Log(parameter + ": " + element.Key + " = " + element.Value,
            //            LogLevel.Error);
            //    }
            //}

            // set up to confirm children each have valid textures, dialogues, and schedules
            childrenToConfirm = Game1.player.getChildren();

            // set age and stage to update
            tokenDataUpdated["BASE_AGE"] = true;
            tokenDataUpdated[ConfigsMain.dataChildStage] = true;


        }


        /*********
        ** /// ENSURING VALIDITY OF CHILD ASSETS
        *********/
        public static List<Child> childrenToConfirm = new List<Child>(); // holds list of children to monitor
        public static List<Child> childrenToRemove = new List<Child>(); // holds list of children to be removed from watch list
        private void confirmChildAssets(object sender, TimeChangedEventArgs e)
        {

            // don't try this right at the beginning of the day
            if (e.NewTime < 610)
            {
                return;
            }

            if (!childrenToConfirm.Any()) // if the list is empty, nothing to do
            {
                return;
            }
            else
            {
                // reset removal list
                childrenToRemove = new List<Child>();

                // confirm children each have valid textures, dialogues, and schedules
                foreach (Child child in childrenToConfirm)
                {

                    Monitor.Log("Checking assets for " + child.Name + " . . . ", LogLevel.Debug);
                    AddData.updateAssets(child, Monitor);
                }

                // remove successfully checked children
                if (childrenToRemove.Any()) // if list is non-empty
                {
                    childrenToConfirm = childrenToConfirm.Except(childrenToRemove).ToList();
                }
            }
        }
        public static void addChildToWatchlist(Child child)
        {
            if (!ModEntry.childrenToConfirm.Contains(child))
            {
                childrenToConfirm.Add(child);
            }
        }

        /*********
        ** /// SETTING UP CONTENT PATCHER TOKENS
        *********/
        public static Dictionary<string, bool> tokenDataUpdated = new Dictionary<string, bool>()
        {
            { "BASE_NAME", false },
            { "BASE_AGE", false },
            { "BASE_GENDER", false },
            { "BASE_BIRTHDAY", false },
            { "BASE_BIRTHSEASON", false },
            { ConfigsMain.dataBirthOrder, false },
            { ConfigsMain.dataChildStage, false },
            { ConfigsMain.dataLivesHome, false },
            { ConfigsMain.dataParent1, false },
            { ConfigsMain.dataParent1G, false },
            { ConfigsMain.dataParent2, false },
            { ConfigsMain.dataParent2ID, false },
            { ConfigsMain.dataParent2G, false },
            { ConfigsMain.dataTemperament, false }
        };

        private void makeAPITokens(object sender, GameLaunchedEventArgs e)
        {

            var api = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            // simple token for whether mod has fully started up
            api.RegisterToken(this.ModManifest, "modLoaded", () =>
            {
                return fullLoadComplete ? new[] { "true" } : null;
            });

            // simple tokens for modder preferences on parent names
            api.RegisterToken(this.ModManifest, "playerParentName", () =>
            {
                // still on title screen: return nothing
                if (!Context.IsWorldReady && SaveGame.loaded?.player == null)
                {
                    return null;
                }

                // otherwise, get player gender
                bool playerIsMale = Context.IsWorldReady ? Game1.player.IsMale : SaveGame.loaded.player.IsMale;
                return new[] { playerIsMale ? parentDialogueFills["player male"] : parentDialogueFills["player female"] };
            });

            api.RegisterToken(this.ModManifest, "femaleParentName", () =>
            {
                return new[] { parentDialogueFills["npc female"] };
            });
            api.RegisterToken(this.ModManifest, "maleParentName", () =>
            {
                return new[] { parentDialogueFills["npc male"] };
            });


            //api.RegisterToken(this.ModManifest, "PlayerName", () =>
            //{
            //    // save is loaded
            //    if (Context.IsWorldReady)
            //        return new[] { Game1.player.Name };

            //    // or save is currently loading
            //    if (SaveGame.loaded?.player != null)
            //        return new[] { SaveGame.loaded.player.Name };

            //    // no save loaded (e.g. on the title screen)
            //    return null;
            //});


            //parentDialogueFills = new Dictionary<string, string>()
            //{
            //    { "player male", this.Config.PlayerParentNicknameMale },
            //    { "player female", this.Config.PlayerParentNicknameFemale },
            //    { "npc male", this.Config.SpouseParentNicknameMale },
            //    { "npc female", this.Config.SpouseParentNicknameFemale }
            //};

            //api.RegisterToken(this.ModManifest, "childA", new ChildAToken());
            api.RegisterToken(this.ModManifest, "childName", new ChildToken("name"));
            api.RegisterToken(this.ModManifest, "childAge", new ChildToken("age"));
            api.RegisterToken(this.ModManifest, "childGender", new ChildToken("gender"));
            api.RegisterToken(this.ModManifest, "childBirthDay", new ChildToken("birthday"));
            api.RegisterToken(this.ModManifest, "childBirthSeason", new ChildToken("birthseason"));
            api.RegisterToken(this.ModManifest, "childBirthOrder", new ChildToken("birthorder", source: ConfigsMain.dataBirthOrder));
            api.RegisterToken(this.ModManifest, "childStage", new ChildToken("stage", source: ConfigsMain.dataChildStage));
            api.RegisterToken(this.ModManifest, "childLivesHome", new ChildToken("liveshome", source: ConfigsMain.dataLivesHome));
            api.RegisterToken(this.ModManifest, "childParentA", new ChildToken("parent1", source: ConfigsMain.dataParent1));
            api.RegisterToken(this.ModManifest, "childParentAG", new ChildToken("parent1g", source: ConfigsMain.dataParent1G));
            api.RegisterToken(this.ModManifest, "childParentB", new ChildToken("parent2", source: ConfigsMain.dataParent2));
            api.RegisterToken(this.ModManifest, "childParentBID", new ChildToken("parent2id", source: ConfigsMain.dataParent2ID));
            api.RegisterToken(this.ModManifest, "childParentBG", new ChildToken("parent2g", source: ConfigsMain.dataParent2G));
            api.RegisterToken(this.ModManifest, "childTemperament", new ChildToken("temperament", source: ConfigsMain.dataTemperament));

            // // LATER
            // api.RegisterToken(this.ModManifest, "siblingsOlderC", new ChildToken("siblings_older_n", source: "special")); // how many older sibs? ("c"ount)
            // api.RegisterToken(this.ModManifest, "siblingsOlderCF", new ChildToken("siblings_older_n", source: "special")); // how many older female sibs? ("c"ount "f"emale)
            // api.RegisterToken(this.ModManifest, "siblingsOlderCM", new ChildToken("siblings_older_n", source: "special")); // how many older male sibs? ("c"ount "m"ale)
            // api.RegisterToken(this.ModManifest, "siblingsOlderIG", new ChildToken("siblings_younger_n", source: "special")); // sequential gender of older sibs ("i"nfo "g"ender)
            // api.RegisterToken(this.ModManifest, "siblingsOlderIN", new ChildToken("siblings_younger_n", source: "special")); // sequential name of older sibs ("i"nfo "n"ame)
            // api.RegisterToken(this.ModManifest, "siblingsYoungerC", new ChildToken("siblings_younger_n", source: "special")); // how many younger sibs? ("c"ount)
            // api.RegisterToken(this.ModManifest, "siblingsYoungerCF", new ChildToken("siblings_younger_n", source: "special")); // how many younger female sibs? ("c"ount "f"emale)
            // api.RegisterToken(this.ModManifest, "siblingsYoungerCM", new ChildToken("siblings_younger_n", source: "special")); // how many younger male sibs? ("c"ount "m"ale)
            // api.RegisterToken(this.ModManifest, "siblingsYoungerIG", new ChildToken("siblings_younger_n", source: "special")); // sequential gender of younger sibs ("i"nfo "g"ender)
            // api.RegisterToken(this.ModManifest, "siblingsYoungerIN", new ChildToken("siblings_younger_n", source: "special")); // sequential name of younger sibs ("i"nfo "n"ame)
        }

        // save which child is used which kind of token
        public static Dictionary<string, Dictionary<string, string>> pairedLogicPaths = new Dictionary<string, Dictionary<string, string>>()
        {
            { "base", new Dictionary<string, string>()},
            { "texture", new Dictionary<string, string>()},
            { "dialogue", new Dictionary<string, string>()},
            { "schedule", new Dictionary<string, string>()},
            { "gifts", new Dictionary<string, string>()}
        };

        /*********
        ** /// SMAPI-CONTROLLED ASSET EDITING / LOADED
        *********/

        // updated on CanEdit
        public static List<string> validPortraits { get; set; } = new List<string> { };
        public static List<string> validSprites { get; set; } = new List<string> { };
        public static List<string> validSchedules { get; set; } = new List<string> { };
        public static List<string> validDialogues { get; set; } = new List<string> { };

        // SMAPI-related methods for asset editing
        public bool CanEdit<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Data/NPCGiftTastes") || asset.AssetNameEquals("Data/NPCDispositions") || asset.AssetNameEquals("Characters/Dialogue/rainy"))
            //(asset.AssetName.Contains("Characters") && asset.AssetName.Contains("schedules"))) // || asset.AssetNameEquals("Data/animationDescriptions"))
            {
                return Context.IsWorldReady;
            }
            return false;
        }

        public class listAdditionShell
        {
            public listAdditionKeeper[] additions { get; set; }
        }
        public class listAdditionKeeper
        {
            public string templateName { get; set; }
            public string templateValue { get; set; }
        }
        public void Edit<T>(IAssetData asset)
        {
            // only need to edit if player actually has children
            string[] playerChildren = DataGetters.getChildrenList(Game1.player);
            if (playerChildren.Length > 0)
            {
                IDictionary<string, string> data = asset.AsDictionary<string, string>().Data;
                //string vincent = data["Vincent"];
                //string jas = data["Jas"];
                if (asset.AssetNameEquals("Data/NPCGiftTastes"))
                {
                    // load in defaults
                    listAdditionShell defaultGiftTastes = this.Helper.Data.ReadJsonFile<listAdditionShell>("assets/defaults/text/list_additions/gift_tastes.json");
                    for (int i = 0; i < defaultGiftTastes.additions.Count<listAdditionKeeper>(); i++)
                    {
                        listAdditionKeeper entry = defaultGiftTastes.additions[i];
                        data[entry.templateName] = entry.templateValue;
                    }

                    // for individual children
                    foreach (string childName in playerChildren)
                    {
                        if (childName == "") { continue; } // blank
                        NPC child = Game1.getCharacterFromName(childName, mustBeVillager: false);

                        int childStage = DataGetters.getChildStage(child as Child);
                        if (childStage <= 3) // toddler or younger
                        {
                            continue; // skip this child, they cannot have gift tastes yet
                        }

                        string childGiftTastes = null;
                        if (!Config.DynamicGiftTastes)
                        {
                            string dataEntryLabel = DataGetters.getModderControl(child as Child, "gifts");
                            if (data.TryGetValue(dataEntryLabel, out string result))
                            {
                                childGiftTastes = result;
                            }
                        }
                        else
                        {
                            // LATER: implement dynamic gift tastes
                        }

                        data[childName] = childGiftTastes;
                    }

                }
                else if (asset.AssetNameEquals("Data/NPCDispositions"))
                {

                    foreach (string childName in playerChildren)
                    {
                        if (childName == "") { continue; } // blank

                        NPC child = Game1.getCharacterFromName(childName, mustBeVillager: false);

                        string baseDisposition = null;
                        int childStage = DataGetters.getChildStage(child as Child);

                        if (childStage <= 3) // toddler or younger
                        {
                            continue; // skip this child, they cannot have gift tastes yet
                        }

                        string manners = null;
                        string anxiety = null;
                        string optimism = null;

                        bool foundDispositionString = false;
                        if (!Config.DynamicPersonalities)
                        {
                            string dataEntryLabel = DataGetters.getModderEntryLabel(data, child as Child, "base");
                            if (data.TryGetValue(dataEntryLabel, out string result))
                            {
                                baseDisposition = data[dataEntryLabel];

                                try
                                {
                                    string[] baseDispositionData = baseDisposition.Split('/');
                                    manners = baseDispositionData[1].ToLower();
                                    anxiety = baseDispositionData[2].ToLower();
                                    optimism = baseDispositionData[3].ToLower();

                                    // check for purposefully blank values
                                    manners = manners.Equals("") && child.modData.TryGetValue(ConfigsMain.dataManners, out string valueM) ? child.modData[ConfigsMain.dataManners] : manners;
                                    anxiety = anxiety.Equals("") && child.modData.TryGetValue(ConfigsMain.dataAnxiety, out string valueA) ? child.modData[ConfigsMain.dataAnxiety] : anxiety;
                                    optimism = optimism.Equals("") && child.modData.TryGetValue(ConfigsMain.dataOptimism, out string valueO) ? child.modData[ConfigsMain.dataOptimism] : optimism;

                                    // check for incorrect values
                                    if ((!manners.Equals("polite") && !manners.Equals("rude")) || (!anxiety.Equals("outgoing") && !anxiety.Equals("shy")) || (!optimism.Equals("positive") && !manners.Equals("negative")))
                                    {
                                        Monitor.Log($"Resetting some dispositional factors to 'neutral' in {dataEntryLabel} logic path for child {child.Name}.", LogLevel.Debug);
                                    }

                                    manners = (!manners.Equals("polite") && !manners.Equals("rude")) ? "neutral" : manners;
                                    anxiety = (!anxiety.Equals("outgoing") && !anxiety.Equals("shy")) ? "neutral" : anxiety;
                                    optimism = (!optimism.Equals("positive") && !manners.Equals("negative")) ? "neutral" : optimism;

                                    foundDispositionString = true;
                                }
                                catch (Exception ex)
                                {
                                    foundDispositionString = false;
                                    Monitor.Log($"Could not correctly parse disposition string provided by modder logic path {dataEntryLabel} for child {child.Name}. Error:\n{ex}", LogLevel.Error);
                                }
                            }

                        }
                        if (!foundDispositionString)
                        {
                            // LATER: implement dynamic gift tastes
                        }

                        // build out dispositions line
                        // // stage of life
                        string age = null;
                        switch (childStage)
                        {
                            case 4:
                                age = "child";
                                break;
                            case 5:
                                age = "teen";
                                break;
                            case 6:
                                age = "adult";
                                break;
                            default:
                                age = "teen";
                                break;
                        }

                        // // familial relationships
                        string parentage = "";
                        string parent2 = child.modData.TryGetValue(ConfigsMain.dataParent2, out string valuex) ? child.modData[ConfigsMain.dataParent2] : "";
                        if (data.ContainsKey(parent2))
                        {
                            parentage = child.modData[ConfigsMain.dataParent2];
                            if (child.modData[ConfigsMain.dataParent2G].Equals("m"))
                            {
                                parentage += (" '" + parentDialogueFills["npc male"].ToLower() + "'");
                            } else if (child.modData[ConfigsMain.dataParent2G].Equals("f"))
                            {
                                parentage += (" '" + parentDialogueFills["npc female"].ToLower() + "'");
                            }
                            else
                            {
                                parentage += " ''"; // blank familial relationship
                            }
                        }


                        // default location

                        child.DefaultMap = "FarmHouse";
                        int childIndex = (child as Child).GetChildIndex();
                        Point bedSpot = (Game1.getLocationFromName("FarmHouse") as FarmHouse).GetChildBedSpot(childIndex);

                        // // make array of all info
                        string[] dispositionArray = new string[12]{
                            age,
                            manners,
                            anxiety,
                            optimism,
                            child.Gender == 0 ? "male" : "female",
                            "not-datable",
                            "", // love interest
                            "Town",
                            child.Birthday_Season.ToLower() + " " + child.Birthday_Day.ToString(),
                            parentage,
                            "FarmHouse 3 3", // this will reset the child into the FarmHouse at day start
                            childName
                        };

                        data[child.Name] = String.Join("/", dispositionArray);
                    }
                }
                else if (asset.AssetNameEquals("Characters/Dialogue/rainy"))
                {
                    // load in defaults
                    listAdditionShell rainyDialogue = this.Helper.Data.ReadJsonFile<listAdditionShell>("assets/defaults/text/list_additions/rainy_dialogue.json");
                    for (int i = 0; i < rainyDialogue.additions.Count<listAdditionKeeper>(); i++)
                    {
                        listAdditionKeeper entry = rainyDialogue.additions[i];
                        data[entry.templateName] = entry.templateValue;
                    }

                    // for individual children
                    foreach (string childName in playerChildren)
                    {
                        if (childName == "") { continue; } // blank
                        NPC child = Game1.getCharacterFromName(childName, mustBeVillager: false);

                        if (!DataGetters.getCanTalk(child as Child)) // can they talk?
                        {
                            continue; // skip this child, they cannot talk yet
                        }

                        string childRainyDialogue = null;
                        string dataEntryLabel = DataGetters.getModderControl(child as Child, "dialogue");
                        if (data.TryGetValue(dataEntryLabel, out string result))
                        {
                            childRainyDialogue = result;
                        }

                        data[childName] = childRainyDialogue;
                    }
                }
                else if (asset.AssetName.Contains("Characters") && asset.AssetName.Contains("schedules"))
                {
                    int lastIndexOfSlash = asset.AssetName.LastIndexOf("/");
                    lastIndexOfSlash = lastIndexOfSlash < 0 ? asset.AssetName.LastIndexOf("\\") : lastIndexOfSlash;
                    string character = asset.AssetName.Substring(lastIndexOfSlash + 1);

                    if (ConfigsMain.vanillaChars.Contains(character)) { return; } // not concerned with vanilla characters

                    NPC test = Game1.getCharacterFromName(character, mustBeVillager: false);

                    if (test != null && test is Child &&
                        (DataGetters.getChildStage(test as Child) > 3 || (DataGetters.getChildStage(test as Child) == 3 & toddlerSchedules)))
                    {
                        return; // only concerned with child or older Child objects [or scheduled toddlers]
                    }

                    // was this child able to get a schedule?
                    string schedLogic = DataGetters.getModderControl(test as Child, "schedule");
                    if (schedLogic == null)
                    {
                        return; // do nothing
                    }

                    data = Game1.content.Load<Dictionary<string, string>>($"Characters//schedules//{schedLogic}");
                }
            }

        }
        public bool CanLoad<T>(IAssetInfo asset)
        {
            if (!Context.IsWorldReady) // do not attempt until all world data is ready
            {
                return false;
            }

            if (asset.AssetName.Contains("SPDEFAULT")) // "nuclear option" textures
            {
                return true;
            }

            if (!(asset.AssetName.Contains("Characters") && asset.AssetName.Contains("schedules"))) // only need to edit/load child schedules
            {
                return false;
            }


            int lastIndexOfSlash = asset.AssetName.LastIndexOf("/");
            lastIndexOfSlash = lastIndexOfSlash < 0 ? asset.AssetName.LastIndexOf("\\") : lastIndexOfSlash;
            string character = asset.AssetName.Substring(lastIndexOfSlash + 1);

            if (ConfigsMain.vanillaChars.Contains(character)) { return false; } // not concerned with vanilla characters

            NPC test = Game1.getCharacterFromName(character, mustBeVillager: false);
            if (test != null && test is Child && DataGetters.getChildStage(test as Child) > 3) // only concerned with child or older Child objects
            {
                return true;
            }

            return false;
        }
        public T Load<T>(IAssetInfo asset)
        {
            if (asset.AssetName.Contains("SPDEFAULT"))
            {
                int startNameParameter = asset.AssetName.IndexOf("SPDEFAULT");
                if (startNameParameter < 0) // SPDEFAULT not found
                {
                    throw new InvalidOperationException($"Unexpected asset '{asset.AssetName}'.");
                }

                // get the asset path
                string assetPath = ConfigsMain.assetToFilePath(asset.AssetName);
                if (assetPath == null)
                {
                    throw new InvalidOperationException($"Could not parse file path for asset '{asset.AssetName}'.");
                }

                // load the asset
                try
                {
                    return this.Helper.Content.Load<T>(assetPath, ContentSource.ModFolder);
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Could not parse expected SPDEFAULT asset named '{asset.AssetName}' with error:\n{ex}", LogLevel.Error);
                    throw new InvalidOperationException($"Could not parse SPDEFAULT asset '{asset.AssetName}'.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unexpected asset '{asset.AssetName}'.");
            }
        }


        /*********
        ** /// NPC AGING
        *********/

        public static void generalAgeUp(Child child, int newStage) // performs operations involving setting age
        {
            // set age variables
            child.modData[ConfigsMain.dataModdedAge] = newStage > 3 ? "3" : newStage.ToString(); // set to stage, up to max vanilla stage
            child.modData[ConfigsMain.dataChildStage] = newStage.ToString(); // set Child's stage to new value
        }

        public static void ageToChild(Child child) // age from toddler to "child"
        {
            generalAgeUp(child, 4);

            // deal with hat
            MiscInteractive.ManageHats.safelyManageHat(child);

            // schedule
            try
            {
                child.Schedule = child.getSchedule(Game1.dayOfMonth);
            }
            catch (Exception ex)
            {
                GeneralMonitor.Log("Issue with loading aged up child schedule:\n" + ex, LogLevel.Error);
            }

        }

        public static Dictionary<string, Action<Child>> ageupFunctions = new Dictionary<string, Action<Child>>()
        {
            { "baby", (param) => { generalAgeUp(param, 1); } },
            { "crawler", (param) => { generalAgeUp(param, 2); } },
            { "toddler", (param) => { generalAgeUp(param, 3); } },
            { "child", (param) => { ageToChild(param); } }
        };

        public void calculateAgeUp(Child child)
        {
            int ageDaysTomorrow = child.daysOld.Value + 1;
            int ageStageNow = DataGetters.getChildStage(child);

            int ageStageTomorrowKey = ageupBrackets.Keys.ToList().FindLast(item => item <= ageDaysTomorrow); // at what transitional age did/does the child's correct stage start?
            int ageStageTomorrow = ageupBrackets.Keys.ToList().FindLastIndex(item => item <= ageDaysTomorrow); // at what 0-based index is the child's correct stage info stored?

            if (ageStageTomorrow <= ageStageNow) // if it's equal, we're in the right; if they have prematurely aged up, just let it be
            {
                return; // nothing to do
            }
            else // this child needs to transition ages
            {
                string stageNameTomorrow = ageupBrackets[ageStageTomorrowKey];

                Monitor.Log($"Aging {child.Name} up to {stageNameTomorrow} stage.", LogLevel.Info);

                ageupFunctions[stageNameTomorrow](child); // call age appropriate function
                verifyLogicPaths(child, stageNameTomorrow); // call logic path verifier
            }
        }

        public void verifyLogicPaths(Child child, string newStage) // make sure the Child's logic paths are valid for its age group
        {

            // determine if any mod info needs reassigning
            string[] dataSubtypes = new string[5] { "base", "texture", "dialogue", "schedule", "gifts" };
            foreach (string subtype in dataSubtypes)
            {
                if (!allLogicPaths[subtype][newStage].Contains(DataGetters.getModderControl(child, subtype))) // is this a valid path for this age group?
                {
                    child.modData[DataGetters.getStringNameFromSubtype(subtype)] = null;
                }
            }

            // reassign any null values
            AddData.assignLogicPaths(child, this.Monitor);

            // refresh caches
            Monitor.Log("Invalidating caches to reload new data for " + child.Name + "'s age up to child stage.", LogLevel.Trace);

            Helper.Content.InvalidateCache("Data/NPCGiftTastes");
            Helper.Content.InvalidateCache("Data/NPCDispositions");
            Helper.Content.InvalidateCache("Characters/Dialogue/rainy");
        }

        /*********
        ** /// ASSET/"LOGIC PATH" ASSIGNMENT
        *********/

        // constants
        public static List<string> possiAges = new List<string>(){"baby", "toddler", "child", "teen", "adult"};
        public static List<string> possiGenders = new List<string>(){"boy", "girl"};

        // all available modder logic paths
        public static Dictionary<string, Dictionary<string, List<string>>> allLogicPaths = new Dictionary<string, Dictionary<string, List<string>>>()
        {
            { "base", new Dictionary<string, List<string>>()},
            { "texture", new Dictionary<string, List<string>>()},
            { "dialogue", new Dictionary<string, List<string>>()},
            { "schedule", new Dictionary<string, List<string>>()},
            { "gifts", new Dictionary<string, List<string>>()}
        };
        // currently in use modder logic paths
        public static Dictionary<string, List<string>> usedLogicPaths = new Dictionary<string, List<string>>()
        {
            { "base", new List<string>()},
            { "texture", new List<string>()},
            { "dialogue", new List<string>()},
            { "schedule", new List<string>()},
            { "gifts", new List<string>()}
        };
        // specialty listings
        public static Dictionary<string, List<string>> allOrNone = new Dictionary<string, List<string>>(); // "all or none"
        public static Dictionary<string, List<string>> usesAnimations = new Dictionary<string, List<string>>(); // must have same texture + schedule
        public static Dictionary<string, List<string>> blacklistLogicPaths = new Dictionary<string, List<string>>() // should not be used this game run, due to errors
        {
            { "base", new List<string>()},
            { "texture", new List<string>()},
            { "dialogue", new List<string>()},
            { "schedule", new List<string>()},
            { "gifts", new List<string>()}
        };
        public static List<string> disallowsToddlerSpeech = new List<string>(); // if this texture is assigned, toddler *cannot* speak [no portraits]


        // dictionaries of mod logic path -> Child utilizing it, for ease of use
        public static Dictionary<string, Dictionary<string, string>> pathToChildDict = new Dictionary<string, Dictionary<string, string>>();
        
        public static List<string> ParseModderListing(ModderLogicShell entry, IMonitor monitor, int maxKids)
        {
            entry.ModderHandle = entry.ModderHandle.Trim();

            List<string> addedPaths = new List<string>();
            for (int i = 0; i < entry.Paths.Count<ModderLogic>(); i++)
            {
                ModderLogic path = entry.Paths[i];

                try
                {
                    /// **** VERIFIY FIELDS ****///
                    // PATH NAME
                    // // can't have missing path
                    if (path.PathName == null || path.PathName.Trim() == "")
                    {
                        monitor.Log("Error in parsing logic path: Cannot parse paths with missing or empty PathName fields.", LogLevel.Debug);
                        continue; // skip
                    }

                    // // interlude: set error string start for quicker access
                    path.PathName = path.PathName.Trim();
                    string errorString = $"Error in parsing logic path {entry.ModderHandle}.{path.PathName}: ";

                    // // can't have white space or numbers
                    if (path.PathName.Any(char.IsDigit) || path.PathName.Any(char.IsWhiteSpace))
                    {
                        monitor.Log(errorString + $"PathName contains numbers and/or white spaces. Omitting.", LogLevel.Debug);
                        continue; // skip
                    }

                    // ALL OR NONE
                    if (path.AllorNone && !(path.ProvidesTextures && path.ProvidesDialogue && path.ProvidesSchedule && path.ProvidesDisposition && path.ProvidesGiftTastes))
                    {
                        monitor.Log(errorString + "Cannot be 'all or none' if not all assets are provided. Omitting.", LogLevel.Debug);
                        continue; // skip
                    }

                    // VALID GENDERS
                    List<string> validGenders = new List<string>();
                    path.ValidGenders = path.ValidGenders.ToLower();
                    if (path.ValidGenders == "both")
                    {
                        validGenders.Add("girl");
                        validGenders.Add("boy");
                    }
                    else
                    {
                        string[] specGenders = path.ValidGenders.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string s in specGenders)
                        {
                            if (s.Trim() == "boy" || s.Trim() == "male") { validGenders.Add("boy"); }
                            if (s.Trim() == "girl" || s.Trim() == "female") { validGenders.Add("girl"); }
                        }
                    }
                    if (validGenders.Count == 0)
                    {
                        monitor.Log(errorString + $"ValidGenders input '{path.ValidGenders}' does not generate any matches. Defaulting to 'both'.", LogLevel.Debug);
                        validGenders.Add("girl");
                        validGenders.Add("boy");
                    }

                    // VALID PARENTS
                    List<string> validParents = new List<string>();
                    path.ValidParents = path.ValidParents.ToLower();
                    if (path.ValidParents.Contains("all"))
                    {
                        validParents.Add("all_parent");
                    }
                    else
                    {
                        string[] specParents = path.ValidParents.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int s = 0; s < specParents.Length; s++)
                        {
                            specParents[s] = specParents[s].Trim();
                            if (specParents[s].Contains("multip")) { specParents[s] = "multiplayer"; }
                        }
                        validParents.AddRange(specParents);
                        if (validParents.Contains("vanilla"))
                        {
                            validParents.Remove("vanilla");
                            validParents.AddRange(ConfigsMain.vanillaCharsLower);
                        }
                    }
                    if (validParents.Count == 0)
                    {
                        monitor.Log(errorString + $"ValidParents input '{path.ValidParents}' does not generate any matches. Defaulting to 'all'.", LogLevel.Debug);
                        validParents.Add("all_parent");
                    }

                    // VALID AGES
                    
                    List<string> validAges = new List<string>();
                    if (path.ValidAges.Contains("all"))
                    {
                        validAges.AddRange(possiAges);
                    }
                    else
                    {
                        List<string> interpretAges = new List<string>();
                        string[] specAges = path.ValidAges.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int s = 0; s < specAges.Length; s++)
                        {
                            specAges[s] = specAges[s].Trim().ToLower();
                            if (possiAges.Contains(specAges[s])) { validAges.Add(specAges[s]); }
                            else { monitor.Log(errorString + $"ValidAges term '{specAges[s]}' not recognized. Ignoring.", LogLevel.Debug); }
                        }
                    }
                    if (validAges.Count == 0)
                    {
                        monitor.Log(errorString + $"ValidAges input '{path.ValidAges}' does not generate any matches. Defaulting to 'child'.", LogLevel.Debug);
                        validAges.Add("child");
                    }

                    // VALID BIRTH ORDERS
                    List<int> possiOrders = new List<int>();
                    for (int b = 0; b < maxKids; b++)
                    {
                        possiOrders.Add(b);
                    }
                    List<int> validOrders = new List<int>();
                    if (path.ValidBirthOrder.ToLower().Contains("all")) // all ages
                    {
                        validOrders.AddRange(possiOrders);
                    }
                    else if (path.ValidBirthOrder.Contains("-")) // ranges
                    {
                        string[] birthOrderRange = path.ValidBirthOrder.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (birthOrderRange.Length != 2)
                        {
                            monitor.Log(errorString + $"ValidBirthOrder input '{path.ValidBirthOrder}' contains an invalid range. Defaulting to 'all'.", LogLevel.Debug);
                            validOrders.AddRange(possiOrders);
                        }
                        else
                        {
                            bool succeed = false;
                            try
                            {
                                birthOrderRange[0] = birthOrderRange[0].Trim();
                                birthOrderRange[1] = birthOrderRange[1].Trim();

                                if (!birthOrderRange[0].All(char.IsNumber) || !birthOrderRange[1].All(char.IsNumber))
                                {
                                    monitor.Log(errorString + $"ValidBirthOrder input '{path.ValidBirthOrder}' contains non-numeric range. Defaulting to 'all'.", LogLevel.Debug);
                                }
                                else
                                {
                                    int rangeStart = int.Parse(birthOrderRange[0]);
                                    int rangeStop = int.Parse(birthOrderRange[1]);

                                    if (rangeStart > maxKids - 1)
                                    {
                                        monitor.Log($"Warning in parsing logic path {entry.ModderHandle}.{path.PathName}: Range starts past player-config max children. Setting to max children.", LogLevel.Debug);
                                        rangeStart = maxKids - 1;
                                    }

                                    if (rangeStop > maxKids - 1)
                                    {
                                        monitor.Log($"Warning in parsing logic path {entry.ModderHandle}.{path.PathName}: Range stops past player-config max children. Setting to max children.", LogLevel.Debug);
                                        rangeStop = maxKids - 1;
                                    }

                                    for (int range = rangeStart; range < rangeStop; range++)
                                    {
                                        validOrders.Add(range);
                                    }

                                    succeed = true;
                                }

                            }
                            catch (Exception ex)
                            {
                                monitor.Log(errorString + $"ValidBirthOrder input '{path.ValidBirthOrder}' could not be parsed as a range. Defaulting to 'all'. System error:\n{ex}", LogLevel.Error);
                            }

                            if (!succeed) { validOrders.AddRange(possiOrders); }
                        }
                    }
                    else //non-ranges
                    {
                        string comparison = "equal";
                        if (path.ValidBirthOrder.Substring(0, 0).Equals(">"))
                        {
                            comparison = "greater";
                            path.ValidBirthOrder = path.ValidBirthOrder.Substring(1);
                        }
                        else if (path.ValidBirthOrder.Substring(0, 0).Equals("<"))
                        {
                            comparison = "lesser";
                            path.ValidBirthOrder = path.ValidBirthOrder.Substring(1);
                        }

                        if (!path.ValidBirthOrder.All(char.IsNumber))
                        {
                            monitor.Log(errorString + $"ValidBirthOrder input '{path.ValidBirthOrder}' is non-numeric. Defaulting to 'all'.", LogLevel.Debug);
                            validOrders.AddRange(possiOrders);
                        }
                        else
                        {
                            int orderParam = int.Parse(path.ValidBirthOrder);
                            int rangeStart = orderParam;
                            int rangeStop = orderParam;
                            if (comparison.Equals("greater"))
                            {
                                rangeStop = maxKids;
                            }
                            else if (comparison.Equals("lesser"))
                            {
                                rangeStart = maxKids;
                            }

                            monitor.Log($"Warning in parsing logic path {entry.ModderHandle}.{path.PathName}: Range stops past player-config max children. Setting to max children.", LogLevel.Debug);
                            rangeStop = maxKids - 1;

                            if (rangeStop == rangeStart)
                            {
                                validOrders.Add(rangeStop);
                            }
                            else
                            {
                                for (int range = rangeStart; range < rangeStop; range++)
                                {
                                    validOrders.Add(range);
                                }
                            }
                        }
                    }
                    if (validOrders.Count == 0)
                    {
                        monitor.Log(errorString + $"ValidBirthOrder input '{path.ValidBirthOrder}' does not generate any matches. Defaulting to 'all'.", LogLevel.Debug);
                        validOrders.AddRange(possiOrders);
                    }

                    /// **** INDEX LOGIC PATH ****///
                    List<string> allKeys = validGenders;
                    allKeys.AddRange(validParents);
                    allKeys.AddRange(validAges);
                    allKeys.AddRange(validOrders.ConvertAll<string>(x => "birth" + x.ToString()));

                    if (path.AllorNone)
                    {
                        foreach (string valid in allKeys)
                        {
                            if (!allOrNone.ContainsKey(valid))
                            {
                                allOrNone[valid] = new List<string>() { path.PathName };
                            }
                            else
                            {
                                allOrNone[valid].Add(path.PathName);
                            }
                        }
                    }
                    else
                    {
                        foreach (string valid in allKeys)
                        {
                            if (path.ProvidesSchedule)
                            {
                                if (!allLogicPaths["schedule"].ContainsKey(valid)) { allLogicPaths["schedule"].Add(valid, new List<string>() { path.PathName }); }
                                else { allLogicPaths["schedule"][valid].Add(path.PathName); }
                            }
                            
                            if (path.ProvidesTextures)
                            {
                                if (!allLogicPaths["texture"].ContainsKey(valid)) { allLogicPaths["texture"].Add(valid, new List<string>() { path.PathName }); }
                                else { allLogicPaths["texture"][valid].Add(path.PathName); }
                            }

                            if (path.ProvidesDialogue)
                            {
                                if (!allLogicPaths["dialogue"].ContainsKey(valid)) { allLogicPaths["dialogue"].Add(valid, new List<string>() { path.PathName }); }
                                else { allLogicPaths["dialogue"][valid].Add(path.PathName); }
                            }

                            if (path.ProvidesDisposition)
                            {
                                if (!allLogicPaths["base"].ContainsKey(valid)) { allLogicPaths["base"].Add(valid, new List<string>() { path.PathName }); }
                                else { allLogicPaths["base"][valid].Add(path.PathName); }
                            }

                            if (path.ProvidesGiftTastes)
                            {
                                if (!allLogicPaths["gifts"].ContainsKey(valid)) { allLogicPaths["gifts"].Add(valid, new List<string>() { path.PathName }); }
                                else { allLogicPaths["gifts"][valid].Add(path.PathName); }
                            }

                            if (path.ProvidesAnimation)
                            {
                                if (!usesAnimations.ContainsKey(valid)) { usesAnimations[valid] = new List<string>() { path.PathName }; }
                                else { usesAnimations[valid].Add(path.PathName); }
                            }                            
                        }
                    }

                    /// **** ADD TO EASE-OF-ACCESS CONTROL DICTIONARIES ****///
                    pathToChildDict.Add(path.PathName, new Dictionary<string, string>());
                    if (path.ProvidesTextures || path.AllorNone) { pathToChildDict[path.PathName].Add("texture", null); }
                    if (path.ProvidesDialogue || path.AllorNone) { pathToChildDict[path.PathName].Add("dialogue", null); }
                    if (path.ProvidesDisposition || path.AllorNone) { pathToChildDict[path.PathName].Add("base", null); }
                    if (path.ProvidesGiftTastes || path.AllorNone) { pathToChildDict[path.PathName].Add("gifts", null); }
                    if (!path.AllowsToddlerSpeech) { disallowsToddlerSpeech.Add(path.PathName); }


                    // Logging
                    monitor.Log($"Finished parsing logic path {entry.ModderHandle}.{path.PathName}.", LogLevel.Trace);
                    addedPaths.Add(path.PathName);
                }
                catch (Exception ex)
                {
                    string modderHandleDisp = entry.ModderHandle;
                    if (modderHandleDisp == null) { modderHandleDisp = ""; }
                    else { modderHandleDisp = modderHandleDisp.Trim(); }

                    string pathNameDisp = path.PathName;
                    if (pathNameDisp == null) { pathNameDisp = ""; }
                    else { pathNameDisp = pathNameDisp.Trim(); }

                    monitor.Log($"Unknown critical error in parsing logic path {modderHandleDisp}.{pathNameDisp}:\n{ex}", LogLevel.Error);
                }

            }

            return addedPaths;
        }
        public static void validateAllCatsExist(int maxKids)
        {
            // make list of types of paths
            List<string> allSubtypes = new List<string>()
            {
                "base", "texture", "dialogue", "schedule", "gifts"
            };

            // make list of categories
            List<string> allCategories = new List<string>();
            for (int b = 0; b < maxKids; b++)
            {
               allCategories.Add("birth" + b.ToString()); // add all birth orders possible under this config
            }
            allCategories.AddRange(possiGenders); // add genders
            allCategories.AddRange(possiAges); // add stages

            foreach (string index2 in allCategories)
            {
                foreach (string index1 in allSubtypes)
                {
                    if (!allLogicPaths[index1].TryGetValue(index2, out List<string> output)) // if this index doesn't exist...
                    {
                        allLogicPaths[index1][index2] = new List<string>(); // ... create it.
                    }
                }
            }
        }
    }
}