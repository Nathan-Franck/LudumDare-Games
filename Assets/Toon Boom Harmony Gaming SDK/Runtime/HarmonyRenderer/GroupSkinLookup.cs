
using System;
using System.Collections.Generic;
using System.Linq;
using ToonBoom.Harmony;

namespace ToonBoom.Harmony
{
    public class GroupSkinLookup
    {

        public struct Key
        {
            public string group;
            public string skin;
        }
        ILookup<Key, GroupSkin> _Lookup;

        public static GroupSkinLookup FromProject(HarmonyProject Project)
        {
            var result = new GroupSkinLookup();
            if (Project.Groups != null && Project.Skins != null)
            {
                result._Lookup = Project.Groups
                    .SelectMany((group, groupIndex) =>
                        Project.Skins.Select((skin, skinIndex) =>
                            new { key = new Key { group = group, skin = skin }, value = new GroupSkin(groupIndex, skinIndex) }))
                    .ToLookup(elem => elem.key, elem => elem.value);
            }
            return result;
        }

        public GroupSkin GetKeyValuePair(string groupKey, string skinKey)
        {
            return _Lookup[new Key { group = groupKey, skin = skinKey }].FirstOrDefault();
        }
    }
}