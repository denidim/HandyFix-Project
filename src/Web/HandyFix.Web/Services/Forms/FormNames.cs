namespace HandyFix.Web.Services.Forms
{
    using HandyFix.Common;

    // The public forms the form guard looks after. Each name is used three times and has to be
    // the same each time: the form's view gives it to the _FormGuard partial, which hands it to
    // the Turnstile widget as its action; the controller gives it to the guard when the form
    // comes back; and Cloudflare repeats it when asked about the token. Letters, digits, hyphens
    // and underscores only: that is what Turnstile accepts as an action.
    public static class FormNames
    {
        public const string Contact = "contact";

        public const string JoinTeam = "join-team";

        public const string Booking = "booking";

        // What a person is told when the Turnstile check did not pass. It is said out loud, with
        // a way round it, because a person can fail it: a slow connection, a blocked widget.
        public const string ChallengeFailedMessage =
            "We could not confirm that you are a person and not a program. Please wait for the check above the button to finish, then send the form again. If it keeps happening, call us or message us on WhatsApp on "
            + GlobalConstants.BusinessPhone + ".";
    }
}
