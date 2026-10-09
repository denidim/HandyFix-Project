namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // Where the site keeps the keys that sign the login cookie, the form tokens and the
    // password reset links between one start and the next (PROJECT_STATE.md Section 3ci).
    public sealed class DataProtectionKeysWebTests : IClassFixture<SqliteWebApplicationFactory>, IDisposable
    {
        private readonly SqliteWebApplicationFactory server;
        private readonly string keysFolder = Path.Combine(Path.GetTempPath(), "handyfix-keys-" + Guid.NewGuid().ToString("N"));

        public DataProtectionKeysWebTests(SqliteWebApplicationFactory server)
        {
            this.server = server;
        }

        public void Dispose()
        {
            if (Directory.Exists(this.keysFolder))
            {
                Directory.Delete(this.keysFolder, recursive: true);
            }
        }

        // On the servers the site runs in a container that every deploy throws away. The keys
        // lived inside it, so each deploy made new ones: the admin was signed out, a form that
        // was open at that moment came back "that didn't go through", and a password reset link
        // sent before the deploy stopped working. Two hosts here stand for the site before a
        // deploy and after it, with nothing in common but the folder.
        [Fact]
        public void WhatTheSiteSignedBeforeADeployItCanStillReadAfterIt()
        {
            string signed;
            using (WebApplicationFactory<Program> before = this.ASiteKeepingItsKeysIn(this.keysFolder))
            {
                signed = Protector(before).Protect("signed in as the admin");
            }

            // The keys are in the folder the setting names, and not wherever the framework
            // would have put them by itself.
            Assert.NotEmpty(Directory.GetFiles(this.keysFolder, "key-*.xml"));

            using WebApplicationFactory<Program> after = this.ASiteKeepingItsKeysIn(this.keysFolder);
            Assert.Equal("signed in as the admin", Protector(after).Unprotect(signed));
        }

        private static IDataProtector Protector(WebApplicationFactory<Program> site)
        {
            return site.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("a deploy");
        }

        private WebApplicationFactory<Program> ASiteKeepingItsKeysIn(string folder)
        {
            return this.server.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string> { ["DataProtection:KeysPath"] = folder })));
        }
    }
}
