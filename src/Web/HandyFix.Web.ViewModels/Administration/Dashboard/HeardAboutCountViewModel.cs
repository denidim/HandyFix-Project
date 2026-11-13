namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data.Models;

    // One line of the Dashboard's "How customers heard about us" table (PROJECT_STATE.md
    // Sections 3ej and 3et): an answer, how many enquiries gave it and how many jobs did. An
    // enquiry that is made a job is in both columns: the two are counted apart, not added up.
    public class HeardAboutCountViewModel
    {
        public string Label { get; set; }

        public int Enquiries { get; set; }

        public int Jobs { get; set; }

        // Every answer has its line, a nought included: a channel that brings nobody is worth
        // seeing too. "Not said" comes last.
        public static IReadOnlyList<HeardAboutCountViewModel> Table(
            IReadOnlyDictionary<HeardAbout, int> enquiries,
            IReadOnlyDictionary<HeardAbout, int> jobs)
        {
            return HeardAboutText.Answers
                .Append(HeardAbout.NotSaid)
                .Select(answer => new HeardAboutCountViewModel
                {
                    Label = HeardAboutText.Label(answer),
                    Enquiries = enquiries.GetValueOrDefault(answer),
                    Jobs = jobs.GetValueOrDefault(answer),
                })
                .ToList();
        }
    }
}
