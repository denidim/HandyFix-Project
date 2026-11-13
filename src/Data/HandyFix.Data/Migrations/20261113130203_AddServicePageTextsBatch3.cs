using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The third five service pages' own text (PROJECT_STATE Section 3es): Emergency Plumbing,
            // Shower Installation, Kitchen Plumbing, Washing Machine & Dishwasher Install, Outside
            // Garden Tap Installation. The description, the six blocks and the page's questions, by the
            // service's address, as batches 1 and 2 did (AddServicePageTextsBatch1 and 2). A service the
            // admin has deleted, or renamed (its address follows its name), is not found, and nothing is
            // changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'This is the one job we take at any hour of the day or night: a pipe that has burst, water coming through a ceiling, or a leak that will not stop when the nearest valve is closed. Call or WhatsApp us with what is happening and your postcode, and we aim to have a technician with you within 60 to 90 minutes.' + NCHAR(10) + N'' + NCHAR(10) + N'It is never booked as an online slot, because the next free slot could be tomorrow. A drip that a bowl can catch until a weekday is a Leak Repair, booked online at the ordinary plumbing rate.',
    [Summary] = N'Burst pipes, water through a ceiling and leaks that will not stop, taken by phone or WhatsApp at any hour. We aim to be with you within 60 to 90 minutes.',
    [CommonFindings] = N'[warning] A pipe split by frost. Pipes freeze in a loft, a garage or an outside wall, and Thames Water warns that the leak often shows only when the weather turns milder and the ice thaws.' + NCHAR(10) + N'[water_drop] A burst hose under a sink or behind a machine. A braided tap connector or a washing machine''s fill hose gives way at full mains pressure, and the cupboard or the kitchen floor floods before anyone notices.' + NCHAR(10) + N'[home] A tank or cistern that will not stop filling. The float valve in a loft tank or a toilet cistern sticks open, and what the overflow cannot carry away comes through the ceiling below.' + NCHAR(10) + N'[plumbing] A stop valve that will not close. A stopcock left untouched for years can seize, or turn round and round without shutting anything off.',
    [VisitSteps] = N'We make it safe first: the supply off at the stop valve, or at a valve nearer the leak so the rest of the house is not cut off.' + NCHAR(10) + N'Where water has reached a ceiling, a light or a socket, we check the electricity is off in that part of the house before we touch anything.' + NCHAR(10) + N'We find the burst or the failed fitting, opening boxing or lifting a floorboard where the pipe runs.' + NCHAR(10) + N'We cut out and replace the split length of pipe, remake the joint, or change the hose or valve that failed.' + NCHAR(10) + N'When the full repair needs a part we do not carry, we cap or isolate that pipe so the supply can go back on to the rest of the house, and agree the part and its price with you.' + NCHAR(10) + N'Once the supply is back on, we check the repair at full pressure before we leave.',
    [BeforeWeArrive] = N'[plumbing] Turn the water off at the inside stop valve. It is often under the kitchen sink, but WaterSafe notes it can be in a cupboard, a garage or a cellar. Turn it clockwise.' + NCHAR(10) + N'[bolt] If water has reached the electrics, switch the electricity off at the mains, as Thames Water and WaterSafe both advise. Do not touch a switch, a socket or a fuse box that is wet.' + NCHAR(10) + N'[timer] Turn the heating off, then open the hot and cold taps to drain what is left in the pipes.' + NCHAR(10) + N'[inventory_2] Move what you can out of the way. Soak up the spill with towels, or catch the drips in a bucket.' + NCHAR(10) + N'[emergency] If you smell gas, that is not a plumbing emergency. Call the National Gas Emergency Service on 0800 111 999, open the windows and doors, and do not touch any electrical switch.',
    [Included] = N'Shutting off the supply and stopping the leak' + NCHAR(10) + N'Finding the burst, opening boxing or lifting a floorboard where the pipe runs' + NCHAR(10) + N'Repairing split pipes, failed joints, hoses and valves' + NCHAR(10) + N'Capping or isolating a pipe until a part is fitted, so the rest of the house has water' + NCHAR(10) + N'Checking the repair at full pressure',
    [NotIncluded] = N'A leak in the road or the pavement: Thames Water''s guidance is that the main and the pipe linking it to your supply pipe are the water company''s, and it asks for leaks there to be reported to it' + NCHAR(10) + N'A blocked or overflowing drain outside the house: we clear blockages indoors only' + NCHAR(10) + N'A ceiling brought down by water: that is Plastering & Ceiling Repairs, priced after a survey'
WHERE [Slug] = N'emergency-plumbing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'emergency-plumbing');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Who is responsible for a leak between the street and my house?', N'You are, from the boundary of your property inwards. Thames Water''s guidance is that the main and the communication pipe linking it to your supply pipe are the water company''s, while the supply pipe under your garden or drive is the owner''s, as is every pipe and fitting inside. If you rent, your landlord is responsible for fixing leaks.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'emergency-plumbing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'A pipe has frozen. What should I do?', N'Turn the supply off at the stop valve and open the taps on that pipe, then thaw it slowly with a hot water bottle or warm towels wrapped round it, as Thames Water advises. Before turning the supply back on, run a dry cloth along the pipe: if it comes away wet, leave it off and call or WhatsApp us.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'emergency-plumbing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will my home insurance pay for the repair?', N'That depends on your policy, so check it before anything else is spent, as Thames Water and WaterSafe both advise. Take photos of the damage, and keep the invoice we send for the repair.', 3, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'emergency-plumbing';

UPDATE [Services]
SET [Description] = N'Which shower a home can take depends on how it makes its hot water. A combi boiler gives hot water at mains pressure, which Worcester Bosch says suits a mains-pressure mixer shower. A hot water cylinder fed from a cold tank in the loft gives low pressure, and that is what a pump-assisted power shower is made for.' + NCHAR(10) + N'' + NCHAR(10) + N'We fit mixer, power and electric showers, with the pipework run to the valve, connected and tested. A new circuit for a shower is notifiable work under the Building Regulations, so the wiring for an electric shower or a shower pump is done by a registered electrician we bring in, who certifies it.',
    [Summary] = N'Mixer, power and electric showers fitted, chosen to suit your boiler or cylinder, with the pipework connected and tested, and any wiring done by a registered electrician we bring in.',
    [CommonFindings] = N'[construction] A valve built into the wall, or one on its face. A concealed valve needs the wall opened for its pipes and then retiled. A bar mixer sits on the tiles and takes its pipes through them, so the tiling can stay.' + NCHAR(10) + N'[warning] A handset that can reach the toilet. Water Regs UK treats a shower hose that can reach into a toilet or bidet as the highest risk to drinking water. A clip that holds the hose back, a shorter hose or a fixed head removes the risk.' + NCHAR(10) + N'[water_drop] A shower head that can lie in the bath water. The handset has to stay a set gap above the rim of the bath or the tray, or the supplies need double check valves, so that used water cannot be drawn back into the pipes.' + NCHAR(10) + N'[plumbing] Hot and cold that can cross. Water Regs UK asks for check valves on both supplies to a shower valve, so that under a fault cold water at mains pressure cannot push into the hot, or hot water into the cold mains.',
    [VisitSteps] = N'We look at the boiler or cylinder, and check the shower you have chosen suits it before anything is cut.' + NCHAR(10) + N'With the water off, we run the pipes to where the valve will go, with a check valve and an isolating valve on each supply.' + NCHAR(10) + N'We fix the valve, the rail or the fixed head, and the hose, set so the handset cannot reach the toilet or lie in the water.' + NCHAR(10) + N'Once the supply is back on, we run the shower at full heat and full cold, and check every joint for leaks.',
    [BeforeWeArrive] = N'[add_a_photo] Show us the boiler. A photo of it, of the cylinder and loft tank if you have them, and of the wall where the shower will go, added to your booking, tells us which showers will work.' + NCHAR(10) + N'[inventory_2] If you have bought the shower, keep it in its box with the instructions, so every part can be checked before we start.' + NCHAR(10) + N'[home] Clear the bath or the tray and the floor round it.',
    [Included] = N'Checking the shower suits your boiler or cylinder' + NCHAR(10) + N'Running the pipework to the valve and connecting it' + NCHAR(10) + N'A check valve and an isolating valve on each supply' + NCHAR(10) + N'Fitting the valve, the rail or fixed head, and the hose' + NCHAR(10) + N'A hose clip where the handset could otherwise reach the toilet' + NCHAR(10) + N'The wiring for an electric shower or a pump, by a registered electrician we bring in, who certifies it' + NCHAR(10) + N'Testing every joint for leaks',
    [NotIncluded] = N'Tiling round a new valve built into the wall: that is Wall & Floor Tiling, priced after a survey'
WHERE [Slug] = N'shower-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'shower-installation');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can I have a power shower with a combi boiler?', N'Not as a rule, and you do not need one. A combi already delivers mains pressure, which suits a mains-pressure mixer shower, while Worcester Bosch describes the pump-assisted kind as made for low-pressure systems fed from a tank.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'shower-installation';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will a new shower give me more pressure?', N'Only as much as your system has. A mixer gives the pressure its supplies already have. On a tank-fed system that depends on how far the loft tank sits above the shower head, which is why a power shower adds a pump there.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'shower-installation';

UPDATE [Services]
SET [Description] = N'The plumbing at the kitchen sink: a new or replacement sink with its waste and trap, the hot and cold feeds to the kitchen tap, and a valve on each feed so one part can be shut off while the rest of the house keeps its water.' + NCHAR(10) + N'' + NCHAR(10) + N'A new tap on its own, or a dripping one, is a Tap Repair; a washing machine or dishwasher at the sink is a Washing Machine & Dishwasher Install.',
    [Summary] = N'A kitchen sink fitted or replaced, with its waste, trap, tap feeds and isolating valves, and every joint under it checked dry with the taps running.',
    [CommonFindings] = N'[build] A trap that drips. The nut on a plastic trap works loose, or its washer perishes, and a slow drip collects at the back of the cupboard.' + NCHAR(10) + N'[warning] A swollen cupboard base. A slow leak goes unseen for months, until the chipboard base of the unit swells and comes apart.' + NCHAR(10) + N'[water_drop] A failed seal between sink and worktop. Water from the draining board runs under the rim and into the cupboard, while every pipe is dry.' + NCHAR(10) + N'[plumbing] A waste pipe with too little fall. When the pipe to the outside runs level, or rises anywhere along its length, water and scraps sit in it and the bowl drains slowly.',
    [VisitSteps] = N'First the supply goes off, at the valves under the sink or at the stop valve where there are none.' + NCHAR(10) + N'We disconnect and take out the old sink or fittings, and look at the pipes and the cupboard behind them for signs of old leaks.' + NCHAR(10) + N'We fit the new sink, its waste and trap, and connect the tap feeds with an isolating valve on each.' + NCHAR(10) + N'We seal the rim to the worktop, turn the supply back on, fill and empty the bowl, and check every joint below it is dry.',
    [BeforeWeArrive] = N'[inventory_2] Empty the cupboard under the sink.' + NCHAR(10) + N'[add_a_photo] Photograph the pipes. Add the picture to your booking, with one of the new sink or tap if you have bought them.' + NCHAR(10) + N'[search] If the new sink is a different size or shape, measure the old one and the new one, and say so in your booking.',
    [Included] = N'Fitting a new or replacement kitchen sink, with its waste and trap' + NCHAR(10) + N'Connecting the hot and cold feeds to the kitchen tap' + NCHAR(10) + N'An isolating valve on each feed' + NCHAR(10) + N'Sealing the sink to the worktop' + NCHAR(10) + N'Checking every joint with the taps running',
    [NotIncluded] = N'A blocked drain or gully outside the house: we clear blockages indoors only' + NCHAR(10) + N'New kitchen units or worktops: that is Kitchen Fitting & Alterations, priced after a survey'
WHERE [Slug] = N'kitchen-plumbing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'kitchen-plumbing');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does the kitchen cold tap have to come from the mains?', N'In nearly every home, yes. The Water Fittings Regulations require at least one tap for drinking water, fed from the supply pipe or from a cistern that holds wholesome water, and the kitchen cold tap is the one used for it.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'kitchen-plumbing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Do I need valves under my kitchen sink?', N'Yes, one on each tap feed. The Water Fittings Regulations ask for enough servicing valves to keep the water lost to a minimum when a fitting is repaired or replaced, and with them a tap can be changed while the rest of the house stays on.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'kitchen-plumbing';

UPDATE [Services]
SET [Description] = N'A new washing machine, washer-dryer or dishwasher connected to the water and the waste and set up the way its maker''s instructions say. For a washing machine, Bosch''s own steps are to take out the transit bolts and fit the blanks supplied, connect the fill hose to the cold supply, run the waste to a standpipe or the sink trap with no kinks in either hose, and level the machine on its feet.' + NCHAR(10) + N'' + NCHAR(10) + N'Where there is no valve or waste for it yet, we add them: a valve on the cold pipe, and a waste connection under the sink or a standpipe of its own.',
    [Summary] = N'A washing machine or dishwasher connected to the water and the waste, set up as its maker says, and run to check for leaks. A new valve or waste added where there is none.',
    [CommonFindings] = N'[warning] Transit bolts still in. They lock a washing machine''s drum for delivery, and a machine run with them in shakes hard on its first spin.' + NCHAR(10) + N'[search] A machine that empties while it fills. When the waste hose is pushed too far down the standpipe, or the standpipe is too low, the water siphons out as fast as it goes in. The maker''s instructions give the height the waste should reach.' + NCHAR(10) + N'[water_drop] A hose kinked against the wall. Pushing an appliance back into its space can fold the fill or waste hose behind it, which slows the water or splits the hose in time.' + NCHAR(10) + N'[inventory_2] A dishwasher that cannot be pulled out. Bosch''s installation guide asks for the plug and hoses to reach at least 60 cm, so the dishwasher comes out of its housing for maintenance without being disconnected.',
    [VisitSteps] = N'We turn off the valve and disconnect the old appliance, if there is one, and check the valve and waste it used.' + NCHAR(10) + N'On a new washing machine, we take out the transit bolts and fit the blanks.' + NCHAR(10) + N'We connect the fill hose to the cold valve and the waste to the trap under the sink or to the standpipe, with no kinks in either.' + NCHAR(10) + N'We level a freestanding machine, or fix an integrated dishwasher into its housing with the bracket supplied.' + NCHAR(10) + N'We run it and watch every joint and the waste while it fills and empties.',
    [BeforeWeArrive] = N'[inventory_2] Have the new appliance in the kitchen, and clear the space and the cupboard under the sink.' + NCHAR(10) + N'[bolt] Check there is a socket the plug can reach. Bosch''s instructions plug a machine into an ordinary socket.' + NCHAR(10) + N'[add_a_photo] Send the model number. Add it to your booking, with a photo of the space and of the valves and pipes there now.',
    [Included] = N'Connecting a washing machine, washer-dryer or dishwasher to the water and the waste' + NCHAR(10) + N'Taking out the transit bolts and levelling a freestanding machine' + NCHAR(10) + N'A new valve or waste connection where there is none' + NCHAR(10) + N'Fixing an integrated dishwasher into its housing' + NCHAR(10) + N'A test run with every joint checked',
    [NotIncluded] = N'A new socket for the appliance: that is Minor Electrical Tasks, at the handyman rate' + NCHAR(10) + N'New kitchen units, or cutting a space for the appliance: that is Kitchen Fitting & Alterations, priced after a survey'
WHERE [Slug] = N'washing-machine-dishwasher-install';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'washing-machine-dishwasher-install');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Does a washing machine need a hot water connection?', N'Usually not, but check its instructions. Bosch''s steps connect the fill hose to the cold supply only, and a machine with one inlet heats its own water.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'washing-machine-dishwasher-install';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can a washing machine and a dishwasher share the sink waste?', N'Yes. A sink trap with two connections on its side takes both waste hoses, and each hose is looped up high under the worktop so water from the sink cannot run back into the appliance.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'washing-machine-dishwasher-install';

UPDATE [Services]
SET [Description] = N'We fit a tap to an outside wall and feed it from the cold supply inside the house, often the pipe under the kitchen sink, with the wall drilled through and sealed round the pipe.' + NCHAR(10) + N'' + NCHAR(10) + N'A garden hose can draw water back into the house''s pipes: left in a pond, a water butt or a bucket of weedkiller, it can siphon that water into the drinking supply. Water Regs UK puts a hose used for watering a garden or washing a car in fluid category 3, which a double check valve protects against. We fit it indoors, next to a shut-off valve for winter, because one fitted outdoors can freeze and fail.',
    [Summary] = N'A new tap on the outside wall, fed from the cold supply, with its backflow protection and a shut-off valve fitted indoors, out of the frost.',
    [CommonFindings] = N'[warning] An old tap with no backflow protection. Water Regs UK lists the ways to add it: a double check valve indoors on the tap''s feed, a tap with one built in, or a device screwed onto the outlet.' + NCHAR(10) + N'[water_drop] A tap or pipe split by frost. Water left standing in the tap and in the pipe through the wall freezes and expands, and WaterSafe warns that this can crack and burst them.' + NCHAR(10) + N'[search] A cracked valve inside the tap. Some garden taps carry their check valve in the tap body, where Water Regs UK notes it is prone to freezing and failing.' + NCHAR(10) + N'[plumbing] No way to shut the tap off on its own. Without a valve indoors, draining it for winter means turning off the water to the whole house.',
    [VisitSteps] = N'We find the nearest cold pipe to where the tap will go, and check the wall for cables and pipes before drilling.' + NCHAR(10) + N'We drill through the wall and run the pipe through, sealing round it on both sides.' + NCHAR(10) + N'We fit the shut-off valve and the double check valve indoors, and the tap on a wall plate outside.' + NCHAR(10) + N'With the water back on, we run the tap, check every joint inside and out, and insulate the pipe where it is exposed.',
    [BeforeWeArrive] = N'[home] Choose where on the wall you want the tap. A spot close to the kitchen keeps the pipe inside short.' + NCHAR(10) + N'[inventory_2] Clear the cupboard under the sink, and anything against the wall on both sides.' + NCHAR(10) + N'[add_a_photo] Show us the wall. A photo of the outside wall and one of the pipes under the sink, added to your booking, show us the route the pipe will take.',
    [Included] = N'A new outside tap connected to the cold supply' + NCHAR(10) + N'Drilling the wall and sealing round the pipe' + NCHAR(10) + N'Backflow protection and a shut-off valve, fitted indoors' + NCHAR(10) + N'Insulating the exposed pipe' + NCHAR(10) + N'Adding backflow protection to an existing tap that has none',
    [NotIncluded] = NULL
WHERE [Slug] = N'outside-garden-tap-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'outside-garden-tap-installation');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Should I turn my outside tap off in winter?', N'Yes. WaterSafe''s advice is to disconnect the hose, shut off the outside supply if you can and drain the tap, and fit an insulating cover. The shut-off valve we fit indoors is there for this: close it, then open the tap outside to let the water out.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'outside-garden-tap-installation';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can I fill a pond or a hot tub from an outside tap?', N'Not from one protected only by a double check valve. Water Regs UK classes a hose used to refill a pond, a swimming pool, a spa bath or a hot tub as fluid category 5, the highest risk, unless your water company assesses it otherwise, so ask your water company before you do.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'outside-garden-tap-installation';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'emergency-plumbing');

UPDATE [Services]
SET [Description] = N'Urgent response for burst pipes, flooding, or critical plumbing failures across South London.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'emergency-plumbing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'shower-installation');

UPDATE [Services]
SET [Description] = N'Full assembly and electrical/plumbing integration of power showers and mixers.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'shower-installation';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'kitchen-plumbing');

UPDATE [Services]
SET [Description] = N'Installing kitchen sinks, garbage disposals, and other kitchen plumbing fixtures and fittings.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'kitchen-plumbing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'washing-machine-dishwasher-install');

UPDATE [Services]
SET [Description] = N'Connecting new washing machines or dishwashers to the water supply and waste, including hose and valve checks.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'washing-machine-dishwasher-install';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'outside-garden-tap-installation');

UPDATE [Services]
SET [Description] = N'Fitting an external garden tap with a double-check valve, connected to the internal water supply.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'outside-garden-tap-installation';");

        }
    }
}
