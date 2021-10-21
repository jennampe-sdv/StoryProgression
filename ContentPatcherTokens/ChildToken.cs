using StardewValley;
using StoryProgression.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StoryProgression;
using StardewValley.Characters;
using StoryProgression.Calculations;
using StoryProgression.Patches;
using StardewModdingAPI;

namespace StoryProgression.ContentPatcherTokens
{

    public class ChildToken
    {
        /*********
        ** Fields
        *********/
        /// <summary>Fields relevant to all child tokens.</summary>
        //public string currentChild = null;

        public string quality = null;
        public string source = null;
        public static IMonitor monitor = PatchMaster.Monitor;
        public static IModHelper scope = PatchMaster.OverallScope;

        public Dictionary<int, string> outputValues = new Dictionary<int, string>();

        /****
        ** Metadata
        ****/
        /// <summary>Get whether the token allows input arguments (e.g. a feature of the Child object).</summary>
        public bool AllowsInput()
        {
            return true;
        }

        /// <summary>Whether the token may return multiple values for the given input.</summary>
        /// <param name="input">The input arguments, if applicable.</param>
        public bool CanHaveMultipleValues(string input = null)
        {
            return false;
        }

        /****
        ** State
        ****/

        /// <summary>Get whether the token is available for use.</summary>
        public bool IsReady()
        {
            if (!Context.IsWorldReady)
            {
                return false; // game not ready yet
            }
            return (this.outputValues.Count > 0); // return ready if outputValues is populated
        }

        public bool UpdateContext()
        {
            if (!Context.IsWorldReady) { return false; }

            // mark the update dictionary key value
            var dataUpdateKey = (this.source == null) ? ("BASE_" + this.quality.ToUpper()) : this.source;
            var dataUpdate = ModEntry.tokenDataUpdated.TryGetValue(dataUpdateKey, out bool dataUpdateOut) ? dataUpdateOut : true;

            // cases where we definitely DON'T need to rebuild
            if ((Game1.player.getChildrenCount() == 0 && Game1.stats.getStat("childrenTurnedToDoves") == 0) || // there are literally no children in the game
                this.outputValues.Count > 0 // we have *something* in the dictionary
                )
            {
                if (!dataUpdate) // the data update dictionary value is false
                {
                    return false; // no update needed
                }
            }

            // if we get to this point, we need to update, because one of the following is true:
            // // the dict of outputValues is empty, but there are children in the game
            // // the data update dictionary value is true
            // so, let's update
            int i = 0; // iterator
            foreach (string childName in ChildSorting.getChildrenInOrder(Game1.player, resetBirthOrder: true))
            {
                i++; // iterate up the birth order

                Child child = (Child)Game1.getCharacterFromName(childName, mustBeVillager: false);
                if (child == null) // child is not found for some reason
                {
                    continue;
                }

                // find the value to save
                string outVal = null;
                if (source == null) // this is a base value
                {
                    switch (this.quality)
                    {
                        case "name":
                            outVal = child.Name;
                            break;
                        case "age":
                            outVal = child.daysOld.ToString();
                            break;
                        case "gender":
                            outVal = child.Gender.ToString();
                            break;
                        case "birthday":
                            outVal = child.Birthday_Day.ToString();
                            break;
                        case "birthseason":
                            outVal = child.Birthday_Season;
                            break;
                    }
                } else // this is a modData value
                {
                    outVal = child.modData.TryGetValue(this.source, out string outValTemp) ? outValTemp : null;
                }

                // save the value
                if (outputValues.ContainsKey(i))
                {
                    outputValues[i] = outVal;
                }
                else
                {
                    outputValues.Add(i, outVal);
                }
            }

            // finally, let ContentPatcher know there's been a change
            return true;
        }

        public IEnumerable<string> GetValues(string input)
        {
            // initialize output
            string outputVal = null;

            // parse arguments
            string[] inputs = input.Split(separator: ',');

            // decide if this is the user is doing a call on a child index or not
            int childIndex = int.TryParse(inputs[0].Trim(), out int res) ? res : -999;
            // if can't parse, the user did not input an actual parseable integer: assume it's a modder handle

            // parse for childIndex
            if (childIndex != -999)
            {
               outputVal = outputValues.TryGetValue(childIndex, out string childIndexOutput) ? childIndexOutput : null;
            }

            if (outputVal == null)
            {
                // parse for modder handle
                string subtypeParse = "b";
                if (inputs.Length > 1)
                {
                    subtypeParse = inputs[1].Trim().ToLower().Substring(0, 1);
                }

                string subtypeKey;
                switch (subtypeParse)
                {
                    case "b":
                        subtypeKey = "base"; break;
                    case "g":
                        subtypeKey = "gifts"; break;
                    case "d":
                        subtypeKey = "dialogue"; break;
                    case "t":
                        subtypeKey = "texture"; break;
                    case "s":
                        subtypeKey = "schedule"; break;
                    default:
                        subtypeKey = "base"; break;
                }

                string matchedChild = ModEntry.pairedLogicPaths[subtypeKey].TryGetValue(inputs[0].Trim(), out string matchedChildOut) ? matchedChildOut : null;
                if (matchedChild != null)
                {

                    int matchedChildIndex = Array.IndexOf(ChildSorting.getChildrenInOrder(Game1.player, resetBirthOrder: false),
                                                            matchedChild) + 1;
                    outputVal = outputValues.TryGetValue(matchedChildIndex, out string finalOut) ? finalOut : null; // if this birth order is recorded, return the value; otherwise, null
                }
            }

            // if all else fails, return null
            yield return outputVal;

        }

        /****
        ** Customization Across Several Similar Tokens
        ****/
        public ChildToken(string qualityInput, string source=null)
        {
            this.quality = qualityInput.ToLower();
            this.source = source;
        }

    }

}
