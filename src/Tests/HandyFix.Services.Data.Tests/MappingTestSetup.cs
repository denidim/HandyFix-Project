namespace HandyFix.Services.Data.Tests
{
    using System.Runtime.CompilerServices;

    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels;

    internal static class MappingTestSetup
    {
        // Unlike HandyFix.Web.Tests (which boots the real Program and therefore calls this for
        // real), this project never runs Program.cs, so MappingConfig.GlobalConfig was null for
        // every test here. Mapster silently falls back to its own bare default config in that
        // case rather than throwing, so any test asserting on a custom-mapped property (anything
        // registered via IHaveCustomMappings, e.g. CategoryViewModel.BasePrice) would silently
        // get a default/zero value instead of a real failure.
        [ModuleInitializer]
        internal static void RegisterMappings()
        {
            MappingConfig.RegisterMappings(typeof(ErrorViewModel).Assembly);
        }
    }
}
