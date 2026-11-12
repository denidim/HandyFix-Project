namespace HandyFix.Web.Presentation
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A service's description cut down for a card that leads to the service's own page: its
    /// first sentence, and the second with it when the first is under 60 characters ("Not sure
    /// which service you need?" says nothing alone). An area page printed the whole description
    /// on its cards until PROJECT_STATE.md Section 3el, which put the boiler's 240 words on every
    /// town's page. The full text is on the service's page, one link away.
    /// </summary>
    public static class Summaries
    {
        private const int ShortSentenceLength = 60;

        // The space after a full stop, a question mark or an exclamation mark. "1.5 to 4 days"
        // has no space after its dot and stays whole.
        private static readonly Regex SentenceEnd = new(@"(?<=[.!?])\s+", RegexOptions.None, TimeSpan.FromSeconds(1));

        public static string Of(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return string.Empty;
            }

            string[] sentences = SentenceEnd.Split(description.Trim());

            return sentences[0].Length < ShortSentenceLength && sentences.Length > 1
                ? sentences[0] + " " + sentences[1]
                : sentences[0];
        }
    }
}
