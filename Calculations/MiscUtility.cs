using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StardewValley;
using StoryProgression.Configs;

namespace StoryProgression.Calculations
{
    class MiscUtility
    {
        public static Dictionary<string, string> readSimpleJSON(string filePath)
        {
            Dictionary<string, string> output = new Dictionary<string, string>() { };
            //List<string> fileInput = File.ReadLines(filePath).ToList();

            List<string> fileInput = new List<string> () {
                "{",
                "hello: this is a test",
                "second_one: this is another test",
                "}"
            };

            foreach (string entry in fileInput)
            {
                // don't parse brackets
                if (entry == "{")
                {
                    continue;
                } else if (entry == "}")
                {
                    continue;
                }

                // find colon; skip if missing
                int colonPos = entry.IndexOf(':');
                if (colonPos < 0) { continue; }

                // parse out key
                string keyUse = entry.Substring(0, colonPos).Trim();

                // parse out body
                int bodyLength = entry.Length - (colonPos + 1);
                Match endCommaMatch = Regex.Match(entry, @",$");
                bodyLength = endCommaMatch.Success ? bodyLength - 1 : bodyLength;
                string bodyRaw = entry.Substring(colonPos + 1, bodyLength);

                string bodyUse = bodyRaw.Trim().Trim('"').Trim();
                output.Add(keyUse, bodyUse);
            }

            return output;
        }

        public static string fillDefaultText(string input, NPC speaker)
        {
            if (input.Contains(ConfigsMain.defaultDialogueFills["player parent"]))
            {
                input = input.Replace(ConfigsMain.defaultDialogueFills["player parent"],
                                    speaker.modData[ConfigsMain.dataParent1G] == "m" ? ModEntry.parentDialogueFills["player male"] : ModEntry.parentDialogueFills["player female"]);
            }
            if (input.Contains(ConfigsMain.defaultDialogueFills["npc parent"]))
            {
                input = input.Replace(ConfigsMain.defaultDialogueFills["npc parent"],
                                    speaker.modData[ConfigsMain.dataParent2G] == "m" ? ModEntry.parentDialogueFills["npc male"] : ModEntry.parentDialogueFills["npc female"]);
            }
            if (input.Contains(ConfigsMain.defaultDialogueFills["npc parent s/he"]))
            {
                input = input.Replace(ConfigsMain.defaultDialogueFills["npc parent s/he"],
                                    speaker.modData[ConfigsMain.dataParent2G] == "m" ? "he" : "she");
                // LATER: this is not i8n friendly
            }

            return input;
        }
    }
}
