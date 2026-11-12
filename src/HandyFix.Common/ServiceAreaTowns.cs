namespace HandyFix.Common
{
    using System.Collections.Generic;
    using System.Linq;

    // The 20 places of the Google Business Profile's service area, in the order the profile
    // lists them. They must be changed together with the profile: the structured data on every
    // page and the "Do you cover my location?" answer are built from this one list, so Google
    // reads the same places from the site as from the profile (PROJECT_STATE Section 3eg).
    public static class ServiceAreaTowns
    {
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "Chessington",
            "Surbiton",
            "Kingston upon Thames",
            "New Malden",
            "Esher",
            "Molesey",
            "Walton-on-Thames",
            "Weybridge",
            "Cobham",
            "Worcester Park",
            "Ewell",
            "Epsom",
            "Ashtead",
            "Leatherhead",
            "Cheam",
            "Sutton",
            "Banstead",
            "Wimbledon",
            "Twickenham",
            "Richmond",
        };

        // "Chessington, Surbiton, ... Twickenham and Richmond": commas, and "and" before the last.
        public static string AsSentenceList()
        {
            return string.Join(", ", Names.Take(Names.Count - 1)) + " and " + Names[Names.Count - 1];
        }
    }
}
