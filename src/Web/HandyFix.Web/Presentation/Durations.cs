namespace HandyFix.Web.Presentation
{
    /// <summary>
    /// How long a service takes, as the public pages write it: "1.5 Hours" from an hour up,
    /// "45 Mins" below. The category page, the service page and the pricing card each had a copy
    /// until PROJECT_STATE.md Section 3dj. The home page's "45m" and the admin's "45 mins" are
    /// written differently on purpose and stay as they are.
    /// </summary>
    public static class Durations
    {
        public static string HoursOrMinutes(int minutes)
        {
            return minutes >= 60
                ? $"{(minutes / 60.0).ToString("0.#")} Hours"
                : $"{minutes} Mins";
        }
    }
}
