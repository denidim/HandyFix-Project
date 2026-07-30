namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    /// <summary>
    /// Deliberately carries no DataAnnotations. FAQ rows are a dynamic collection where a
    /// completely blank row means "the admin added a row and changed their mind", not "invalid
    /// input" - so blank rows are pruned before validation. Annotating the properties would make
    /// the binder raise errors keyed to pre-prune indices, which then render against the wrong row
    /// once the collection shifts. ServiceAreasController validates the surviving rows explicitly
    /// against the ServiceAreaFaq entity's own limits (5-300 / 5-1000).
    /// </summary>
    public class ServiceAreaFaqInputModel : IMapFrom<ServiceAreaFaq>
    {
        /// <summary>
        /// Populated on read so the edit form can restore the saved order. Not posted back -
        /// ServiceAreasService reassigns it sequentially from the submitted list order on save.
        /// </summary>
        public int DisplayOrder { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }
    }
}
