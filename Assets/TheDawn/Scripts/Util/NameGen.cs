using UnityEngine;

namespace TheDawn
{
    /// <summary>Makes up short, old-sounding names.</summary>
    public static class NameGen
    {
        static readonly string[] Start = { "Ka", "Tor", "Mi", "El", "Ul", "Bri", "Dan", "Ash", "Ru", "Ne", "Sar", "Og", "Ira", "Bel", "Hal", "Yen", "Ota", "Wen", "Gar", "Lin", "Ama", "Ko", "Esk", "Fa" };
        static readonly string[] Mid = { "", "", "ra", "lo", "na", "ri", "ma", "du", "ke", "sa", "vi" };
        static readonly string[] EndM = { "k", "n", "r", "th", "g", "m", "rok", "dan", "ul", "an", "ash" };
        static readonly string[] EndF = { "a", "e", "ia", "na", "ra", "el", "is", "wen", "ya", "ei", "la" };

        public static string Person(bool female)
        {
            string s = Start[Random.Range(0, Start.Length)] + Mid[Random.Range(0, Mid.Length)];
            s += female ? EndF[Random.Range(0, EndF.Length)] : EndM[Random.Range(0, EndM.Length)];
            return s;
        }

        static readonly string[] BeliefAdj = { "Grey", "Ash", "Red", "Silent", "Old", "Deep", "Bright", "Hollow", "First", "Long" };

        public static string Belief(string theme)
        {
            string adj = BeliefAdj[Random.Range(0, BeliefAdj.Length)];
            switch (theme)
            {
                case "ancestors":
                    return Pick(new[] { "The " + adj + " Mothers", "Those Who Walk Before", "The " + adj + " Elders Below", "Whisperers of the " + adj + " Earth" });
                case "beasts":
                    return Pick(new[] { "Spirit of the " + adj + " Stag", "The " + adj + " Hunter Above", "The Antler Pact", "Children of the " + adj + " Herd" });
                case "hearth":
                    return Pick(new[] { "The " + adj + " Flame", "Keepers of the Ember", "The Hearth Mother", "The Fire That Remembers" });
                case "wild":
                    return Pick(new[] { "The " + adj + " Wolf", "Fear of the Night Teeth", "The Watcher in the Trees", "The " + adj + " Howl" });
                default:
                    return Pick(new[] { "The " + adj + " Sky", "The Turning of Seasons", "The " + adj + " Sun" });
            }
        }

        static string Pick(string[] a) { return a[Random.Range(0, a.Length)]; }
    }
}
