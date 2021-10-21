using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Locations;

namespace StoryProgression.MiscInteractive
{
    class ManageHats
    {
        public static void safelyManageHat(Child child)
        {
            if (child.hat.Value == null) // they didn't have a hat at all in the first place: nothing to do here
            {
                return;
            }

            // save and remove the hat
            Hat hatItem = (Hat)child.hat; // copied from vanilla
            child.hat.Value = null; // take off the hat

            // do something with the hat
            // LATER: better hat "get rid of" options

            // find a place to put the hat
            Point hatDepositSpot = Point.Zero;
            try
            {
                Farmer parent = Game1.getFarmerMaybeOffline(long.Parse(child.modData[Configs.ConfigsMain.dataParent1ID]));
                FarmHouse home = Utility.getHomeOfFarmer(parent);
                Point childBedSpot = Patches.ChildMethods.getNPCBed(child as NPC, home, BedFurniture.BedType.Child).GetBedSpot();

                // from vanilla
                int bedWidth = 2;
                int bedHeight = 4;
                bool foundSpot = false;

                foreach (int horiz in new int[] { -1, 0, 1, -2, 2 })
                {
                    if (!foundSpot)
                    {
                        foreach (int verti in new int[] { -1, 0, 1, -2, 2 })
                        {
                            Point test = new Point(childBedSpot.X + (horiz) + ((horiz < 0 ? -1 : 1) * bedWidth),
                                                   childBedSpot.Y + (verti) + ((verti < 0 ? -1 : 1) * bedHeight));

                            // tests based on vanilla
                            if (home.isTileOnMap(test.X, test.Y) &&
                                home.getTileIndexAt(test.X, test.Y, "Back") != -1 &&
                                home.isTileLocationTotallyClearAndPlaceable(test.X, test.Y) &&
                                !Utility.pointInRectangles(home.getWalls(), test.X, test.Y))
                            {
                                // we've found a good spot!
                                foundSpot = true;
                                hatDepositSpot = test;
                                break;
                            }
                        }
                    } else
                    {
                        break; // if already found our spot, kill loop
                    }    
                }

                if (!foundSpot)
                {
                    hatDepositSpot = home.getRandomOpenPointInHouse(Game1.random);
                }

            } catch
            {
                hatDepositSpot = Utility.getHomeOfFarmer(Game1.MasterPlayer).getRandomOpenPointInHouse(Game1.random);
            }
            
            // put the hat there
            Game1.createItemDebris(hatItem, new Vector2(hatDepositSpot.X, hatDepositSpot.Y), 0);
        }
    }
}
