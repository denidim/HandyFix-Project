using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The sixth five service pages' own text (PROJECT_STATE Section 3ew): Curtain and Blind
            // Fitting, Plastering & Ceiling Repairs, Painting Touch-Ups, Gutter Clearing, Wall & Floor
            // Tiling. The description, the six blocks and the page's questions, by the service's
            // address, as batches 1 to 5 did (AddServicePageTextsBatch1 to 5). A service the admin has
            // deleted, or renamed (its address follows its name), is not found, and nothing is changed
            // for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'Curtain poles and tracks, and roller, Roman, Venetian and vertical blinds, put up level and on fixings that suit what is above your window: brick, plasterboard, or the steel or concrete lintel that carries the wall over the frame.' + NCHAR(10) + N'' + NCHAR(10) + N'You buy the poles, tracks or blinds, and we fit them. A blind with a cord or a chain gets its child-safety fitting on the wall before we leave.',
    [Summary] = N'Curtain poles, tracks and blinds put up level, on fixings chosen for the wall above the window, with each cord or chain held out of a child''s reach.',
    [CommonFindings] = N'[warning] A steel lintel just above the window. A masonry bit will not drill it, so the brackets go into the brick above it, or the steel is drilled with a metal bit and the fixing chosen for steel.' + NCHAR(10) + N'[build] A long pole that bows in the middle. Lined and interlined curtains are heavy, and a pole held only at its two ends sags between them, so a long pole gets a centre bracket as well.' + NCHAR(10) + N'[home] A recess too shallow for the blind. A blind inside the recess needs room for its brackets and for the window handles to clear it. Where there is not enough, the blind goes on the wall above and overlaps the recess, which also keeps more light out.' + NCHAR(10) + N'[health_and_safety] A cord or chain a child can reach. GOV.UK says the cleat that holds a blind''s cord should be on a surface at least 1.5 metres from the floor, with the spare cord wound on in a figure of eight after each use.',
    [VisitSteps] = N'We look at what is above and beside each window, and scan for cables and pipes before drilling.' + NCHAR(10) + N'For a blind in the recess, we check it against the recess at the top, the middle and the bottom.' + NCHAR(10) + N'We mark the brackets level, drill and fix them.' + NCHAR(10) + N'We hang the curtains or the blind and fit its cleat or chain tensioner.' + NCHAR(10) + N'We run each blind up and down and draw each curtain, to check it moves freely.',
    [BeforeWeArrive] = N'[checklist] Say in your booking how many windows there are, and whether each has a pole, a track or a blind. Have them at home with all their brackets and screws.' + NCHAR(10) + N'[home] Clear the window sills and move furniture away from the windows.' + NCHAR(10) + N'[info] If an old pole or track is coming down, take the curtains off it if you can.',
    [Included] = N'Curtain poles and tracks' + NCHAR(10) + N'Roller, Roman, Venetian and vertical blinds' + NCHAR(10) + N'Cleats and chain tensioners fitted out of children''s reach' + NCHAR(10) + N'Taking down the old pole, track or blind',
    [NotIncluded] = N'Electric blinds or tracks that have to be wired into the mains' + NCHAR(10) + N'Making or altering curtains and blinds'
WHERE [Slug] = N'curtain-and-blind-fitting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'curtain-and-blind-fitting');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does a blind with a cord need a safety device?', N'Yes. GOV.UK says new blinds with looped cords must have child safety devices fitted when they are made, or sold with the blind, and the cleat goes at least 1.5 metres from the floor. An older blind without one can have a cleat or a chain tensioner added.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'curtain-and-blind-fitting';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Should a blind go inside the window recess or above it?', N'Inside, if the recess is deep enough for the brackets and the window handles clear the blind: it looks neater and leaves the sill free. Above, overlapping the recess on each side, keeps out more light and suits a shallow recess.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'curtain-and-blind-fitting';

UPDATE [Services]
SET [Description] = N'Plaster that has cracked, blown or been holed is cut back and made good; whole walls and ceilings are skimmed smooth; a ceiling a leak has brought down is replaced with new plasterboard; and coving is fitted, or a missing length matched to the old.' + NCHAR(10) + N'' + NCHAR(10) + N'Where water did the damage, the leak is mended before any new plaster goes up.',
    [Summary] = N'Cracks, holes and loose plaster repaired, walls and ceilings skimmed, and ceilings damaged by a leak boarded again, at a fixed price agreed after a free survey.',
    [CommonFindings] = N'[water_drop] A ceiling stained or bowed after a leak. Plasterboard that has been soaked loses its strength and can sag. Once the leak above is mended, the wet board comes down and new board goes up, taped and skimmed.' + NCHAR(10) + N'[search] Plaster that sounds hollow when tapped. A blown patch has come away from the wall behind it. It is cut back to firm plaster and filled in layers.' + NCHAR(10) + N'[home] An old lath and plaster ceiling that sags. In older houses the ceilings are lime plaster pressed through thin strips of timber. Where the plaster has let go of the strips, it is screwed back with plaster washers, or the ceiling is boarded over.' + NCHAR(10) + N'[warning] A textured ceiling, such as Artex. HSE says to assume a building built or refurbished before 2000 contains asbestos, and textured coatings are among the materials it lists. A coating like that is sampled and tested before it is sanded, scraped or cut.',
    [VisitSteps] = N'At the survey we look at what caused the damage, and give you the fixed quote.' + NCHAR(10) + N'We cover the floor and the furniture, and take off the loose plaster or the damaged board.' + NCHAR(10) + N'We fix new board where it is needed, and treat the wall so the new plaster grips.' + NCHAR(10) + N'We plaster, and skim the whole wall or ceiling where a patch would show.' + NCHAR(10) + N'We tell you when the new plaster can be painted.',
    [BeforeWeArrive] = N'[emergency] If a ceiling is bulging with water, keep everyone out of the room under it and leave its light switched off. If water is still coming through, that is an emergency: call us or message us on WhatsApp.' + NCHAR(10) + N'[home] Clear the room as far as you can. What has to stay is covered.' + NCHAR(10) + N'[info] If a ceiling has a textured finish and you know when it went up, tell us at the survey.',
    [Included] = N'A free survey and a fixed quote' + NCHAR(10) + N'Patching cracks, holes and loose plaster' + NCHAR(10) + N'Skimming walls and ceilings' + NCHAR(10) + N'New plasterboard for a ceiling damaged by water' + NCHAR(10) + N'Coving fitted, or a short length matched to the old',
    [NotIncluded] = N'Removing a textured coating that contains asbestos'
WHERE [Slug] = N'plastering-ceiling-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'plastering-ceiling-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'When can new plaster be painted?', N'When it is thoroughly dry, which is what British Gypsum asks for its Thistle plasters. Until then it allows only a permeable paint, because that lets the plaster go on drying underneath.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'plastering-ceiling-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can an Artex ceiling be plastered over?', N'Usually, yes: once it is firm, it is skimmed over and the old coating stays in place underneath. If it may be from before 2000, a sample is tested for asbestos first, because sanding or scraping it would put any fibres into the air.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'plastering-ceiling-repairs';

UPDATE [Services]
SET [Description] = N'When a shelf comes down, a bracket moves or furniture leaves its mark, the wall needs filling, sanding and painting before it looks right again. We do that on walls, ceilings, doors, frames and skirting boards.' + NCHAR(10) + N'' + NCHAR(10) + N'Before we start, we tell you whether a patch will blend in, or whether the whole wall needs a coat to look even.',
    [Summary] = N'Holes, dents and scuffs filled, sanded and painted on walls, ceilings and woodwork, and the whole wall painted where a patch would show.',
    [CommonFindings] = N'[visibility] A patch that shows on a silk or satin wall. Farrow & Ball says filled areas take up paint at a different rate from the wall round them, and that the difference shows far more on paints with a higher sheen. On a wall like that, the paint may need to go from corner to corner.' + NCHAR(10) + N'[warning] Old paint that may contain lead. Cambridge City Council says a home built before the 1960s that still has its original coats of paint could have some lead in it, and HSE names dry sanding old paint and stripping it with blow lamps among the risky jobs. Old woodwork is rubbed down wet, never dry, and never burnt off.' + NCHAR(10) + N'[build] An old wall plug left in its hole. Filled over, it shows through again, so it is pulled out, or set below the surface, before the hole is filled.' + NCHAR(10) + N'[refresh] A crack that keeps coming back in a corner. Where a wall meets the ceiling or a door frame, the two move slightly apart and hard filler cracks again. A flexible decorator''s caulk takes the movement.',
    [VisitSteps] = N'We look at the finish and the colour, and tell you whether patches will blend or the wall needs a full coat.' + NCHAR(10) + N'We fill each hole, dent and crack, let the filler dry and sand it smooth.' + NCHAR(10) + N'We prime the filled spots, so the paint does not sink into them.' + NCHAR(10) + N'We paint, with a second coat where the first does not cover.' + NCHAR(10) + N'We take up the dust sheets and clean up.',
    [BeforeWeArrive] = N'[inventory_2] If you have the tin the wall was painted with, leave it out: its label gives the paint and the colour. If not, tell us the brand and the colour''s name if you know them.' + NCHAR(10) + N'[checklist] Put a small piece of masking tape next to each mark you want done, so none is missed.' + NCHAR(10) + N'[home] Move small furniture and pictures away from the walls being painted.',
    [Included] = N'Filling holes, dents and cracks' + NCHAR(10) + N'Sanding and priming the filler' + NCHAR(10) + N'Painting walls, ceilings, doors, frames and skirting boards' + NCHAR(10) + N'Flexible caulk in corners that keep cracking',
    [NotIncluded] = N'Plaster that has come away from the wall, or a ceiling damaged by water: that is Plastering & Ceiling Repairs'
WHERE [Slug] = N'painting-touch-ups';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'painting-touch-ups');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will a touch-up show?', N'It can. Filled spots take up paint differently from the wall round them, and a silk or satin finish shows that most, which is why we may suggest painting the whole wall. On a flat matt wall a patch is harder to see, though a low light across the wall can still pick it out.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'painting-touch-ups';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can you paint over a damp patch?', N'Not until the cause is mended. A stain left by a leak comes back through new emulsion, so the leak is fixed first, the patch is left to dry, and it is sealed with a stain-blocking primer before it is painted.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'painting-touch-ups';

UPDATE [Services]
SET [Description] = N'We clear gutters by hand: the leaves, moss and silt come out, the outlets and downpipes are freed, and water is run along each length to check it drains to its downpipe and not down your wall.' + NCHAR(10) + N'' + NCHAR(10) + N'SPAB, the building conservation charity, advises clearing gutters every autumn, and in spring as well.',
    [Summary] = N'Gutters and downpipes cleared by hand and tested with water, with leaking joints and loose brackets found while we are up there.',
    [CommonFindings] = N'[water_drop] An outlet blocked solid. Leaves and moss collect where the gutter drops into the downpipe, the water backs up behind them and spills over the front edge.' + NCHAR(10) + N'[visibility] Stained brickwork under a joint. SPAB suggests looking at gutters and downpipes in heavy rain, when a leaking joint shows, or in dry weather for staining on the brick below it. A leaking joint gets a new seal or a new connector.' + NCHAR(10) + N'[build] A gutter that falls the wrong way. SPAB''s advice is to have gutters refixed if they slope the wrong way or pour water onto the wall. A loose or dropped bracket is reset so the gutter falls to its outlet.',
    [VisitSteps] = N'We look at the gutters from the ground and agree with you where the ladder goes.' + NCHAR(10) + N'We scoop out the leaves, moss and silt by hand and bag them.' + NCHAR(10) + N'We clear each outlet and downpipe.' + NCHAR(10) + N'We run water along every length and watch where it goes.' + NCHAR(10) + N'We tell you about any joint, bracket or downpipe that needs more than a repair on the day.',
    [BeforeWeArrive] = N'[directions_car] Move cars, bins and pots away from under the gutters, so the ladder can stand on firm ground.' + NCHAR(10) + N'[info] Say in your booking how many storeys the house has, and whether a conservatory or an extension roof sits below any gutter.' + NCHAR(10) + N'[visibility] If you have seen where water spills over in the rain, or birds going in and out under the roof, tell us.',
    [Included] = N'Gutters at the front and the back' + NCHAR(10) + N'Outlets and downpipes cleared' + NCHAR(10) + N'A water test along every length' + NCHAR(10) + N'Resetting a loose bracket',
    [NotIncluded] = N'Houses of more than two storeys' + NCHAR(10) + N'Drains and gullies below the ground' + NCHAR(10) + N'Roof repairs, such as slipped tiles or flashing'
WHERE [Slug] = N'gutter-clearing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'gutter-clearing');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How often should gutters be cleared?', N'Every autumn, once the leaves are down, and in spring as well: that is SPAB''s advice. A house under trees may need it more often.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'gutter-clearing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can you clear a gutter with birds nesting in it?', N'Not the part with an active nest. The RSPB says an active nest must not be removed and the chicks must be allowed to leave first, so that stretch is cleared once the nest is empty. House sparrows and starlings are the birds it names as most likely to share a home.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'gutter-clearing';

UPDATE [Services]
SET [Description] = N'Tiling starts with what the tiles go on. We make the wall or the floor ready for the tile first, set the tiles out so the cut ones at each edge match, then fix and grout them, and seal with silicone where they meet a bath, a shower tray or a worktop.' + NCHAR(10) + N'' + NCHAR(10) + N'That covers kitchen splashbacks, bathroom walls, and kitchen, bathroom and hallway floors, in ceramic, porcelain or natural stone.',
    [Summary] = N'Walls and floors tiled in ceramic, porcelain or stone, on a base made ready for the tile''s weight, quoted after a free survey at your home.',
    [CommonFindings] = N'[build] Floorboards that move underfoot. A floor that flexes cracks the grout and then the tiles, so loose boards are screwed down firmly before the new base goes over them.' + NCHAR(10) + N'[construction] Heavy tiles for a plastered wall. Instarmac, the adhesive maker, gives the most a wall can carry as 20kg a square metre on gypsum plaster and 32kg on plasterboard with no skim, adhesive and grout included. Large porcelain or stone can weigh more, so the wall is boarded to take it.' + NCHAR(10) + N'[timer] New plaster that has not dried out. British Gypsum says tiles should not go on until the plaster and the wall behind it are dry.' + NCHAR(10) + N'[search] A floor that is out of level. Long tiles show every dip as a lip at their edges, so the floor is levelled with a levelling compound first.',
    [VisitSteps] = N'At the survey we measure, look at the wall or floor the tiles will go on, agree the layout with you, and give you the fixed quote.' + NCHAR(10) + N'We take off the old tiles or floor covering where they have to come off, and prepare the base: boards, a membrane or a levelling compound.' + NCHAR(10) + N'We set the tiles out from the centre lines, so the cut tiles at the edges are even.' + NCHAR(10) + N'We fix the tiles, cutting round pipes, sockets and corners.' + NCHAR(10) + N'We grout, and seal with silicone where the tiles meet a bath, a shower tray or a worktop.',
    [BeforeWeArrive] = N'[add_a_photo] For the survey, have the tiles you like, or pictures of them, and their size.' + NCHAR(10) + N'[inventory_2] Keep a few spare tiles from the same batch. The batch or shade number is on the box, and a later repair is easier with a tile that matches.' + NCHAR(10) + N'[home] A tiled floor sits higher than carpet or vinyl, so a door that opens over it may need trimming. Tell us which doors.',
    [Included] = N'Measuring up and a fixed quote at the survey' + NCHAR(10) + N'Preparing the wall or the floor' + NCHAR(10) + N'A backer board or a membrane over timber floors' + NCHAR(10) + N'Levelling an uneven floor' + NCHAR(10) + N'Cutting round pipes, sockets and corners' + NCHAR(10) + N'Grouting, and silicone along baths, trays and worktops' + NCHAR(10) + N'Sealing natural stone',
    [NotIncluded] = NULL
WHERE [Slug] = N'wall-floor-tiling';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'wall-floor-tiling');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can tiles go straight onto floorboards?', N'No. The Tile Association says that under BS 5385-3:2024, the British Standard for floor tiling, timber boards are no longer recommended for tiling directly, and a layer such as a tile backer board or an uncoupling membrane goes over them first.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'wall-floor-tiling';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can new tiles go over old ones?', N'Sometimes. Every old tile has to be firmly stuck, and the wall has to carry the weight of both layers. Where either is in doubt, the old tiles come off first.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'wall-floor-tiling';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'curtain-and-blind-fitting');

UPDATE [Services]
SET [Description] = N'Hanging curtain rods, tracks, roller blinds, Venetian blinds, or vertical blinds.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'curtain-and-blind-fitting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'plastering-ceiling-repairs');

UPDATE [Services]
SET [Description] = N'Full room skimming, plaster patching, ceiling boarding after water damage, and coving to create clean, smooth surfaces ready for painting.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'plastering-ceiling-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'painting-touch-ups');

UPDATE [Services]
SET [Description] = N'Filling holes, sanding down, and repainting damaged walls, doors, or skirting boards.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'painting-touch-ups';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'gutter-clearing');

UPDATE [Services]
SET [Description] = N'Clearing leaves and debris from gutters and downpipes to prevent overflow and water damage.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'gutter-clearing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'wall-floor-tiling');

UPDATE [Services]
SET [Description] = N'Professional tiling for kitchens, bathrooms, and hallways using ceramic, porcelain, natural stone, or metro tiles with clean grouting and sealing.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'wall-floor-tiling';");

        }
    }
}
