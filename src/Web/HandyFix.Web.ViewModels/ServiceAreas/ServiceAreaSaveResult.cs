namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System;

    /// <summary>
    /// What came of saving an area from the admin form: its id, or the form field a problem goes
    /// under and the sentence for it (a town the lookup could not find, for one).
    /// </summary>
    public class ServiceAreaSaveResult
    {
        public Guid? AreaId { get; set; }

        public string ErrorKey { get; set; }

        public string ErrorMessage { get; set; }

        public bool Succeeded => this.ErrorKey == null;

        public static ServiceAreaSaveResult Saved(Guid areaId) => new ServiceAreaSaveResult { AreaId = areaId };

        public static ServiceAreaSaveResult Failed(string key, string message) => new ServiceAreaSaveResult { ErrorKey = key, ErrorMessage = message };
    }
}
