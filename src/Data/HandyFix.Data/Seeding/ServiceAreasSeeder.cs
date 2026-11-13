namespace HandyFix.Data.Seeding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Seeds the initial coverage areas into an empty table. Once the table holds any area, this
    /// seeder does nothing: editing the list below has no effect on a database that has already
    /// been seeded, and an area deleted in the admin panel stays deleted. Use the admin panel at
    /// /Administration/ServiceAreas to add, change or remove areas.
    /// <para>
    /// Areas are hard-deleted, never soft-deleted: IX_ServiceAreas_Slug is unique with no
    /// IsDeleted filter, so a soft-deleted area would keep its slug taken while hidden from every
    /// query, and the admin could never use that slug again.
    /// </para>
    /// <para>See docs/WORKFLOW_SERVICE_AREAS.md for adding and updating an area.</para>
    /// </summary>
    internal class ServiceAreasSeeder : ISeeder
    {
        // Each area's place on the coverage diagram: direction from Chessington, which side the
        // name goes on, and a shorter name where the full one does not fit. Databases that
        // already had the areas got the same values from the AddDiagramPlaceToServiceArea
        // migration. Chessington is the centre and needs none.
        private static readonly Dictionary<string, (double Bearing, DiagramLabelSide Side, string Label)> DiagramPlaces = new()
        {
            ["kingston-upon-thames"] = (10, DiagramLabelSide.Right, "Kingston"),
            ["wimbledon"] = (42, DiagramLabelSide.Right, null),
            ["worcester-park-ewell"] = (61, DiagramLabelSide.Right, "Worcester Park"),
            ["sutton"] = (88, DiagramLabelSide.Right, null),
            ["banstead"] = (122, DiagramLabelSide.Right, null),
            ["epsom"] = (138, DiagramLabelSide.Left, null),
            ["reigate"] = (154, DiagramLabelSide.Right, null),
            ["dorking"] = (189, DiagramLabelSide.Below, null),
            ["leatherhead"] = (196, DiagramLabelSide.Left, null),
            ["guildford"] = (234, DiagramLabelSide.Left, null),
            ["cobham"] = (247, DiagramLabelSide.Left, null),
            ["esher"] = (284, DiagramLabelSide.Left, null),
            ["walton-on-thames-weybridge"] = (290, DiagramLabelSide.Left, "Walton & Weybridge"),
            ["surbiton"] = (345, DiagramLabelSide.Above, null),

            // The six towns of Section 3em, placed from their first postcode district.
            ["richmond"] = (4, DiagramLabelSide.Right, null),
            ["new-malden"] = (38, DiagramLabelSide.Right, null),
            ["cheam"] = (82, DiagramLabelSide.Right, null),
            ["ashtead"] = (180, DiagramLabelSide.Right, null),
            ["molesey"] = (315, DiagramLabelSide.Left, null),
            ["twickenham"] = (351, DiagramLabelSide.Left, null),
        };

        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            // Chessington, Surbiton, Kingston, Worcester Park & Ewell and Esher carry more local text
            // since PROJECT_STATE.md Section 3en: what they said before, then the town's facts in
            // paragraphs. A database that already had them got it from the
            // AddLocalTextToFiveTownAreas migration, which holds the same text.
            var areas = new[]
            {
                new
                {
                    Slug = "chessington",
                    Name = "Chessington",
                    DriveTimeMinutes = 0,
                    IsFeatured = true,
                    Districts = "KT9",
                    IntroCopy = "Chessington is where it all starts for us - our technicians are dispatched from right here, so it's usually our fastest turnaround area for both handyman and plumbing work.\n\nChessington was still farmland at the start of the 20th century. Kingston Council's character study describes how it was then built in pockets, each over a short time: the late 1930s, the 1950s, the 1970s. A street here is usually of one decade, so your address tells us the age of the house before we set off.",
                    NeighbourhoodsCopy = "Much of it is 1930s. Moorfield Road is semi-detached, with tile-hung gables and oriel windows. Fircroft Road and Hook Rise have semis with mansard fronts, and Cheshire Gardens is short terraces with tile-hung bays. The terraces around Compton Crescent are pebbledashed under clay pantiles.\n\nThe 1950s brought the council-built terraces of Sanger Avenue and Coutts Avenue, with pantiled roofs and the Surbiton Borough Council emblem on them, and the short terraces of Chantry and Mount Roads. Garrison Lane is mostly post-war: flats, maisonettes and semis. The estate at the east end of Chantry Road is from the 1970s and 80s, and Merritt Gardens was laid out in the 1990s on the site of the Air Ministry's rehabilitation centre.\n\nHook, either side of Hook Road, is mostly 1930s semis as well, though parts of Clayton, Haycroft and Gladstone Roads go back to the 1880s. Malden Rushett, further down the Leatherhead Road, is a hamlet of Victorian cottages and villas, with 1950s houses in Woodview.\n\nWe know the mix of properties that make up Chessington inside out.",
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
                    DriveTimeMinutes = 8,
                    IsFeatured = false,
                    Districts = "KT5, KT6, KT7",
                    IntroCopy = "Surbiton's blend of Victorian conservation-area streets and riverside apartments means every job is a little different, and our technicians are equipped for both.\n\nSurbiton was planned around its station. Thomas Pooley bought most of Maple Farm and drew up a new town with the station at its heart, and the grid of roads between Maple Road and the river was laid out as part of it. Many of the Victorian houses still stand. On St Mark's Hill many of the large villas have been divided into flats, and on Uxbridge and Palace Roads and Surbiton Crescent big houses have given way to blocks of flats.\n\nIn a divided house the stop valve for your flat is not always inside your flat. If you know where yours is, say so when you book. If you do not, finding it is the first thing we do.",
                    NeighbourhoodsCopy = "We regularly work on the grand Victorian terraces around Maple Road and St Mark's Hill, the mansion flats near Surbiton station, and the quieter family homes towards Berrylands and Tolworth.\n\nOne street is not like the next. St Andrew's Square is late Victorian terraces and semis round a central garden. Cleveland Road is narrow plots and short terraces, while Cadogan and Grove Roads have substantial villas. The Mall and the south side of Westfield Road were not built until the 1930s. Along Portsmouth Road the blocks of flats facing the river rise to as much as nine storeys.\n\nEast of the centre the houses get later: Edwardian around Cranes Drive and Guildford Avenue, then streets planned in the 1920s and 30s. Berrylands was farmland until the early 1930s and was built up within about ten years, mostly as semi-detached houses on long avenues with grass verges.\n\nKingston Council's character study says the streets round the town centre are \"sometimes overwhelmed with parking\". If yours is one of them, tell us where a van can stop.",
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
                    DriveTimeMinutes = 10,
                    IsFeatured = true,
                    Districts = "KT1, KT2, KT3",
                    IntroCopy = "From the Victorian terraces around Norbiton to the riverside flats by Kingston Bridge and the family homes stretching out towards Coombe and Berrylands, our local technicians already know the mix of properties that make up Kingston. One call books the right person for the job.\n\nCanbury, north of the station, was open fields until the railway reached Kingston in 1863. What was built there is a grid of two-storey Victorian houses with small front gardens and their back gardens back to back. South of the centre, the farmland of Grove was sold off in parcels for building from 1865.\n\nHouses of that age have solid brick walls, and a shelf or a television bracket goes onto them differently from the plasterboard of a newer flat. Say which you have when you book and the right fixings come with us.",
                    NeighbourhoodsCopy = "We're on your side of the borough already, with no travel charge and no call-out fee within our standard service area.\n\nMost of Norbiton north of the cemetery dates from the turn of the last century. Chatham, Chesham, Cobham and Clevedon Roads are late Victorian detached villas, and Dawson Road is Edwardian, two storeys with ground-floor bays. Between Richmond Road and the riverside park at Canbury Gardens the side roads are late Victorian too, with mansions of that date on Albany Park Road.\n\nNorth Kingston's Tudor Estate was built by G T Crouch and finished in the mid-1930s. The council's character study notes how many of its houses have been extended since. In Coombe the houses are detached, on large plots, and many of the roads are private.",
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
                    DriveTimeMinutes = 10,
                    IsFeatured = false,
                    Districts = "KT4, KT17, KT19",
                    IntroCopy = "Worcester Park and Ewell's rows of well-kept 1930s semis are some of the most consistent handyman and plumbing work we take on - reliable properties, straightforward access, and usually a quick turnaround.\n\nBoth places were built up between the wars. Worcester Park's station opened in 1859, on what was then Malden Green, but Sutton Council's character study says the area largely developed in the interwar years. At Ewell the railway was electrified in the 1920s, trains to London became more frequent, and developers bought up the farmland for housing estates.\n\nA house built before 1970 may have lead pipes, the Drinking Water Inspectorate says, and most houses here were. The place to look is the stop tap where the water comes in: unpainted lead is dull grey, often with a swollen joint next to the tap. If that is what you find, tell us when you book.",
                    NeighbourhoodsCopy = "Whether it's a semi off Green Lane in Worcester Park or a family home near Ewell Village and Bourne Hall, we know the layout of these estates well enough to arrive prepared.\n\nIn Worcester Park the houses near the station are semi-detached pairs and short terraces with gardens front and back, many of them half-timbered. The Victorian terraces of Longfellow Road are older than their neighbours, and The Hamptons, with its timber-boarded houses, was finished around 2010 on the site of a sewage works.\n\nEwell is far older. Its name comes from the Old English for a river spring, and the springs that rise by Bourne Hall are the source of the Hogsmill. Epsom & Ewell Council's appraisal of the village counts timber-framed houses of the 16th and 17th centuries among its 100 listed entries, and there are weatherboarded cottages in West Street, Kingston Road and Mill Lane. Beyond the bypass of 1932 and the railway, the estates are from between the wars.\n\nThe water is hard as well: 14 Clarke degrees in SES Water's 2025 report for Ewell, from the village to Ewell Court, and for the Sutton side of KT4. Limescale on taps and in cisterns is ordinary here.",
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
                    DriveTimeMinutes = 12,
                    IsFeatured = true,
                    Districts = "KT17, KT18, KT19, KT21",
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
                    DriveTimeMinutes = 15,
                    IsFeatured = true,
                    Districts = "SM1, SM2, SM3, SM5, SM6",
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
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    Districts = "SM7, KT20",
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
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    Districts = "KT8, KT10",
                    IntroCopy = "Esher has older cottages and period houses around the High Street and Esher Green, family homes in the roads towards Hinchley Wood and Claygate, and large detached houses on its quieter roads. Older pipework and original bathrooms bring the usual dripping taps, leaking joints and blocked sinks. We are about 15 minutes away in Chessington and bring the tools and common parts to finish most jobs in one visit.\n\nEsher's greens were kept open on purpose. The owners of the Claremont Estate were also lords of the manor, and they made sure the land around the town stayed undeveloped. Building came in the 20th century, in small and medium parcels, and Elmbridge Council describes the result as mostly large detached houses, many of them on private or semi-private estates protected by covenants.\n\nSome of those estates are gated. If yours is, give us the gate code or a number to ring when you book.",
                    NeighbourhoodsCopy = "On Clare Hill, south of the centre, early 20th-century houses stand on the edge of Claremont's 18th-century park. The estate insisted on a single architect, Blair Imrie, for most of them, and he favoured bricks in tan, peach, orange and brown with cream mortar. A wall patched there has those shades to match.\n\nEsher Place, north of the centre, is a 1930s extension: large houses on straight, tree-lined roads. Lower Green, on the far side of Sandown Park, is different again, with two-storey houses from between the wars and after, some of them terraced. West End is a village of its own round a triangular green, where buildings are rarely over two and a half storeys and often of cottage scale.\n\nMaterials for higher-spec fixtures are always agreed with you up front.",
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
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    Districts = "KT22, KT23",
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
                    DriveTimeMinutes = 20,
                    IsFeatured = false,
                    Districts = "SW19, SW20, SM4",
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
                    DriveTimeMinutes = 20,
                    IsFeatured = true,
                    Districts = "KT11",
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
                    DriveTimeMinutes = 22,
                    IsFeatured = false,
                    Districts = "KT12, KT13",
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
                    DriveTimeMinutes = 25,
                    IsFeatured = false,
                    Districts = "RH1, RH2",
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
                    DriveTimeMinutes = 25,
                    IsFeatured = false,
                    Districts = "RH4, RH5",
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
                    DriveTimeMinutes = 23,
                    IsFeatured = true,
                    Districts = "GU1, GU2, GU4",
                    IntroCopy = "Guildford is the largest town in our Surrey coverage, from the historic streets around the castle and High Street to the newer estates on the outskirts - and we're ready to take on the full range of handyman and plumbing work it brings.",
                    NeighbourhoodsCopy = "We work across the period terraces near the town centre, the family homes towards Merrow and Burpham, and the newer developments around Park Barn.",
                    Faqs = new[]
                    {
                        ("Do you cover all parts of Guildford?", "Yes, our Guildford coverage spans the town centre and the surrounding residential areas."),
                        ("Can I book multiple jobs in one visit in Guildford?", "Yes - many customers save time by listing several smaller jobs for one visit."),
                    },
                },

                // The six towns added in November 2026 (PROJECT_STATE.md Section 3em), with texts written
                // from the councils' own planning documents. A database that already had areas got them
                // from the AddSixNewTownAreas migration, which holds the same texts.
                new
                {
                    Slug = "twickenham",
                    Name = "Twickenham",
                    DriveTimeMinutes = 25,
                    IsFeatured = false,
                    Districts = "TW1, TW2",
                    IntroCopy = "Twickenham's streets were built in very different periods, and the houses show it. Montpelier Row is tall Georgian townhouses. Queen's Road is mostly Victorian terraces in yellow stock brick, with slate roofs and timber sash windows. Cole Park Road was built up with large detached homes in the Edwardian period, and the Leeson Estate was laid out in the 1930s as semi-detached and terraced houses in stucco and red brick.\n\nTell us which of these yours is when you book: a radiator valve or a wall fixing in a Victorian terrace is not the same job as in a 1930s semi.",
                    NeighbourhoodsCopy = "Around Queen's Road the bigger town houses run to three and a half storeys with a lower ground floor, and Grosvenor Road is plain Victorian cottages under shallow slate roofs. In brick houses of that age a television bracket or a run of shelves goes into masonry, with the fixings for it, not into plasterboard.\n\nThe inter-war streets are different. The Rivermead Estate and Lincoln Avenue are garden-city style semis and short terraces, and Campbell Close is an early Span development from about 1955. Where a house like these has had its kitchen moved or an extension added, we trace where the pipes now run before we cut into anything.\n\nParking is worth a minute before we come. If your road is in one of Richmond Council's controlled parking zones, the council sells trader permits for a workman's vehicle, as a scratch card or pay-as-you-go. With one ready, the van is outside your door and not streets away.",
                    Faqs = new[]
                    {
                        ("Can you park outside my house in Twickenham on an event day?", "Yes, with a permit to show. Roads in Richmond Council's Twickenham Event Zone (R) need one on event days, as the signs at the entry to the zone say, and the council issues trader permits for people working on your property. Ask for one before the visit and tell us when you book."),
                    },
                },
                new
                {
                    Slug = "richmond",
                    Name = "Richmond",
                    DriveTimeMinutes = 29,
                    IsFeatured = false,
                    Districts = "TW9, TW10",
                    IntroCopy = "Around Richmond Green the houses are mainly late 17th and early 18th century terraces of townhouses; Maids of Honour Row dates from 1724-5 and Old Palace Terrace is listed. Elsewhere in the town, The Alberts were developed in the 1870s as tightly packed two-storey terraces with slate roofs on narrow streets, and the roads west of Queen's Road are large Edwardian and inter-war semis.\n\nIn a listed house we keep to repairs: a tap re-seated, a cistern mended, a door eased. Nothing that would need consent is altered.",
                    NeighbourhoodsCopy = "On the hill, numbers 3 to 17 Hill Rise are three storeys with shops at street level and homes above, and 21 to 33 Petersham Road is a terrace of three-storey Victorian houses with double-height bay windows. If you live above a shop, find out where your stop valve is before we come, or ask the managing agent. It is not always inside the flat.\n\nNorth of Sheen Road, up to the railway, the streets are rows of large late Victorian terraced and semi-detached houses with slate roofs.\n\nParking decides how long a visit to the centre takes. Richmond Council's Richmond Town zone (A1) runs Monday to Saturday, 8.30am to 6.30pm, in part of the zone. The council sells trader permits for a workman's vehicle, as a scratch card or pay-as-you-go, and one in hand saves a walk from a car park with the tools.",
                    Faqs = new[]
                    {
                        ("Do you work in the listed houses around Richmond Green?", "Yes, for repairs and like-for-like replacements: taps, toilets, radiators, doors and locks. We do not alter the fabric of a listed building, and anything that needs listed building consent from Richmond Council is yours to arrange before we start."),
                    },
                },
                new
                {
                    Slug = "molesey",
                    Name = "Molesey",
                    DriveTimeMinutes = 13,
                    IsFeatured = false,
                    Districts = "KT8",
                    IntroCopy = "Molesey gets newer as you go west. Kent Town, across the river from Hampton Court Palace, was laid out in the mid-19th century with large detached villas in London stock brick and stucco under slate roofs. West Molesey's Howard Houses, some 300 of them around First Avenue, date from about 1933. The Hurst Park estate, long terraces of two and three storey houses with high-rise flats north of Victoria Road, is from the 1970s.\n\nElmbridge Council notes how much of this housing has since been extended, altered and remodelled. Pipes and wiring move with every round of that, so tell us what has been changed in yours.",
                    NeighbourhoodsCopy = "Inside Kent Town the houses step down from north to south: the largest, some of four storeys, are at the north end, and the roads finish in modest two-storey cottages by Walton Road. Around Bridge Road and Creek Road the shops have homes above them.\n\nWest Molesey, between Walton Road and Hurst Road, is mostly two-storey semi-detached houses with some bungalows, the same house type repeated along a street. Most of the Howard Houses have had a pitched roof added since they were built.\n\nThe Thames runs along the north side of Molesey and the River Ember along the east, with reservoirs and gravel pits to the west and south. We cover all of it, Hurst Park included.",
                    Faqs = new[]
                    {
                        ("Do you cover both East and West Molesey?", "Yes, both, and Hurst Park between them and the river. The two are one stretch of houses, older by Hampton Court Bridge and newer towards the reservoirs, and a visit costs the same anywhere in it."),
                    },
                },
                new
                {
                    Slug = "new-malden",
                    Name = "New Malden",
                    DriveTimeMinutes = 9,
                    IsFeatured = false,
                    Districts = "KT3",
                    IntroCopy = "New Malden is a railway town. Its station opened in 1846, seventeen years before Kingston's, and the first streets laid out were the six Groves west of the High Street: Acacia, Chestnut, Elm, Lime, Poplar and Sycamore, each planted with the tree it is named after. The plots there filled slowly, the last of them not until 1960, so houses of the 1860s and of the 1950s share a road.\n\nThe age of a house tells us what its pipes and walls are likely to be made of, so it is worth a line in the description box when you book.",
                    NeighbourhoodsCopy = "East of the High Street, by Beverley Park, substantial Edwardian houses stand among semi-detached and detached houses of the 1930s. Graham Road and Albemarle Gardens are Edwardian terraces and semis with tile hanging, roughcast render and leaded panels in the windows.\n\nFurther out the houses are later: 1930s semis and short terraces on Green Lane, tile-hung bays on Oxford Crescent, 1950s bungalows on Windsor Avenue. South Lane was cut in two by the A3 in the 1920s. Where a house of that age has had its kitchen moved or an extension added, the pipes no longer run where the builder put them, and we find them first.\n\nIf your road has permit parking, Kingston Council sells visitor vouchers, though not for every zone. You buy them online and start each one with the vehicle's registration number, so ask us for the van's when you book.",
                    Faqs = new[]
                    {
                        ("My road in New Malden has permit parking. What do you need from me?", "A visitor voucher for the time we are with you. Kingston Council sells them online, though not for every zone, and you do not need a resident permit of your own to buy one. Each voucher is started with our van's registration number, the date and the time."),
                    },
                },
                new
                {
                    Slug = "ashtead",
                    Name = "Ashtead",
                    DriveTimeMinutes = 14,
                    IsFeatured = false,
                    Districts = "KT21",
                    IntroCopy = "Ashtead grew up on the spring line, where the chalk to the south meets the clay to the north. The oldest houses are in the Village: timber-framed buildings on Rectory Lane and, south of The Street, narrow streets of Victorian semi-detached and terraced cottages. Lower Ashtead, to the north, is younger and more mixed, with whole roads of semis begun in the 1930s and carried on in the 1950s.\n\nSES Water classes the water here as moderately hard, so on a plumbing job in Ashtead we check for limescale early: on taps, in shower heads and in cisterns.",
                    NeighbourhoodsCopy = "Woodfield Lane has a row of 14 Edwardian houses that is a conservation area. Around it, Woodfield and the roads from Oakfield Road to The Marld are almost all detached houses in large gardens, the earliest from between the wars.\n\nThe Berg Estate, north and east of the level crossing, was built just before the Second World War in brick, render and clay tiles, with long, narrow back gardens. A garden that long is where an outside tap is worth having: we fit one with a double-check valve, connected to the supply indoors.\n\nSES Water's 2025 report for the middle of Ashtead gives the hardness as 14 Clarke degrees. Limescale, in SES's words, can build up in pipes, on fittings and in kettles. When a tap drips or a shower runs weak, scale in the cartridge or the head is where we start.",
                    Faqs = new[]
                    {
                        ("Is the water in Ashtead hard?", "Moderately. SES Water's 2025 report for central Ashtead puts it in a medium hardness zone, at 14 Clarke degrees. That is enough to scale up taps, shower heads and toilet cisterns over a few years. SES has a postcode search on its website that gives the figure for your own address, which is the one to use when you set a dishwasher."),
                    },
                },
                new
                {
                    Slug = "cheam",
                    Name = "Cheam",
                    DriveTimeMinutes = 15,
                    IsFeatured = false,
                    Districts = "SM3, SM2",
                    IntroCopy = "Cheam stayed a quiet village until the 1920s: in 1801 it had 616 people. Then, during the 1920s and early 1930s, farm and brewery land at the crossroads was released for building, Malden Road was widened and lined with new mock Tudor shops, and the houses around the village went up. Sutton Council calls them typical inter-war housing stock.\n\nSo most of Cheam is about a hundred years old, the age at which original taps, valves and pipe joints come to the end of their life.",
                    NeighbourhoodsCopy = "The older Cheam is around Park Lane and Whitehall, a timber-framed house built in the early 1500s that is now a museum. The listed buildings there date from the 16th to the 19th centuries, and Jubilee Road has a terrace of Victorian houses.\n\nSouth Cheam is different again. The Burton Estates were built between the wars as large detached houses on large plots, in an Arts and Crafts manner, and their builder said there was no such thing as a standard type of house on the estate. The Drive and parts of Burdon Lane are older, Edwardian.\n\nSES Water classes the water in central Cheam as hard: 15 Clarke degrees in its 2025 report. A tap that has gone stiff or a toilet that fills slowly can be limescale, and we clear or replace the scaled part before anything bigger.",
                    Faqs = new[]
                    {
                        ("Do I get free visitor parking for a tradesman in Cheam?", "Yes, if your road is in a controlled parking zone. Sutton Council gives each household in a zone 50 free hours of visitor vouchers a year, added through your online account, and you do not need a resident permit to apply."),
                    },
                },
            };

            // Into an empty table only, as ServicesSeeder does and for the same reason. Looking
            // each area up by slug and adding the missing ones brought an area the admin had
            // deleted back at the next start, and gave an area whose questions the admin had
            // removed its two seeded questions again (PROJECT_STATE Section 3cb).
            if (dbContext.ServiceAreas.IgnoreQueryFilters().Any())
            {
                return;
            }

            foreach (var item in areas)
            {
                var area = new ServiceArea
                {
                    Slug = item.Slug,
                    Name = item.Name,
                    DriveTimeMinutes = item.DriveTimeMinutes,
                    IntroCopy = item.IntroCopy,
                    LocalNeighbourhoodsCopy = item.NeighbourhoodsCopy,
                    IsFeatured = item.IsFeatured,

                    // The districts each area starts with. Databases that already had the areas
                    // got the same lists from the AddPostcodeDistrictsToServiceArea migration.
                    PostcodeDistricts = item.Districts,
                };

                if (DiagramPlaces.TryGetValue(item.Slug, out var place))
                {
                    area.DiagramBearing = place.Bearing;
                    area.DiagramLabelSide = place.Side;
                    area.DiagramLabel = place.Label;
                }

                var displayOrder = 1;
                foreach ((string question, string answer) in item.Faqs)
                {
                    area.Faqs.Add(new ServiceAreaFaq
                    {
                        Question = question,
                        Answer = answer,
                        DisplayOrder = displayOrder++,
                    });
                }

                await dbContext.ServiceAreas.AddAsync(area);
            }

            // One save for all of them, so a failed start cannot leave a part-filled table.
            await dbContext.SaveChangesAsync();
        }
    }
}
