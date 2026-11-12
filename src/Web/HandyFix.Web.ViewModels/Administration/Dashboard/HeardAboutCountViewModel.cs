namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    // One line of the Dashboard's "How customers heard about us" table (PROJECT_STATE.md
    // Section 3ej): an answer and how many jobs of the last 30 days gave it.
    public class HeardAboutCountViewModel
    {
        public string Label { get; set; }

        public int Count { get; set; }
    }
}
