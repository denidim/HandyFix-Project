using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The seventh five service pages' own text (PROJECT_STATE Section 3ex): Flooring
            // Installation, External Brickwork & Paving, General Handyman Call-Out, Minor Home
            // Repairs, Property Maintenance. The description, the six blocks and the page's
            // questions, by the service's address, as batches 1 to 6 did (AddServicePageTextsBatch1
            // to 6). A service the admin has deleted, or renamed (its address follows its name), is
            // not found, and nothing is changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'Laminate, luxury vinyl tiles (LVT) and engineered wood, laid on a base made ready for them: firm, level and dry, with the underlay the boards are made for, beading along the skirting and a trim at each doorway where the new floor meets the next one.' + NCHAR(10) + N'' + NCHAR(10) + N'You buy the flooring, and we fit it. If you want, we can find it for you.',
    [Summary] = N'Laminate, LVT and engineered wood laid on a firm, level, dry base, with the right underlay, beading and door trims, at a fixed price from a survey.',
    [CommonFindings] = N'[water_drop] A concrete floor that is still damp. New screed takes time to dry out, and an old slab may have no damp-proof membrane under it. Karndean''s UK guide for its glue-down tiles asks for less than 75% relative humidity at the surface, so the concrete is tested first, and laminate goes down over a damp-proof membrane.' + NCHAR(10) + N'[search] Boards that flex or dip. Old floorboards that move, or a floor that is out of level, show through a new floor as joints that click and open. Loose boards are screwed down and the low spots levelled before the underlay goes on.' + NCHAR(10) + N'[home] A door that catches. The new boards and their underlay raise the floor, and a door that swung clear of the old carpet drags on them. We can trim it on the same visit.',
    [VisitSteps] = N'At the survey we measure the rooms, look at the floor under the old covering, agree the flooring and where the joins and trims go, and give you the fixed quote.' + NCHAR(10) + N'We take up the old floor covering, with its underlay and any carpet gripper.' + NCHAR(10) + N'We make the base ready: loose boards screwed down, dips levelled, a damp-proof membrane over concrete.' + NCHAR(10) + N'We lay the underlay and the floor, keeping the gap at each wall that lets it move.' + NCHAR(10) + N'We fit the beading along the skirting and a trim at each doorway, then check that every door swings clear.',
    [BeforeWeArrive] = N'[key] In a flat, read your lease before choosing a hard floor. Some leases ask for carpet, or for the landlord''s consent and an acoustic underlay first.' + NCHAR(10) + N'[add_a_photo] For the survey, have the name and maker of the flooring you like. The maker''s instructions set the underlay and the gap at the walls.' + NCHAR(10) + N'[home] The whole floor has to be clear on the day. Tell us at the survey about anything too heavy to move, such as a piano or a fitted wardrobe.',
    [Included] = N'A survey of the rooms and a fixed quote' + NCHAR(10) + N'Taking up the old floor covering' + NCHAR(10) + N'Screwing down loose boards and levelling dips' + NCHAR(10) + N'A damp-proof membrane over concrete' + NCHAR(10) + N'Underlay suited to the flooring' + NCHAR(10) + N'Beading along the skirting and trims at the doorways',
    [NotIncluded] = N'Carpet' + NCHAR(10) + N'Ceramic, porcelain and stone floors, which are Wall & Floor Tiling'
WHERE [Slug] = N'flooring-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'flooring-installation');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How long does new flooring need to sit in the room before it is laid?', N'At least 48 hours for laminate, in its unopened packs laid flat: that is Wickes''s laminate guide. Karndean asks for its glue-down vinyl tiles to come out of their packs at least 24 hours before fitting, in the room they are for.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'flooring-installation';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Why does a laminate floor need a gap at the walls?', N'Because laminate expands and contracts over time. Wickes''s guide asks for a gap of 10 to 12mm all round the room, and warns that a floor laid without one can warp. The beading or the skirting covers it.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'flooring-installation';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can I walk on a new vinyl floor straight away?', N'Not on glue-down vinyl tiles. Karndean asks for them to be kept free of traffic for 24 hours after fitting, or covered with board or sheeting.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'flooring-installation';

UPDATE [Services]
SET [Description] = N'The brick and paving outside the house: worn mortar joints raked out and repointed, garden walls repaired, patio slabs lifted and relaid, outdoor steps rebuilt, and concrete bases laid for sheds.' + NCHAR(10) + N'' + NCHAR(10) + N'A patio laid against the house is kept at least 150mm below the damp-proof course, the layer in the brickwork that stops damp rising up the wall. Marshalls, the paving maker, sets that as the minimum in its laying guide.',
    [Summary] = N'Repointing, garden wall repairs, patios relaid, outdoor steps rebuilt and shed bases laid, at a fixed price after a free survey.',
    [CommonFindings] = N'[build] Mortar worn back from the face of the brick. Shallow new mortar on top of it does not last, so LABC''s guidance is to rake the old joints out at least 12mm deep, and no more than 19mm, before repointing.' + NCHAR(10) + N'[warning] Brick faces crumbling round hard pointing. A cement mortar that is stronger and less permeable than the brick sends the wet out through the bricks instead of the joints, and the bricks decay while the mortar stands proud. LABC says the mortar should be slightly weaker and more permeable than the brick, so the new mix is chosen to suit it.' + NCHAR(10) + N'[water_drop] Slabs that rock or hold puddles. They are lifted, set on a new bed and laid to a fall so the rain runs off. Marshalls'' guide asks for a fall of at least 1 in 80, and next to a house the fall runs away from it.' + NCHAR(10) + N'[search] A garden wall that leans, or has cracked through the bricks. Repointing will not hold it, so the stretch that has moved is taken down and rebuilt.',
    [VisitSteps] = N'At the survey we look at the wall, the joints or the paving, agree what is repaired and what is rebuilt, and give you the fixed quote.' + NCHAR(10) + N'We rake out the joints, take down the loose brickwork, or lift the slabs and clear the bed under them.' + NCHAR(10) + N'We repoint, rebuild or relay, with a mortar mixed to suit the brick.' + NCHAR(10) + N'We brush the new joints and clean the mortar off the faces of the bricks and slabs.' + NCHAR(10) + N'We cover fresh work when rain or frost is due.',
    [BeforeWeArrive] = N'[directions_car] Tell us at the survey how we reach the back garden: by a side gate, or only through the house.' + NCHAR(10) + N'[home] If the wall is on the boundary with a neighbour, talk to them before the work starts.' + NCHAR(10) + N'[visibility] Note where water stands on the patio after rain, and which way it runs.',
    [Included] = N'The survey at your home, with a fixed quote' + NCHAR(10) + N'Raking out and repointing' + NCHAR(10) + N'Rebuilding a damaged stretch of garden wall' + NCHAR(10) + N'Lifting and relaying patio slabs on a new bed' + NCHAR(10) + N'Rebuilding outdoor steps' + NCHAR(10) + N'Concrete bases for sheds',
    [NotIncluded] = N'Driveways' + NCHAR(10) + N'Retaining walls, the walls that hold back soil'
WHERE [Slug] = N'external-brickwork-paving';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'external-brickwork-paving');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Do I need planning permission to pave my front garden?', N'Only for a hard surface that water cannot drain through, over more than five square metres, with no lawn or border for the rain to run to. Gravel and permeable block paving need no permission at any size. That is the Planning Portal''s guidance for houses; flats and maisonettes have different rules.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'external-brickwork-paving';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How high can a garden wall be without planning permission?', N'One metre next to a road or its pavement, and two metres anywhere else, measured from the ground. That is the Planning Portal''s guidance. A listed building, or a planning condition or council direction for the area, can take that right away.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'external-brickwork-paving';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can brickwork and paving be done in winter?', N'On mild days. Marshalls'' guide says paving is not laid when the temperature is below 5°C and falling, night-time included, and fresh mortar in brickwork needs the same care, so work in a cold spell waits for a milder day.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'external-brickwork-paving';

UPDATE [Services]
SET [Description] = N'An hour of a handyman''s time, for a few small jobs at once, or for one that is not on our list. Tell us in your booking what the jobs are and add photos, so the technician knows what to bring.' + NCHAR(10) + N'' + NCHAR(10) + N'There is no call-out fee, whatever the name says. You pay for the time the jobs take, and for any materials agreed with you first.',
    [Summary] = N'An hour of a handyman''s time for a mix of small jobs, or one that is not on our list, with no call-out fee.',
    [CommonFindings] = N'[inventory_2] A part we do not carry. A handle to match the others, a hinge of an old size or a length of trim is priced and agreed with you before it is bought.' + NCHAR(10) + N'[construction] A job bigger than a visit. A new floor, a rebuilt wall or a refitted bathroom is not done by the hour: it is priced at a free survey, with a fixed quote.',
    [VisitSteps] = N'We go through your list with you and agree the order, the job that matters most first.' + NCHAR(10) + N'We do the jobs one by one, and show you each one when it is done.' + NCHAR(10) + N'We tidy up, and tell you what, if anything, is left on the list.',
    [BeforeWeArrive] = N'[checklist] Write the jobs down, in the order they matter to you.' + NCHAR(10) + N'[add_a_photo] Add a photo of each job to your booking: a close-up, and one from a step back that shows the wall or the room.' + NCHAR(10) + N'[home] Clear a space round each job, so the work can start straight away.',
    [Included] = N'Several small jobs in the same booking' + NCHAR(10) + N'Jobs that are not on our list of services' + NCHAR(10) + N'A price for any part needed, before it is bought',
    [NotIncluded] = N'Plumbing, which is booked under Plumbing' + NCHAR(10) + N'Gas work, which is for our Gas Safe registered engineer' + NCHAR(10) + N'New circuits and other notifiable electrical work' + NCHAR(10) + N'Emergencies, which are taken by phone or WhatsApp'
WHERE [Slug] = N'general-handyman-call-out';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'general-handyman-call-out');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Should I buy the materials before the visit?', N'Yes, for anything you choose yourself: the shelf, the mirror, the handles, the curtain pole. Anything else the jobs need beyond what the technician carries is priced and agreed with you before it is bought.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'general-handyman-call-out';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What if the list takes more than the hour?', N'We start with the job that matters most to you, and say before the hour is up how much is left. You decide whether we carry on, or book the rest for another day.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'general-handyman-call-out';

UPDATE [Services]
SET [Description] = N'Small faults build up in every house, and we put them right by the hour: cracks in the plaster raked out and filled, floorboards that creak screwed down, and loose handles, hinges, towel rails and skirting boards fixed back firm.' + NCHAR(10) + N'' + NCHAR(10) + N'A filled crack is left sanded smooth and ready for paint.',
    [Summary] = N'Plaster cracks filled, creaking floorboards screwed down, and loose handles, hinges, rails and skirting fixed firm, by the hour.',
    [CommonFindings] = N'[refresh] A crack that opens and closes with the seasons. NHBC, the new-home warranty body, says the materials of a house expand and shrink as the temperature and humidity change through the year, which leaves fine cracks where an outside wall meets a plasterboard one, along the joins of a plasterboard ceiling, and at the corners of skirting boards and architraves. A flexible filler suits these better than a hard one.' + NCHAR(10) + N'[build] A floorboard that creaks. The board is moving against its nails or the board beside it. A screw down into the joist holds it still, once we have checked for pipes and cables under it.' + NCHAR(10) + N'[handyman] A handle or a towel rail that keeps working loose. Its screws have worn their holes too big to grip, so the holes are plugged and drilled again, or the fixing is changed for one that suits the wall.' + NCHAR(10) + N'[home] A cupboard door that hangs crooked or will not close. Most kitchen hinges have screws that adjust the door''s height, depth and sideways position, and a few turns bring it square.',
    [VisitSteps] = N'We go round the repairs on your list with you, and look at each crack before it is filled.' + NCHAR(10) + N'We rake out each crack, fill it, and sand it flush once the filler has set.' + NCHAR(10) + N'We screw down creaking boards and fix back whatever is loose.' + NCHAR(10) + N'We tell you about anything that needs more than a repair, such as a crack to show a surveyor.',
    [BeforeWeArrive] = N'[checklist] List the repairs in your booking, with a photo of each.' + NCHAR(10) + N'[visibility] Tell us how long each crack has been there, and whether it has grown.' + NCHAR(10) + N'[home] Clear the floor over a creaking board: rugs and furniture off it.',
    [Included] = N'Filling cracks in plaster, ready for paint' + NCHAR(10) + N'Screwing down creaking floorboards' + NCHAR(10) + N'Fixing back loose handles, hinges, towel rails and skirting' + NCHAR(10) + N'Adjusting kitchen cupboard doors on their hinges',
    [NotIncluded] = N'Cracks wider than 5mm, or still growing, until a surveyor has seen them' + NCHAR(10) + N'Painting over the repair, which is Painting Touch-Ups' + NCHAR(10) + N'Replastering a wall or a ceiling, which is Plastering & Ceiling Repairs' + NCHAR(10) + N'Dripping taps and other plumbing, which are booked under Plumbing'
WHERE [Slug] = N'minor-home-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'minor-home-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Are cracks in a new house normal?', N'Fine ones, usually. NHBC says the plaster, mortar and concrete of a new home hold a lot of water and shrink as they dry out, which can leave minor cracks in walls and floors, often along a wall near the ceiling or the floor. Those cracks normally stay the same width and are less than 2mm wide.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'minor-home-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'When is a crack more than a repair?', N'When it is wider than about 5mm, or getting wider. BRE, the Building Research Establishment, treats cracks up to 5mm wide, in general, as a matter of redecoration. It says whether a crack is getting wider can change that, and that a building professional should be consulted where the damage is significant.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'minor-home-repairs';

UPDATE [Services]
SET [Description] = N'A longer visit, of about three hours, for the jobs a house collects over the months. We work through your list room by room, inside and out, and check the small things that fail without anyone noticing: the alarms, the seals round the bath and shower, the doors and their catches.' + NCHAR(10) + N'' + NCHAR(10) + N'It suits a home between tenants, one being made ready for sale, or one that has not had a going-over for a while. The plumbing has a visit of its own, General Plumbing Maintenance.',
    [Summary] = N'About three hours of handyman repairs and checks round the whole home in one visit, from your list, with every alarm tested.',
    [CommonFindings] = N'[health_and_safety] A smoke alarm that stays silent. Every test button is pressed on the visit. For a landlord, the Smoke and Carbon Monoxide Alarm (England) Regulations 2015 ask for each alarm to be in proper working order on the day a new tenancy begins.' + NCHAR(10) + N'[water_drop] Sealant round the bath or shower that has gone black or come away. Water gets behind it each time the bath or shower is used, so it is cut out and run again.' + NCHAR(10) + N'[home] A door that sticks in winter and swings free in summer. NHBC says a house expands and shrinks with the temperature and humidity through the year, and doors can stick in their frames or swing open. The hinges and the catch are adjusted, or the door is eased where it binds.' + NCHAR(10) + N'[build] A loose handrail on the stairs. It is fixed first, because it is the fitting people put their weight on.',
    [VisitSteps] = N'We walk round with your list and decide with you what comes first.' + NCHAR(10) + N'We work through it room by room, then outside.' + NCHAR(10) + N'We press the test button on every smoke and carbon monoxide alarm.' + NCHAR(10) + N'We tell you about anything that needs a quote, a survey or another trade.' + NCHAR(10) + N'Before we leave, we go through what was done and what is left.',
    [BeforeWeArrive] = N'[checklist] Walk round first and note the jobs room by room, then put the list and a photo of each in your booking.' + NCHAR(10) + N'[key] If the home is let, give your tenants at least 24 hours'' notice. GOV.UK''s guidance for landlords asks for that before entering to inspect or carry out repairs.' + NCHAR(10) + N'[inventory_2] Have the parts you have chosen there, such as new handles, a toilet seat or light bulbs.',
    [Included] = N'Your list of handyman repairs, inside and out' + NCHAR(10) + N'A test of every smoke and carbon monoxide alarm' + NCHAR(10) + N'Resealing round baths, showers and sinks' + NCHAR(10) + N'Adjusting doors that stick, and cupboard doors that hang crooked' + NCHAR(10) + N'Fixing back loose handrails, handles and rails',
    [NotIncluded] = N'Plumbing, which is General Plumbing Maintenance' + NCHAR(10) + N'The yearly gas safety check, which is for a Gas Safe registered engineer' + NCHAR(10) + N'The electrical safety report a landlord needs at least every five years, which is for a qualified electrician'
WHERE [Slug] = N'property-maintenance';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'property-maintenance');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does this visit cover a landlord''s gas and electrical safety checks?', N'No. The HSE says a landlord''s gas appliances and flues must be checked by a Gas Safe registered engineer at least every 12 months, and the rules for rented homes ask for the electrics to be checked at least every 5 years by a properly qualified person. This visit is the handyman work around those checks.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'property-maintenance';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How is this different from a General Handyman Call-Out?', N'In time and in reach. A call-out is an hour for a few small jobs. Property Maintenance is about three hours for a whole list round the house, with the checks that go with it, such as testing every alarm.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'property-maintenance';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'flooring-installation');

UPDATE [Services]
SET [Description] = N'Supplying and fitting laminate, luxury vinyl tiles (LVT), and engineered wood flooring, complete with underlay, beading, and threshold trims.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'flooring-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'external-brickwork-paving');

UPDATE [Services]
SET [Description] = N'Mortar repointing, garden wall repairs, patio relaying, outdoor step rebuilds, and concrete shed bases built on a manageable domestic scale.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'external-brickwork-paving';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'general-handyman-call-out');

UPDATE [Services]
SET [Description] = N'Not sure which service you need? Book a handyman for an hour and tell us about the job. Good for small mixed jobs and anything not on the list.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'general-handyman-call-out';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'minor-home-repairs');

UPDATE [Services]
SET [Description] = N'Addressing minor plaster cracks, squeaky floors, loose handles, or other general small repairs.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'minor-home-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'property-maintenance');

UPDATE [Services]
SET [Description] = N'Comprehensive handyman checkups and general repairs across multiple areas of the property in a single visit.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'property-maintenance';");

        }
    }
}
