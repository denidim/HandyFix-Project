using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The fourth five service pages' own text (PROJECT_STATE Section 3eu): Pipe Repairs,
            // Bathroom Plumbing, General Plumbing Maintenance, Door Trimming & Shaving, Door Repairs.
            // The description, the six blocks and the page's questions, by the service's address, as
            // batches 1 to 3 did (AddServicePageTextsBatch1 to 3). A service the admin has deleted, or
            // renamed (its address follows its name), is not found, and nothing is changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'Pipes split in a frost, take a nail through a floorboard, wear thin, or are still the old lead. We cut out the damaged length and join in new copper or plastic pipe, then test the run under full mains pressure.' + NCHAR(10) + N'' + NCHAR(10) + N'Lead pipe inside the house is replaced as far as the inside stop valve. Thames Water says the pipe from your outside stop valve to the inside one is yours, and that you are unlikely to have lead if the house was built after 1970.',
    [Summary] = N'Damaged lengths of pipe cut out and replaced in copper or plastic: frost splits, nail holes, worn runs and old lead inside the house, tested under full mains pressure.',
    [CommonFindings] = N'[warning] A split left by frost. Pipes in a loft, a garage or along an outside wall freeze in a cold spell. A split pipe needs a new section, and a clamp put on in an emergency is only a stopgap.' + NCHAR(10) + N'[build] A nail or screw through a pipe. New flooring, a shelf or a picture rail fixed over a hidden pipe. The water often shows only when the fixing is pulled out or works loose.' + NCHAR(10) + N'[search] Lead pipe. Thames Water''s signs: dull grey when unpainted, rounded swollen joints, and shiny silver metal where it is gently scratched.' + NCHAR(10) + N'[plumbing] Pipes that knock or bang. Usually a pipe that has come loose from its clips, so it moves when a tap or a valve shuts.',
    [VisitSteps] = N'We find the damaged length, and turn off the water at the nearest valve that covers it.' + NCHAR(10) + N'We open boxing or lift a floorboard where the pipe runs, and drain the pipe down.' + NCHAR(10) + N'We cut out the bad section and join in new pipe, clipped so it cannot move.' + NCHAR(10) + N'Once the water is back on, we check every new joint under full pressure, then put the boxing or floorboard back.',
    [BeforeWeArrive] = N'[home] Clear the way to the pipe: the cupboard under the sink, the loft hatch, or the furniture over the floorboards where it runs.' + NCHAR(10) + N'[add_a_photo] Add a photo of the damage, and of the inside stop valve, to your booking.' + NCHAR(10) + N'[emergency] Water spraying, or coming through a ceiling, cannot wait for a slot. Turn the water off at the stop valve and call or WhatsApp us.',
    [Included] = N'Finding the damaged length and draining it down' + NCHAR(10) + N'Opening boxing or lifting floorboards where the pipe runs' + NCHAR(10) + N'Cutting out the damaged section and joining in new copper or plastic pipe' + NCHAR(10) + N'Lead pipe inside the house, as far as the inside stop valve' + NCHAR(10) + N'Clipping loose runs that knock' + NCHAR(10) + N'Testing every new joint under full pressure',
    [NotIncluded] = N'The underground supply pipe between the outside stop valve and the house, which has to be dug up and relaid' + NCHAR(10) + N'The water company''s own pipe, up to the outside stop valve: the water company repairs that'
WHERE [Slug] = N'pipe-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'pipe-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Who pays to replace a lead pipe?', N'You pay for your part, and the water company for its own. Thames Water owns the pipe from its main to the outside stop valve, and for a house built before 1970 its scheme replaces that part free when the owner replaces their own lead supply pipe.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'pipe-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What can I do until a lead pipe is replaced?', N'Run the cold kitchen tap for two minutes in the morning, or after a few days away, and use only that tap for drinks and cooking. That is Thames Water''s advice. It adds that boiling the water does not help, and that lowering lead matters most if you are pregnant or have young children.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'pipe-repairs';

UPDATE [Services]
SET [Description] = N'A new basin, bath, bidet or toilet in the place of the old one, with the tiles and the layout staying as they are. We take the old piece out, fit the new one, connect it up, and run water through until every joint is dry.' + NCHAR(10) + N'' + NCHAR(10) + N'Moving things round, or retiling, is a new bathroom rather than a swap. That is Full Bathroom Refurbishment, priced after a free survey.',
    [Summary] = N'A basin, bath, bidet or toilet swapped for a new one in the same place, its taps and waste connected and every joint tested dry.',
    [CommonFindings] = N'[construction] A wall-hung basin on a stud wall. Plasterboard alone will not hold a basin and the weight of someone leaning on it, so it needs timber behind the board or a fixing frame.' + NCHAR(10) + N'[build] Tap holes that do not match. A basin or bath with one tap hole takes a mixer, and one with two takes a pair of pillar taps, so the taps are chosen with the new piece.' + NCHAR(10) + N'[plumbing] An outlet in a different place. A new toilet''s outlet has to meet the old soil pipe, and a new basin''s waste the old waste pipe, or the pipe behind is extended or moved.' + NCHAR(10) + N'[info] A large bath. A bath that holds more than 230 litres, measured to the centre of its overflow, needs the water company''s consent before it is fitted.',
    [VisitSteps] = N'We turn off the water and isolate the supplies, and take the old piece out with its taps and waste.' + NCHAR(10) + N'We check the floor or wall where the new one goes, and fix its support, frame or legs.' + NCHAR(10) + N'We fit the new piece, its taps and its trap, with an isolating valve on each supply.' + NCHAR(10) + N'With the water back on, we fill and drain it, and check every joint until it stays dry.',
    [BeforeWeArrive] = N'[checklist] Before you buy a new toilet, measure from the wall behind it to the middle of the old pan''s outlet. The new one has to reach the same soil pipe.' + NCHAR(10) + N'[inventory_2] Keep the new piece and its taps in their boxes, with the fitting instructions, so every part can be checked before the old one comes out.' + NCHAR(10) + N'[home] Clear the bathroom floor and the shelves round the basin or bath.' + NCHAR(10) + N'[add_a_photo] Add photos of the old piece and of the pipes under it to your booking.',
    [Included] = N'Taking out the old basin, bath, bidet or toilet' + NCHAR(10) + N'Fitting the new one with its support, frame or legs' + NCHAR(10) + N'Connecting the taps, waste and trap' + NCHAR(10) + N'An isolating valve on each supply' + NCHAR(10) + N'Testing every joint dry with the water running',
    [NotIncluded] = N'Moving a bath, basin or toilet to a new place, or retiling: that is Full Bathroom Refurbishment, priced after a survey'
WHERE [Slug] = N'bathroom-plumbing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'bathroom-plumbing');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Do I need to tell the water company about a new bath?', N'Only for a large one. The Water Fittings Regulations ask for notice and the water company''s consent before fitting a bath that holds more than 230 litres, measured to the centre of the overflow. The capacity is in the bath''s specification. If the company has not answered within ten working days, consent is taken as given.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'bathroom-plumbing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can you fit a basin or toilet I have bought myself?', N'Yes. Send us its name or a photo of the box when you book, so we can check its outlet and fixings suit the pipes and the wall it is going on.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'bathroom-plumbing';

UPDATE [Services]
SET [Description] = N'An hour spent going round the plumbing before something fails: the stop valve, the small valves under sinks and toilets, the tank in the loft if there is one, and the pipes that freeze. It is a chance to turn valves nobody has touched in years, and to catch parts that have started to drip.' + NCHAR(10) + N'' + NCHAR(10) + N'We tell you what we found as we go. Small things, such as a valve that will not shut or a washer that drips, we can put right on the same visit. Anything bigger we price before we start.',
    [Summary] = N'A visit to check the stop valve, the isolating valves, the loft tank and the pipes most likely to freeze, with small faults put right on the same visit.',
    [CommonFindings] = N'[plumbing] A stop valve that will not turn, or will not shut. Left untouched for years it can seize, and the day it is needed is the day of a leak.' + NCHAR(10) + N'[water_drop] A pipe dripping from the wall outside. It is a warning pipe from a toilet cistern or a loft tank, and it means the valve that fills it is not shutting off.' + NCHAR(10) + N'[home] A loft tank without a proper lid. The Water Fittings Regulations ask for a storage tank to have a cover that keeps out light and insects, insulation against frost, and an overflow with a screen.' + NCHAR(10) + N'[warning] Bare pipes in a cold place. Thames Water names pipes in unheated lofts, garages and utility rooms, along cold outside walls and to outside taps as the ones most likely to freeze.',
    [VisitSteps] = N'We find the inside stop valve, turn it off and on again, and check the water stops.' + NCHAR(10) + N'We try each isolating valve under the sinks, toilets and appliances, and look at the joints and hoses round them.' + NCHAR(10) + N'In the loft, we look at the tank''s lid, insulation and overflow, and the pipes up there.' + NCHAR(10) + N'We put right what can be done in the time booked, and tell you what is left and what it would cost.',
    [BeforeWeArrive] = N'[key] Make sure we can get into the loft, the cupboard under the stairs and any utility room or garage with pipes in it.' + NCHAR(10) + N'[search] If you do not know where your stop valve is, Thames Water says it is usually under the kitchen sink. We will find it if it is not.' + NCHAR(10) + N'[checklist] Write down anything you have noticed: a tap that drips, a toilet that runs, a noise in the pipes, a damp patch.',
    [Included] = N'Finding the inside stop valve and checking it shuts the water off' + NCHAR(10) + N'Trying the isolating valves under sinks, toilets and appliances' + NCHAR(10) + N'Looking at the hoses and joints round them' + NCHAR(10) + N'Checking the loft tank''s lid, insulation and overflow' + NCHAR(10) + N'Small fixes on the same visit, such as a washer or a valve' + NCHAR(10) + N'A list of anything left, with what it would cost',
    [NotIncluded] = N'A boiler service or any gas check: those are for our Gas Safe registered engineer, on the boiler page'
WHERE [Slug] = N'general-plumbing-maintenance';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'general-plumbing-maintenance');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What happens if the stop valve is seized?', N'We do not force it, because a stop valve forced after years can start to leak at the spindle. We tell you, and a stop valve that does not shut is worth replacing: until it is, a burst cannot be stopped inside the house.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'general-plumbing-maintenance';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What time of year suits a check?', N'Before the first frost. Thames Water asks for bare pipes in lofts, garages and outside to be lagged before a cold spell, and the check shows which of yours need it.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'general-plumbing-maintenance';

UPDATE [Services]
SET [Description] = N'New carpet, laminate or tiles raise the floor, and the doors that cleared the old one start to catch. We take each door off its hinges, mark the cut from the new floor, trim the bottom edge, seal the bare wood and hang it back.' + NCHAR(10) + N'' + NCHAR(10) + N'We leave a gap over the floor, so air still passes under the door as well as the door swinging clear.',
    [Summary] = N'Doors that catch on new carpet, laminate or tiles taken off, trimmed to clear the floor, the cut edge sealed, and hung back.',
    [CommonFindings] = N'[home] A door that drags across its whole width. The floor has risen under it, and the bottom edge needs trimming.' + NCHAR(10) + N'[build] A door that catches at one corner only. It has usually dropped on a loose top hinge, and tightening the hinge cures it without a cut. That is a door repair.' + NCHAR(10) + N'[warning] A hollow door. It has a solid timber frame round a hollow core, and its maker sets how much can come off. A cut past that opens the core.' + NCHAR(10) + N'[info] A fire door. Fire doors are tested at their exact size, and the government''s guidance asks a flat''s front door to close by itself against any friction with the floor, so a new floor can stop one doing its job.',
    [VisitSteps] = N'We measure from the floor with the door shut, and mark where the bottom edge has to come to.' + NCHAR(10) + N'We take the door off its hinges and cut the bottom edge against a straight guide, so the cut stays clean.' + NCHAR(10) + N'We seal the bare wood of the cut edge, so the door does not take up damp through it.' + NCHAR(10) + N'We hang the door back and check it swings over the new floor and closes on its latch.',
    [BeforeWeArrive] = N'[timer] Wait until the new floor is down. A door trimmed for the underlay before the carpet goes on ends up being cut twice.' + NCHAR(10) + N'[checklist] Count the doors that catch and put the number in your booking, so we can plan the time for all of them.' + NCHAR(10) + N'[home] Clear the floor on both sides of each door.',
    [Included] = N'Measuring each door from the finished floor' + NCHAR(10) + N'Taking the door off and trimming the bottom edge' + NCHAR(10) + N'Sealing the cut edge' + NCHAR(10) + N'Hanging the door back and checking it closes',
    [NotIncluded] = N'Trimming a fire door, including most flats'' front doors: the gaps round it are set by its certificate' + NCHAR(10) + N'Trimming a door by more than its maker allows: a door cut that far needs replacing'
WHERE [Slug] = N'door-trimming-shaving';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'door-trimming-shaving');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How much can you take off a door?', N'As much as its maker allows, and that differs from door to door. A solid timber door can lose more than a hollow one, whose core opens once the cut goes past its frame. If the new floor needs more than the door can give, we tell you before we cut.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'door-trimming-shaving';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does a door need a gap underneath?', N'Yes, so that air can move between rooms. Approved Document F, the government''s ventilation guidance for new homes, gives 10mm above the floor finish for internal doors, or 20mm above the bare floor where the finish is still to go down.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'door-trimming-shaving';

UPDATE [Services]
SET [Description] = N'When a door sticks in its frame, has dropped, rattles, will not latch or lets in a draught, we look first at its hinges, its latch and keep, and its edges, and put it right where it hangs. We also fit draught strips and adjust or fit door closers.' + NCHAR(10) + N'' + NCHAR(10) + N'A door that catches on a new floor is a different job: that is Door Trimming & Shaving.',
    [Summary] = N'Sticking, dropped and rattling doors put right at the hinges, the latch or the edge, with draught strips and door closers fitted or adjusted.',
    [CommonFindings] = N'[build] Loose hinge screws. The top hinge carries most of the door''s weight, and when its screws work loose the door drops and rubs the frame at the top of the latch side.' + NCHAR(10) + N'[lock] A latch that misses its keep. When the door has moved, the latch no longer meets the plate in the frame, so the door will not stay shut or it rattles. The keep is moved or adjusted to meet it.' + NCHAR(10) + N'[water_drop] A door that sticks in damp weather. Bare or worn timber at the edges takes up moisture and swells. It is eased with a plane and the edge sealed again.' + NCHAR(10) + N'[refresh] A closer that slams the door, or will not shut it. An overhead closer has valves that set how fast it swings and how hard it shuts.',
    [VisitSteps] = N'We open and close the door, and look at where it rubs, how it hangs and how the latch meets the keep.' + NCHAR(10) + N'We tighten or refix the hinges first, since a door that has dropped needs no planing once they hold.' + NCHAR(10) + N'We plane an edge that still rubs, or move the keep, and seal any bare wood we have cut.' + NCHAR(10) + N'We check the door opens, closes and latches without force before we leave.',
    [BeforeWeArrive] = N'[home] If it is your flat''s front door, ask the building''s managing agent first. In most blocks it is a fire door, and the government''s guidance asks residents to report its faults and not to tamper with its closer.' + NCHAR(10) + N'[add_a_photo] Add a photo of where the door rubs, or of the gap round it, to your booking.' + NCHAR(10) + N'[inventory_2] If you have bought draught strips or a closer, keep them in their packets with the instructions.',
    [Included] = N'Tightening, refixing or replacing hinges' + NCHAR(10) + N'Easing a door that sticks, and sealing the planed edge' + NCHAR(10) + N'Moving or adjusting the latch keep' + NCHAR(10) + N'Fitting draught strips and brush seals' + NCHAR(10) + N'Fitting or adjusting a door closer',
    [NotIncluded] = N'Work on a fire door, including most flats'' front doors: its seals, gaps and fittings are for a fire door specialist' + NCHAR(10) + N'New locks and handles: that is Lock & Handle Replacement'
WHERE [Slug] = N'door-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'door-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Why does my door stick only in winter?', N'Because the timber takes up moisture in damp weather and swells, then shrinks back when it dries. Easing the edge and sealing it again keeps the damp out of the timber. We plane off only what it needs, or the door will have a gap in summer.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'door-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How can I check that my flat''s front door closes properly?', N'The government''s guidance gives a simple test: open the door fully and let it go, then open it to about 15 degrees and let it go. Both times it should close fully into the frame and latch without help.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'door-repairs';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'pipe-repairs');

UPDATE [Services]
SET [Description] = N'Replacing damaged copper, plastic, or lead piping to restore secure water flow.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'pipe-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'bathroom-plumbing');

UPDATE [Services]
SET [Description] = N'Installing baths, basins, bidets, or replacing complete bathroom sanitaryware.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'bathroom-plumbing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'general-plumbing-maintenance');

UPDATE [Services]
SET [Description] = N'Routine plumbing checks, minor valve adjustments, and general system health checks.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'general-plumbing-maintenance';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'door-trimming-shaving');

UPDATE [Services]
SET [Description] = N'Trimming or easing a door that catches on new carpet or flooring so it opens and closes freely.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'door-trimming-shaving';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'door-repairs');

UPDATE [Services]
SET [Description] = N'Adjusting sticking doors, replacing hinges, or fitting draft excluders and door closers.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'door-repairs';");

        }
    }
}
