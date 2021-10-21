using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StoryProgression.Configs;
using StoryProgression.Calculations;
using StoryProgression.ManageData;
using StardewValley.Locations;

namespace StoryProgression.Patches
{
    class NPCMethods
    {
        //**** NPC CLASS METHODS
        [HarmonyPatch(typeof(NPC), "isVillager")]
        [HarmonyPatch("isVillager")]
        [HarmonyPriority(Priority.High)]
        class newIsVillager : PatchMaster
        {
            public static void Postfix(StardewValley.NPC __instance, ref bool __result)
            {
                //bool originalResult = __result;
                if (__instance is Child)
                {
                    Child currentChild = __instance as Child;
                    try
                    {
                        int childStage = getChildStage(currentChild);

                        if (childStage >= 4 || (childStage == 3 && ModEntry.toddlerSchedules)) // Child object is "child" age or older, or toddler with schedule
                        {
                            __result = true;
                        }

                        // success
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"Failed in isVillager alternative:\n{ex}", LogLevel.Error);
                        // failure: run original logic
                        //__result = originalResult;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("isMarried")]
        [HarmonyPriority(Priority.High)]
        class newIsMarried : PatchMaster
        {
            public static bool Prefix(StardewValley.NPC __instance, ref bool __result)
            {
                // true NPCs
                if (!(__instance is Child)) { return true; } // original logic

                int childStage = getChildStage(__instance as Child);

                string tempMarryRaw = __instance.modData.TryGetValue(ConfigsMain.tempSetMarried, out string value1) ? value1 : "";

                if (tempMarryRaw.ToLower().Equals("true"))
                {
                    __result = true; // set to true
                    return false; //success
                }

                return true; // return to original logic
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("getSpouse")]
        [HarmonyPriority(Priority.High)]
        class newGetSpouse : PatchMaster
        {
            public static bool Prefix(StardewValley.NPC __instance, ref Farmer __result)
            {
                // true NPCs
                if (!(__instance is Child)) { return true; } // original logic

                int childStage = getChildStage(__instance as Child);

                if (__instance.isMarried()) // returns true when parent is set to "spouse" in select cases
                {
                    try
                    {
                        Farmer parent = Game1.getFarmerMaybeOffline(long.Parse(__instance.modData[ConfigsMain.dataParent1ID]));
                        __result = parent;
                        return false; // success
                    }
                    catch
                    {
                        Monitor.Log("Could not find parent as subsitute for getSpouse() for " + __instance.Name, LogLevel.Error);
                        return true; // return to original logic
                    }
                }

                return true; // return to original logic
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("getHome")]
        [HarmonyPriority(Priority.High)]
        class newGetHome : PatchMaster
        {
            public static void Postfix(NPC __instance, ref GameLocation __result)
            {
                if (__instance is Child)
                {
                    Child currentChild = __instance as Child;
                    try
                    {
                        __result = Utility.getHomeOfFarmer(Game1.player);

                        // LATER: alternative living locations
                        
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"Failed to re-define Child's getHome result:\n{ex}", LogLevel.Error);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("reloadData")]
        [HarmonyPriority(Priority.High)]
        class newReloadData : PatchMaster
        {
            public static bool Prefix(NPC __instance)
            {
                // *** DO NOT ATTEMPT TO ACCESS MOD DATA BEFORE GAME IS FULLY LOADED
                if (!Context.IsWorldReady)
                {
                    return true;
                }

                // *** HANDLE TRUE NPCS
                if (!(__instance is Child))
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE BABIES AND TODDLERS
                int childStage = getChildStage(__instance as Child);
                if (childStage <= 3) // Child object is "toddler" age or younger
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE OLDER "CHILDREN"
                try
                {
                    // potentially updated by non-dynamic disposition control
                    string Manners = null;
                    string SocialAnxiety = null;
                    string Optimism = null;

                    Dictionary<string, string> NPCDispositions = Game1.content.Load<Dictionary<string, string>>("Data\\NPCDispositions");
                    // TO DO: make this conditional on whether the user config states dynamic personalities or not
                    // // or better yet, just draw directly from some modData somewhere?
                    string controlDisposition = DataGetters.getModderControl(__instance as Child, "base");
                    if (controlDisposition != null && NPCDispositions.ContainsKey(controlDisposition))
                    {
                        // copied from vanilla code
                        string[] dataSplit = NPCDispositions[__instance.Name].Split('/');
                        Manners = dataSplit[1];
                        SocialAnxiety = dataSplit[2];
                        Optimism = dataSplit[3];
                    }
                    else
                    {
                        Manners = __instance.modData.TryGetValue(ConfigsMain.dataManners, out string value2) ?
                                            __instance.modData[ConfigsMain.dataManners] : null;
                        SocialAnxiety = __instance.modData.TryGetValue(ConfigsMain.dataAnxiety, out string value3) ?
                                            __instance.modData[ConfigsMain.dataAnxiety] : null;
                        Optimism = __instance.modData.TryGetValue(ConfigsMain.dataOptimism, out string value4) ?
                                            __instance.modData[ConfigsMain.dataOptimism] : null;
                    }

                    // always determined dynamically
                    if (childStage == 5) { __instance.Age = StardewValley.NPC.teen; }
                    else if (childStage == 6) { __instance.Age = StardewValley.NPC.adult; }
                    else { __instance.Age = StardewValley.NPC.child; }

                    // polite vs. neutral vs. rude
                    int MannersNum = 0;
                    if (Manners != null)
                    {
                        if (Manners.ToLower().Equals("polite")) { MannersNum = 1; }
                        else if (Manners.ToLower().Equals("rude")) { MannersNum = 2; }
                    }
                    __instance.Manners = MannersNum;

                    // shy vs. neutral vs. outgoing
                    __instance.SocialAnxiety = (SocialAnxiety != null && SocialAnxiety.ToLower().Equals("shy")) ? 1 : 0;

                    // positive vs. neutral vs. negative
                    __instance.Optimism = (Optimism != null && Optimism.ToLower().Equals("negative")) ? 1 : 0;

                    // gender is already set

                    // your own children are not datable
                    __instance.datable.Value = false;

                    // no static love interest

                    // home region is "Town"
                    __instance.homeRegion = 2;

                    // birthday/season is already set

                    // set ID to number of NPCs, + 200 [for padding], + birth order
                    int birthOrder = 0;
                    try { birthOrder = int.Parse(__instance.modData[ConfigsMain.dataBirthOrder]); }
                    catch { }
                    birthOrder = (birthOrder == 0) ? (__instance as Child).daysOld.Value : birthOrder;

                    __instance.id = NPCDispositions.Count + 200 + birthOrder;

                    // display name is already set
                    __instance.displayName = __instance.Name;

                    // reload default location
                    string livesLocationName = __instance.modData.TryGetValue(ConfigsMain.dataLivesHome, out string value5) ?
                                                    __instance.modData[ConfigsMain.dataLivesHome] : "farm";
                    // LATER: functionality for other living locations
                    //if (livesLocationName.Equals("farm"))
                    //{
                    __instance.DefaultMap = "FarmHouse";
                    int childIndex = (__instance as Child).GetChildIndex();

                    Point bedSpot = (Game1.getLocationFromName("FarmHouse") as StardewValley.Locations.FarmHouse).GetChildBedSpot(childIndex);
                    __instance.DefaultPosition = new Vector2(bedSpot.X, bedSpot.Y);
                    //}

                    return false; // success
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in Child reloadData alternative:\n{ex}", LogLevel.Error);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("getTextureNameForCharacter")]
        [HarmonyPriority(Priority.High)]
        class newGetTexture : PatchMaster
        {
            public static bool Prefix(string character_name, ref string __result)
            {
                try
                {
                    string[] playerChildren = DataGetters.getChildrenList(Game1.player);

                    if (playerChildren.Length == 0)
                    {
                        // no children documented
                        return true; // run original logic
                    }
                    if (playerChildren.Contains(character_name))
                    {
                        NPC child = Game1.getCharacterFromName(character_name, mustBeVillager: false);

                        string textureLogic = child.modData[ConfigsMain.dataTexture]; // this was already confirmed correct at start-up

                        if (isUsingDefault(child as Child, "texture"))
                        {
                            textureLogic = ConfigsMain.childToAsset(child as Child, true);
                        }

                        __result = textureLogic;
                        return false; // success: do not run original logic
                    }
                    else
                    {
                        // this is not a child
                        return true; // run original logic
                    }
                }
                catch
                {
                    Monitor.Log("Could not parse texture data for character " + character_name, LogLevel.Error);
                    return true; // run original logic
                }
            }
        }

        /*** SCHEDULING ***/
        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("getMasterScheduleRawData")]
        [HarmonyPriority(Priority.High)]
        class newGetSchedule : PatchMaster
        {
            public static bool Prefix(NPC __instance, ref Dictionary<string, string> __result)
            {

                // *** DO NOT ATTEMPT TO ACCESS MOD DATA BEFORE GAME IS FULLY LOADED
                if (!Context.IsWorldReady)
                {
                    return true;
                }

                // *** HANDLE TRUE NPCS
                if (!(__instance is Child))
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE BABIES AND TODDLERS
                int childStage = getChildStage(__instance as Child);
                if (childStage <= 2 || (childStage == 3 && !ModEntry.toddlerSchedules)) // Child object is "crawler" age or younger, or non-scheduled toddler
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE OLDER "CHILDREN"
                try
                {
                    string modderControl = getModderControl(__instance as Child, "schedule");
                    try
                    {
                        // LATER: compat with SDV 1.6 assets
                        __result = OverallScope.Content.Load<Dictionary<string, string>>($"Characters\\schedules\\{modderControl}", ContentSource.GameContent);
                    }
                    catch
                    {
                        Monitor.Log($"Unable to load schedule for control logic path {modderControl}");
                        __result = null;
                    }


                    return false; // success
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in Child GetMasterScheduleRawData:\n{ex}", LogLevel.Error);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("getMasterScheduleEntry")]
        [HarmonyPriority(Priority.High)]
        class newGetScheduleEntry : PatchMaster
        {
            public static bool Prefix(NPC __instance, ref string __result, string schedule_key, ref string ____lastLoadedScheduleKey)
            {

                // *** DO NOT ATTEMPT TO ACCESS MOD DATA BEFORE GAME IS FULLY LOADED
                if (!Context.IsWorldReady)
                {
                    return true;
                }

                // *** HANDLE TRUE NPCS
                if (!(__instance is Child))
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE BABIES AND TODDLERS
                int childStage = getChildStage(__instance as Child);
                if (childStage <= 2 || (childStage == 3 && !ModEntry.toddlerSchedules)) // Child object is "crawler" age or younger, or non-scheduled toddler
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE OLDER "CHILDREN"
                try
                {
                    string modderControl = getModderControl(__instance as Child, "schedule");
                    Dictionary<string, string> schedule = OverallScope.Content.Load<Dictionary<string, string>>($"Characters\\schedules\\{modderControl}", ContentSource.GameContent);

                    // COPIED FROM VANILLA CODE
                    if (schedule == null)
                    {
                        throw new KeyNotFoundException("The schedule file for NPC '" + __instance.Name + "' could not be loaded...");
                    }
                    if (schedule.TryGetValue(schedule_key, out var data))
                    {
                        ____lastLoadedScheduleKey = schedule_key;
                        __result = data;
                        return false; // success
                    }
                    throw new KeyNotFoundException("The schedule file for NPC '" + __instance.Name + "' has no schedule named '" + schedule_key + "'.");
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in Child GetMasterScheduleEntry:\n{ex}", LogLevel.Error);
                    return true;
                }
            }

            public static void Postfix()
            {

            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("parseMasterSchedule")]
        [HarmonyPriority(Priority.High)]
        class newParseMasterSched : PatchMaster
        {

            public static bool Prefix(NPC __instance, ref string rawData)
            {
                // set child to use spousal logic (for leaving farmhouse)
                if (__instance is Child)
                {
                    __instance.modData[ConfigsMain.tempSetMarried] = "true";
                } else
                {
                    return true; // skip this evaluation for non-Child NPCs
                }

                Monitor.Log(rawData, LogLevel.Warn); // TO DO: remove

                // parse schedule string
                if (rawData == null)
                {
                    return true; // let vanilla method deal with this
                }
                else if (rawData.Split('/').ToList().Count == 0) // no data here anyway
                {
                    return true; // let vanilla method deal with this
                }
                // search for any custom fields
                List<string> split = rawData.Split('/').ToList();
                List<string> splitReform = new List<string>();
                int i = 0; // TO DO: remove
                foreach (string entry in split)
                {
                    if (entry.Contains("WAKEUP"))
                    {
                        i++;
                        Monitor.Log("Detecting a WAKEUP string at position " + i.ToString(), LogLevel.Warn);
                        // regardless, this needs to be removed
                        // // so, not adding to list

                        // parse
                        string wakeupTime = entry.Split(' ')[0];
                        bool validWakeupTime = int.TryParse(wakeupTime, out int parsedWakeupTime);
                        if (!validWakeupTime)
                        {
                            Monitor.Log("Could not parse wakeup string for " + __instance.Name + ": '" + entry + "'. Skipping.", LogLevel.Debug);
                        }
                        else
                        {
                            __instance.modData[ConfigsMain.scheduleWakeUp] = parsedWakeupTime.ToString();
                        }
                    }
                    else if (entry.Contains("SLEEP"))
                    {
                        i++;
                        Monitor.Log("Detecting a SLEEP string at position " + i.ToString(), LogLevel.Warn);
                        // regardless, this needs to be removed
                        // // so, not adding to list

                        // parse
                        string GoSleepTime = entry.Split(' ')[0];
                        bool validGoSleepTime = int.TryParse(GoSleepTime, out int parsedGoSleepTime);
                        if (!validGoSleepTime)
                        {
                            Monitor.Log("Could not parse bedtime string for " + __instance.Name + ": '" + entry + "'. Skipping.", LogLevel.Debug);
                        }
                        else
                        {
                            __instance.modData[ConfigsMain.scheduleGoSleep] = parsedGoSleepTime.ToString();
                        }
                    }
                    else if (entry.Contains("HOME"))
                    {
                        i++;
                        Monitor.Log("Detecting a HOME string at position " + i.ToString(), LogLevel.Warn);

                        // LATER: more dynamic than just assuming farmhouse is home
                        string GoHomeLocation = "BusStop -1 23 3";

                        string GoHomeTime = entry.Split(' ')[0];
                        bool validGoHomeTime = int.TryParse(GoHomeTime, out int parsedGoHomeTime);
                        if (!validGoHomeTime)
                        {
                            Monitor.Log("Could not parse return home string for " + __instance.Name + ": '" + entry + "'. Skipping.", LogLevel.Debug);
                        }
                        else
                        {
                            string newEntry = GoHomeTime + " " + GoHomeLocation;
                            splitReform.Add(newEntry);
                        }
                    }
                    else // this is a "normal" entry
                    {
                        i++;
                        Monitor.Log("Detecting a normal string at position " + i.ToString(), LogLevel.Warn);

                        splitReform.Add(entry);
                    }
                }


                // piece it back together
                //rawData = String.Join("/", split.ToArray());
                rawData = String.Join("/", splitReform.ToArray());

                Monitor.Log("NEW RAW DATA: " + rawData, LogLevel.Warn);

                return true; // return original logic regardless

                //List<string> splitRemoves = new List<string>();
                //foreach (string entry in split)
                //{
                //    if (entry.Contains("WAKEUP"))
                //    {
                //        // regardless, this needs to be removed
                //        splitRemoves.Add(entry);

                //        // parse
                //        string wakeupTime = entry.Split(' ')[0];
                //        bool validWakeupTime = int.TryParse(wakeupTime, out int parsedWakeupTime);
                //        if (!validWakeupTime)
                //        {
                //            Monitor.Log("Could not parse wakeup string for " + __instance.Name + ": '" + entry + "'. Skipping.", LogLevel.Debug);
                //        }
                //        else
                //        {
                //            __instance.modData[ConfigsMain.scheduleWakeUp] = parsedWakeupTime.ToString();
                //        }
                //    }

                //    if (entry.Contains("SLEEP"))
                //    {
                //        // regardless, this needs to be removed
                //        splitRemoves.Add(entry);

                //        // parse
                //        string GoSleepTime = entry.Split(' ')[0];
                //        bool validGoSleepTime = int.TryParse(GoSleepTime, out int parsedGoSleepTime);
                //        if (!validGoSleepTime)
                //        {
                //            Monitor.Log("Could not parse bedtime string for " + __instance.Name + ": '" + entry + "'. Skipping.", LogLevel.Debug);
                //        }
                //        else
                //        {
                //            __instance.modData[ConfigsMain.scheduleGoSleep] = parsedGoSleepTime.ToString();
                //        }
                //    }
                //}

                //// remove any custom entries
                //if (splitRemoves.Count > 0)
                //{
                //    split = split.Except(splitRemoves).ToList();
                //}
            }
            public static void Postfix(NPC __instance)
            {

                // unset child to use spousal logic

                if (__instance is Child)
                {
                    __instance.modData[ConfigsMain.tempSetMarried] = "false";
                }
            }
        }

        /*** DIALOGUE ***/

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("Dialogue", MethodType.Getter)]
        [HarmonyPriority(Priority.High)]
        class newGetDialogue : PatchMaster
        {
            public static bool Prefix(NPC __instance, ref Dictionary<string, string> __result)
            {

                // *** HANDLE TRUE NPCS
                if (!(__instance is Child))
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE BABIES AND TODDLERS
                int childStage = getChildStage(__instance as Child);
                if (!getCanTalk(__instance as Child)) // Child cannot speak yet
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE OLDER "CHILDREN"

                string modderControl = getModderControl(__instance as Child, "dialogue");

                if (modderControl == null)
                {
                    Monitor.Log("Could not find modder control path to establish dialogue for child " + __instance.Name, LogLevel.Error);
                    __result = new Dictionary<string, string>();
                    return false; // returning null may produce unexpected results with NPC behaviors
                }

                string dialogue_file = "Characters\\Dialogue\\" + modderControl; // LATER: SDV 1.6 compat

                // attempt to get dialogue
                try
                {
                    // COPIED from vanilla code
                    Dictionary<string, string> outputDict = Game1.content.Load<Dictionary<string, string>>(dialogue_file).Select(delegate (KeyValuePair<string, string> pair)
                    {
                        string key = pair.Key;
                        string text = pair.Value;
                        if (text.Contains("¦"))
                        {
                            text = ((!Game1.player.IsMale) ? text.Substring(text.IndexOf("¦") + 1) : text.Substring(0, text.IndexOf("¦")));
                        }

                        text = MiscUtility.fillDefaultText(text, __instance); // fill "XXNPCXX" etc.

                        return new KeyValuePair<string, string>(key, text);
                    }).ToDictionary((KeyValuePair<string, string> p) => p.Key, (KeyValuePair<string, string> p) => p.Value);

                    __result = outputDict;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Could not parse dialogue data for modder control path {modderControl} for child { __instance.Name}, with error:\n{ex}", LogLevel.Error);

                    // try to get this child dialogue moving forward
                    string newDialoguePath = AddData.selectLogicPath((Child)__instance, "dialogue");
                    AddData.updateLogicPaths((Child)__instance, "dialogue", newDialoguePath, true);

                    __result = new Dictionary<string, string>();
                }

                return false; // don't run: returning null may produce unexpected results with NPC behaviors
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("GetDialogueSheetName")]
        [HarmonyPriority(Priority.High)]
        class newGetDialogueSheet : PatchMaster
        {
            public static bool Prefix(NPC __instance, ref string __result)
            {
                // *** DO NOT ATTEMPT TO ACCESS MOD DATA BEFORE GAME IS FULLY LOADED
                if (!Context.IsWorldReady)
                {
                    return true;
                }

                // *** HANDLE TRUE NPCS
                if (!(__instance is Child))
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE BABIES AND TODDLERS
                int childStage = getChildStage(__instance as Child);
                if (childStage <= 3) // Child object is "toddler" age or younger
                {
                    // maintain original method/logic
                    return true;
                }

                // *** HANDLE OLDER "CHILDREN"
                try
                {
                    __result = getModderControl(__instance as Child, "dialogue");
                    return true;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in Child GetDialogueSheet:\n{ex}", LogLevel.Error);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("receiveGift")]
        [HarmonyPriority(Priority.High)]
        class newReceiveObject : PatchMaster
        {
            public static bool Prefix(NPC __instance, Farmer giver, out Dictionary<string, string> __state)
            {
                __state = null;

                // if (__instance.modData.TryGetValue(ConfigsMain.dataGiftTastes, out string childGiftLogic))
                string childGiftLogic = getModderControl(__instance as Child, "gifts");
                if (childGiftLogic != null)
                {
                    if (Game1.NPCGiftTastes.TryGetValue(childGiftLogic, out string originalValue))
                    {
                        string newLogic = MiscUtility.fillDefaultText(originalValue, __instance);
                        Game1.NPCGiftTastes[__instance.Name] = newLogic;
                    }
                }

                return true; // continue regardless
            }

            public static void Postfix(NPC __instance, Farmer giver, Dictionary<string, string> __state)
            {
                if (__state != null) // this was a child: need to fix everything we changed
                {
                    // fix friendship data
                    giver.friendshipData[__state["child_name"]].GiftsToday = giver.friendshipData[__instance.Name].GiftsToday;
                    giver.friendshipData[__state["child_name"]].GiftsThisWeek = giver.friendshipData[__instance.Name].GiftsThisWeek;
                    giver.friendshipData[__state["child_name"]].LastGiftDate = giver.friendshipData[__instance.Name].LastGiftDate;
                    giver.friendshipData.Remove(__instance.Name); // delete the temporary entry

                    // reset the gift tastes to the original value (so can be re-parsed as necessary later)
                    Game1.NPCGiftTastes[__instance.Name] = __state["original_gift_logic"];

                    // change name back
                    __instance.Name = __state["child_name"];

                    
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("checkForNewCurrentDialogue")]
        [HarmonyPriority(Priority.High)]
        class newCheckDialogue : PatchMaster
        {
            public static bool Prefix(NPC __instance, out string __state)
            {

                __state = __instance.Name; // save the real name
                __instance.Name = getModderControl(__instance as Child, "dialogue"); // set to logic path

                return true; // continue regardless
            }

            public static void Postfix(NPC __instance, string __state)
            {

                if (__state != null) // this was a child: need to fix everything we changed
                {
                    // change name back
                    __instance.Name = __state;
                }
            }
        }

        /*** MOVEMENT ***/
        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("prepareToDisembarkOnNewSchedulePath")]
        [HarmonyPriority(Priority.High)]
        class newPrepareToDisembark : PatchMaster
        {
            public static bool Prefix(NPC __instance)
            {
                // set child to use spousal logic (for leaving farmhouse)
                if (__instance is Child)
                {
                    __instance.modData[ConfigsMain.tempSetMarried] = "true";
                }

                return true; // return original logic regardless
            }
            public static void Postfix(NPC __instance)
            {

                // unset child to use spousal logic

                if (__instance is Child)
                {
                    __instance.modData[ConfigsMain.tempSetMarried] = "false";
                }
            }
        }

        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("updateMovement")]
        [HarmonyPriority(Priority.High)]
        class newUpdateMovement : PatchMaster
        {
            public static bool Prefix(NPC __instance)
            {
                // set child to use spousal logic (for leaving farmhouse)
                if (__instance is Child)
                {
                    __instance.modData[ConfigsMain.tempSetMarried] = "true";
                }

                return true; // return original logic regardless
            }
            public static void Postfix(NPC __instance)
            {

                // unset child to use spousal logic

                if (__instance is Child)
                {
                    __instance.modData[ConfigsMain.tempSetMarried] = "false";
                }
            }
        }

        /*** PREGNANCY ***/
        [HarmonyPatch(typeof(NPC))]
        [HarmonyPatch("canGetPregnant")]
        class newCanPregnantNPC : PatchMaster
        {
            public static void Postfix(NPC __instance, ref bool __result)
            {
                if (!__result && // don't interfere with anything that has made this result true as of yet
                    !(__instance is Horse || __instance.Name.Equals("Krobus") || __instance.isRoommate())) // don't mess with these categories
                {
                    
                    // COPIED FROM VANILLA (including second half of conditional)
                    Farmer spouse = __instance.getSpouse();
                    if (spouse != null && !(bool)spouse.divorceTonight.Value)
                    {
                        FarmHouse farmHouse = Utility.getHomeOfFarmer(spouse);

                        bool foundReflection = false; // assume worst
                        bool houseReady = false;
                        try
                        {
                            Type utilType = typeof(Utility);                            
                            var housePregnantMethod = utilType.GetMethod("playersCanGetPregnantHere", BindingFlags.NonPublic | BindingFlags.Static);
                            var houseReadyCall = housePregnantMethod.Invoke(obj: null, new object[] { farmHouse });

                            houseReady = (bool)houseReadyCall;
                            foundReflection = true;
                        } catch
                        {
                            Monitor.Log("Failed to reflect to FarmHouse.playersCanGetPregnantHere. Not modifying result of NPC.canGetPregnant.", LogLevel.Error);
                        }

                        if (foundReflection && houseReady)
                        {
                            Friendship spouseFriendship = spouse.GetSpouseFriendship();
                            if (farmHouse.upgradeLevel >= 2 && spouse.getFriendshipHeartLevelForNPC(__instance.Name) >= 10 &&
                                spouseFriendship.DaysUntilBirthing < 0 && spouse.GetDaysMarried() >= 7) // LATER: this is bad for multiple spouses
                            {
                                __result = true;
                            }
                        }   
                    }
                }
            }
        }
    }
}
