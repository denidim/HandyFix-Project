namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Services.Data.Invoices;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Invoices;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class AdminInvoicesControllerTests
    {
        [Fact]
        public async Task TheListShowsTheMonthAskedFor()
        {
            var invoices = new Mock<IInvoicesService>();
            invoices.Setup(x => x.GetMonthAsync(2026, 10)).ReturnsAsync(new List<InvoiceViewModel>());
            var controller = new InvoicesController(invoices.Object, TimeProvider.System);

            var result = await controller.Index("2026-10");

            var model = Assert.IsType<InvoiceListViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("October 2026", model.MonthName);
            Assert.Equal("2026-09", model.PreviousMonth);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not a month")]
        [InlineData("1999-01")]
        public async Task AMonthThatIsNotOneIsThisMonth(string month)
        {
            var invoices = new Mock<IInvoicesService>();
            invoices.Setup(x => x.GetMonthAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(new List<InvoiceViewModel>());
            var controller = new InvoicesController(invoices.Object, TimeProvider.System);

            await controller.Index(month);

            DateTime today = UkTime.FromUtc(DateTime.UtcNow);
            invoices.Verify(x => x.GetMonthAsync(today.Year, today.Month), Times.Once);
        }

        [Fact]
        public async Task APdfForAJobWithNoInvoiceIsNotFound()
        {
            var controller = new InvoicesController(new Mock<IInvoicesService>().Object, TimeProvider.System);

            Assert.IsType<NotFoundResult>(await controller.Pdf(Guid.NewGuid()));
        }

        [Fact]
        public async Task TheMonthsCsvIsNamedByTheMonth()
        {
            var invoices = new Mock<IInvoicesService>();
            invoices.Setup(x => x.GetMonthAsync(2026, 11)).ReturnsAsync(new List<InvoiceViewModel>());
            invoices.Setup(x => x.MonthCsv(It.IsAny<IReadOnlyList<InvoiceViewModel>>())).Returns(new byte[] { 1 });
            var controller = new InvoicesController(invoices.Object, TimeProvider.System);

            var file = Assert.IsType<FileContentResult>(await controller.Csv("2026-11"));

            Assert.Equal("text/csv", file.ContentType);
            Assert.Equal("Invoices 2026-11.csv", file.FileDownloadName);
        }
    }
}
