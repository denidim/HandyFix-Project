namespace HandyFix.Web.ViewModels.Services
{
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ServiceFaqViewModel : IMapFrom<ServiceFaq>
    {
        public string Question { get; set; }

        public string Answer { get; set; }

        public int DisplayOrder { get; set; }
    }
}
