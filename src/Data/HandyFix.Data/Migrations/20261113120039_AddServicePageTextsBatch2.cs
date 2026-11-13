using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The next five service pages' own text (PROJECT_STATE Section 3er): Radiator & TRV
            // Replacement, TV Mounting, Furniture Assembly, Shelf Installation, Full Bathroom
            // Refurbishment. The description, the six blocks and the page's questions, by the service's
            // address, as batch 1 did (AddServicePageTextsBatch1). A service the admin has deleted, or
            // renamed (its address follows its name), is not found, and nothing is changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'A thermostatic radiator valve, or TRV, sits where the radiator meets its pipe and turns the radiator down as the room warms up, so each room holds the temperature its dial is set to. We change a manual valve for a TRV, replace a TRV that has stuck or started to weep, and swap an old radiator for a new one.' + NCHAR(10) + N'' + NCHAR(10) + N'When the pipes are already where the new radiator needs them, it goes straight on. When they are not, we alter the pipe ends to suit.',
    [Summary] = N'A radiator swapped for a new one, a manual valve changed for a thermostatic one (TRV), or a stuck or weeping valve replaced. The system is refilled, bled and checked before we leave.',
    [CommonFindings] = N'[refresh] A stuck TRV pin. Under the head is a small pin that the head pushes in and out; after a summer with the heating off it can stick, and the radiator stays hot or cold whatever the dial says.' + NCHAR(10) + N'[water_drop] A weeping valve. Water creeps out round the spindle, or at the nut where the valve joins the radiator, and leaves a stain or rust on the floor below.' + NCHAR(10) + N'[warning] A radiator rusting through. Small leaks start along the bottom or the seams of an old steel one. Once it has rusted through, it is replaced, not repaired.' + NCHAR(10) + N'[info] A TRV in the room with the room thermostat. BEAMA''s guidance on the Building Regulations puts a TRV on every radiator except those in the room where the room thermostat is, so that radiator keeps a plain valve.' + NCHAR(10) + N'[home] A TRV hidden behind a curtain or a sofa. Worcester Bosch says a TRV should be in the open, not covered by curtains or furniture: a covered head reads the warm air trapped round it and turns the radiator down too early.',
    [VisitSteps] = N'We turn the heating off and let the system cool.' + NCHAR(10) + N'We close the valves either side, or drain the water down below the radiator when a valve itself is being changed.' + NCHAR(10) + N'We fit the new radiator or valve, altering the pipe ends if the new radiator''s connections sit in a different place.' + NCHAR(10) + N'We refill the system, bleed every radiator on it, and top up the pressure on a sealed system.' + NCHAR(10) + N'We run the heating and check that every new joint stays dry once it is hot.',
    [BeforeWeArrive] = N'[timer] Turn the heating off before we come, so the water has cooled when we start.' + NCHAR(10) + N'[search] Measure the radiator you want replaced: its width, its height, and the distance between the two pipes where they come out of the floor or the wall. Add the figures, or a photo with a tape measure in it, to your booking.' + NCHAR(10) + N'[inventory_2] Move furniture back from the radiator and lift anything off the floor beneath it: a little dark water comes out when one is taken off.',
    [Included] = N'Changing a manual valve for a TRV, or an old TRV for a new one' + NCHAR(10) + N'Replacing a radiator with one of the same size or a new size' + NCHAR(10) + N'Altering the pipe ends to suit a new radiator' + NCHAR(10) + N'Refilling, bleeding and repressurising the system afterwards' + NCHAR(10) + N'Checking every new joint with the heating hot',
    [NotIncluded] = N'Work on the boiler itself: that is for our Gas Safe registered engineer, under Boiler Installation & Servicing'
WHERE [Slug] = N'radiator-trv-replacement';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'radiator-trv-replacement');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can a TRV be changed without draining the heating?', N'The head, yes: it unscrews from the valve body, and a new one goes on with the water still in. Changing the valve body means draining the water down below it first, then refilling and bleeding the system.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'radiator-trv-replacement';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does every radiator need a TRV?', N'Every one except in the room with your room thermostat, by BEAMA''s guidance. Since June 2022 in England, a new boiler in an existing home has to come with thermostatic control in each room, and TRVs are the usual way to give it.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'radiator-trv-replacement';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will a new radiator fit my old pipes?', N'If its connections are in the same place as the old one''s, yes, with no change to the pipes. A different size usually means altering the two pipe ends, and the measurements in your booking let us plan for that.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'radiator-trv-replacement';

UPDATE [Services]
SET [Description] = N'We fix a flat-screen TV to the wall on a bracket that matches the holes on the back of the set and carries its weight, level and at the height you choose, with the cables tidied into a cover on the wall.' + NCHAR(10) + N'' + NCHAR(10) + N'On brick or block the bracket is fixed into the masonry. On a plasterboard wall it goes into the timber studs behind the board, which is RoSPA''s advice for a TV on plasterboard.',
    [Summary] = N'A flat-screen TV fixed to brick, block or plasterboard on a bracket that matches it, level and at the height you choose, with the cables tidied.',
    [CommonFindings] = N'[search] A plasterboard wall. It sounds hollow when you knock on it. A bracket screwed into the board alone can pull out, so we find the studs and fix into those.' + NCHAR(10) + N'[bolt] A socket under the spot. Electrical Safety First says cables buried in a wall must run straight up, down or across from a socket or switch, so the wall right above a socket is where a cable is most likely to be. We trace it with a detector before drilling.' + NCHAR(10) + N'[info] A bracket that does not match the TV. The holes on the back of a TV are set to a VESA size, such as 200 by 200 millimetres, and a bracket''s box lists the sizes and the weight it takes.' + NCHAR(10) + N'[warning] An arm that swings out. With the TV pulled away from the wall, the top fixings carry far more pull than on a flat bracket, so an arm needs masonry or two studs behind it.',
    [VisitSteps] = N'We agree the position and the height with you before anything is drilled.' + NCHAR(10) + N'We find what the wall is made of and check the spot for cables and pipes.' + NCHAR(10) + N'We fix the bracket level, into the masonry or into the studs.' + NCHAR(10) + N'We hang the TV, check that it sits level and that any arm locks, and tidy the cables.',
    [BeforeWeArrive] = N'[visibility] Mark where you want the middle of the screen with a piece of masking tape, and look at it from where you sit.' + NCHAR(10) + N'[checklist] If you have bought a bracket, check that its box lists your TV''s VESA size and a weight above your TV''s. Both are in the TV''s manual.' + NCHAR(10) + N'[inventory_2] Move the TV stand and anything else away from that wall.' + NCHAR(10) + N'[add_a_photo] Photograph the wall and the back of the TV, and add both to your booking.',
    [Included] = N'Finding what the wall is made of, and checking it for cables and pipes' + NCHAR(10) + N'Fixing the bracket into the masonry or into the studs' + NCHAR(10) + N'Hanging the TV level, and checking that any arm locks' + NCHAR(10) + N'Tidying the cables into a cover on the wall',
    [NotIncluded] = NULL
WHERE [Slug] = N'tv-mounting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'tv-mounting');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can a TV go on a plasterboard wall?', N'Yes, when the bracket is fixed to the timber studs inside the wall: that is RoSPA''s advice for plasterboard walls. If there is no stud where you want the screen, we show you where the nearest ones allow it.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'tv-mounting';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'My TV stays on its stand. Can it be made safe for small children?', N'Yes, with an anti-tip strap, fixed to the back of the set and to a bracket on the wall, which is what RoSPA advises. RoSPA knows of eight babies and young children in the UK killed by a falling television since July 2008. We can fit a strap on the same visit.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'tv-mounting';

UPDATE [Services]
SET [Description] = N'Wardrobes, beds, chests of drawers, desks, tables and bookcases from the flat pack, built to the maker''s instructions in the room where they will stand.' + NCHAR(10) + N'' + NCHAR(10) + N'A chest of drawers, a wardrobe or a tall bookcase is then fixed to the wall with the fitting the maker supplies. IKEA explains why: a pulled-out drawer works like a lever, and even a small child standing on it can tip a large chest that is not fixed.',
    [Summary] = N'Flat-pack wardrobes, beds, desks and chests of drawers built in the room they belong in, and tall pieces fixed to the wall so they cannot tip.',
    [CommonFindings] = N'[checklist] A part missing or damaged in the box. We check every part before we start, so you know at once and can ask the shop for it.' + NCHAR(10) + N'[home] A wardrobe taller than the room allows. Built lying down, a tall wardrobe needs more than its own height to be stood up, so we check it against the ceiling before it is built.' + NCHAR(10) + N'[warning] A thick carpet or an uneven floor. IKEA names both as causes of tipping. The unit is levelled where it stands, and still fixed to the wall.' + NCHAR(10) + N'[refresh] Doors that do not line up. The hinges on flat-pack doors have adjusting screws, and they are set once the unit stands level in its place.',
    [VisitSteps] = N'We unpack in the room where the furniture will stand and count the parts against the list.' + NCHAR(10) + N'We build it to the maker''s instructions.' + NCHAR(10) + N'We stand it in its place, level it, and fix it to the wall where the maker supplies a fitting.' + NCHAR(10) + N'We set the doors and drawers so they close straight.',
    [BeforeWeArrive] = N'[inventory_2] Have the boxes in the room where the furniture is going, with a clear patch of floor about the size of the unit lying down.' + NCHAR(10) + N'[checklist] Say in your booking what you bought, with the maker and the model, or add a photo of the box label: the time a piece takes depends on it.' + NCHAR(10) + N'[key] If you rent, check that you may drill the wall for the safety fitting.',
    [Included] = N'Checking that every part is there' + NCHAR(10) + N'Building it as the instructions show' + NCHAR(10) + N'Fixing tall pieces to the wall with the maker''s fitting' + NCHAR(10) + N'Setting the doors, hinges and drawers',
    [NotIncluded] = N'Fitted furniture made to measure, such as an alcove cupboard: that is Custom Carpentry & Boxing-In, priced after a survey'
WHERE [Slug] = N'furniture-assembly';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'furniture-assembly');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What happens if a part is missing?', N'We find out at the start, when we check the parts. If the piece cannot be finished without it, we stop there and you pay for the time used, with the one-hour minimum.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'furniture-assembly';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will fixing a chest of drawers to the wall leave marks?', N'Two small screw holes behind it. IKEA''s advice for when you move the furniture is simply to fill them.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'furniture-assembly';

UPDATE [Services]
SET [Description] = N'The wall decides how a shelf is fixed, so we find out what it is before anything is drilled: solid brick or block, plasterboard on a timber frame, or plasterboard stuck to brick, where the fixing has to reach the brick behind.' + NCHAR(10) + N'' + NCHAR(10) + N'We put up floating shelves, shelves on brackets, shelving on wall uprights, and shelves cut to fit an alcove, level and fixed for what will sit on them.',
    [Summary] = N'Floating shelves, shelves on brackets, wall uprights and alcove shelves, put up level with fixings chosen for the wall and for what they will carry.',
    [CommonFindings] = N'[warning] Plasterboard stuck to brick. Some walls that sound hollow are boards fixed to the brick with dabs of adhesive, leaving a gap behind. A short plug holds only the board, so the fixing has to go through to the brick.' + NCHAR(10) + N'[shelves] A floating shelf that droops. Its hidden rods turn the load into leverage on the fixings, so it needs solid masonry or studs, and a flat wall.' + NCHAR(10) + N'[home] Alcoves that are not square. The walls either side of a chimney breast are rarely true in an older house, so each alcove shelf is cut to its own alcove.' + NCHAR(10) + N'[construction] Old plaster that crumbles round the drill. In older houses the plaster can be soft, so the fixing is set in the brick behind it, not in the plaster.',
    [VisitSteps] = N'We agree the height and the spacing with you and mark a level line.' + NCHAR(10) + N'We check the wall along that line for cables and pipes.' + NCHAR(10) + N'We cut the shelves to length where they need it, and fix the brackets or battens.' + NCHAR(10) + N'We put each shelf up, check it is level, and lean on it to test the fixings.',
    [BeforeWeArrive] = N'[shelves] Have the shelves and brackets there, and say in your booking what will go on them: the weight decides the fixing.' + NCHAR(10) + N'[inventory_2] Clear the wall and the floor in front of it.' + NCHAR(10) + N'[add_a_photo] Put a photo of the wall in your booking, and for an alcove, one with a tape measure across it.',
    [Included] = N'Finding what the wall is made of, and checking it for cables and pipes' + NCHAR(10) + N'Fixings chosen for the wall and the load' + NCHAR(10) + N'Cutting shelves and battens to fit an alcove' + NCHAR(10) + N'Fixing every shelf level, and testing it before we leave',
    [NotIncluded] = N'Built-in alcove units with doors or cupboards, made to measure: those are Custom Carpentry & Boxing-In, quoted after a survey'
WHERE [Slug] = N'shelf-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'shelf-installation');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How much weight can a floating shelf hold?', N'No more than its maker''s figure, and that only when it is fixed into brick, block or studs. On plasterboard alone it holds far less, which is why we ask in the booking what will go on it.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'shelf-installation';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can you put shelves on a plasterboard wall?', N'Yes. Light ones can hang on cavity fixings in the board. Heavy ones need timber behind the board, as Siniat, a plasterboard maker, advises, so they go onto the studs.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'shelf-installation';

UPDATE [Services]
SET [Description] = N'A new bathroom from bare walls: the old bath or shower, basin, toilet and tiles taken out, the pipes and wastes moved where the new layout needs them, the walls and floor prepared and waterproofed, then tiled, and the new suite fitted, connected and sealed.' + NCHAR(10) + N'' + NCHAR(10) + N'It starts with a survey at your home, where we measure the room and agree the layout and what goes in it.',
    [Summary] = N'The old bathroom stripped out and a new one fitted: pipework, waterproofing, wall and floor tiles and sanitaryware, priced after a free survey at your home.',
    [CommonFindings] = N'[water_drop] Soft floorboards under the old bath or toilet. Years of small leaks round a toilet or along the edge of a bath rot the boards, and they are replaced before anything new goes down.' + NCHAR(10) + N'[warning] Plain plasterboard round the shower. The Tile Association''s guide to BS 5385, the tiling standard, says plasterboard, moisture-resistant board included, is generally unsuitable for wet areas unless it is tanked. Walls like that are tanked or boarded again before tiling.' + NCHAR(10) + N'[plumbing] No isolating valves. An older bathroom may have no valve on the pipes to the toilet and the taps, so we fit one to each, and a later repair needs no whole-house shut-off.' + NCHAR(10) + N'[construction] Old tiles that bring the plaster with them. Tiles stuck on for decades can pull the plaster away as they come off, so the wall is boarded or plastered again before the new tiles go on.',
    [VisitSteps] = N'We survey the room: we measure it, look at the pipes and the floor, agree the layout and the suite with you, and give you the fixed quote.' + NCHAR(10) + N'We take out the old suite and tiles and check the floor and walls underneath.' + NCHAR(10) + N'We run the new pipes and wastes, the registered electrician does the electrics, and we board and tank the walls that will get wet.' + NCHAR(10) + N'We tile the walls and the floor.' + NCHAR(10) + N'We fit and connect the new suite, seal it, and run every tap, the shower and the toilet to check for leaks.',
    [BeforeWeArrive] = N'[add_a_photo] For the survey, have pictures of bathrooms you like, or the suite and tiles you have in mind.' + NCHAR(10) + N'[key] If you live in a flat, read your lease or ask the managing agent whether the work needs the freeholder''s consent, and when noisy work is allowed.' + NCHAR(10) + N'[home] If this is your only bathroom, plan where you will wash while it is out of use, and tell us at the survey.',
    [Included] = N'The survey at your home and a fixed quote' + NCHAR(10) + N'Taking out the old suite and tiles' + NCHAR(10) + N'New pipework and wastes for the new layout' + NCHAR(10) + N'Boarding and tanking the wet walls' + NCHAR(10) + N'The electrics round the bath or shower, by a registered electrician who certifies them' + NCHAR(10) + N'Wall and floor tiling' + NCHAR(10) + N'Fitting, connecting and sealing the new suite',
    [NotIncluded] = NULL
WHERE [Slug] = N'full-bathroom-refurbishment';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'full-bathroom-refurbishment');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does a bathroom refit need building regulations approval?', N'Usually not. The Planning Portal says refitting a bathroom with new units and fittings does not generally need approval, but drainage or electrical work that is part of the refit may. In England, electrical work round a bath or shower is notifiable: an electrician registered with a competent person scheme certifies it, and you get a Building Regulations Compliance Certificate within 30 days.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'full-bathroom-refurbishment';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Do I have to fit an extractor fan?', N'Not if the bathroom never had one. The Planning Portal says a refurbished bathroom must keep or replace any extract ventilation it already has, and one without it need not add it. A bathroom made in a room that was not one before does need extract ventilation.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'full-bathroom-refurbishment';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'radiator-trv-replacement');

UPDATE [Services]
SET [Description] = N'Replacing an old radiator or upgrading manual valves to thermostatic radiator valves (TRVs) for better heating control.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'radiator-trv-replacement';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'tv-mounting');

UPDATE [Services]
SET [Description] = N'Securely mounting flat-screen TVs to drywall, concrete, or brick walls with cable tidying.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'tv-mounting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'furniture-assembly');

UPDATE [Services]
SET [Description] = N'Professional flat pack furniture assembly for wardrobes, beds, desks, and tables.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'furniture-assembly';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'shelf-installation');

UPDATE [Services]
SET [Description] = N'Fitting floating shelves, heavy-duty brackets, or bookcase shelving systems.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'shelf-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'full-bathroom-refurbishment');

UPDATE [Services]
SET [Description] = N'Complete bathroom renovation including removal of old suite, plumbing, wall and floor tiling, sanitaryware fitting, and electrical/lighting prep.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'full-bathroom-refurbishment';");

        }
    }
}
