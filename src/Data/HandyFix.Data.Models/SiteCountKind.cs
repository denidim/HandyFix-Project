namespace HandyFix.Data.Models
{
    /// <summary>
    /// What the website counts of its visitors' doings (<see cref="SiteCount"/>). Saved as its
    /// number, so the order here must not change.
    /// </summary>
    public enum SiteCountKind
    {
        // Someone arrived from outside the site (a search, a link, the address typed in) and
        // scrolled or tapped. The same person coming twice is two visits.
        VisitOnPhone = 0,

        VisitOnComputer = 1,

        BookingOpened = 2,

        // A free time was tapped on the booking page.
        TimePicked = 3,

        CallTapped = 4,

        WhatsAppTapped = 5,
    }
}
