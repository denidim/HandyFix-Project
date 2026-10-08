namespace HandyFix.Common
{
    public static class NameFormat
    {
        // A first name, with the last name after it when there is one. A technician can be on the
        // roster under a first name alone (PROJECT_STATE.md Section 3cd); written as
        // "{first} {last}" that name came out with a space hanging after it.
        public static string Full(string firstName, string lastName)
        {
            return string.IsNullOrWhiteSpace(lastName) ? firstName : firstName + " " + lastName;
        }
    }
}
