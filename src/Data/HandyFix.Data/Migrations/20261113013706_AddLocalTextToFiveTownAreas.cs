using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalTextToFiveTownAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // More local text for five of the first fifteen towns: Chessington, Surbiton, Kingston,
            // Esher and Worcester Park & Ewell (PROJECT_STATE Section 3en). What each page said
            // before stays, and the town's facts follow in paragraphs. The seeder fills an empty table
            // only, so a database that already has the areas gets the text here; the seeder holds the
            // same text for a new database. By slug: on an empty table, or for an area the admin has
            // deleted, the UPDATE finds no row and does nothing.
            migrationBuilder.Sql(@"
UPDATE [ServiceAreas]
SET [IntroCopy] = N'Chessington is where it all starts for us - our technicians are dispatched from right here, so it''s usually our fastest turnaround area for both handyman and plumbing work.' + NCHAR(10) + NCHAR(10) + N'Chessington was still farmland at the start of the 20th century. Kingston Council''s character study describes how it was then built in pockets, each over a short time: the late 1930s, the 1950s, the 1970s. A street here is usually of one decade, so your address tells us the age of the house before we set off.',
    [LocalNeighbourhoodsCopy] = N'Much of it is 1930s. Moorfield Road is semi-detached, with tile-hung gables and oriel windows. Fircroft Road and Hook Rise have semis with mansard fronts, and Cheshire Gardens is short terraces with tile-hung bays. The terraces around Compton Crescent are pebbledashed under clay pantiles.' + NCHAR(10) + NCHAR(10) + N'The 1950s brought the council-built terraces of Sanger Avenue and Coutts Avenue, with pantiled roofs and the Surbiton Borough Council emblem on them, and the short terraces of Chantry and Mount Roads. Garrison Lane is mostly post-war: flats, maisonettes and semis. The estate at the east end of Chantry Road is from the 1970s and 80s, and Merritt Gardens was laid out in the 1990s on the site of the Air Ministry''s rehabilitation centre.' + NCHAR(10) + NCHAR(10) + N'Hook, either side of Hook Road, is mostly 1930s semis as well, though parts of Clayton, Haycroft and Gladstone Roads go back to the 1880s. Malden Rushett, further down the Leatherhead Road, is a hamlet of Victorian cottages and villas, with 1950s houses in Woodview.' + NCHAR(10) + NCHAR(10) + N'We know the mix of properties that make up Chessington inside out.'
WHERE [Slug] = N'chessington';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'Surbiton''s blend of Victorian conservation-area streets and riverside apartments means every job is a little different, and our technicians are equipped for both.' + NCHAR(10) + NCHAR(10) + N'Surbiton was planned around its station. Thomas Pooley bought most of Maple Farm and drew up a new town with the station at its heart, and the grid of roads between Maple Road and the river was laid out as part of it. Many of the Victorian houses still stand. On St Mark''s Hill many of the large villas have been divided into flats, and on Uxbridge and Palace Roads and Surbiton Crescent big houses have given way to blocks of flats.' + NCHAR(10) + NCHAR(10) + N'In a divided house the stop valve for your flat is not always inside your flat. If you know where yours is, say so when you book. If you do not, finding it is the first thing we do.',
    [LocalNeighbourhoodsCopy] = N'We regularly work on the grand Victorian terraces around Maple Road and St Mark''s Hill, the mansion flats near Surbiton station, and the quieter family homes towards Berrylands and Tolworth.' + NCHAR(10) + NCHAR(10) + N'One street is not like the next. St Andrew''s Square is late Victorian terraces and semis round a central garden. Cleveland Road is narrow plots and short terraces, while Cadogan and Grove Roads have substantial villas. The Mall and the south side of Westfield Road were not built until the 1930s. Along Portsmouth Road the blocks of flats facing the river rise to as much as nine storeys.' + NCHAR(10) + NCHAR(10) + N'East of the centre the houses get later: Edwardian around Cranes Drive and Guildford Avenue, then streets planned in the 1920s and 30s. Berrylands was farmland until the early 1930s and was built up within about ten years, mostly as semi-detached houses on long avenues with grass verges.' + NCHAR(10) + NCHAR(10) + N'Kingston Council''s character study says the streets round the town centre are ""sometimes overwhelmed with parking"". If yours is one of them, tell us where a van can stop.'
WHERE [Slug] = N'surbiton';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'From the Victorian terraces around Norbiton to the riverside flats by Kingston Bridge and the family homes stretching out towards Coombe and Berrylands, our local technicians already know the mix of properties that make up Kingston. One call books the right person for the job.' + NCHAR(10) + NCHAR(10) + N'Canbury, north of the station, was open fields until the railway reached Kingston in 1863. What was built there is a grid of two-storey Victorian houses with small front gardens and their back gardens back to back. South of the centre, the farmland of Grove was sold off in parcels for building from 1865.' + NCHAR(10) + NCHAR(10) + N'Houses of that age have solid brick walls, and a shelf or a television bracket goes onto them differently from the plasterboard of a newer flat. Say which you have when you book and the right fixings come with us.',
    [LocalNeighbourhoodsCopy] = N'We''re on your side of the borough already, with no travel charge and no call-out fee within our standard service area.' + NCHAR(10) + NCHAR(10) + N'Most of Norbiton north of the cemetery dates from the turn of the last century. Chatham, Chesham, Cobham and Clevedon Roads are late Victorian detached villas, and Dawson Road is Edwardian, two storeys with ground-floor bays. Between Richmond Road and the riverside park at Canbury Gardens the side roads are late Victorian too, with mansions of that date on Albany Park Road.' + NCHAR(10) + NCHAR(10) + N'North Kingston''s Tudor Estate was built by G T Crouch and finished in the mid-1930s. The council''s character study notes how many of its houses have been extended since. In Coombe the houses are detached, on large plots, and many of the roads are private.'
WHERE [Slug] = N'kingston-upon-thames';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'Esher has older cottages and period houses around the High Street and Esher Green, family homes in the roads towards Hinchley Wood and Claygate, and large detached houses on its quieter roads. Older pipework and original bathrooms bring the usual dripping taps, leaking joints and blocked sinks. We are about 15 minutes away in Chessington and bring the tools and common parts to finish most jobs in one visit.' + NCHAR(10) + NCHAR(10) + N'Esher''s greens were kept open on purpose. The owners of the Claremont Estate were also lords of the manor, and they made sure the land around the town stayed undeveloped. Building came in the 20th century, in small and medium parcels, and Elmbridge Council describes the result as mostly large detached houses, many of them on private or semi-private estates protected by covenants.' + NCHAR(10) + NCHAR(10) + N'Some of those estates are gated. If yours is, give us the gate code or a number to ring when you book.',
    [LocalNeighbourhoodsCopy] = N'On Clare Hill, south of the centre, early 20th-century houses stand on the edge of Claremont''s 18th-century park. The estate insisted on a single architect, Blair Imrie, for most of them, and he favoured bricks in tan, peach, orange and brown with cream mortar. A wall patched there has those shades to match.' + NCHAR(10) + NCHAR(10) + N'Esher Place, north of the centre, is a 1930s extension: large houses on straight, tree-lined roads. Lower Green, on the far side of Sandown Park, is different again, with two-storey houses from between the wars and after, some of them terraced. West End is a village of its own round a triangular green, where buildings are rarely over two and a half storeys and often of cottage scale.' + NCHAR(10) + NCHAR(10) + N'Materials for higher-spec fixtures are always agreed with you up front.'
WHERE [Slug] = N'esher';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'Worcester Park and Ewell''s rows of well-kept 1930s semis are some of the most consistent handyman and plumbing work we take on - reliable properties, straightforward access, and usually a quick turnaround.' + NCHAR(10) + NCHAR(10) + N'Both places were built up between the wars. Worcester Park''s station opened in 1859, on what was then Malden Green, but Sutton Council''s character study says the area largely developed in the interwar years. At Ewell the railway was electrified in the 1920s, trains to London became more frequent, and developers bought up the farmland for housing estates.' + NCHAR(10) + NCHAR(10) + N'A house built before 1970 may have lead pipes, the Drinking Water Inspectorate says, and most houses here were. The place to look is the stop tap where the water comes in: unpainted lead is dull grey, often with a swollen joint next to the tap. If that is what you find, tell us when you book.',
    [LocalNeighbourhoodsCopy] = N'Whether it''s a semi off Green Lane in Worcester Park or a family home near Ewell Village and Bourne Hall, we know the layout of these estates well enough to arrive prepared.' + NCHAR(10) + NCHAR(10) + N'In Worcester Park the houses near the station are semi-detached pairs and short terraces with gardens front and back, many of them half-timbered. The Victorian terraces of Longfellow Road are older than their neighbours, and The Hamptons, with its timber-boarded houses, was finished around 2010 on the site of a sewage works.' + NCHAR(10) + NCHAR(10) + N'Ewell is far older. Its name comes from the Old English for a river spring, and the springs that rise by Bourne Hall are the source of the Hogsmill. Epsom & Ewell Council''s appraisal of the village counts timber-framed houses of the 16th and 17th centuries among its 100 listed entries, and there are weatherboarded cottages in West Street, Kingston Road and Mill Lane. Beyond the bypass of 1932 and the railway, the estates are from between the wars.' + NCHAR(10) + NCHAR(10) + N'The water is hard as well: 14 Clarke degrees in SES Water''s 2025 report for Ewell, from the village to Ewell Court, and for the Sutton side of KT4. Limescale on taps and in cisterns is ordinary here.'
WHERE [Slug] = N'worcester-park-ewell';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [ServiceAreas]
SET [IntroCopy] = N'Chessington is where it all starts for us - our technicians are dispatched from right here, so it''s usually our fastest turnaround area for both handyman and plumbing work.',
    [LocalNeighbourhoodsCopy] = N'From the family estates around Hook and Malden Rushett to the flats near Chessington World of Adventures and the older housing along Garrison Lane, we know the mix of properties that make up Chessington inside out.'
WHERE [Slug] = N'chessington';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'Surbiton''s blend of Victorian conservation-area streets and riverside apartments means every job is a little different, and our technicians are equipped for both.',
    [LocalNeighbourhoodsCopy] = N'We regularly work on the grand Victorian terraces around Maple Road and St Mark''s Hill, the mansion flats near Surbiton station, and the quieter family homes towards Berrylands and Tolworth.'
WHERE [Slug] = N'surbiton';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'From the Victorian terraces around Norbiton to the riverside flats by Kingston Bridge and the family homes stretching out towards Coombe and Berrylands, our local technicians already know the mix of properties that make up Kingston. One call books the right person for the job.',
    [LocalNeighbourhoodsCopy] = N'We''re on your side of the borough already - most Kingston bookings are slotted the same week, with no travel charge and no call-out fee within our standard service area.'
WHERE [Slug] = N'kingston-upon-thames';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'Esher has older cottages and period houses around the High Street and Esher Green, family homes in the roads towards Hinchley Wood and Claygate, and large detached houses on its quieter roads. Older pipework and original bathrooms bring the usual dripping taps, leaking joints and blocked sinks. We are about 15 minutes away in Chessington and bring the tools and common parts to finish most jobs in one visit.',
    [LocalNeighbourhoodsCopy] = N'Our technicians carry the range of tools and parts to handle a large executive home or a village cottage without a second visit, and materials for higher-spec fixtures are always agreed with you up front.'
WHERE [Slug] = N'esher';

UPDATE [ServiceAreas]
SET [IntroCopy] = N'Worcester Park and Ewell''s rows of well-kept 1930s semis are some of the most consistent handyman and plumbing work we take on - reliable properties, straightforward access, and usually a quick turnaround.',
    [LocalNeighbourhoodsCopy] = N'Whether it''s a semi off Green Lane in Worcester Park or a family home near Ewell Village and Bourne Hall, we know the layout of these estates well enough to arrive prepared.'
WHERE [Slug] = N'worcester-park-ewell';
");
        }
    }
}
