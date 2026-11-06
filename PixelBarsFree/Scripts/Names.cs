using System.Text.RegularExpressions;
using UnityEngine;

namespace HeyHeyThere.PixelBarsFree
{
    /// <summary>
    /// The pack's files carry type prefixes (T_, SH_, PF_...), but a project updated from a release
    /// before them keeps the old names, so each is found under its plain name too.
    /// </summary>
    public static class Names
    {
        static readonly Regex Prefixed = new Regex(@"(^|/)(?:T|M|PF|L|SH|AN|AC|AOC|A|SM|F|DA)_(?=[^/]+$)");

        /// <summary>A name or Resources path without its file's type prefix.</summary>
        public static string Plain(string name) => Prefixed.Replace(name, "$1");

        /// <summary>Resources.Load under the prefixed path, else the plain one.</summary>
        public static T Load<T>(string path) where T : Object
        {
            var found = Resources.Load<T>(path);
            return found != null || Plain(path) == path ? found : Resources.Load<T>(Plain(path));
        }

        /// <summary>Whether `asset` is the file `name`, prefixed or not.</summary>
        public static bool Is(Object asset, string name) => asset && Plain(asset.name) == Plain(name);
    }
}
