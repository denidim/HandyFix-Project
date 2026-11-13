using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePageTextsBatch5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The fifth five service pages' own text (PROJECT_STATE Section 3ev): Silicone & Mastic
            // Resealing, Wall Mounting, Lock & Handle Replacement, Bath & Shower Screen Fitting, Minor
            // Electrical Tasks. The description, the six blocks and the page's questions, by the
            // service's address, as batches 1 to 4 did (AddServicePageTextsBatch1 to 4). A service the
            // admin has deleted, or renamed (its address follows its name), is not found, and nothing
            // is changed for it.
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'When the sealant round a bath, shower tray, basin or kitchen sink has split, lifted or gone black, we cut it out, clean the joint back to a bare, dry surface, and run a new bead of sanitary silicone.' + NCHAR(10) + N'' + NCHAR(10) + N'A failed seal lets water run down behind the bath or tray each time it is used. It shows later as a stain on the ceiling below, so the seal is worth redoing while it is only a black line.',
    [Summary] = N'Split, lifted or mouldy sealant cut out round baths, showers, basins and sinks, and a new bead of sanitary silicone run on a clean, dry joint.',
    [CommonFindings] = N'[water_drop] Black mould inside the bead. Once mould has grown into the silicone, a cleaner only reaches the surface. UniBond''s own advice is that sealant in that state has to come out and be replaced.' + NCHAR(10) + N'[warning] A seal that splits along the length of the bath. A bath drops a little under the weight of water and a person, so a bead run on an empty bath is stretched every time it is filled. We fill the bath before sealing and leave it full while the bead cures.' + NCHAR(10) + N'[build] A bath or tray that moves when you stand in it. If its feet or frame are loose, the bead tears again within weeks. We check them and tighten or adjust them first.' + NCHAR(10) + N'[refresh] New sealant over old. Fresh silicone does not stick to old silicone or to the film it leaves behind, so the new bead peels away at the edges. Every trace of the old one is cut and cleaned off.',
    [VisitSteps] = N'We cut out the old sealant, clean off what is left of it, and let the joint dry.' + NCHAR(10) + N'We fill the bath with water, so it sits as it will when in use.' + NCHAR(10) + N'We mask the edges of a wide joint and run one continuous bead, smoothed into the corner.' + NCHAR(10) + N'We leave the bath full and tell you how long the sealant needs before the bath or shower is used.',
    [BeforeWeArrive] = N'[inventory_2] Clear bottles and soap from the bath edge, the shelves and round the sink.' + NCHAR(10) + N'[timer] If you can, leave the shower unused for a few hours before we come, so the joint is dry when we seal it.' + NCHAR(10) + N'[warning] If there is a stain on the ceiling under the bathroom, say so in your booking. The leak may be the seal, or it may be the waste or a pipe under the bath.',
    [Included] = N'Cutting out the old sealant and cleaning the joint' + NCHAR(10) + N'Baths, shower trays, basins and kitchen sinks' + NCHAR(10) + N'Tightening or adjusting the feet of a bath that moves' + NCHAR(10) + N'Filling the bath before it is sealed' + NCHAR(10) + N'Sanitary silicone with a mould inhibitor',
    [NotIncluded] = N'Regrouting or replacing tiles: that is Wall & Floor Tiling' + NCHAR(10) + N'A leak from the waste or a pipe under the bath: that is Leak Repairs'
WHERE [Slug] = N'silicone-mastic-resealing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'silicone-mastic-resealing');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How long before I can use the bath or shower?', N'Usually about a day. Sanitary silicone cures from the outside in, and the time on the tube is the one to follow. Bostik''s data sheet, for example, gives about 24 hours for each 2mm of depth, so a deep joint takes longer.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'silicone-mastic-resealing';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Why does new silicone go black again?', N'Because mould grows where water stays. Silicone with a mould inhibitor holds it off for longer, but a bathroom that is aired after every bath or shower, with the bead wiped dry, keeps it clean longest.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'silicone-mastic-resealing';

UPDATE [Services]
SET [Description] = N'Pictures, heavy mirrors, whiteboards, noticeboards and gallery walls go up on fixings chosen for the wall and for the weight: plugs and screws into brick or block, and on plasterboard either the timber studs behind it or cavity fixings that open out behind the board.' + NCHAR(10) + N'' + NCHAR(10) + N'For a gallery wall we lay the frames out with you before a single hole is drilled.',
    [Summary] = N'Pictures, heavy mirrors, whiteboards and gallery walls hung level, on fixings chosen for the wall and for what each piece weighs.',
    [CommonFindings] = N'[warning] A mirror that is heavier than it looks. Glass 4mm thick weighs about 10kg a square metre before the frame is counted, so a large mirror goes on two fixings rated for it, or on a split batten, not on one hook.' + NCHAR(10) + N'[build] Plasterboard with no stud where the piece has to go. A cavity fixing such as a spring toggle opens out behind the board. Rawlplug''s figures for its spring toggle in 12.5mm board come to roughly 12 to 16kg a fixing, so a heavy piece is spread over several of them.' + NCHAR(10) + N'[info] One wire across the back of a heavy frame. The wire and its two screw eyes then carry all the weight, and the frame leans forward off the wall. Two D-rings on two fixings hold it flat and keep it level.',
    [VisitSteps] = N'We find out what the wall is made of and scan each spot for pipes and cables before drilling.' + NCHAR(10) + N'For a gallery wall, we set the frames out on the floor, then tape paper outlines of them to the wall so you can see the layout first.' + NCHAR(10) + N'We mark, drill and fix, and hang each piece level.' + NCHAR(10) + N'We check every piece is firm on its fixings before we leave.',
    [BeforeWeArrive] = N'[checklist] Say in your booking what is going up, how many pieces, and the weight of anything heavy. A mirror''s weight is usually on its box or its listing.' + NCHAR(10) + N'[key] If you rent, check your tenancy agreement, or ask your landlord, before holes go in the walls.' + NCHAR(10) + N'[home] Clear the wall and the floor in front of it.',
    [Included] = N'Pictures and frames, one at a time or as a gallery wall' + NCHAR(10) + N'Mirrors, heavy ones included' + NCHAR(10) + N'Whiteboards and noticeboards' + NCHAR(10) + N'Fixings chosen for the wall and the weight',
    [NotIncluded] = N'TVs: that is TV Mounting' + NCHAR(10) + N'Shelves: that is Shelf Installation' + NCHAR(10) + N'A mirror with a light or a demister pad that has to be wired in'
WHERE [Slug] = N'wall-mounting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'wall-mounting');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How heavy a mirror can go on plasterboard?', N'It depends on the fixings, not the board alone. Each cavity fixing has a load its maker gives, so a heavy mirror is spread over several of them, or fixed into the timber studs. Tell us its weight when you book.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'wall-mounting';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Can pictures go up without drilling holes?', N'Yes, if the room has a picture rail. Pictures hang from it on hooks and cord, once we have checked the rail itself is firmly fixed. Mirrors and anything heavy are fixed into the wall.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'wall-mounting';

UPDATE [Services]
SET [Description] = N'We replace door locks, latches and handles that are worn, broken or no longer to be trusted: euro cylinders in uPVC and composite doors, mortice locks and night latches in timber doors, and handles inside and out.' + NCHAR(10) + N'' + NCHAR(10) + N'When tenants change or a key goes missing, a new cylinder or lock is the quickest way to make every old key useless.',
    [Summary] = N'Euro cylinders, mortice locks, night latches and handles replaced, anti-snap cylinders and BS 3621 deadlocks included, so old keys no longer open the door.',
    [CommonFindings] = N'[lock] A euro cylinder that can be snapped. An ordinary cylinder in a uPVC or composite door can be broken at its weak middle. Police guidance names a TS 007 3-star cylinder, or one approved to Sold Secure Diamond (SS312), both tested against snapping.' + NCHAR(10) + N'[key] A timber door with only a night latch. For a solid timber door, police guidance asks for a five-lever mortice deadlock to BS 3621:2017. A night latch alone can be slipped or forced.' + NCHAR(10) + N'[warning] A uPVC handle that is stiff to lift, or a key that will not turn. This is often the door that has dropped, so the locking points bind on their keeps. We check how the door sits before blaming the lock, since a new one would bind the same way.' + NCHAR(10) + N'[refresh] A handle that droops. The spring inside the handle has worn, and the handle is replaced with one that matches the spindle and the screw holes already in the door.',
    [VisitSteps] = N'We look at the door and the lock it has, and measure it: a euro cylinder from the fixing screw on the edge of the door to each end.' + NCHAR(10) + N'We check the door closes and the bolts meet their keeps without force.' + NCHAR(10) + N'We fit the new lock or handle, using the cut-outs already in the door where the new one matches them.' + NCHAR(10) + N'We test every key from both sides, with the door open and then closed.',
    [BeforeWeArrive] = N'[info] Read what your home insurance policy says about locks. It may name the standard it needs.' + NCHAR(10) + N'[add_a_photo] Add a photo of the lock and of the edge of the door to your booking. The plate on the edge often shows the maker and the standard.' + NCHAR(10) + N'[key] Count the keys you have, and tell us if any are missing.',
    [Included] = N'Euro cylinders in uPVC and composite doors, anti-snap ones included' + NCHAR(10) + N'Mortice locks and night latches in timber doors' + NCHAR(10) + N'Door handles and latches, inside and out' + NCHAR(10) + N'A new set of keys with every new lock or cylinder',
    [NotIncluded] = N'Getting in when you are locked out: that is for a locksmith' + NCHAR(10) + N'Locks on a fire door, which in a block of flats includes the front door of each flat'
WHERE [Slug] = N'lock-handle-replacement';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'lock-handle-replacement');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What lock does my home insurance need?', N'The one your policy names, so check its wording first. BS 3621 is the British Standard for a lock such as a mortice deadlock on a timber door, and police guidance asks for one to BS 3621:2017.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'lock-handle-replacement';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'How can I tell if my cylinder is anti-snap?', N'Look at its face, beside the keyhole. A TS 007 cylinder carries the Kitemark and its stars. Three stars means the cylinder is rated on its own, without special handles. A Sold Secure Diamond one carries Sold Secure''s mark. A cylinder with no mark at all is not one you can count on.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'lock-handle-replacement';

UPDATE [Services]
SET [Description] = N'You buy the glass bath screen or shower screen, and we fit it: the wall profile drilled through the tiles and fixed into the wall behind, the glass hung and set upright, and the joints sealed on the side its maker names.' + NCHAR(10) + N'' + NCHAR(10) + N'If an old screen is in the way, we take it down and cut its sealant out first.',
    [Summary] = N'A glass bath or shower screen fixed through the tiles into the wall, hung upright, and sealed to its maker''s instructions.',
    [CommonFindings] = N'[water_drop] A screen sealed on the wrong side. Makers do not agree. Several UK makers say to seal the wall profile on the outside only, so water that gets behind it runs back into the bath; others ask for the inside, or both. We follow the screen''s own instructions, since one maker warns that sealing against them ends the warranty.' + NCHAR(10) + N'[construction] Tiles that crack when drilled. Glazed and porcelain tiles are drilled with a tile or diamond bit and the hammer action off, slowly, so the glaze does not chip round the hole.' + NCHAR(10) + N'[search] A bath that is not level. Screen instructions ask for the bath to be level before the profile is sealed, or water collects at one end. We check it with a level first, and adjust a bath on feet.' + NCHAR(10) + N'[warning] A chip on the edge of the glass. Toughened glass is strong across its face, but damage to an edge weakens the whole panel, so a chipped one is not fitted.',
    [VisitSteps] = N'We take down the old screen, if there is one, and cut out its sealant.' + NCHAR(10) + N'We check the bath or tray is level, then hold the wall profile upright and mark its holes.' + NCHAR(10) + N'We drill the tiles, fix the profile, hang the glass and set it upright.' + NCHAR(10) + N'We seal the joints the instructions name and tell you how long to leave it before the bath or shower is used.',
    [BeforeWeArrive] = N'[inventory_2] Unpack the screen when it arrives and check the glass edges and the parts against the list. Suppliers usually want damage reported before anything is fitted.' + NCHAR(10) + N'[checklist] Say in your booking which screen it is, with the maker and the model, or add a photo of the box label.' + NCHAR(10) + N'[home] Clear the bath or tray, its edge and the floor beside it.',
    [Included] = N'Taking down an old screen and its sealant' + NCHAR(10) + N'Fixing the wall profile through the tiles' + NCHAR(10) + N'Hanging and setting fixed and hinged screens' + NCHAR(10) + N'Sealing to the screen''s instructions',
    [NotIncluded] = N'Retiling round the bath or shower: that is Wall & Floor Tiling' + NCHAR(10) + N'Supplying the screen'
WHERE [Slug] = N'bath-shower-screen-fitting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'bath-shower-screen-fitting');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Which side of a bath screen should be sealed?', N'The side its maker''s instructions say. Several UK makers say outside only, so water that gets behind the profile drains back into the bath, but others ask for the inside or both. Sealing against the instructions can void the screen''s warranty.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'bath-shower-screen-fitting';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Will any screen fit my bath?', N'No. A straight screen needs a straight bath edge. A shaped shower bath, wider at the shower end, takes the screen made for that bath and for its hand, left or right.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'bath-shower-screen-fitting';

UPDATE [Services]
SET [Description] = N'This is the small electrical work a handyman visit covers: replacing light switches, dimmers, sockets and light fittings on the wiring you already have, and fitting smoke alarms.' + NCHAR(10) + N'' + NCHAR(10) + N'Swaps like these, on existing wiring, do not have to be notified under Part P of the Building Regulations in England. Work that does is listed below, and it is done by a registered electrician we bring in.',
    [Summary] = N'Switches, dimmers, sockets and light fittings replaced on your existing wiring, and smoke alarms fitted, with the circuit tested dead before work starts.',
    [CommonFindings] = N'[bolt] A metal light fitting and no earth wire at the ceiling. Older lighting circuits often have no earth. A metal fitting needs one, unless it is double insulated: Class II, marked with a square inside a square. Where there is no earth, only a Class II fitting goes up.' + NCHAR(10) + N'[info] LED bulbs that flicker on a dimmer. Many older dimmers were made for filament bulbs and have a minimum load the LEDs do not reach. A dimmer made for LEDs, with bulbs marked dimmable, stops it.' + NCHAR(10) + N'[warning] A socket or switch with brown scorch marks, or one that crackles or smells hot. That is heat at the terminals. It is switched off and replaced, and if the cable behind it is damaged too, that is a job for an electrician.' + NCHAR(10) + N'[timer] A smoke alarm past its date. Surrey Fire and Rescue Service says to replace a smoke alarm after 10 years, and to test it once a week. The date is usually on the back or the side.',
    [VisitSteps] = N'We switch the circuit off at the consumer unit and test that it is dead before anything is touched.' + NCHAR(10) + N'We note how the old one is wired, then connect the new one to the same cables, with the earth where there is one.' + NCHAR(10) + N'We check every terminal is tight and the faceplate or fitting sits flat.' + NCHAR(10) + N'We switch back on and test it: the light, the socket with a socket tester, the alarm with its test button.',
    [BeforeWeArrive] = N'[warning] If a socket or switch is hot, scorched or crackling, switch that circuit off at the consumer unit and do not use it until it is replaced.' + NCHAR(10) + N'[home] Know where your consumer unit (fuse box) is, and keep the way to it clear.' + NCHAR(10) + N'[inventory_2] If you have bought the new fitting, keep it in its box with its instructions, and say in your booking if it is heavy.',
    [Included] = N'Replacing light switches and dimmer switches' + NCHAR(10) + N'Replacing sockets, USB sockets included' + NCHAR(10) + N'Replacing ceiling and wall light fittings' + NCHAR(10) + N'Fitting smoke alarms, and battery carbon monoxide alarms' + NCHAR(10) + N'Testing each one after it is fitted',
    [NotIncluded] = N'New circuits, a new consumer unit, and new wiring or additions in a bathroom: these are notified under Part P, and done by a registered electrician we bring in' + NCHAR(10) + N'Finding a fault that keeps tripping a circuit' + NCHAR(10) + N'A check of the whole installation (an Electrical Installation Condition Report)'
WHERE [Slug] = N'minor-electrical-tasks';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'minor-electrical-tasks');

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'Do I need an electrician to change a light fitting?', N'Not by law in England, if the new fitting goes on the existing wiring. Part P does not ask for that to be notified. It still has to be done safely, which is why the circuit is tested dead before it is touched, and tested again when it is back on.', 1, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'minor-electrical-tasks';

INSERT INTO [ServiceFaqs] ([Id], [Question], [Answer], [DisplayOrder], [ServiceId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'What alarms does a landlord have to fit?', N'In England, a smoke alarm on every storey with a room lived in, and a carbon monoxide alarm in any room with a fixed appliance that burns fuel, such as a boiler or a wood burner, but not a gas cooker. The rules have applied since 1 October 2022, and a faulty alarm has to be repaired or replaced once the tenant reports it.', 2, [Id], GETUTCDATE(), 0 FROM [Services] WHERE [Slug] = N'minor-electrical-tasks';");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'silicone-mastic-resealing');

UPDATE [Services]
SET [Description] = N'Removing old, mouldy silicone and resealing around baths, showers, and sinks to stop leaks and damp.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'silicone-mastic-resealing';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'wall-mounting');

UPDATE [Services]
SET [Description] = N'Mounting pictures, heavy mirrors, whiteboards, or gallery walls with correct anchors.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'wall-mounting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'lock-handle-replacement');

UPDATE [Services]
SET [Description] = N'Replacing worn or broken door locks, handles, and latches for security or a tenancy changeover.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'lock-handle-replacement';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'bath-shower-screen-fitting');

UPDATE [Services]
SET [Description] = N'Fitting a glass bath or shower screen, including tile drilling and sealing for a watertight finish.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'bath-shower-screen-fitting';

DELETE FROM [ServiceFaqs] WHERE [ServiceId] IN (SELECT [Id] FROM [Services] WHERE [Slug] = N'minor-electrical-tasks');

UPDATE [Services]
SET [Description] = N'Replacing light switches, power outlets, light fixtures, or fitting new smoke alarms.',
    [Summary] = NULL, [CommonFindings] = NULL, [VisitSteps] = NULL, [BeforeWeArrive] = NULL, [Included] = NULL, [NotIncluded] = NULL
WHERE [Slug] = N'minor-electrical-tasks';");

        }
    }
}
