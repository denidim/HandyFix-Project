namespace HandyFix.Common
{
    using System.Collections.Generic;

    // The 20 places of the Google Business Profile's service area, in the order the profile
    // lists them. They must be changed together with the profile: the structured data on every
    // page is built from this one list, so Google reads the same places from the site as from
    // the profile (PROJECT_STATE Section 3eg).
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
    }
}
