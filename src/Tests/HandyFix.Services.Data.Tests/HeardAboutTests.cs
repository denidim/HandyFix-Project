namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Administration.Dashboard;
    using HandyFix.Web.ViewModels.Booking;

    using Xunit;

    // "How did you hear about us?": the answer kept on each job, however it was made, and the
    // Dashboard's count of the last 30 days (PROJECT_STATE.md Section 3ej).
    public class HeardAboutTests
    {
        [Fact]
        public async Task AWebsiteBookingKeepsTheCustomersAnswer()
        {
            using var world = new BookingWorld();
            BookingInputModel form = world.BookingFor("12 Main Rd, Sutton");
            form.HeardAbout = HeardAbout.GoogleMaps;

            await world.Bookings.CreateBookingAsync(form, new List<string>());

            Assert.Equal(HeardAbout.GoogleMaps, world.DbContext.Bookings.Single().HeardAbout);
        }

        // The form sends a name or a number; a number that is no answer is "Not said", not a
        // value no page can name.
        [Fact]
        public async Task AnAnswerMadeUpInTheRequestIsNotSaid()
        {
            using var world = new BookingWorld();
            BookingInputModel form = world.BookingFor("12 Main Rd, Sutton");
            form.HeardAbout = (HeardAbout)99;

            await world.Bookings.CreateBookingAsync(form, new List<string>());

            Assert.Equal(HeardAbout.NotSaid, world.DbContext.Bookings.Single().HeardAbout);
        }

        [Fact]
        public async Task AWrittenInJobKeepsTheAnswerAndAChangeIsWrittenInItsHistory()
        {
            using var world = new BookingWorld();
            JobInputModel written = world.WrittenInJob();
            written.HeardAbout = HeardAbout.Facebook;
            Guid id = await world.Jobs.CreateWrittenInJobAsync(written);
            Booking job = world.DbContext.Bookings.Single(x => x.Id == id);
            Assert.Equal(HeardAbout.Facebook, job.HeardAbout);

            JobEditInputModel untouched = world.EditFormOf(job);
            Assert.Equal(JobEditOutcome.Unchanged, (await world.Jobs.EditDetailsAsync(untouched)).Outcome);

            JobEditInputModel edited = world.EditFormOf(job);
            edited.HeardAbout = HeardAbout.FriendOrNeighbour;
            Assert.Equal(JobEditOutcome.Saved, (await world.Jobs.EditDetailsAsync(edited)).Outcome);

            Assert.Equal(HeardAbout.FriendOrNeighbour, world.DbContext.Bookings.Single(x => x.Id == id).HeardAbout);
            Assert.Contains(world.HistoryOf(job), line => line.Contains("How they heard about us was: Facebook.", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TheDashboardCountsEveryAnswerOfTheJobsMadeSinceTheDayItIsGiven()
        {
            using var world = new BookingWorld();
            await AddJobAsync(world, HeardAbout.GoogleMaps, daysAgo: 1);
            await AddJobAsync(world, HeardAbout.GoogleMaps, daysAgo: 29);
            await AddJobAsync(world, HeardAbout.GoogleMaps, daysAgo: 40);
            await AddJobAsync(world, HeardAbout.NotSaid, daysAgo: 2);

            IReadOnlyList<HeardAboutCountViewModel> counts = await world.Bookings.GetHeardAboutCountsAsync(DateTime.UtcNow.AddDays(-30));

            Assert.Equal(HeardAboutText.Answers.Count + 1, counts.Count);
            Assert.Equal(2, counts.Single(x => x.Label == "Google Maps").Count);
            Assert.Equal(0, counts.Single(x => x.Label == "Facebook").Count);
            Assert.Equal("Not said", counts.Last().Label);
            Assert.Equal(1, counts.Last().Count);
        }

        private static async Task AddJobAsync(BookingWorld world, HeardAbout answer, int daysAgo)
        {
            JobInputModel written = world.WrittenInJob();
            written.HeardAbout = answer;
            Guid id = await world.Jobs.CreateWrittenInJobAsync(written);

            world.DbContext.Bookings.Single(x => x.Id == id).CreatedOn = DateTime.UtcNow.AddDays(-daysAgo);
            await world.DbContext.SaveChangesAsync();
        }
    }
}
