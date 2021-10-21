using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StoryProgression.Configs;
using StoryProgression.Calculations;
using StardewValley.Network;

namespace StoryProgression.Patches
{
    class PathFinderMethods
    {

        [HarmonyPatch(typeof(PathFindController))]
        [HarmonyPatch("handleWarps")]
        [HarmonyPriority(Priority.High)]
        class newHandleWarps : PatchMaster
        {
            public static bool Prefix(ref PathFindController __instance, ref Character ___character)
            {
                // set child to use spousal logic (for leaving farmhouse)
                if (___character is Child)
                {
                    ___character.modData[ConfigsMain.tempSetMarried] = "true";
                }

                return true; // return original logic regardless
            }
            public static void Postfix(ref PathFindController __instance, ref Character ___character)
            {

                // unset child to use spousal logic

                if (___character is Child)
                {
                    ___character.modData[ConfigsMain.tempSetMarried] = "false";
                }
            }
        }

        [HarmonyPatch(typeof(PathFindController))]
        [HarmonyPatch("moveCharacter")]
        [HarmonyPriority(Priority.High)]
        class impassableWalls : PatchMaster
        {
            public static bool Prefix(ref PathFindController __instance, ref Character ___character)
            {
                if (!(___character.currentLocation is FarmHouse))
                {
                    return true; // alternate function only valid for FarmHouse
                }
                if (!(__instance.location is FarmHouse))
                {
                    return true; // alternate function only valid for movement solely within FarmHouse (i.e., not directed elsewhere)
                }

                Point peek = __instance.pathToEndPoint.Peek();
                FarmHouse farmhouse = ___character.currentLocation as FarmHouse;

                // original:
                // // !farmhouse.isTileLocationTotallyClearAndPlaceable(peek.X, peek.Y)
                // new:
                //  (isTileOnMap(v) && !isTileOccupied(v) && isTilePassable(new Location((int)v.X, (int)v.Y), Game1.viewport))

                string rootCause = "";
                //if (farmhouse.getTileIndexAt(peek.X, peek.Y, "Back") == -1 || farmhouse.getTileIndexAt(peek.X, peek.Y, "Back") == 0) { rootCause += "tile indexes; "; }
                //if (farmhouse.isTileOccupied(new Vector2(peek.X, peek.Y))) { rootCause += "tile occupied; "; }
                //if (farmhouse.isTilePassable(new xTile.Dimensions.Location(peek.X, peek.Y), Game1.viewport)) { rootCause += "tile not passable; "; }
                if (Utility.pointInRectangles(farmhouse.getWalls(), peek.X, peek.Y)) { rootCause += "point is in the wall; "; }
                if (!farmhouse.isTileOnMap(peek.X, peek.Y)) { rootCause += "off map; "; }

                if (//farmhouse.getTileIndexAt(peek.X, peek.Y, "Back") == -1 || farmhouse.getTileIndexAt(peek.X, peek.Y, "Back") == 0 || // ??? from vanilla)
                   // farmhouse.isTileOccupied(new Vector2(peek.X, peek.Y)) ||
                    // farmhouse.isTilePassable(new xTile.Dimensions.Location(peek.X, peek.Y), Game1.viewport) ||
                    Utility.pointInRectangles(farmhouse.getWalls(), peek.X, peek.Y) || // point not inside a wall
                    !farmhouse.isTileOnMap(peek.X, peek.Y)) // point is on the map
                {
                    // adapted from VANILLA behavior for impassable objects
                    ___character.Halt();
                    ___character.controller = null;
                    return false; // no need to evaluate further
                }
                else
                {
                    return true; // otherwise, proceed with typical collision behavior
                }
            }
        }
    }
}
