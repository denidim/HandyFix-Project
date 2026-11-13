using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The first five service pages' own text (PROJECT_STATE Section 3eq): Tap Repairs, Toilet
            // Repairs, Leak Repairs, Blocked Drains, Boiler Installation & Servicing. The description,
            // the six blocks and the page's questions, by the service's address. The seeder fills an
            // empty table only, so a database that already has the services gets the text here; the
            // seeder holds the same text for a new database. A service the admin has deleted, or renamed
            // (its address follows its name), is not found, and nothing is changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'A tap that drips, will not turn off fully or has gone stiff nearly always has one worn part inside it, and that part can be changed without changing the tap. Taps with separate hot and cold heads close on a rubber washer; quarter-turn and most mixer taps close on a ceramic cartridge.' + NCHAR(10) + N'' + NCHAR(10) + N'We fit new basin, bath and kitchen taps too, when the old one is past repair or you want a different one.',
    [Summary] = N'A dripping tap, a stiff handle or water round the base, fixed by changing the worn washer, cartridge or O-ring rather than the whole tap. New taps fitted too.',
    [CommonFindings] = N'[water_drop] A worn washer. In taps with separate hot and cold heads, the spout keeps dripping however hard the head is turned.' + NCHAR(10) + N'[construction] A worn ceramic cartridge. In quarter-turn and mixer taps, the handle turns but the water does not stop cleanly.' + NCHAR(10) + N'[refresh] A perished O-ring. Water gathers round the base of the spout while the tap runs, or creeps out under the head.' + NCHAR(10) + N'[info] A scaled or worn valve seat. A new washer will not seal on it, so the seat is cleaned or re-cut first.' + NCHAR(10) + N'[home_repair_service] A tap that turns on the basin. The back nut underneath has worked loose, and every turn pulls on the pipe joints below.',
    [VisitSteps] = N'We shut the water off at the isolating valve under the basin or sink, or at the stop valve if there is none.' + NCHAR(10) + N'We take the tap apart and show you the worn part.' + NCHAR(10) + N'We fit the new washer, cartridge or O-ring, or tell you plainly if the tap is past repair.' + NCHAR(10) + N'We turn the water back on and check that it closes fully and stays dry underneath.',
    [BeforeWeArrive] = N'[plumbing] Find your inside stop valve and check that it turns. Thames Water says it is usually under the kitchen sink, and that it closes clockwise.' + NCHAR(10) + N'[inventory_2] Clear the cupboard under the basin or sink, so the pipes can be reached.' + NCHAR(10) + N'[add_a_photo] Add a photo of the tap to your booking, with the maker''s name if one is printed on it: cartridges differ from one make to the next.',
    [Included] = N'Finding which part has worn, and why' + NCHAR(10) + N'New washers, O-rings and ceramic cartridges' + NCHAR(10) + N'Cleaning or re-cutting a scaled valve seat' + NCHAR(10) + N'Tightening a loose tap from underneath' + NCHAR(10) + N'Fitting a new tap, agreed and priced before it goes in',
    [NotIncluded] = N'A tap that will not turn off at all and cannot be isolated: call or WhatsApp us, as it cannot wait for a booked slot'
WHERE [Slug] = N'tap-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'tap-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Is it worth repairing a tap, or should I replace it?', N'Usually repair. A washer, an O-ring or a cartridge is a small part, and the body of a tap rarely wears out. A new tap makes more sense when the body is cracked, the finish is coming away, or no cartridge is made for that model any more.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'tap-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How much water does a dripping tap waste?', N'Over 12,000 litres a year if it drips once a second, by Waterwise''s figure. On a water meter you pay for every litre of it.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'tap-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will you have the right part for my tap?', N'A photo of the tap with your booking is the surest way. Ceramic cartridges are made to fit one make and range, so we match it from the photo. A part we do not carry is priced and agreed with you before it is fitted.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'tap-repairs';

UPDATE [Services]
SET [Description] = N'Running, slow to fill, a weak flush, water on the floor: most toilet faults come down to one part, either inside the cistern or behind the pan. It is the flush valve, the fill valve, the seal between cistern and pan, or the pan connector. We find which one has failed and change it, with the toilet left in place.' + NCHAR(10) + N'' + NCHAR(10) + N'A toilet that trickles into the pan all day is easy to miss and costly on a water meter: Waterwise puts the loss from a leaking toilet at 200 to 400 litres a day.',
    [Summary] = N'A running, slow-filling or leaking toilet, fixed by finding the one failed part (flush valve, fill valve, seal or pan connector) and changing it.',
    [CommonFindings] = N'[water_drop] A flush valve that no longer seals. Water trickles into the pan long after the flush. WaterSafe finds this most on dual-flush toilets with a push button.' + NCHAR(10) + N'[refresh] A fill valve that does not shut off. The cistern keeps filling and the water runs away through the overflow.' + NCHAR(10) + N'[build] A worn seal between the cistern and the pan. On a close-coupled toilet, water collects on the floor behind the pan after each flush.' + NCHAR(10) + N'[warning] A cracked or loose pan connector. Water, or a smell, where the back of the pan joins the soil pipe.' + NCHAR(10) + N'[checklist] A broken button or lever link. The flush works only now and then, or the button stays down.',
    [VisitSteps] = N'We close the valve on the pipe that feeds the cistern and flush it empty.' + NCHAR(10) + N'We find the failed part and show it to you.' + NCHAR(10) + N'We fit the new valve, seal or connector, and set the water level in the cistern to its mark.' + NCHAR(10) + N'We flush several times and check that the floor behind the pan stays dry.',
    [BeforeWeArrive] = N'[search] Not sure it leaks? WaterSafe''s test: 30 minutes after a flush, wipe the back of the pan dry and lay a dry sheet of toilet paper there for a few hours. A wet or torn sheet means water is getting past the flush valve.' + NCHAR(10) + N'[add_a_photo] Lift the cistern lid and add a photo of the inside, and of the flush button, to your booking.' + NCHAR(10) + N'[home] Clear the floor round the toilet and anything kept on the cistern lid.',
    [Included] = N'Flush valves, fill valves and their seals' + NCHAR(10) + N'The seal and bolts of a close-coupled toilet' + NCHAR(10) + N'Pan connectors' + NCHAR(10) + N'Buttons, levers and the links inside the cistern',
    [NotIncluded] = N'A blocked toilet: book Blocked Drains, which covers toilets'
WHERE [Slug] = N'toilet-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'toilet-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Why does my toilet keep running?', N'Either the flush valve no longer seals, so water trickles into the pan, or the fill valve does not shut, so the cistern fills into its overflow. WaterSafe names these two as the usual causes. Both sit inside the cistern.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'toilet-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Is water on the floor behind the toilet always a leak?', N'Not always. A cold cistern sweats in winter and the drops run down the back of the pan. Dry everything, flush, and watch where water appears first: the joint under the cistern, the connector at the back, or the supply pipe. That tells us which part to bring.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'toilet-repairs';

UPDATE [Services]
SET [Description] = N'A leak we can see or reach: a drip from a joint under a sink, a valve weeping at a radiator, a pipe behind a bath panel, a stain spreading on the ceiling under a bathroom. We find where the water starts, stop it, and leave the repair dry under full pressure.',
    [Summary] = N'Dripping joints, weeping valves and leaking pipes we can see or reach, under sinks, behind bath panels and at radiators, found, fixed and left dry under pressure.',
    [CommonFindings] = N'[build] A loose or worn compression joint. A drop forms on the nut of a joint under a sink or at a valve, because the nut has worked loose or the brass ring inside has worn.' + NCHAR(10) + N'[water_drop] A weeping valve. Isolating valves, radiator valves and stop valves leak round the spindle when they are turned after years untouched.' + NCHAR(10) + N'[warning] A failed flexible hose. The braided hoses that feed many taps split, or rust at their ends.' + NCHAR(10) + N'[home] A gap in the seal round a bath or shower. Water runs behind the bath and stains the ceiling below, while every pipe is dry.' + NCHAR(10) + N'[search] A pinhole in old copper pipe. A fine jet or a steady drip from the middle of a pipe, with no joint near it.',
    [VisitSteps] = N'We turn the water off at the nearest valve and dry everything, so a fresh drop shows where the leak starts.' + NCHAR(10) + N'We trace the water back to where it comes from, taking off a bath panel or kickboard where it leads.' + NCHAR(10) + N'We remake the joint, change the valve or hose, or cut out the bad length of pipe.' + NCHAR(10) + N'We turn the water back on, check the repair under pressure, and check it again before we leave.',
    [BeforeWeArrive] = N'[plumbing] More than a drip? Turn the water off at the inside stop valve, which Thames Water says is usually under the kitchen sink and closes clockwise. Most homes also have valves that shut off one room.' + NCHAR(10) + N'[emergency] Water coming through a ceiling, or near lights and sockets, cannot wait for a slot: keep away from wet electrics, and call or WhatsApp us.' + NCHAR(10) + N'[add_a_photo] Add photos of the leak, and of any stain, to your booking.',
    [Included] = N'Tracing a leak we can see or reach, opening a bath panel or kickboard where needed' + NCHAR(10) + N'Remaking joints, and replacing valves, hoses and short lengths of pipe' + NCHAR(10) + N'Checking the repair under full pressure',
    [NotIncluded] = N'A leak hidden inside a wall or under a floor, that shows nowhere we can reach'
WHERE [Slug] = N'leak-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'leak-repairs');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How can I tell if there is a leak I cannot see?', N'Thames Water lists the signs: less water from the cold kitchen tap than usual, damp patches in the garden in dry weather, pipes that are noisy when nothing is running, and a metered bill higher than it should be.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'leak-repairs';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can you find a leak inside a wall or under a floor?', N'Only when it shows itself where we can reach it: a joint behind a panel, or a damp patch we can follow back. We find leaks by eye and by testing, so one that cannot be traced that way needs a specialist to find it first.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'leak-repairs';

UPDATE [Services]
SET [Description] = N'We clear blockages inside the house: sinks, basins, baths, showers and toilets that drain slowly or not at all. We take the trap off, clear it and the waste pipe behind it, and run water through to show it flows again.' + NCHAR(10) + N'' + NCHAR(10) + N'We do not clear outside drains, gullies or manholes.',
    [Summary] = N'Slow and blocked sinks, basins, baths, showers and toilets inside the house: the trap and waste pipe cleared and water run through. Outside drains are not included.',
    [CommonFindings] = N'[water_drop] Fat in a kitchen waste. Cooking fat sets in the trap and the pipe behind it, and hot water does not shift it.' + NCHAR(10) + N'[build] Hair and soap in a bath or shower trap. The water slows a little more each week until it stands in the tray.' + NCHAR(10) + N'[warning] Wipes in a toilet. Thames Water''s advice is that wet wipes block pipes, even the ones labelled flushable.' + NCHAR(10) + N'[search] A waste pipe that runs too flat. Water sits in the pipe, and the same blockage comes back within weeks.' + NCHAR(10) + N'[home] A blockage outside the house. When several fittings back up together, or water rises in a downstairs toilet as the bath empties, the blockage is usually in the drain outside.',
    [VisitSteps] = N'We cover the floor and the cupboard, then take off the trap and clear it.' + NCHAR(10) + N'We clear the waste pipe beyond it with rods or a plunger, as the fitting needs.' + NCHAR(10) + N'We refit the trap, with new washers where the old ones are worn.' + NCHAR(10) + N'We run water through each fitting we cleared and check underneath for drips.',
    [BeforeWeArrive] = N'[warning] Do not pour drain cleaner in before we come: it stays in the standing water and makes taking the trap off unsafe.' + NCHAR(10) + N'[info] Check the other sinks, baths and toilets. If several are slow at once, tell us in your booking, as the cause may be outside.' + NCHAR(10) + N'[inventory_2] Empty the cupboard under the sink and leave the bath panel clear.',
    [Included] = N'Sinks, basins, baths, showers and toilets inside the house' + NCHAR(10) + N'Traps and waste pipes, as far as where they leave the house' + NCHAR(10) + N'New trap washers where the old ones are worn',
    [NotIncluded] = N'Outside drains, gullies and manholes' + NCHAR(10) + N'Shared drains and sewers, which the water company looks after'
WHERE [Slug] = N'blocked-drains';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'blocked-drains');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Who clears a blocked drain outside my house?', N'It depends on the pipe. A drain that serves only your home, inside your boundary, is yours to have cleared. A pipe shared with neighbours, or past your boundary, has belonged to the sewerage company since 1 October 2011, and it clears those. Thames Water''s website explains which pipes moved to it.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'blocked-drains';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will hot water and washing-up liquid clear a greasy sink?', N'No. Thames Water says hot water and soap do not dissolve fats and oils, and the soap can harden in the pipe. Let fat set in a jar or on foil and put it in the bin.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'blocked-drains';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What can go down a toilet?', N'Pee, poo and toilet paper, and nothing else, as Thames Water puts it. Wipes, cotton wool, sanitary products, nappies, dental floss and hair all go in the bin.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'blocked-drains';

UPDATE [Services]
SET [Description] = N'We fit new boilers and service existing ones. All boiler work is carried out by a Gas Safe registered engineer, as UK law requires for any work on gas appliances.' + NCHAR(10) + N'' + NCHAR(10) + N'A new boiler starts with a survey. We look at how much heating and hot water your home needs, check the existing pipework and flue, and help you choose the right type of boiler: combi, system or conventional. You get a clear quote with the price and timings before any work starts.' + NCHAR(10) + N'' + NCHAR(10) + N'A like-for-like swap usually takes a day. Changing a regular boiler to a combi, or moving the boiler to a new place, takes 1.5 to 4 days.',
    [Summary] = N'New gas boilers fitted and existing ones serviced, all by our Gas Safe registered engineer, with a survey and a clear quote before any work starts.',
    [CommonFindings] = N'[warning] Low pressure. Worcester Bosch puts the right reading at 1 to 1.5 bar with the system cold; below 1 bar a boiler may stop. Topping up more than once or twice a year points to a leak or a fault.' + NCHAR(10) + N'[water_drop] A frozen condensate pipe. In a hard frost the plastic pipe that drains a condensing boiler outside can freeze. The boiler gurgles, stops and shows a fault code.' + NCHAR(10) + N'[refresh] Sludge in the system. Radiators stay cold at the bottom and the boiler grows noisy. A new boiler goes in after the system is flushed, with a magnetic filter to catch what is left.',
    [VisitSteps] = N'On installation day we turn off the water, drain the system and take out the old boiler, with any tanks or cylinders you no longer need.' + NCHAR(10) + N'We mount the new boiler level and secure, and fit and seal the flue through the wall.' + NCHAR(10) + N'We flush the system, fit a magnetic filter, and connect the flow, return, gas and condensate pipes.' + NCHAR(10) + N'We test everything, carry out the gas safety checks and set up your controls, smart thermostats included.' + NCHAR(10) + N'We hand over the compliance and warranty certificates, and notify the installation as Building Regulations require.',
    [BeforeWeArrive] = N'[health_and_safety] If you smell gas, do not wait for a visit. Open doors and windows, turn the gas off at the meter, and call the National Gas Emergency number, 0800 111 999.' + NCHAR(10) + N'[info] Have the boiler''s make and model to hand, and any fault code it shows.' + NCHAR(10) + N'[home] Clear the way to the boiler, the gas meter and the stop valve.',
    [Included] = N'A survey and a clear quote before any work starts' + NCHAR(10) + N'Taking out the old boiler, and any tanks or cylinders you no longer need' + NCHAR(10) + N'Flushing the system and fitting a magnetic filter' + NCHAR(10) + N'Gas safety checks, your controls set up, and the compliance and warranty certificates' + NCHAR(10) + N'A yearly service, with a written record of what was checked',
    [NotIncluded] = NULL
WHERE [Slug] = N'boiler-installation-servicing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'boiler-installation-servicing');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How often should a boiler be serviced?', N'Once a year. Gas Safe Register advises a yearly service to keep gas appliances safe and working well, and most manufacturers need one to keep the warranty valid. We check that the boiler burns gas safely, test the flue and the pressure, clean the key parts and give you a record of the service.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'boiler-installation-servicing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How do I check that an engineer is Gas Safe registered?', N'Ask for the engineer''s ID card and read both sides. The front has their photo and licence number; the back lists the work they are qualified for, which for a boiler should include gas boilers. The licence number can be checked on the Register''s website.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'boiler-installation-servicing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What paperwork comes with a new boiler?', N'The warranty papers, and a Building Regulations Compliance Certificate. The installation has to be notified to the local authority within 30 days, which we do through Gas Safe Register. The Register then posts the certificate to you, and you can check online that it was done.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'boiler-installation-servicing';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'tap-repairs');

UPDATE [Services]
SET [Description] = N'Fixing dripping taps, replacing seals, cartridges, or installing brand new basin mixers.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'tap-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'toilet-repairs');

UPDATE [Services]
SET [Description] = N'Repairing running toilets, faulty flush valves, fill mechanisms, or toilet leaks.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'toilet-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'leak-repairs');

UPDATE [Services]
SET [Description] = N'Fixing leaking pipes, joints and fittings under sinks and around baths, toilets and radiators.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'leak-repairs';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'blocked-drains');

UPDATE [Services]
SET [Description] = N'Clearing blocked sinks, toilets, baths and showers inside the home. We do not clear outside drains.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'blocked-drains';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'boiler-installation-servicing');

UPDATE [Services]
SET [Description] = N'We fit new boilers and service existing ones. All boiler work is carried out by a Gas Safe registered engineer, as UK law requires for any work on gas appliances.' + NCHAR(10) + N'' + NCHAR(10) + N'A new boiler starts with a survey. We look at how much heating and hot water your home needs, check the existing pipework and flue, and help you choose the right type of boiler: combi, system or conventional.' + NCHAR(10) + N'' + NCHAR(10) + N'On installation day we turn off the water, drain the system and remove the old boiler, along with any tanks or cylinders you no longer need. The new boiler is mounted level and secure, and the flue is fitted and sealed through the wall. We flush the system to clear out sludge, fit a magnetic filter, and connect the flow, return, gas and condensate pipes. Finally we test everything, carry out the gas safety checks, set up your controls, including smart thermostats, and hand over your compliance and warranty certificates.' + NCHAR(10) + N'' + NCHAR(10) + N'A like-for-like swap usually takes a day. Changing a regular boiler to a combi, or moving the boiler to a new place, takes 1.5 to 4 days. You get a clear quote with the price and timings before any work starts.' + NCHAR(10) + N'' + NCHAR(10) + N'A yearly service keeps your boiler safe and running efficiently, and most manufacturers need it to keep the warranty valid. We check that the boiler burns gas safely, test the flue and pressure, clean the key parts and give you a record of the service.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'boiler-installation-servicing';");

        }
    }
}
