namespace HandyFix.Web.ViewModels.Administration.Services
{
    // One question row of the Edit Service form. No annotations, as on ServiceAreaFaqInputModel:
    // a row left blank means the admin added it and changed their mind, so blank rows are dropped
    // first and the rest are checked against ServiceFaq's limits (ServicesService).
    public class ServiceFaqInputModel
    {
        public string Question { get; set; }

        public string Answer { get; set; }
    }
}
