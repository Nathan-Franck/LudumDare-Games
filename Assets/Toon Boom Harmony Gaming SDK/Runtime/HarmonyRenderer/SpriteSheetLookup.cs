
using System.Collections.Generic;
using System.Linq;

namespace ToonBoom.Harmony
{
    public class SpriteSheetLookup
    {
        private int _ResolutionIndex;
        private Dictionary<string, SpriteSheetLookup> _Lookup;
        private HarmonyProject _Project;

        public SpriteSheetLookup(HarmonyProject project)
        {
            _Project = project;
        }

        public void FillForSplitsDashSeparated(string[] splits, int readIdx = 0)
        {
            if (readIdx == splits.Length)
            {
                _ResolutionIndex = _Project.SpriteSheets.FindIndex(sheetResolution =>
                    sheetResolution.ResolutionName == string.Join("-", splits));
                return;
            }
            if (_Lookup == null)
            {
                _Lookup = new Dictionary<string, SpriteSheetLookup>();
            }
            string split = splits[readIdx];
            if (!_Lookup.TryGetValue(split, out SpriteSheetLookup entry))
            {
                entry = new SpriteSheetLookup(_Project);
                _Lookup.Add(split, entry);
            }
            entry.FillForSplitsDashSeparated(splits, readIdx + 1);
        }

        public static SpriteSheetLookup FromProject(HarmonyProject Project)
        {
            var result = new SpriteSheetLookup(Project);
            var dashSeparatedStrings = Project?.SpriteSheets?
                .Select(sheetResolution => sheetResolution.ResolutionName);
            if (dashSeparatedStrings == null) return result;
            foreach (var dashSeparatedString in dashSeparatedStrings)
            {
                var splits = dashSeparatedString.Split('-');
                result.FillForSplitsDashSeparated(splits);
            }
            return result;
        }

        public IEnumerable<int> IndicesSatisfiesKeys(string[] keys)
        {
            if (_Lookup == null)
            {
                if (keys.Length == 0)
                {
                    return Enumerable.Repeat(_ResolutionIndex, 1);
                }
                else
                {
                    return Enumerable.Empty<int>();
                }
            }
            var matchingFromLookup = _Lookup
                .Where(kvp => keys.Any(key => key == kvp.Key));
            var matchingKeysSet = new HashSet<string>(matchingFromLookup
                .Select(kvp => kvp.Key));
            var remainingKeys = keys
                .Where(key => !matchingKeysSet.Contains(key))
                .ToArray();
            var matchingValues = matchingFromLookup
                .Select(kvp => kvp.Value);
            if (!matchingFromLookup.Any())
            {
                matchingValues = _Lookup.Values;
            }
            return matchingValues.Aggregate<SpriteSheetLookup, IEnumerable<int>>(
                Enumerable.Empty<int>(),
                (source, acc) => source.Concat(acc.IndicesSatisfiesKeys(remainingKeys)));
        }

        public int FirstSatisfiesKeys(string[] keys)
        {
            var indices = IndicesSatisfiesKeys(keys);
            return indices.FirstOrDefault();
        }
    }
}