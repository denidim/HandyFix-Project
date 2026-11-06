namespace HandyFix.Common
{
    // The names the database gives a booking's status, in one place. They are constants, not
    // read-only fields, so a switch arm and an [InlineData] can use them, and inside an EF query
    // each is written into the SQL as the same literal it replaced. What the admin reads for
    // each one is JobLabels.
    public static class BookingStatusNames
    {
        // A website booking waiting for its deposit.
        public const string Pending = "Pending";

        // A job that is on: a website booking with its deposit in, or one the admin wrote in.
        public const string Approved = "Approved";

        // Seeded, and read as a job that is on, but nothing sets it today.
        public const string InProgress = "InProgress";

        public const string Completed = "Completed";

        public const string Cancelled = "Cancelled";

        // A website booking whose deposit never came, dropped after about fifteen minutes.
        public const string Abandoned = "Abandoned";
    }
}
