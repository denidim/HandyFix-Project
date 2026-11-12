namespace HandyFix.Data.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The words each answer is shown in: to the customer on the forms, and in the admin panel.
    /// </summary>
    public static class HeardAboutText
    {
        /// <summary>
        /// The answers a form offers, in the order it offers them. "Not said" is the empty choice.
        /// </summary>
        public static readonly IReadOnlyList<HeardAbout> Answers = new[]
        {
            HeardAbout.GoogleSearch,
            HeardAbout.GoogleMaps,
            HeardAbout.Facebook,
            HeardAbout.Instagram,
            HeardAbout.Nextdoor,
            HeardAbout.FriendOrNeighbour,
            HeardAbout.UsedBefore,
            HeardAbout.Other,
        };

        public static string Label(HeardAbout answer)
        {
            return answer switch
            {
                HeardAbout.GoogleSearch => "Google search",
                HeardAbout.GoogleMaps => "Google Maps",
                HeardAbout.Facebook => "Facebook",
                HeardAbout.Instagram => "Instagram",
                HeardAbout.Nextdoor => "Nextdoor",
                HeardAbout.FriendOrNeighbour => "A friend or neighbour",
                HeardAbout.UsedBefore => "Used you before",
                HeardAbout.Other => "Other",
                _ => "Not said",
            };
        }

        /// <summary>
        /// What a form sent, kept only when it is one of the answers: a number made up in the
        /// request becomes "Not said" rather than a value no page can name.
        /// </summary>
        public static HeardAbout Checked(HeardAbout answer)
        {
            return Enum.IsDefined(answer) ? answer : HeardAbout.NotSaid;
        }
    }
}
