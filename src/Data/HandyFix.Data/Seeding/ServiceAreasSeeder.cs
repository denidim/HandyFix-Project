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
            // The first fifteen towns carry more local text: what they said before, then the town's
            // facts in paragraphs. Chessington, Surbiton, Kingston, Worcester Park & Ewell and Esher
            // since PROJECT_STATE.md Section 3en, the other ten since Section 3eo. A database that
            // already had them got it from the AddLocalTextToFiveTownAreas and
            // AddLocalTextToTenTownAreas migrations, which hold the same text.
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
                    IntroCopy = "Epsom has been one of our busiest areas for years, from the terraces near the town centre to the larger detached homes out towards the Downs and the racecourse.\n\nEpsom was a spa before it was a racing town. In the dry summer of 1618 cattle were seen to avoid one spring, the spring was made into a well, and the village grew quickly into a resort for Londoners. Epsom & Ewell Council's appraisal of the town centre says many buildings of those years went up in haste, a timber frame faced in weatherboarding, and some still stand in South Street. The brickworks on the commons expanded from 1663 to meet the demand.\n\nThe town as it is now took shape from the middle of the 19th century. Along College Road and the north-eastern end of Burgh Heath Road the later Victorian houses, some of them in terraces, are of local red brick with sash windows and panelled doorways under slate or tile. A panelled door of that age that sticks or has dropped on its hinges is a job we do by the hour.",
                    NeighbourhoodsCopy = "We're familiar with everything from the Victorian streets around Epsom station to the newer developments near Nonsuch Park and the period homes bordering Epsom Common.\n\nSouth of the station, the fields either side of Church Street were filled with houses and cottages between the 1870s and 1900, and the streets behind it, Heathcote Road and Laburnum Road among them, still have their 19th-century houses. Further out, the land along Burgh Heath Road became a spacious suburb between the late 1890s and the First World War. Lynwood Road has large detached houses with Arts and Crafts details, and the Chase Estate, which laid out Downs Avenue, also built in Temple Road, Chase Road and around West Hill.\n\nThe Downs Road Estate is another thing altogether. Wates built it from the late 1950s to designs by their own architect, Kenneth Bland, with clay pantile roofs and, on some of the houses, a mansard first floor.\n\nBy the Common, Stamford Green has late 19th-century cottages, many of them built to house staff of the five hospitals that went up nearby between 1890 and 1915. The first houses of the Hookfield estate were ready for tenants by 1921, and the housing round the Common went on improving through the 1920s and 30s.",
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
                    IntroCopy = "Sutton's mix of busy high-street flats and the leafier streets towards Cheam and Belmont keeps our technicians well practised across almost every job type.\n\nStagecoaches made Sutton before trains did. From the 18th century it was the first stop on one of the most direct roads from London to Brighton. The railway came in 1847, with a second line in 1865 and a third in 1868, and Sutton Council's character study dates the town's building boom from there. Mains water and sewage works had arrived by 1863.\n\nTown and suburb meet without warning. The study notes how the tall buildings of the High Street give way at once to two-storey Victorian terraces, and south of the centre the blocks of flats built from the 1970s onwards stand where large Victorian and Edwardian houses used to be. If yours is one of the flats, tell us the floor and whether there is a lift when you book: it decides what we carry up.",
                    NeighbourhoodsCopy = "From the Victorian terraces near Sutton station to the larger family homes around Rosehill and Benhilton, we've built up a good working knowledge of the area.\n\nEast of the High Street, Newtown is two-storey Victorian terraces with small front gardens, and Victoria Road is much the same behind small walled ones. Round Beulah Road and Clarence Road the houses are late Victorian, with some early Edwardian semis. Landseer Road, to the south, is Edwardian and was finished by 1913.\n\nNorth of the centre most of the houses came in the early 20th century and between the wars: streets of semis, and Sutton Garden Suburb, begun in 1912 as cottage-style terraces and semis round small greens. The study says these houses have stayed much as they were built, apart from small extensions, loft conversions and front gardens turned into drives. Beyond them, at Rosehill, is the St Helier Estate, in its day one of the largest social housing estates in Europe.\n\nBelmont has grown bit by bit since its station opened in 1865. Carshalton changed faster: Carshalton Beeches station opened in 1906, and in the 1920s and 30s the fields round the old village and its ponds were covered with two-storey houses.\n\nThe water is hard: 14 Clarke degrees in SES Water's 2025 report for the town centre and Sutton North, and 15 in Carshalton and Wallington.",
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
                    IntroCopy = "Banstead's mix of large detached homes and village-edge cottages means jobs here are often bigger in scope, and our technicians carry the range of parts to match.\n\nFor most of its history Banstead was a farming village on the chalk. In the 17th century it had a name as a health resort, with riding, hunting, shooting and horse racing, and in the 18th the big houses were built: Nork House, Garratts Hall, Banstead Place, Rooks Nest and Yewlands. What stands now is later. Reigate & Banstead Council's design guide says the town centre reflects the building of the 1930s to the 1950s, with few buildings older than the 20th century.\n\nThose were the years when the farmland here, at Tadworth and at Burgh Heath went under housing estates: bay-windowed houses, almost all in brick and tile. The older material is flint. On the Downs it is in garden walls as much as in houses, usually framed in orange-red brick, so if a wall needs repair, tell us whether it is flint or brick when you book.",
                    NeighbourhoodsCopy = "We know the leafy roads around Banstead Downs and the High Street, as well as the newer homes towards Tattenham Corner.\n\nThe council's guide picks out Longcroft Avenue as a highly decorated example of the houses of the 1930s to the 1950s, and its earlier edition names Nork among the suburbs of those years. Tadworth's private estates were built for commuters, within a walk of the station.\n\nKingswood grew up with its railway station too. The Kingswood Warren estate was sold in the early 20th century and built on over a number of years, as large houses at low density among the trees. The Preston Estate is council housing of the 1960s and 70s.\n\nThe water is hard: 14 Clarke degrees in SES Water's 2025 report for Nork and for Tattenham Corner. Further south, round Tadworth and Lower Kingswood, the figure is the same and SES classes it as moderately hard.",
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
                    IntroCopy = "Leatherhead sits right on our route down the A243, so it's an easy area for us to reach quickly, whatever the job.\n\nLeatherhead grew up where the River Mole could be forded, and was laid out round the crossroads of Bridge Street, High Street, North Street and Church Street, probably in the 13th century. It lived mostly by farming until the 20th. The railway came in 1859, and the land south of the High Street, old common-field strips crossed by Church Road, was built on only after 1870.\n\nSo the old centre is small, and round it the houses are Victorian and Edwardian: brick and slate cottages on narrow streets and footpaths, and substantial red-brick villas on the steep hill of Park Rise. If your front door opens onto one of those footpaths, say so when you book, so we know how far the tools have to be carried.",
                    NeighbourhoodsCopy = "Our technicians regularly work on the period homes around Leatherhead town centre, the riverside properties near the River Mole, and the family estates towards Fetcham and the Oxshott borders.\n\nMole Valley Council's design code for the town, published in November 2025, sorts the rest by age. Near the station most of the houses are from the 1930s, substantial and well built. North of the railway, older houses face Kingston Road and most of what lies behind is two large developments of the 1950s, semi-detached and terraced. Towards Epsom Road the houses and bungalows are mostly from the 1930s and the 1950s, in red brick, pale render and clay tile, with 1970s houses and flats at Levett Road and Cressall Mead. Along Reigate Road and Headley Road the houses are Edwardian and inter-war, each to its own design and set well back in its grounds.\n\nFlint is the local material beside the red brick. The retaining walls on Gimcrack Hill are flint, and very high.\n\nFetcham grew most in the 1930s and the 1950s, when the estates in the north of the village were built. Its old centre is half a kilometre further south, on The Street, where the houses run from the early 17th century to the 20th.\n\nThe water in Leatherhead, Fetcham and Bookham is moderately hard: 14 Clarke degrees in SES Water's 2025 report.",
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
                    IntroCopy = "Wimbledon's grand Victorian houses and the busy flats around the Broadway and station keep our plumbing and carpentry teams equally busy.\n\nWimbledon started at the top of the hill, where the Village is, and came down it. The railway opened in 1838 and the centre of the town moved to the station. In the second half of the 19th century villas and terraces spread out between the two, and Merton Council's character study puts the peak of the building in the 1930s.\n\nThe study also records what became of the biggest houses: during the Second World War many of the largest Victorian ones were divided into flats or replaced with blocks. If your home is a flat in one of them, tell us when you book how we get in: a street door of your own, an entry phone or a porter.",
                    NeighbourhoodsCopy = "We're well practised on the large period homes near Wimbledon Village and the Common, as well as the denser housing around South Wimbledon and Merton Park.\n\nEast of the centre, round Queen's Road, the streets are a grid of terraces, with double-fronted villas facing South Park Gardens. The terraces carry on north towards Durnsford Road and Plough Lane, dense and all of a piece, the Haydons Park estate among them. South-west of the centre, Wimbledon Chase is quiet Victorian and Edwardian streets, and the roads that climb west from Worple Road mix villas, terraced cottages and mansion blocks.\n\nUp the hill it opens out. On the Ridgway the plots are large and the ground steep, with detached houses, cottages and blocks of flats stepping up the slope. Parkside and West Wimbledon are substantial detached houses on big plots, many of them on quiet dead-end roads.\n\nMerton Park was developed by John Innes at the end of the 19th century, on early garden-suburb lines. The centre of South Wimbledon stands on what was Admiral Lord Nelson's estate. Morden is later: the Northern line reached it in 1926, the streets grew up round the station, and most of it is suburban houses and cottage estates.",
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
                    IntroCopy = "Cobham's mix of period cottages around the village, larger family homes along Fairmile, and the newer executive developments near Stoke D'Abernon all come with their own maintenance quirks - from listed-building-sensitive repairs to keeping a large garden and outbuildings in order. Our technicians carry the range of tools and parts to handle both.\n\nCobham used to be two places: Street Cobham on the Portsmouth Road and Church Cobham by the river, joined up mostly in the 20th century. The 19th century added streets to the north-east, and the railway, with stations at Stoke D'Abernon and Oxshott, brought more around 1900. After that came the private estates. Elmbridge Council's guide describes how a large house would be pulled down and its grounds built over.\n\nFairmile is a number of those estates, with hedges and trees for boundaries, no pavements and no street lighting. On a winter afternoon a house name is hard to read from the road, so give us a landmark when you book, or leave a light on.",
                    NeighbourhoodsCopy = "Round Tartar Hill the streets are a tight late Victorian and Edwardian grid, terraces and semis with small front gardens, on the lower parts of Hogshill Lane, Cedar Road and Spencer Road. Freelands Road and the top of Hogshill Lane came next, with larger houses set back in their gardens.\n\nBy the river, on Mill Road and The Tilt, the houses are older and individually designed, often in pairs and rarely over two storeys. Downside stands apart, across the Green Belt: cottage-sized houses round a large green, with post-war semis to the south. Near Stoke D'Abernon station the housing is denser, on narrow roads where most front gardens have become parking.\n\nMaterials for higher-spec fixtures are sourced and agreed with you up front, so a premium finish never means a surprise on the invoice.",
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
                    IntroCopy = "Walton-on-Thames and Weybridge's riverside setting brings a steady mix of premium apartments and larger family homes, many changing hands or tenants often enough that a reliable local handyman matters.\n\nWalton is the largest town in Elmbridge, and most of it is 20th-century. Until the 1920s the south side of the High Street was the edge of Ashley Park, the grounds of a house built between 1602 and 1607. The house came down in 1929 and the park was sold off in half-acre plots from 1930. It is a private estate of large detached houses now.\n\nWeybridge began as a village at a ford on the River Wey and grew up beside two great estates, Oatlands Palace of the mid-16th century and Dorchester House of the 1670s. St George's Hill came after: large gated houses on wooded plots that were, historically, never less than an acre.\n\nOn plots of half an acre and more, the tap by the back door is a long way from the far end of the garden. A second outside tap is a small job, and one we fit.",
                    NeighbourhoodsCopy = "We regularly work along the riverside developments near Walton Bridge, the period housing around Weybridge village, and the newer estates towards St George's Hill.\n\nIn Walton, Winchester Road was laid out in the late 19th century and became the spine of a tight grid of streets filled in over the next fifty years. East of Sidney Road the houses are inter-war and post-war, and Rydens has what Elmbridge Council's guide calls possibly the largest concentration of single-storey and chalet-style houses in the borough. Old Walton, by the river, is modest two-storey houses where many of the roof spaces have been converted. The tallest flats are in the town centre, seven storeys at The Heart and ten beside Wellington Close.\n\nIn Weybridge the roads off the High Street, to the north above all, are Victorian and Edwardian: red brick, sash windows and low front walls or hedges, with small gardens and parking on the street. Portmore Park Road has very late Victorian and Edwardian houses on large plots, and the villas and terraces round Heath Road, on the way in from the station, are of the same years.",
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
                    IntroCopy = "Reigate's mix of period homes near the town centre and larger properties towards the hill keeps our technicians on their toes, and it's an area we're keen to build a stronger presence in.\n\nReigate was laid out as a planned town before 1170, after the Warennes built the castle. The road to Brighton was turnpiked in 1755, the station opened in 1849, and by 1860 many houses had been built for people who travelled to work in London. In 1921 \"the Great Sale of Reigate\" put shops, hotels, businesses and homes on the market, and Reigate & Banstead Council's design guide records that the rebuilding which followed cost the town many fine Georgian houses.\n\nReigate has a stone of its own, dug at Merstham: greenish-grey and easily worked, which means it wears. The guide says it is rare on houses, where brick with hung tiles on the upper floor is the usual thing. Where an old wall or a step is Reigate stone, expect wear. Small repairs to brickwork and paving are our work; a stone wall we look at first, and say plainly whether it is a job for us or for a mason.",
                    NeighbourhoodsCopy = "From the Georgian and Victorian houses around Reigate High Street to the family homes towards Redhill and Merstham, we cover the full spread of property types here.\n\nRedhill is the younger of the two towns. It was a hamlet in the early 19th century and grew with the London to Brighton railway and its station of 1841, housing railway workers as well as commuters. Much of its old centre went in the rebuilding of the 1960s to the 1980s.\n\nCouncil housing arrived between the wars, with later estates at Colesmead and Meadvale, and the big estates round Merstham were built for Londoners moving out after the war.\n\nSES Water's 2025 report gives 13 Clarke degrees for the centre of Reigate and for Redhill, and 12 for South Park and Woodhatch. It classes both as moderately hard.",
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
                    IntroCopy = "Dorking's market-town character means a lot of older housing stock, and our technicians are experienced at working carefully around period features without slowing the job down.\n\nDorking has had a market since at least 1278, when its lord claimed one by ancient right. The sandstone under the town is soft enough to dig, and there are caves cut into it behind the High Street and South Street, some of them made by mining the sand. What it was known for, though, was lime. Mole Valley Council's appraisal of the town records that London bricklayers favoured Dorking lime for their mortar and that, because it set under water, it went into the building of the London docks in 1805.\n\nThe town's older walls were laid and plastered with lime, not cement. Tell us the age of your house when you book brickwork or plastering, so that the right materials come with us.",
                    NeighbourhoodsCopy = "We cover the historic streets around Dorking High Street, the surrounding villages towards Box Hill, and the newer estates on the town's edges.\n\nSouth of the centre, where the town spread in the 19th century either side of the Horsham Road, the streets are Victorian and each has its own look: the plain terraced cottages of Mount Street, with no front gardens, against the semi-detached villas of Howard Road. Rose Hill is a set piece, Italianate and Tudor villas of 1838 to 1860 round a paddock where livestock still graze.\n\nThe 1930s took the town north-east, towards the meeting of the A24 and the A25. Along Deepdene Avenue and its side roads the houses are of the 1930s and the 1950s, and north of Ashcombe Road they are mainly detached and of the 1930s. Tower Hill, steep and looping, was largely built up in the 1970s.\n\nThe estates are on the southern edge. The Chart Downs houses were built by the council in the 1950s, in red brick and plain clay tile round a green. Goodwyns has terraces, small blocks of flats and, rare for a small market town, two tower blocks. Between them they have joined North Holmwood, once a village two kilometres out, to the town. Pixham, across the railway, has a church by Lutyens of 1903.\n\nSES Water's 2025 report puts Dorking's water at 14 Clarke degrees, which it classes as moderately hard.",
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
                    IntroCopy = "Guildford is the largest town in our Surrey coverage, from the historic streets around the castle and High Street to the newer estates on the outskirts - and we're ready to take on the full range of handyman and plumbing work it brings.\n\nGuildford grew where the old road along the chalk ridge dropped down to cross the River Wey. The chalk is still part of the town: the assessment written for Guildford Borough Council notes disused quarries and the entrances to old mines of clunch, the chalk stone that Castle Street and the castle were built with. The Wey was made navigable in 1653, the railway came in 1845, and it was the railway that set the population growing.\n\nA good deal of the town is on a slope. If there are steps up to your door, say so when you book anything heavy to be fitted, a washing machine or a radiator, so we come ready for the carry.",
                    NeighbourhoodsCopy = "We work across the period terraces near the town centre, the family homes towards Merrow and Burpham, and the newer developments around Park Barn.\n\nCharlotteville, south-east of the centre, was the first large estate. Dr T. Jenner Sells owned the land, named it after his wife and named the streets after famous doctors. Unusually, villas for the middle classes and smaller houses for workers were built side by side there.\n\nThe streets nearest the centre are late Victorian: straight roads of two-storey terraces and semis, with larger villas, many of them now flats, in brick with a little stone or flint. There are late Victorian terraces along Station View and north of North Street, and Georgian villas on the Portsmouth Road.\n\nOnslow Village was begun after the First World War, when soldiers coming home made the shortage of houses worse, on the hillside of the old royal deer park west of the town. The Downs limit building to the south, so the council estates that followed went north. Bellfields and Park Barn are large post-war estates. Since the 1960s more have been added at Rydes Hill and Boxgrove, and out at Burpham and Merrow.",
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
