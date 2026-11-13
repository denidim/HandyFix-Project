using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The last three service pages' own text (PROJECT_STATE Section 3ey): Kitchen Fitting &
            // Alterations, Partition Walls & Stud Work, Custom Carpentry & Boxing-In. The description,
            // the six blocks and the page's questions, by the service's address, as batches 1 to 7 did
            // (AddServicePageTextsBatch1 to 7). A service the admin has deleted, or renamed (its
            // address follows its name), is not found, and nothing is changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'A new kitchen fitted in place of the old one, or the one you have changed: a unit added or moved, a new worktop, new doors, the sink or an appliance put somewhere new. We level the base units, hang the wall units on fixings chosen for the wall, cut and seal the worktops, and connect the sink and the appliances.' + NCHAR(10) + N'' + NCHAR(10) + N'You buy the kitchen, and we fit it. If you want, we can find it for you.',
    [Summary] = N'A new kitchen fitted, or the one you have changed: units levelled and fixed, worktops cut and sealed, the sink and appliances connected, at a fixed price from a survey.',
    [CommonFindings] = N'[water_drop] A wet or soft floor behind the old sink. A trap or a tap connector that dripped unseen for years shows when the old units come out. The leak is mended and the wet board cut out before a new unit goes over it.' + NCHAR(10) + N'[build] A floor that falls and a wall that bows. The base units stand on legs set from the highest point of the floor, so the worktop runs level, and where the wall is not straight the back edge of the worktop is cut to follow it.' + NCHAR(10) + N'[warning] A sink or hob drawn too close to the end of a worktop. Howdens'' guide for its solid wood worktops says a cut-out should be at least 100mm from the end of the worktop and from the next cut-out, or the worktop is seriously weakened. We check the plan for it at the survey.',
    [VisitSteps] = N'At the survey we measure the room, go through the kitchen plan with you, find where the water, the waste and the electrics run, and give you the fixed quote.' + NCHAR(10) + N'We turn off the supplies to the old kitchen, the gas by our Gas Safe registered engineer, and take it out.' + NCHAR(10) + N'We fit the base units level, then the wall units, checking the wall for cables and pipes before each fixing.' + NCHAR(10) + N'We cut and join the worktops, with the cut-outs for the sink and the hob, and seal every cut edge.' + NCHAR(10) + N'We fit the sink, the taps and the waste, connect the appliances, and fit the plinths, end panels and splashback.' + NCHAR(10) + N'We adjust the doors and drawers to line up, then run the water and check every joint under the sink before we leave.',
    [BeforeWeArrive] = N'[checklist] Have the kitchen delivered before the first day, and open the boxes to check that every unit is there and undamaged. A missing unit stops the fitting at the point where it goes.' + NCHAR(10) + N'[inventory_2] For the survey, have the supplier''s plan and the model of each appliance. Each appliance''s instructions set the space it needs and the supply it connects to.' + NCHAR(10) + N'[home] Empty the cupboards, and plan where you will make a drink and wash up while the kitchen is out of use. We tell you at the survey how many days that will be.',
    [Included] = N'A survey of the room and a fixed quote' + NCHAR(10) + N'Taking out the old kitchen' + NCHAR(10) + N'Base and wall units, levelled and fixed' + NCHAR(10) + N'Worktops cut, joined and sealed, with the cut-outs for the sink and the hob' + NCHAR(10) + N'The sink, the taps and the waste connected' + NCHAR(10) + N'Appliances fitted and connected' + NCHAR(10) + N'Plinths, end panels, upstands and a splashback panel' + NCHAR(10) + N'A new circuit, where an oven or a hob needs one, by a registered electrician we bring in' + NCHAR(10) + N'A gas hob or cooker connected by our Gas Safe registered engineer',
    [NotIncluded] = N'Taking out a wall that holds up a floor or the roof, which needs a structural engineer and a beam' + NCHAR(10) + N'Tiled splashbacks and tiled floors: those are Wall & Floor Tiling, priced after a survey'
WHERE [Slug] = N'kitchen-fitting-alterations';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'kitchen-fitting-alterations');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can the sink move to another wall?', N'Yes, if its waste pipe can reach the drain with a steady fall. Approved Document H, the government''s guidance on drainage, gives the longest run and the slope a waste pipe can have without a vent of its own. Past them, it needs a vent or an air admittance valve, so the water seal in the trap is not pulled out. We look at the run at the survey.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'kitchen-fitting-alterations';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Why is there a gap between a wooden worktop and the wall?', N'So the wood can move. Howdens'' guide asks for 3 to 4mm at the back edge, because the worktop expands and contracts with the humidity of the kitchen, and at least 40mm beside a cooker that is always hot, such as an Aga or a Rayburn. An upstand, or the tiling and a silicone joint, covers the gap.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'kitchen-fitting-alterations';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does a solid wood worktop need oiling before it is fitted?', N'Yes, all over. Howdens'' guide says both faces, any trimmed edges and the cut-outs must be oiled before installation, to cut the risk of the wood distorting, and every edge cut on site is oiled straight away.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'kitchen-fitting-alterations';

UPDATE [Services]
SET [Description] = N'We build new walls inside the house on a frame of timber or metal studs, boarded on both sides, to split one bedroom into two, close off a home office or make a walk-in wardrobe. Where the wall has to keep sound in, mineral wool goes into the frame before the boards.' + NCHAR(10) + N'' + NCHAR(10) + N'A stud wall can take a door, sockets and a TV, as long as each is planned before the boards go on.',
    [Summary] = N'A new stud wall to split a room or make a new one, framed in timber or metal, insulated for sound where it is needed and boarded, at a fixed price from a survey.',
    [CommonFindings] = N'[warning] A new room you can only leave through another. A wall that splits a room changes the way out in a fire. The Planning Portal says an inner room, one reached only through another room, may need an escape window of its own, and a new wall between a room and the stairway may need fire resistance, with a door that closes by itself.' + NCHAR(10) + N'[visibility] One window for two rooms. A wall put up across a room with one window leaves one side without it. A room normally needs both an opening for purge ventilation and background ventilation, such as a trickle vent, so the line of the wall is set at the survey to leave each room its own.' + NCHAR(10) + N'[construction] A floor that cannot carry the wall. A new internal wall should stand on something strong enough, whether it holds anything up or not. On a timber floor, the Planning Portal advises asking a structural engineer or surveyor to check the joists, and a new beam may be needed under the wall.' + NCHAR(10) + N'[home] A bedroom beside a bathroom. A new wall that separates a bedroom, or a room with a WC, from another room needs improved sound insulation. Mineral wool in the frame, and the edges sealed, are what stop the sound.',
    [VisitSteps] = N'At the survey we mark out the wall with you, look at the floor and the ceiling it will be fixed to, go through the escape, sound and ventilation the new rooms need, and give you the fixed quote.' + NCHAR(10) + N'We fix a rail to the floor and one to the ceiling, into the joists where the wall crosses them, and stand the studs between.' + NCHAR(10) + N'We frame the doorway, and fit timber between the studs wherever something heavy will hang.' + NCHAR(10) + N'Where the wall has to stop sound, we fill the frame with mineral wool.' + NCHAR(10) + N'We board both sides, tape and fill the joints, fit the door lining, the architrave and the skirting, and hang the door.',
    [BeforeWeArrive] = N'[shelves] Tell us where anything heavy will go on the new wall, such as a TV, shelves, a wall cupboard or a radiator, so timber goes in behind the board first. Siniat recommends a timber nogging or a pattress behind large or heavy items.' + NCHAR(10) + N'[bolt] Decide at the survey where the new room''s sockets, switches and light will be, so the cables are in the frame before it is boarded.' + NCHAR(10) + N'[checklist] Clear the room along the line of the wall before the first day.',
    [Included] = N'A survey and a fixed quote' + NCHAR(10) + N'A timber or metal stud frame, fixed into the floor, the ceiling and the walls' + NCHAR(10) + N'A framed doorway with its lining, and the door hung' + NCHAR(10) + N'Timber behind the board for anything heavy' + NCHAR(10) + N'Sound insulation in the frame where it is needed' + NCHAR(10) + N'Plasterboard on both sides, the joints taped and filled ready for decorating' + NCHAR(10) + N'Architrave and skirting',
    [NotIncluded] = N'Taking out a wall that holds up a floor or the roof, which needs a structural engineer and a beam'
WHERE [Slug] = N'partition-walls-drylining';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'partition-walls-drylining');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does a stud wall need building regulations approval?', N'Generally, yes. The Planning Portal says work to provide a new internal wall generally needs approval under the Building Regulations, because it can change the way out in a fire, the sound between rooms and how the rooms are ventilated.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'partition-walls-drylining';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Do I need planning permission for a new internal wall?', N'Not usually. Internal alterations should not need an application for planning permission. A listed building is the exception: listed building consent is needed for any significant works, inside or out.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'partition-walls-drylining';

UPDATE [Services]
SET [Description] = N'Woodwork made for one place in the house and built there: cupboards and shelves fitted into the alcoves beside a chimney breast, and boxing that hides pipes along a skirting, behind a toilet or up the corner of a room. Each piece is measured and cut to the walls it meets, which in an older house are seldom square.' + NCHAR(10) + N'' + NCHAR(10) + N'Whatever has to be reached later stays within reach. Boxing over a valve or a joint gets a panel that comes off.',
    [Summary] = N'Alcove cupboards and shelving made to measure and built in place, and boxing for pipes with access at every valve, at a fixed price from a survey.',
    [CommonFindings] = N'[build] Two alcoves that do not match. The alcoves either side of a chimney breast are seldom the same width, and their walls lean or bow, so each cupboard is built to its own alcove, with its sides shaped to the wall so that no gap shows.' + NCHAR(10) + N'[plumbing] A soil pipe that is loud when the toilet flushes. Mineral wool packed round the pipe inside the boxing quietens it.' + NCHAR(10) + N'[warning] A boiler flue where the boxing will go. HSE says a flue hidden in a void needs inspection hatches, and a gas engineer who finds one that cannot be inspected will advise that the boiler is ""at risk"" and, with the owner''s permission, turn off its gas. Boxing round a flue is built with hatches.',
    [VisitSteps] = N'At the survey we measure the alcoves or the pipe run, agree the design and the materials with you, and give you the fixed quote.' + NCHAR(10) + N'We build the frame in place, fixed to the wall and the floor.' + NCHAR(10) + N'We fit the doors, shelves and facings, and leave a panel that lifts out at every valve and joint in a boxing.' + NCHAR(10) + N'We fill the fixings and sand the joins, ready for paint.',
    [BeforeWeArrive] = N'[checklist] Clear the alcoves, or the run of pipe to be boxed, and the floor in front of it.' + NCHAR(10) + N'[plumbing] Show us at the survey where your stop valve and any other valves are, so the panels go in the right places.' + NCHAR(10) + N'[water_drop] If boxing in a bathroom will be tiled, tell us at the survey. It is then built from moisture-resistant board, with the panel made to lift out with its tiles.',
    [Included] = N'A survey and a fixed quote' + NCHAR(10) + N'Alcove cupboards and shelving, made to measure and built in place' + NCHAR(10) + N'Boxing for pipes along a wall, behind a toilet or in a corner' + NCHAR(10) + N'An access panel at every valve and joint behind boxing' + NCHAR(10) + N'Inspection hatches where boxing goes round a boiler flue' + NCHAR(10) + N'Fixings filled and joins sanded, ready for paint',
    [NotIncluded] = N'Shelves on brackets and floating shelves: those are Shelf Installation, by the hour' + NCHAR(10) + N'Flat-pack furniture: that is Furniture Assembly, by the hour'
WHERE [Slug] = N'custom-carpentry-boxing-in';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'custom-carpentry-boxing-in');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can a stop valve be boxed in?', N'Yes, if it can still be reached. Water Regs UK, in its guidance on the water fittings rules, says there must be access to the joints and fittings that need maintenance, such as the valves that control the flow. So the boxing is made with a panel there that you can take off.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'custom-carpentry-boxing-in';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can pipes be hidden inside the wall instead of boxed in?', N'Only where nothing else will do. Water Regs UK''s guidance says joints on concealed pipework are likely to lose their integrity over time, so concealing them should be considered only where it cannot be avoided. Boxing with a panel keeps them in reach.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'custom-carpentry-boxing-in';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'kitchen-fitting-alterations');

UPDATE [Services]
SET [Description] = N'Fitting kitchen units, worktops, sink plumbing, splashbacks, and integrated appliances for partial or complete kitchen renovations.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'kitchen-fitting-alterations';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'partition-walls-drylining');

UPDATE [Services]
SET [Description] = N'Constructing timber or metal stud partition walls, acoustic insulation, plasterboarding, and room reconfigurations for home offices or layouts.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'partition-walls-drylining';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'custom-carpentry-boxing-in');

UPDATE [Services]
SET [Description] = N'Bespoke alcove cupboards, floating shelving units, pipework boxing-in, and tailored interior woodwork crafted to suit your room dimensions.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'custom-carpentry-boxing-in';");

        }
    }
}
