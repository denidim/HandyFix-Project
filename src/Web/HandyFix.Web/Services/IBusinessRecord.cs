namespace HandyFix.Web.Services
{
    using System.Collections.Generic;

    // The business as Google reads it from the page's structured data (JSON-LD). Every page that
    // describes the business takes this one record, so the pages cannot disagree with each other
    // or with the Google Business Profile (PROJECT_STATE Section 3eg).
    public interface IBusinessRecord
    {
        // The whole record with "@context", for a page that stands for the business itself.
        Dictionary<string, object> Build();

        // The same record without "@context", for the "provider" of a service on another page.
        Dictionary<string, object> BuildAsProvider();

        // The towns of the service area alone, for the "areaServed" of a service on another page.
        List<Dictionary<string, object>> AreaServed();
    }
}
