namespace HandyFix.Data.Seeding
{
    using System;
    using System.Collections.Generic;

    // One service page's own blocks and questions, as ServicePageTexts holds them for the seeder.
    // The blocks keep one point per line, as the admin form saves them (PROJECT_STATE.md Section
    // 3ep).
    internal class ServicePageText
    {
        public string Summary { get; init; }

        public string CommonFindings { get; init; }

        public string VisitSteps { get; init; }

        public string BeforeWeArrive { get; init; }

        public string Included { get; init; }

        public string NotIncluded { get; init; }

        public IReadOnlyList<(string Question, string Answer)> Faqs { get; init; } = Array.Empty<(string Question, string Answer)>();
    }
}
