using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StoryProgression.Calculations;
using StoryProgression.Configs;

namespace StoryProgression.Patches
{
    class ChildMethods
    {
        /// NON-HARMONY METHODS
        public static BedFurniture getNPCBed(NPC request_NPC, FarmHouse home, BedFurniture.BedType bed_type = BedFurniture.BedType.Any)
        {
            BedFurniture myBed = null;
            List<BedFurniture> emptyBeds = new List<BedFurniture>();

            // COPIED (mostly) FROM VANILLA FarmHouse.GetBed
            foreach (Furniture f in home.furniture)
            {
                if (!(f is BedFurniture))
                {
                    continue;
                }
                BedFurniture bed = f as BedFurniture;
                if (bed_type == BedFurniture.BedType.Any || bed.bedType == bed_type)
                {
                    bool belongsToSomeone = bed.modData.TryGetValue(ConfigsMain.furnitureBedOwner, out string bed_owner);
                    if (!belongsToSomeone)
                    {
                        emptyBeds.Add(bed);
                    }
                    else
                    {
                        if (bed_owner == request_NPC.Name)
                        {
                            myBed = bed;
                            break; // no need to look any further
                        }
                    }
                }
            }

            if (myBed == null & emptyBeds.Count > 0) // no bed assigned for this person, but there are eligible beds
            {
                myBed = emptyBeds[new Random().Next(emptyBeds.Count)]; // randomly select a bed for them
                myBed.modData[ConfigsMain.furnitureBedOwner] = request_NPC.Name; // assign this as their bed forever
            }

            // return the output
            return myBed;
        }

        ////**** VOIDS
        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("reloadSprite")]
        [HarmonyPriority(Priority.High)]
        class newChildReloadSprite : PatchMaster
        {
            public static void Postfix(Child __instance)
            {
                if (!Context.IsWorldReady) { return; } // skip for now

                try
                {

                    int childStage = DataGetters.getChildStage(__instance);

                    // find sprite/texture name for this child
                    string textureLogic = DataGetters.getModderControl(__instance, "texture");
                    string textureName = null;
                    if (textureLogic == null || isUsingDefault(__instance, "texture"))
                    {
                        textureName = ConfigsMain.childToAsset(__instance, true);
                    }
                    else
                    {
                        textureName = textureLogic;
                    }

                    __instance.Sprite = new AnimatedSprite("Characters\\" + textureName); // LATER: migrate to new SDV 1.6 logic
                    if (childStage <= 3) // tweak textures for younger children [make sure all sprite sizing is correct]
                    {
                        switch (childStage)
                        {
                            case 0:
                                __instance.Sprite.SpriteWidth = 22;
                                __instance.Sprite.SpriteHeight = 16;
                                __instance.Sprite.CurrentFrame = 0;
                                break;
                            case 1:
                                __instance.Sprite.SpriteWidth = 22;
                                __instance.Sprite.SpriteHeight = 32;
                                __instance.Sprite.CurrentFrame = 4;
                                break;
                            case 2:
                                __instance.Sprite.SpriteWidth = 22;
                                __instance.Sprite.SpriteHeight = 16;
                                __instance.Sprite.CurrentFrame = 32;
                                break;
                            case 3:
                                __instance.Sprite.SpriteWidth = 16;
                                __instance.Sprite.SpriteHeight = 32;
                                __instance.Sprite.CurrentFrame = 0;
                                break;
                        }

                        if (ModEntry.toddlerSchedules && childStage == 3)
                        {
                            __instance.Schedule = __instance.getSchedule(Game1.dayOfMonth);
                            __instance.faceDirection(__instance.DefaultFacingDirection);
                        }
                    }
                    else if (childStage > 3) // do full NPC-style logic
                    {
                        // turn on breathing
                        __instance.Breather = true;

                        // get values that typically only exist for NPCs
                        __instance.Sprite.SpriteHeight = 32;
                        __instance.resetPortrait();

                        if (Game1.newDay || Game1.gameMode == 6)
                        {
                            __instance.Schedule = __instance.getSchedule(Game1.dayOfMonth);
                            __instance.faceDirection(__instance.DefaultFacingDirection);
                            __instance.resetSeasonalDialogue();
                            __instance.resetCurrentDialogue();
                        }

                        __instance.displayName = __instance.Name;
                    }
                }
                catch (Exception ex)
                {
                    // failure: run original logic only
                    Monitor.Log("Error in alternate reloadSprite: " + ex, LogLevel.Trace);
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("MovePosition")]
        [HarmonyPriority(Priority.High)]
        class newChildMove : PatchMaster
        {
            public static bool Prefix(Child __instance, GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation)
            {
                try
                {

                    int childStage = getChildStage(__instance);

                    if (childStage <= 2 || (childStage == 3 & ModEntry.toddlerSchedules)) // Child object is "toddler" age or younger
                    {
                        // maintain original logic
                        return true;
                    }
                    else
                    {
                        // use NPC logic
                        IntPtr pointer;
                        try
                        {
                            pointer = typeof(NPC).GetMethod("MovePosition").MethodHandle.GetFunctionPointer();
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Could not locate base NPC method for MovePosition. Defaulting to Child method. Error: \n{ex}", LogLevel.Error);
                            return true;
                        }

                        Action<GameTime, xTile.Dimensions.Rectangle, GameLocation> npcMethod = (Action<GameTime, xTile.Dimensions.Rectangle, GameLocation>)Activator.CreateInstance(typeof(Action<GameTime, xTile.Dimensions.Rectangle, GameLocation>), __instance, pointer);
                        npcMethod(time, viewport, currentLocation);

                        // success
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in update() alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("update", new Type[] { typeof(GameTime), typeof(GameLocation) })]
        [HarmonyPriority(Priority.High)]
        class newChildUpdate : PatchMaster
        {
            public static bool Prefix(Child __instance, GameTime time, GameLocation location)
            {
                try
                {

                    int childStage = getChildStage(__instance);

                    if (!(childStage > 3 || (childStage == 3 && ModEntry.toddlerSchedules))) // Child object is "toddler" age or younger
                    {
                        // maintain original logic
                        return true;
                    }
                    else
                    {
                        // use NPC logic
                        IntPtr pointer;
                        try
                        {
                            pointer = typeof(NPC).GetMethod("update", new Type[] { typeof(GameTime), typeof(GameLocation) }).MethodHandle.GetFunctionPointer();
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Could not locate base NPC method for update. Defaulting to Child method. Error: \n{ex}", LogLevel.Error);
                            return true;
                        }

                        Action<GameTime, GameLocation> npcMethod = (Action<GameTime, GameLocation>)Activator.CreateInstance(typeof(Action<GameTime, GameLocation>), __instance, pointer);
                        npcMethod(time, location);

                        // success
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in update() alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("draw", new Type[] { typeof(SpriteBatch), typeof(float) })]
        [HarmonyPriority(Priority.High)]
        class newChildDraw1 : PatchMaster
        {
            public static bool Prefix(Child __instance, SpriteBatch b, float alpha)
            {
                try
                {
                    int childStage = getChildStage(__instance);

                    if (childStage <= 3) // Child object is "toddler" age or younger
                    {
                        // CONFIRM: should be OK to leave as Child method for toddlers?
                        // maintain original method / logic
                        return true;
                    }
                    else
                    {
                        // use NPC logic
                        IntPtr pointer;
                        try
                        {
                            pointer = typeof(NPC).GetMethod("draw", new Type[] { typeof(SpriteBatch), typeof(float) }).MethodHandle.GetFunctionPointer();
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log("Could not locate base NPC method for draw (with SpriteBatch and float). Defaulting to Child method. Error: \n" + ex, LogLevel.Error);
                            return true;
                        }

                        Action<SpriteBatch, float> npcMethod = (Action<SpriteBatch, float>)Activator.CreateInstance(typeof(Action<SpriteBatch, float>), __instance, pointer);
                        npcMethod(b, alpha);

                        //success
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in draw (with SpriteBatch and float) alternative:\n{ex}", LogLevel.Error);
                    //failure: run original logic
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("draw", new Type[] { typeof(SpriteBatch) })]
        [HarmonyPriority(Priority.High)]
        class newChildDraw2 : PatchMaster
        {
            public static bool Prefix(Child __instance, SpriteBatch b)
            {
                try
                {
                    int childStage = getChildStage(__instance);

                    if (childStage <= 3) // Child object is "toddler" age or younger
                    {
                        // maintain original method / logic
                        return true;
                    }
                    else
                    {
                        // use NPC logic
                        IntPtr pointer;
                        try
                        {
                            pointer = typeof(NPC).GetMethod("draw", new Type[] { typeof(SpriteBatch), typeof(float) }).MethodHandle.GetFunctionPointer();
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log("Could not locate base NPC method for draw (with SpriteBatch only). Defaulting to Child method. Error: \n" + ex, LogLevel.Error);
                            return true;
                        }

                        Action<SpriteBatch, float> npcMethod = (Action<SpriteBatch, float>)Activator.CreateInstance(typeof(Action<SpriteBatch, float>), __instance, pointer);
                        npcMethod(b, 1f);

                        // success
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in draw (with SpriteBatch only) alternative:\n{ex}", LogLevel.Error);
                    //failure: run original logic
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("behaviorOnLocalFarmerLocationEntry")]
        [HarmonyPriority(Priority.High)]
        class newChildBehaviorEntry : PatchMaster
        {
            public static bool Prefix(Child __instance, GameLocation location)
            {
                try
                {

                    int childStage = getChildStage(__instance);
                    if (!(childStage > 3 || (childStage == 3 && ModEntry.toddlerSchedules))) // Child object is "toddler" age or younger
                    {
                        // maintain original method/logic
                        return true;
                    }
                    else
                    {
                        //// use NPC logic
                        IntPtr pointer;
                        try
                        {
                            pointer = typeof(NPC).GetMethod("behaviorOnLocalFarmerLocationEntry", new Type[] { typeof(GameLocation) }).MethodHandle.GetFunctionPointer();
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Could not locate base NPC method for behaviorOnLocalFarmerLocationEntry. Defaulting to Child method. Error: \n{ex}", LogLevel.Error);
                            return true;
                        }

                        Action<GameLocation> npcMethod = (Action<GameLocation>)Activator.CreateInstance(typeof(Action<GameLocation>), __instance, pointer);
                        npcMethod(location);

                        // success
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in behaviorOnLocalFarmerLocationEntry alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    return true;
                }
            }
        }


        ////**** BOOLEANS
        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("canPassThroughActionTiles")]
        [HarmonyPriority(Priority.High)]
        class newChildPassThrough : PatchMaster
        {
            public static void Postfix(Child __instance, ref bool __result)
            {
                bool originalResult = __result;

                try
                {
                    int childStage = getChildStage(__instance);
                    if (childStage == 3 && ModEntry.toddlerSchedules)
                    {
                        __result = true; // NPC-style logic
                    }
                    else if (childStage <= 3) // do Child-style logic
                    {
                        __result = false;
                    }
                    else // do NPC-style logic
                    {
                        __result = true;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in canPassThroughActionTiles:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    __result = originalResult;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("hasSpecialCollisionRules")]
        [HarmonyPriority(Priority.High)]
        class newChildSpecialCollision : PatchMaster
        {
            public static void Postfix(Child __instance, ref bool __result)
            {
                bool originalResult = __result;

                try
                {
                    int childStage = getChildStage(__instance);
                    if (childStage == 3 && ModEntry.toddlerSchedules)
                    {
                        __result = false;
                    }
                    else if (childStage <= 3) // do Child-style logic
                    {
                        __result = true;
                    }
                    else // do NPC-style logic
                    {
                        __result = false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in hasSpecialCollissionRules alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    __result = originalResult;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("canTalk")]
        [HarmonyPriority(Priority.High)]
        class newChildCanTalk : PatchMaster
        {
            public static void Postfix(Child __instance, ref bool __result)
            {
                bool originalResult = __result;

                try
                {
                    // determine whether the child can talk based on age + texture + etc
                    if (getCanTalk(__instance))
                    {
                        __result = true; // do NPC-style logic
                    }

                    // success

                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in canTalk alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    __result = originalResult;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("isColliding")]
        [HarmonyPriority(Priority.High)]
        class newChildIsColliding : PatchMaster
        {
            public static void Postfix(Child __instance, ref bool __result, GameLocation l, Vector2 tile)
            {
                bool originalResult = __result;

                try
                {
                    int childStage = getChildStage(__instance);
                    if (childStage >= 4 || (childStage == 3 && ModEntry.toddlerSchedules)) // do NPC-style logic
                    {
                        __result = false;
                    }

                    // success

                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in isColliding alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    __result = originalResult;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("checkAction")]
        [HarmonyPriority(Priority.High)]
        class newChildCheckAction : PatchMaster
        {
            public static bool Prefix(Child __instance, Farmer who, GameLocation l)
            {
                // player has a hat in hand
                if (getChildStage(__instance) == 3 &&
                    who.Items.Count > who.CurrentToolIndex && who.Items[who.CurrentToolIndex] != null && who.Items[who.CurrentToolIndex] is Hat) // COPIED FROM VANILLA
                {
                    // let vanilla game take care of hat management
                    return true; // do original logic
                }

                // player does not have a hat in hand
                try
                {
                    if (!getCanTalk(__instance)) // younger than toddler, or toddler who can't talk
                    {
                        // maintain original method/logic
                        return true;
                    }
                    else
                    {
                        // use NPC logic
                        IntPtr pointer;
                        try
                        {
                            pointer = typeof(NPC).GetMethod("checkAction", new Type[] { typeof(Farmer), typeof(GameLocation) }).MethodHandle.GetFunctionPointer();
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log($"Could not locate base NPC method for checkAction. Defaulting to Child method. Error: \n{ex}", LogLevel.Error);
                            return true;
                        }

                        Action<Farmer, GameLocation> npcMethod = (Action<Farmer, GameLocation>)Activator.CreateInstance(typeof(Action<Farmer, GameLocation>), __instance as NPC, pointer);
                        npcMethod(who, l);

                        // success
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in checkAction alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("resetForPlayerEntry")]
        [HarmonyPriority(Priority.High)]
        class newChildResetEntry : PatchMaster
        {
            public static bool Prefix(Child __instance, GameLocation l)
            {
                try
                {

                    int childStage = getChildStage(__instance);

                    if (childStage <= 2 || (childStage == 3 && !ModEntry.toddlerSchedules)) // Child object is "toddler" age or younger
                    {
                        // maintain original method/logic
                        return true;
                    }
                    else
                    {
                        // do nothing
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in resetForPlayerEntry alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("tenMinuteUpdate")]
        [HarmonyPriority(Priority.High)]
        class newChildTenMinuteUpdate : PatchMaster
        {
            public static bool Prefix(Child __instance)
            {
                try
                {

                    int childStage = getChildStage(__instance);

                    if (childStage <= 2 || (childStage == 3 && !ModEntry.toddlerSchedules)) // Child object is "toddler" age or younger
                    {
                        // maintain original method/logic
                        return true;
                    }
                    else
                    {
                        // test for sleeping
                        bool tooEarly = __instance.modData.TryGetValue(ConfigsMain.scheduleWakeUp, out string wakeupTime) ? (Game1.timeOfDay < Convert.ToInt32(wakeupTime)) : false;
                        bool tooLate = __instance.modData.TryGetValue(ConfigsMain.scheduleGoSleep, out string bedTime) ? (Game1.timeOfDay > Convert.ToInt32(bedTime)) : false;

                        // look for and route to this Child's bed
                        if (tooLate && !__instance.modData.TryGetValue(ConfigsMain.furnitureBedCheckResult, out string hush)) // it's bedtime, and we haven't ran this check
                        {
                            // perform bed check
                            // // note: we're already in the farmhouse, as this method is only called on children in the farmhouse
                            BedFurniture myBed = getNPCBed(__instance as NPC,
                                                           __instance.currentLocation as FarmHouse,
                                                           BedFurniture.BedType.Child);

                            // mark bed check as complete
                            __instance.modData[ConfigsMain.furnitureBedCheckResult] = myBed == null ? "no" : "yes";

                            if (myBed != null) // we found a bed
                            {
                                // route to the bed
                                // COPIED (very loosely) FROM VANILLA
                                __instance.Halt();
                                Point myBedSpot = myBed.GetBedSpot();
                                int faceLeftRight = new Random().Next(0, 2) == 0 ? 1 : 3;
                                if (__instance.currentLocation.farmers.Any()) // a farmer is here
                                {
                                    // so, do a real path
                                    __instance.controller = new PathFindController(__instance, __instance.currentLocation, myBedSpot, faceLeftRight,
                                                                                delegate (Character c, GameLocation l)
                                                                                {
                                                                                    if (!(c is NPC)) { return; }
                                                                                    (c as NPC).playSleepingAnimation();
                                                                                });
                                    if (__instance.controller.pathToEndPoint == null ||
                                        !__instance.currentLocation.isTileOnMap(__instance.controller.pathToEndPoint.Last().X, __instance.controller.pathToEndPoint.Last().Y))
                                    {
                                        __instance.controller = null;
                                    }
                                }
                                else // teleport them
                                {
                                    __instance.setTilePosition(myBedSpot);
                                }

                                myBed?.ReserveForNPC(); // lock the bed
                            }
                        }

                        // useful for path finding
                        bool justWakingUp = !tooEarly && __instance.isSleeping.Value;

                        if (justWakingUp)
                        {
                            BedFurniture myBed = getNPCBed(__instance as NPC,
                                                           __instance.currentLocation as FarmHouse,
                                                           BedFurniture.BedType.Child);
                            myBed?.mutex.ReleaseLock();
                        }

                        if (tooEarly)
                        {
                            __instance.isSleeping.Value = true;
                        }
                        else if (!tooEarly && !tooLate)
                        {
                            __instance.isSleeping.Value = false;
                        }


                        // only run paths every 10th tick
                        if (!justWakingUp && (Game1.timeOfDay % 100 != 0 || __instance.isMoving()))
                        {
                            return false;
                        }

                        // are they awake,  inside the FarmHouse, with a farmer?
                        if (__instance.currentLocation is FarmHouse && __instance.currentLocation.farmers.Any() && !__instance.isSleeping.Value)
                        {
                            FarmHouse home = __instance.currentLocation as FarmHouse;
                            Random r = new Random((int)Game1.stats.DaysPlayed + (int)((int)Game1.uniqueIDForThisGame % __instance.daysOld.Value) + Game1.random.Next(0, 10)); // adapted from vanilla

                            __instance.controller = new PathFindController(__instance, home, home.getRandomOpenPointInHouse(r, 1), r.Next(0, 4), null);
                            if (__instance.controller.pathToEndPoint == null || !home.isTileOnMap(__instance.controller.pathToEndPoint.Last().X, __instance.controller.pathToEndPoint.Last().Y))
                            {
                                Monitor.Log(__instance.Name + " was assigned a bad bath with FarmHouse. Clearing path controller.", LogLevel.Warn);
                                __instance.controller = null;
                            }
                        }

                        // do not do original logic
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in tenMinuteUpdate alternative:\n{ex}", LogLevel.Error);
                    // failure: run original logic
                    return true;
                }
            }
        }

        ////** INTEGERS
        [HarmonyPatch(typeof(Child))]
        [HarmonyPatch("GetChildIndex")]
        [HarmonyPriority(Priority.High)]
        class newGetChildIndex : PatchMaster
        {
            public static void Postfix(Child __instance, ref int __result)
            {

                //string homeDetail = __instance.modData.TryGetValue(ConfigsMain.dataLivesHome, out string value) ? __instance.modData[ConfigsMain.dataLivesHome] : "farm";
                if (__instance.modData.TryGetValue(ConfigsMain.dataBirthOrder, out string value))
                {
                    try
                    {
                        int birthOrder = int.Parse(__instance.modData[ConfigsMain.dataBirthOrder]);
                        __result = birthOrder;
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"Failed in getChildIndex alternative:\n{ex}", LogLevel.Error);
                    }
                }
            }
        }
    }
}
