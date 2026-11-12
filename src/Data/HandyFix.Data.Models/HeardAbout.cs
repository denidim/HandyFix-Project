namespace HandyFix.Data.Models
{
    /// <summary>
    /// What made a customer look for us, as they answer "How did you hear about us?" on the
    /// booking or enquiry form, or as the admin is told on the phone (PROJECT_STATE.md Section
    /// 3ej). Not the same as <see cref="BookingSource"/>, which is the way the job reached us.
    /// Saved as its number, so the order here must not change: 0 is every job and enquiry made
    /// before the question existed, and every one whose customer did not say.
    /// </summary>
    public enum HeardAbout
    {
        NotSaid = 0,

        GoogleSearch = 1,

        GoogleMaps = 2,

        Facebook = 3,

        Instagram = 4,

        Nextdoor = 5,

        FriendOrNeighbour = 6,

        UsedBefore = 7,

        Other = 8,
    }
}
