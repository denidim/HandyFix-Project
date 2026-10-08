namespace HandyFix.Web.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;

    using Xunit;

    // The two cheap marks of a program filling in a public form: a hidden text box with
    // something in it, and a form sent back faster than anyone types (PROJECT_STATE.md
    // Section 3cb).
    public class FormGuardTests
    {
        private readonly AdjustableClock clock = new AdjustableClock(new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        private readonly IDataProtectionProvider keys = new EphemeralDataProtectionProvider();

        [Fact]
        public async Task AFormSentAfterAFewSecondsWithTheHiddenBoxEmptyPasses()
        {
            FormGuard guard = this.BuildGuard();
            var stamp = guard.CreateStamp(Get());

            this.clock.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(FormGuardResult.Passed, await guard.CheckAsync(Post(stamp), "contact"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2.9)]
        public async Task AFormSentBackFasterThanAPersonTypesIsAutomated(double seconds)
        {
            FormGuard guard = this.BuildGuard();
            var stamp = guard.CreateStamp(Get());

            this.clock.Advance(TimeSpan.FromSeconds(seconds));

            Assert.Equal(FormGuardResult.Automated, await guard.CheckAsync(Post(stamp), "contact"));
        }

        // The box is off the page and out of the Tab order. Only a program filling in every
        // field puts anything in it, however long it waited first.
        [Fact]
        public async Task AFormWithTheHiddenBoxFilledInIsAutomated()
        {
            FormGuard guard = this.BuildGuard();
            var stamp = guard.CreateStamp(Get());

            this.clock.Advance(TimeSpan.FromMinutes(2));

            Assert.Equal(FormGuardResult.Automated, await guard.CheckAsync(Post(stamp, honeypot: "https://spam.example"), "contact"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("639000000000000000")]
        [InlineData("not-a-stamp")]
        public async Task AFormWithNoStampOrAMadeUpOneIsAutomated(string stamp)
        {
            FormGuard guard = this.BuildGuard();

            this.clock.Advance(TimeSpan.FromMinutes(2));

            Assert.Equal(FormGuardResult.Automated, await guard.CheckAsync(Post(stamp), "contact"));
        }

        // A stamp is signed with this site's keys. One signed elsewhere, with other keys, is not ours.
        [Fact]
        public async Task AStampSignedWithOtherKeysIsAutomated()
        {
            var foreignStamp = new FormGuard(new EphemeralDataProtectionProvider(), this.clock, Settings(null), NullLogger<FormGuard>.Instance)
                .CreateStamp(Get());

            this.clock.Advance(TimeSpan.FromMinutes(2));

            Assert.Equal(FormGuardResult.Automated, await this.BuildGuard().CheckAsync(Post(foreignStamp), "contact"));
        }

        // The case that would lose a real enquiry. The form comes back with an error (a photo too
        // large, say), the visitor puts it right and sends again two seconds later. Counted from
        // the redisplay that is "too fast"; so a form shown again keeps the stamp it was sent
        // with, and the time is counted from when the person first saw it.
        [Fact]
        public async Task AFormShownAgainAfterAnErrorKeepsItsFirstStampSoAQuickCorrectionPasses()
        {
            FormGuard guard = this.BuildGuard();
            var firstStamp = guard.CreateStamp(Get());

            this.clock.Advance(TimeSpan.FromSeconds(40));
            var stampOnRedisplay = guard.CreateStamp(Post(firstStamp));

            Assert.Equal(firstStamp, stampOnRedisplay);

            this.clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(FormGuardResult.Passed, await guard.CheckAsync(Post(stampOnRedisplay), "contact"));
        }

        // ...but a redisplay does not launder a stamp that was never ours.
        [Fact]
        public void AFormShownAgainAfterAMadeUpStampGetsANewOne()
        {
            FormGuard guard = this.BuildGuard();

            var stamp = guard.CreateStamp(Post("not-a-stamp"));

            Assert.NotEqual("not-a-stamp", stamp);
            Assert.NotEmpty(stamp);
        }

        // An old tab is not a program: a form left open overnight still sends.
        [Fact]
        public async Task AFormLeftOpenForADayStillPasses()
        {
            FormGuard guard = this.BuildGuard();
            var stamp = guard.CreateStamp(Get());

            this.clock.Advance(TimeSpan.FromHours(26));

            Assert.Equal(FormGuardResult.Passed, await guard.CheckAsync(Post(stamp), "booking"));
        }

        // The full-stack tests send a form the instant they have fetched it, so their host sets
        // the minimum to nothing.
        [Fact]
        public async Task TheMinimumTimeComesFromConfiguration()
        {
            var guard = new FormGuard(this.keys, this.clock, Settings("0"), NullLogger<FormGuard>.Instance);
            var stamp = guard.CreateStamp(Get());

            Assert.Equal(FormGuardResult.Passed, await guard.CheckAsync(Post(stamp), "contact"));
            Assert.Equal(FormGuardResult.Automated, await guard.CheckAsync(Post(stamp, honeypot: "x"), "contact"));
        }

        private static IConfiguration Settings(string minimumSeconds)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string> { [FormGuard.MinimumSecondsKey] = minimumSeconds })
                .Build();
        }

        private static HttpContext Get()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            return context;
        }

        private static HttpContext Post(string stamp, string honeypot = "")
        {
            var fields = new Dictionary<string, StringValues> { [FormGuard.HoneypotFieldName] = honeypot };
            if (stamp != null)
            {
                fields[FormGuard.StampFieldName] = stamp;
            }

            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Form = new FormCollection(fields);
            return context;
        }

        private FormGuard BuildGuard()
        {
            return new FormGuard(this.keys, this.clock, Settings(null), NullLogger<FormGuard>.Instance);
        }

        private sealed class AdjustableClock : TimeProvider
        {
            private DateTimeOffset now;

            public AdjustableClock(DateTimeOffset start)
            {
                this.now = start;
            }

            public override DateTimeOffset GetUtcNow() => this.now;

            public void Advance(TimeSpan by) => this.now += by;
        }
    }
}
