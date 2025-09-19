using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PumpSheriff
{
    internal class Animations
    {
        // Key: Character id, Value: Dictionary<Key: Type id, Value: List of Frames>
        public Dictionary<string, Dictionary<string, List<List<string>>>> Frames { get; private set; } = new Dictionary<string, Dictionary<string, List<List<string>>>>();

        // Animation Height dimension (number of lines the ascii art has)
        public int AnimHeightLines { get; private set; } = 300;

        // Animation Width dimension (number of characters each line has in the ascii art)
        public int AnimWidthCharacters { get; private set; } = 200;

        // Constructor - load all animations from a given folder
        public Animations(string folderPath, int animHeightLines = 382, int animWidthCharacters = 275)
        {
            // Set properties
            AnimHeightLines = animHeightLines;
            AnimWidthCharacters = animWidthCharacters;

            // Go over all animation files (animation created with https://www.ascii-animator.com/)
            foreach (var filePath in Directory.EnumerateFiles(
                folderPath,                    // root
                "*.ascii",                     // pattern
                SearchOption.AllDirectories))  // or TopDirectoryOnly
            {
                // parse the file name o extract both character id and animation id
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string[] nameParts = fileName.Split('-');
                var charId = nameParts[0];
                var typeId = nameParts[1];

                // extract frames from file
                string rawFileContent = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                string filteredFileContent = rawFileContent.Substring(rawFileContent.IndexOf("[") + 1).Replace("\\n", "\n");
                var frames = filteredFileContent.Split("\",\"").Where(f => f.Length >= animHeightLines / 3 * animWidthCharacters);

                // store it
                if(!Frames.ContainsKey(charId))
                    Frames[charId] = new Dictionary<string, List<List<string>>>();
                if(!Frames[charId].ContainsKey(typeId))
                    Frames[charId][typeId] = new List<List<string>>();
                Frames[charId][typeId].Add(frames.ToList());
            }
        }
    }
}
