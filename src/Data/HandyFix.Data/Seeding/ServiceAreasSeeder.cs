namespace HandyFix.Data.Seeding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    /// <summary>
    /// Seeds the initial coverage areas. Note this only ever INSERTS missing slugs - it never
    /// updates an area that already exists, so editing the copy below has no effect on a database
    /// that has already been seeded. Use the admin panel at /Administration/ServiceAreas for that.
    /// <para>
    /// Areas must be hard-deleted, never soft-deleted: IX_ServiceAreas_Slug is unique with no
    /// IsDeleted filter, but the global query filter hides soft-deleted rows, so this seeder would
    /// try to re-insert the slug and fail startup with a unique-index violation.
    /// </para>
    /// <para>See docs/WORKFLOW_SERVICE_AREAS.md for the full three-step add/update workflow.</para>
    /// </summary>
    internal class ServiceAreasSeeder : ISeeder
    {
        private const string HomeTurf = "Home Turf";
        private const string SurreyBorders = "Surrey Borders";

        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            var areas = new[]
            {
                new
                {
                    Slug = "chessington",
                    Name = "Chessington",
                    Region = HomeTurf,
                    DriveTimeMinutes = 0,
                    IsFeatured = true,
                    DisplayOrder = 1,
                    IntroCopy = "Chessington is where it all starts for us - our technicians are dispatched from right here, so it's usually our fastest turnaround area for both handyman and plumbing work.",
                    NeighbourhoodsCopy = "From the family estates around Hook and Malden Rushett to the flats near Chessington World of Adventures and the older housing along Garrison Lane, we know the mix of properties that make up Chessington inside out.",
                    Faqs = new[]
                    {
                        ("How quickly can you get to me in Chessington?", "Chessington is our home base, so we typically offer same-day or next-day slots here before almost anywhere else on our books."),
                        ("Do you cover both Chessington North and South?", "Yes - our coverage includes the full KT9 postcode area, from the World of Adventures side through to the borders with Hook and Tolworth."),
                    },
                },
                new
                {
                    Slug = "surbiton",
                    Name = "Surbiton",
                    Region = HomeTurf,
                    DriveTimeMinutes = 8,
                    IsFeatured = false,
                    DisplayOrder = 2,
                    IntroCopy = "Surbiton's blend of Victorian conservation-area streets and riverside apartments means every job is a little different, and our technicians are equipped for both.",
                    NeighbourhoodsCopy = "We regularly work on the grand Victorian terraces around Maple Road and St Mark's Hill, the mansion flats near Surbiton station, and the quieter family homes towards Berrylands and Tolworth.",
                    Faqs = new[]
                    {
                        ("Do you cover Surbiton conservation area properties?", "Yes, and we're used to working carefully around period features and any local conservation-area requirements."),
                        ("Can you help with a rented flat near Surbiton station?", "Absolutely - we regularly work with landlords and letting agents managing flats close to the station and along Ewell Road."),
                    },
                },
                new
                {
                    Slug = "kingston-upon-thames",
                    Name = "Kingston upon Thames",
                    Region = HomeTurf,
                    DriveTimeMinutes = 10,
                    IsFeatured = true,
                    DisplayOrder = 3,
                    IntroCopy = "From the Victorian terraces around Norbiton to the riverside flats by Kingston Bridge and the family homes stretching out towards Coombe and Berrylands, our local technicians already know the mix of properties that make up Kingston. One call books the right person for the job.",
                    NeighbourhoodsCopy = "We're on your side of the borough already - most Kingston bookings are slotted the same week, with no travel charge and no call-out fee within our standard service area.",
                    Faqs = new[]
                    {
                        ("How quickly can someone get to KT1/KT2?", "Kingston sits right inside our core coverage area, so we typically have next-day availability, and same-day where the schedule allows."),
                        ("Do you cover flats near Kingston University?", "Yes, we regularly carry out repairs for both owner-occupied and rented properties around the university and town centre."),
                    },
                },
                new
                {
                    Slug = "worcester-park-ewell",
                    Name = "Worcester Park & Ewell",
                    Region = HomeTurf,
                    DriveTimeMinutes = 10,
                    IsFeatured = false,
                    DisplayOrder = 4,
                    IntroCopy = "Worcester Park and Ewell's rows of well-kept 1930s semis are some of the most consistent handyman and plumbing work we take on - reliable properties, straightforward access, and usually a quick turnaround.",
                    NeighbourhoodsCopy = "Whether it's a semi off Green Lane in Worcester Park or a family home near Ewell Village and Bourne Hall, we know the layout of these estates well enough to arrive prepared.",
                    Faqs = new[]
                    {
                        ("Do you cover both Worcester Park and Ewell Village?", "Yes, our standard coverage spans both, along with the surrounding Stoneleigh and Auriol Park areas."),
                        ("Are you used to working on 1930s semis?", "Very much so - it's one of the most common property types we work on across this part of Surrey."),
                    },
                },
                new
                {
                    Slug = "epsom",
                    Name = "Epsom",
                    Region = HomeTurf,
                    DriveTimeMinutes = 12,
                    IsFeatured = true,
                    DisplayOrder = 5,
                    IntroCopy = "Epsom has been one of our busiest areas for years, from the terraces near the town centre to the larger detached homes out towards the Downs and the racecourse.",
                    NeighbourhoodsCopy = "We're familiar with everything from the Victorian streets around Epsom station to the newer developments near Nonsuch Park and the period homes bordering Epsom Common.",
                    Faqs = new[]
                    {
                        ("Do you cover properties near Epsom Downs and the racecourse?", "Yes, our Epsom coverage extends across the full town, including the Downs side and surrounding villages."),
                        ("Can you take on a job the same week in Epsom?", "In most cases, yes - Epsom sits well within our core coverage area."),
                    },
                },
                new
                {
                    Slug = "sutton",
                    Name = "Sutton",
                    Region = HomeTurf,
                    DriveTimeMinutes = 15,
                    IsFeatured = true,
                    DisplayOrder = 6,
                    IntroCopy = "Sutton's mix of busy high-street flats and the leafier streets towards Cheam and Belmont keeps our technicians well practised across almost every job type.",
                    NeighbourhoodsCopy = "From the Victorian terraces near Sutton station to the larger family homes around Rosehill and Benhilton, we've built up a good working knowledge of the area.",
                    Faqs = new[]
                    {
                        ("Do you cover flats above shops on Sutton High Street?", "Yes, we regularly carry out repairs and maintenance for both residential and small commercial units in the town centre."),
                        ("How far does your Sutton coverage reach?", "It spans the full SM postcode area, including Belmont, Rosehill and the Cheam borders."),
                    },
                },
                new
                {
                    Slug = "banstead",
                    Name = "Banstead",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    DisplayOrder = 7,
                    IntroCopy = "Banstead's mix of large detached homes and village-edge cottages means jobs here are often bigger in scope, and our technicians carry the range of parts to match.",
                    NeighbourhoodsCopy = "We know the leafy roads around Banstead Downs and the High Street, as well as the newer executive homes towards Nork and Tattenham Corner.",
                    Faqs = new[]
                    {
                        ("Do you charge extra for Banstead's larger properties?", "No - our transparent hourly rate applies regardless of property size, with materials for bigger jobs quoted and agreed up front."),
                        ("Do you cover Nork and Tattenham Corner too?", "Yes, our Banstead coverage includes both, along with the surrounding Burgh Heath area."),
                    },
                },
                new
                {
                    Slug = "esher",
                    Name = "Esher",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    DisplayOrder = 8,
                    IntroCopy = "Cobham's mix of period cottages around the village, larger family homes along Fairmile and Downside, and the newer executive developments near Stoke D'Abernon all come with their own maintenance quirks. Esher shares much of that same character, from the streets near Sandown Park to the homes towards Claygate and West End.",
                    NeighbourhoodsCopy = "Our technicians carry the range of tools and parts to handle a large executive home or a village cottage without a second visit, and materials for higher-spec fixtures are always agreed with you up front.",
                    Faqs = new[]
                    {
                        ("Do you charge extra because Esher is outside London?", "No - Esher falls within our standard service radius, so the same transparent hourly rate and no-travel-charge policy applies as anywhere else we cover."),
                        ("Can you help around race days at Sandown Park?", "Yes, though we'd recommend booking a little further ahead on race days when local traffic is heavier."),
                    },
                },
                new
                {
                    Slug = "leatherhead",
                    Name = "Leatherhead",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    DisplayOrder = 9,
                    IntroCopy = "Leatherhead sits right on our route down the A24, so it's an easy area for us to reach quickly, whatever the job.",
                    NeighbourhoodsCopy = "Our technicians regularly work on the period homes around Leatherhead town centre, the riverside properties near the River Mole, and the family estates towards Fetcham and the Oxshott borders.",
                    Faqs = new[]
                    {
                        ("How quickly can you reach Leatherhead?", "Leatherhead is close to our main routes, so next-day availability is typical and same-day is often possible."),
                        ("Do you cover Fetcham as well as Leatherhead itself?", "Yes, our Leatherhead coverage extends to Fetcham and the surrounding area."),
                    },
                },
                new
                {
                    Slug = "wimbledon",
                    Name = "Wimbledon",
                    Region = HomeTurf,
                    DriveTimeMinutes = 20,
                    IsFeatured = false,
                    DisplayOrder = 10,
                    IntroCopy = "Wimbledon's grand Victorian houses and the busy flats around the Broadway and station keep our plumbing and carpentry teams equally busy.",
                    NeighbourhoodsCopy = "We're well practised on the large period homes near Wimbledon Village and the Common, as well as the denser housing around South Wimbledon and Merton Park.",
                    Faqs = new[]
                    {
                        ("Do you cover Wimbledon Village as well as the town centre?", "Yes, our coverage spans the full SW19 postcode, from the Village down to South Wimbledon."),
                        ("Can you help with maintenance for a rented flat near the station?", "Yes, we work regularly with landlords and letting agents across Wimbledon."),
                    },
                },
                new
                {
                    Slug = "cobham",
                    Name = "Cobham",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 20,
                    IsFeatured = true,
                    DisplayOrder = 11,
                    IntroCopy = "Cobham's mix of period cottages around the village, larger family homes along Fairmile and Downside, and the newer executive developments near Stoke D'Abernon all come with their own maintenance quirks - from listed-building-sensitive repairs to keeping a large garden and outbuildings in order. Our technicians carry the range of tools and parts to handle both without a second visit.",
                    NeighbourhoodsCopy = "Materials for higher-spec fixtures are sourced and agreed with you up front, so a premium finish never means a surprise on the invoice.",
                    Faqs = new[]
                    {
                        ("Do you charge extra because Cobham is outside London?", "No - Cobham falls within our standard service radius, so the same transparent hourly rate and no-travel-charge policy applies as anywhere else we cover."),
                        ("Can you work around a large garden or outbuildings?", "Yes, many Cobham properties include outbuildings and larger gardens, and we're set up to handle that scope of work."),
                    },
                },
                new
                {
                    Slug = "walton-on-thames-weybridge",
                    Name = "Walton-on-Thames & Weybridge",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 22,
                    IsFeatured = false,
                    DisplayOrder = 12,
                    IntroCopy = "Walton-on-Thames and Weybridge's riverside setting brings a steady mix of premium apartments and larger family homes, many changing hands or tenants often enough that a reliable local handyman matters.",
                    NeighbourhoodsCopy = "We regularly work along the riverside developments near Walton Bridge, the period housing around Weybridge village, and the newer estates towards St George's Hill.",
                    Faqs = new[]
                    {
                        ("Do you cover both Walton-on-Thames and Weybridge?", "Yes, we treat them as one coverage area given how close together they sit."),
                        ("Can you help landlords managing turnover between tenants?", "Yes, we regularly handle end-of-tenancy repairs and quick-turnaround maintenance for local landlords and agents."),
                    },
                },
                new
                {
                    Slug = "reigate",
                    Name = "Reigate",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 25,
                    IsFeatured = false,
                    DisplayOrder = 13,
                    IntroCopy = "Reigate's mix of period homes near the town centre and larger properties towards the hill keeps our technicians on their toes, and it's an area we're keen to build a stronger presence in.",
                    NeighbourhoodsCopy = "From the Georgian and Victorian houses around Reigate High Street to the family homes towards Redhill and Merstham, we cover the full spread of property types here.",
                    Faqs = new[]
                    {
                        ("Is Reigate outside your standard coverage area?", "No - Reigate falls within our standard service radius, with the same transparent pricing and no travel charge as anywhere else we cover."),
                        ("Do you also cover Redhill?", "Yes, Redhill sits right alongside our Reigate coverage."),
                    },
                },
                new
                {
                    Slug = "dorking",
                    Name = "Dorking",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 25,
                    IsFeatured = false,
                    DisplayOrder = 14,
                    IntroCopy = "Dorking's market-town character means a lot of older housing stock, and our technicians are experienced at working carefully around period features without slowing the job down.",
                    NeighbourhoodsCopy = "We cover the historic streets around Dorking High Street, the surrounding villages towards Box Hill, and the newer estates on the town's edges.",
                    Faqs = new[]
                    {
                        ("Do you work on listed or period properties in Dorking?", "Yes, though for listed buildings we'll always confirm any restrictions with you before starting work."),
                        ("How far into the surrounding villages do you go?", "Our Dorking coverage extends to the immediate surrounding villages - let us know your postcode and we'll confirm straight away."),
                    },
                },
                new
                {
                    Slug = "guildford",
                    Name = "Guildford",
                    Region = SurreyBorders,
                    DriveTimeMinutes = 23,
                    IsFeatured = true,
                    DisplayOrder = 15,
                    IntroCopy = "Guildford is the largest town in our Surrey coverage, from the historic streets around the castle and High Street to the newer estates on the outskirts - and we're ready to take on the full range of handyman and plumbing work it brings.",
                    NeighbourhoodsCopy = "We work across the period terraces near the town centre, the family homes towards Merrow and Burpham, and the newer developments around Park Barn.",
                    Faqs = new[]
                    {
                        ("Do you cover all parts of Guildford?", "Yes, our Guildford coverage spans the town centre and the surrounding residential areas."),
                        ("Can I book multiple jobs in one visit in Guildford?", "Yes - many customers save time by listing several smaller jobs for one visit."),
                    },
                },
            };

            foreach (var item in areas)
            {
                ServiceArea area = dbContext.ServiceAreas.FirstOrDefault(x => x.Slug == item.Slug);
                if (area == null)
                {
                    area = new ServiceArea
                    {
                        Slug = item.Slug,
                        Name = item.Name,
                        Region = item.Region,
                        DriveTimeMinutes = item.DriveTimeMinutes,
                        IntroCopy = item.IntroCopy,
                        LocalNeighbourhoodsCopy = item.NeighbourhoodsCopy,
                        IsFeatured = item.IsFeatured,
                        DisplayOrder = item.DisplayOrder,
                    };

                    await dbContext.ServiceAreas.AddAsync(area);
                    await dbContext.SaveChangesAsync();
                }

                if (!dbContext.ServiceAreaFaqs.Any(x => x.ServiceAreaId == area.Id))
                {
                    var displayOrder = 1;
                    foreach ((string question, string answer) in item.Faqs)
                    {
                        await dbContext.ServiceAreaFaqs.AddAsync(new ServiceAreaFaq
                        {
                            ServiceAreaId = area.Id,
                            Question = question,
                            Answer = answer,
                            DisplayOrder = displayOrder++,
                        });
                    }

                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
