using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSixNewTownAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pages for six towns of the Google profile that had none: Twickenham, Richmond,
            // Molesey, New Malden, Ashtead and Cheam (PROJECT_STATE Section 3em). The seeder fills
            // an empty table only, so a database that already has areas gets them here; the seeder
            // holds the same six for a new database, which is why this does nothing while the table
            // is empty: rows put in here first would stop the seeder adding the other fifteen.
            // An area already at one of the six addresses is replaced: staging had a Molesey added
            // by hand to try the admin form (Section 3dn).
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [ServiceAreas])
BEGIN
    DELETE FROM [ServiceAreaFaqs] WHERE [ServiceAreaId] IN (SELECT [Id] FROM [ServiceAreas] WHERE [Slug] IN (N'twickenham', N'richmond', N'molesey', N'new-malden', N'ashtead', N'cheam'));
    DELETE FROM [ServiceAreas] WHERE [Slug] IN (N'twickenham', N'richmond', N'molesey', N'new-malden', N'ashtead', N'cheam');

    INSERT INTO [ServiceAreas] ([Id], [Slug], [Name], [DriveTimeMinutes], [IntroCopy], [LocalNeighbourhoodsCopy], [IsFeatured], [PostcodeDistricts], [DiagramBearing], [DiagramLabelSide], [CreatedOn], [IsDeleted])
    VALUES (NEWID(), N'twickenham', N'Twickenham', 25,
        N'Twickenham''s streets were built in very different periods, and the houses show it. Montpelier Row is tall Georgian townhouses. Queen''s Road is mostly Victorian terraces in yellow stock brick, with slate roofs and timber sash windows. Cole Park Road was built up with large detached homes in the Edwardian period, and the Leeson Estate was laid out in the 1930s as semi-detached and terraced houses in stucco and red brick.' + NCHAR(10) + NCHAR(10) + N'Tell us which of these yours is when you book: a radiator valve or a wall fixing in a Victorian terrace is not the same job as in a 1930s semi.',
        N'Around Queen''s Road the bigger town houses run to three and a half storeys with a lower ground floor, and Grosvenor Road is plain Victorian cottages under shallow slate roofs. In brick houses of that age a television bracket or a run of shelves goes into masonry, with the fixings for it, not into plasterboard.' + NCHAR(10) + NCHAR(10) + N'The inter-war streets are different. The Rivermead Estate and Lincoln Avenue are garden-city style semis and short terraces, and Campbell Close is an early Span development from about 1955. Where a house like these has had its kitchen moved or an extension added, we trace where the pipes now run before we cut into anything.' + NCHAR(10) + NCHAR(10) + N'Parking is worth a minute before we come. If your road is in one of Richmond Council''s controlled parking zones, the council sells trader permits for a workman''s vehicle, as a scratch card or pay-as-you-go. With one ready, the van is outside your door and not streets away.',
        0, N'TW1, TW2', 351, 2, GETUTCDATE(), 0);
    INSERT INTO [ServiceAreaFaqs] ([Id], [ServiceAreaId], [Question], [Answer], [DisplayOrder], [CreatedOn], [IsDeleted])
    SELECT NEWID(), [Id], N'Can you park outside my house in Twickenham on an event day?',
        N'Yes, with a permit to show. Roads in Richmond Council''s Twickenham Event Zone (R) need one on event days, as the signs at the entry to the zone say, and the council issues trader permits for people working on your property. Ask for one before the visit and tell us when you book.',
        1, GETUTCDATE(), 0 FROM [ServiceAreas] WHERE [Slug] = N'twickenham';

    INSERT INTO [ServiceAreas] ([Id], [Slug], [Name], [DriveTimeMinutes], [IntroCopy], [LocalNeighbourhoodsCopy], [IsFeatured], [PostcodeDistricts], [DiagramBearing], [DiagramLabelSide], [CreatedOn], [IsDeleted])
    VALUES (NEWID(), N'richmond', N'Richmond', 29,
        N'Around Richmond Green the houses are mainly late 17th and early 18th century terraces of townhouses; Maids of Honour Row dates from 1724-5 and Old Palace Terrace is listed. Elsewhere in the town, The Alberts were developed in the 1870s as tightly packed two-storey terraces with slate roofs on narrow streets, and the roads west of Queen''s Road are large Edwardian and inter-war semis.' + NCHAR(10) + NCHAR(10) + N'In a listed house we keep to repairs: a tap re-seated, a cistern mended, a door eased. Nothing that would need consent is altered.',
        N'On the hill, numbers 3 to 17 Hill Rise are three storeys with shops at street level and homes above, and 21 to 33 Petersham Road is a terrace of three-storey Victorian houses with double-height bay windows. If you live above a shop, find out where your stop valve is before we come, or ask the managing agent. It is not always inside the flat.' + NCHAR(10) + NCHAR(10) + N'North of Sheen Road, up to the railway, the streets are rows of large late Victorian terraced and semi-detached houses with slate roofs.' + NCHAR(10) + NCHAR(10) + N'Parking decides how long a visit to the centre takes. Richmond Council''s Richmond Town zone (A1) runs Monday to Saturday, 8.30am to 6.30pm, in part of the zone. The council sells trader permits for a workman''s vehicle, as a scratch card or pay-as-you-go, and one in hand saves a walk from a car park with the tools.',
        0, N'TW9, TW10', 4, 1, GETUTCDATE(), 0);
    INSERT INTO [ServiceAreaFaqs] ([Id], [ServiceAreaId], [Question], [Answer], [DisplayOrder], [CreatedOn], [IsDeleted])
    SELECT NEWID(), [Id], N'Do you work in the listed houses around Richmond Green?',
        N'Yes, for repairs and like-for-like replacements: taps, toilets, radiators, doors and locks. We do not alter the fabric of a listed building, and anything that needs listed building consent from Richmond Council is yours to arrange before we start.',
        1, GETUTCDATE(), 0 FROM [ServiceAreas] WHERE [Slug] = N'richmond';

    INSERT INTO [ServiceAreas] ([Id], [Slug], [Name], [DriveTimeMinutes], [IntroCopy], [LocalNeighbourhoodsCopy], [IsFeatured], [PostcodeDistricts], [DiagramBearing], [DiagramLabelSide], [CreatedOn], [IsDeleted])
    VALUES (NEWID(), N'molesey', N'Molesey', 13,
        N'Molesey gets newer as you go west. Kent Town, across the river from Hampton Court Palace, was laid out in the mid-19th century with large detached villas in London stock brick and stucco under slate roofs. West Molesey''s Howard Houses, some 300 of them around First Avenue, date from about 1933. The Hurst Park estate, long terraces of two and three storey houses with high-rise flats north of Victoria Road, is from the 1970s.' + NCHAR(10) + NCHAR(10) + N'Elmbridge Council notes how much of this housing has since been extended, altered and remodelled. Pipes and wiring move with every round of that, so tell us what has been changed in yours.',
        N'Inside Kent Town the houses step down from north to south: the largest, some of four storeys, are at the north end, and the roads finish in modest two-storey cottages by Walton Road. Around Bridge Road and Creek Road the shops have homes above them.' + NCHAR(10) + NCHAR(10) + N'West Molesey, between Walton Road and Hurst Road, is mostly two-storey semi-detached houses with some bungalows, the same house type repeated along a street. Most of the Howard Houses have had a pitched roof added since they were built.' + NCHAR(10) + NCHAR(10) + N'The Thames runs along the north side of Molesey and the River Ember along the east, with reservoirs and gravel pits to the west and south. We cover all of it, Hurst Park included.',
        0, N'KT8', 315, 2, GETUTCDATE(), 0);
    INSERT INTO [ServiceAreaFaqs] ([Id], [ServiceAreaId], [Question], [Answer], [DisplayOrder], [CreatedOn], [IsDeleted])
    SELECT NEWID(), [Id], N'Do you cover both East and West Molesey?',
        N'Yes, both, and Hurst Park between them and the river. The two are one stretch of houses, older by Hampton Court Bridge and newer towards the reservoirs, and a visit costs the same anywhere in it.',
        1, GETUTCDATE(), 0 FROM [ServiceAreas] WHERE [Slug] = N'molesey';

    INSERT INTO [ServiceAreas] ([Id], [Slug], [Name], [DriveTimeMinutes], [IntroCopy], [LocalNeighbourhoodsCopy], [IsFeatured], [PostcodeDistricts], [DiagramBearing], [DiagramLabelSide], [CreatedOn], [IsDeleted])
    VALUES (NEWID(), N'new-malden', N'New Malden', 9,
        N'New Malden is a railway town. Its station opened in 1846, seventeen years before Kingston''s, and the first streets laid out were the six Groves west of the High Street: Acacia, Chestnut, Elm, Lime, Poplar and Sycamore, each planted with the tree it is named after. The plots there filled slowly, the last of them not until 1960, so houses of the 1860s and of the 1950s share a road.' + NCHAR(10) + NCHAR(10) + N'The age of a house tells us what its pipes and walls are likely to be made of, so it is worth a line in the description box when you book.',
        N'East of the High Street, by Beverley Park, substantial Edwardian houses stand among semi-detached and detached houses of the 1930s. Graham Road and Albemarle Gardens are Edwardian terraces and semis with tile hanging, roughcast render and leaded panels in the windows.' + NCHAR(10) + NCHAR(10) + N'Further out the houses are later: 1930s semis and short terraces on Green Lane, tile-hung bays on Oxford Crescent, 1950s bungalows on Windsor Avenue. South Lane was cut in two by the A3 in the 1920s. Where a house of that age has had its kitchen moved or an extension added, the pipes no longer run where the builder put them, and we find them first.' + NCHAR(10) + NCHAR(10) + N'If your road has permit parking, Kingston Council sells visitor vouchers, though not for every zone. You buy them online and start each one with the vehicle''s registration number, so ask us for the van''s when you book.',
        0, N'KT3', 38, 1, GETUTCDATE(), 0);
    INSERT INTO [ServiceAreaFaqs] ([Id], [ServiceAreaId], [Question], [Answer], [DisplayOrder], [CreatedOn], [IsDeleted])
    SELECT NEWID(), [Id], N'My road in New Malden has permit parking. What do you need from me?',
        N'A visitor voucher for the time we are with you. Kingston Council sells them online, though not for every zone, and you do not need a resident permit of your own to buy one. Each voucher is started with our van''s registration number, the date and the time.',
        1, GETUTCDATE(), 0 FROM [ServiceAreas] WHERE [Slug] = N'new-malden';

    INSERT INTO [ServiceAreas] ([Id], [Slug], [Name], [DriveTimeMinutes], [IntroCopy], [LocalNeighbourhoodsCopy], [IsFeatured], [PostcodeDistricts], [DiagramBearing], [DiagramLabelSide], [CreatedOn], [IsDeleted])
    VALUES (NEWID(), N'ashtead', N'Ashtead', 14,
        N'Ashtead grew up on the spring line, where the chalk to the south meets the clay to the north. The oldest houses are in the Village: timber-framed buildings on Rectory Lane and, south of The Street, narrow streets of Victorian semi-detached and terraced cottages. Lower Ashtead, to the north, is younger and more mixed, with whole roads of semis begun in the 1930s and carried on in the 1950s.' + NCHAR(10) + NCHAR(10) + N'SES Water classes the water here as moderately hard, so on a plumbing job in Ashtead we check for limescale early: on taps, in shower heads and in cisterns.',
        N'Woodfield Lane has a row of 14 Edwardian houses that is a conservation area. Around it, Woodfield and the roads from Oakfield Road to The Marld are almost all detached houses in large gardens, the earliest from between the wars.' + NCHAR(10) + NCHAR(10) + N'The Berg Estate, north and east of the level crossing, was built just before the Second World War in brick, render and clay tiles, with long, narrow back gardens. A garden that long is where an outside tap is worth having: we fit one with a double-check valve, connected to the supply indoors.' + NCHAR(10) + NCHAR(10) + N'SES Water''s 2025 report for the middle of Ashtead gives the hardness as 14 Clarke degrees. Limescale, in SES''s words, can build up in pipes, on fittings and in kettles. When a tap drips or a shower runs weak, scale in the cartridge or the head is where we start.',
        0, N'KT21', 180, 1, GETUTCDATE(), 0);
    INSERT INTO [ServiceAreaFaqs] ([Id], [ServiceAreaId], [Question], [Answer], [DisplayOrder], [CreatedOn], [IsDeleted])
    SELECT NEWID(), [Id], N'Is the water in Ashtead hard?',
        N'Moderately. SES Water''s 2025 report for central Ashtead puts it in a medium hardness zone, at 14 Clarke degrees. That is enough to scale up taps, shower heads and toilet cisterns over a few years. SES has a postcode search on its website that gives the figure for your own address, which is the one to use when you set a dishwasher.',
        1, GETUTCDATE(), 0 FROM [ServiceAreas] WHERE [Slug] = N'ashtead';

    INSERT INTO [ServiceAreas] ([Id], [Slug], [Name], [DriveTimeMinutes], [IntroCopy], [LocalNeighbourhoodsCopy], [IsFeatured], [PostcodeDistricts], [DiagramBearing], [DiagramLabelSide], [CreatedOn], [IsDeleted])
    VALUES (NEWID(), N'cheam', N'Cheam', 15,
        N'Cheam stayed a quiet village until the 1920s: in 1801 it had 616 people. Then, during the 1920s and early 1930s, farm and brewery land at the crossroads was released for building, Malden Road was widened and lined with new mock Tudor shops, and the houses around the village went up. Sutton Council calls them typical inter-war housing stock.' + NCHAR(10) + NCHAR(10) + N'So most of Cheam is about a hundred years old, the age at which original taps, valves and pipe joints come to the end of their life.',
        N'The older Cheam is around Park Lane and Whitehall, a timber-framed house built in the early 1500s that is now a museum. The listed buildings there date from the 16th to the 19th centuries, and Jubilee Road has a terrace of Victorian houses.' + NCHAR(10) + NCHAR(10) + N'South Cheam is different again. The Burton Estates were built between the wars as large detached houses on large plots, in an Arts and Crafts manner, and their builder said there was no such thing as a standard type of house on the estate. The Drive and parts of Burdon Lane are older, Edwardian.' + NCHAR(10) + NCHAR(10) + N'SES Water classes the water in central Cheam as hard: 15 Clarke degrees in its 2025 report. A tap that has gone stiff or a toilet that fills slowly can be limescale, and we clear or replace the scaled part before anything bigger.',
        0, N'SM3, SM2', 82, 1, GETUTCDATE(), 0);
    INSERT INTO [ServiceAreaFaqs] ([Id], [ServiceAreaId], [Question], [Answer], [DisplayOrder], [CreatedOn], [IsDeleted])
    SELECT NEWID(), [Id], N'Do I get free visitor parking for a tradesman in Cheam?',
        N'Yes, if your road is in a controlled parking zone. Sutton Council gives each household in a zone 50 free hours of visitor vouchers a year, added through your online account, and you do not need a resident permit to apply.',
        1, GETUTCDATE(), 0 FROM [ServiceAreas] WHERE [Slug] = N'cheam';
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceAreaFaqs] WHERE [ServiceAreaId] IN (SELECT [Id] FROM [ServiceAreas] WHERE [Slug] IN (N'twickenham', N'richmond', N'molesey', N'new-malden', N'ashtead', N'cheam'));
DELETE FROM [ServiceAreas] WHERE [Slug] IN (N'twickenham', N'richmond', N'molesey', N'new-malden', N'ashtead', N'cheam');");
        }
    }
}
