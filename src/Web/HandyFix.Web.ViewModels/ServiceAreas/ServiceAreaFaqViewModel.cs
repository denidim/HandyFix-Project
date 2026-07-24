namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class ServiceAreaFaqViewModel : IMapFrom<ServiceAreaFaq>
    {
        public string Question { get; set; }

        public string Answer { get; set; }

        public int DisplayOrder { get; set; }
    }
}
