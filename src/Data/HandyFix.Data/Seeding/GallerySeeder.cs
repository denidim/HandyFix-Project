namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;

    internal class GallerySeeder : ISeeder
    {
        private static readonly (string FileName, bool ShowOnHomePage, string StageBadge, string Caption)[] InitialPhotos =
        {
            ("job-01.webp", true, "After", "Bathroom refurbishment with dark green metro tiles, striped floor and pedestal basin"),
            ("job-02.webp", true, null, "Modern grey tiled en-suite with quadrant glass shower enclosure and wall-hung toilet"),
            ("job-03.webp", true, null, "Contemporary shower room with botanical feature tiles, wall-hung WC and vanity unit"),
            ("job-04.webp", true, null, "Walk-in glass shower screen with white herringbone wall tiles and patterned floor"),
            ("job-05.webp", true, null, "Fitted bathroom with cream vanity basin unit, close-coupled toilet and beige tiling"),
            ("job-06.webp", true, null, "Double vanity unit installation with twin ceramic basins and chrome mixer taps"),
            ("job-07.webp", true, null, "Wall-hung twin vanity unit with countertop basins and wall-mounted chrome mixer taps"),
            ("job-08.webp", true, null, "Completed white gloss kitchen installation with dark worktops and oak flooring"),
            ("job-09.webp", true, null, "Modern dark grey handleless kitchen installation with central island and worktops"),
            ("job-10.webp", true, null, "Fitted sage and white gloss kitchen units with undermount sink and glass splashback"),
            ("job-11.webp", true, null, "Traditional black-and-white Victorian geometric floor tiling laid in a hallway"),
            ("job-12.webp", true, null, "Grundfos central heating circulating pump fitted with brass isolation valves"),
            ("job-13.webp", false, "Before", "Setting out green herringbone wall tiles on waterproof backer board in shower"),
            ("job-14.webp", false, "After", "Completed green herringbone tiled shower enclosure with chrome rainfall mixer"),
            ("job-15.webp", false, "Before", "First-fix soldered copper pipework, isolation valves and waste under kitchen sink"),
            ("job-16.webp", false, "After", "Completed double-bowl kitchen sink waste trap, appliance spigot and copper valves"),
            ("job-17.webp", false, "Before", "Replacing worn water pressure valve with new pressure reducing valve and stopcock"),
            ("job-18.webp", false, "After", "Mains water pressure reducing valve with gauge and new brass stopcock installed"),
            ("job-19.webp", false, "After", "New copper pipework, lever isolation valve, drain-off cock and discharge pipe"),
            ("job-20.webp", false, "Before", "Internal heat exchanger cleaning and maintenance during heating unit repair"),
            ("job-21.webp", false, "Before", "Waterproof tile backer board installation with recessed niche and shower pipework"),
            ("job-22.webp", false, "Before", "Temporary Acrow prop support installed during internal wall opening alteration"),
            ("job-23.webp", false, "After", "Steel RSJ beam, timber ceiling joists and stud partition framing installation"),
            ("job-24.webp", false, null, "New close-coupled toilet fitted with chrome thermostatic douche spray and valve"),
            ("job-25.webp", false, null, "Tiled walk-in shower with recessed chrome thermostatic mixer valve and body jets"),
            ("job-26.webp", false, null, "En-suite shower room installation with frameless glass door and dark vanity unit"),
            ("job-27.webp", false, null, "Neat under-bath soldered copper hot and cold tap supplies and waste connection"),
            ("job-28.webp", false, null, "New double-panel convector radiator fitted with copper pipe tails and TRV valve"),
            ("job-29.webp", false, null, "Anthracite vertical radiator installation with chrome TRV and underfloor piping"),
            ("job-30.webp", false, null, "Marble-tiled bathroom with folding glass bath screen and overhead rain shower"),
        };

        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            if (await dbContext.GalleryPhotos.AnyAsync())
            {
                return;
            }

            var photos = InitialPhotos.Select((entry, index) => new GalleryPhoto
            {
                ImageUrl = $"/images/gallery/{entry.FileName}",
                Caption = entry.Caption,
                StageBadge = entry.StageBadge,
                ShowOnHomePage = entry.ShowOnHomePage,
                DisplayOrder = index + 1,
            });

            await dbContext.GalleryPhotos.AddRangeAsync(photos);
        }
    }
}
