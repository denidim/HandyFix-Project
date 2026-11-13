namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;

    internal class ServicesSeeder : ISeeder
    {
        private const string BoilerDescription =
            "We fit new boilers and service existing ones. All boiler work is carried out by a Gas Safe registered engineer, as UK law requires for any work on gas appliances.\n\n" +
            "A new boiler starts with a survey. We look at how much heating and hot water your home needs, check the existing pipework and flue, and help you choose the right type of boiler: combi, system or conventional. You get a clear quote with the price and timings before any work starts.\n\n" +
            "A like-for-like swap usually takes a day. Changing a regular boiler to a combi, or moving the boiler to a new place, takes 1.5 to 4 days.";

        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            ServiceCategory plumbingCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == CategorySlugs.Plumbing || x.Name == "Plumbing");
            ServiceCategory handymanCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == CategorySlugs.Handyman || x.Name == "Handyman");
            ServiceCategory smallBuildingCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == CategorySlugs.SmallBuildingWorks || x.Name == "Small Building & Refurbishments");

            if (plumbingCategory == null || handymanCategory == null || smallBuildingCategory == null)
            {
                // Wait for CategoriesSeeder to complete, or seed categories if not present
                return;
            }

            var services = new[]
            {
                // Plumbing Services
                new { Slug = "emergency-plumbing", Name = "Emergency Plumbing", Description = "This is the one job we take at any hour of the day or night: a pipe that has burst, water coming through a ceiling, or a leak that will not stop when the nearest valve is closed. Call or WhatsApp us with what is happening and your postcode, and we aim to have a technician with you within 60 to 90 minutes.\n\nIt is never booked as an online slot, because the next free slot could be tomorrow. A drip that a bowl can catch until a weekday is a Leak Repair, booked online at the ordinary plumbing rate.", Price = 150.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "leak-repairs", Name = "Leak Repairs", Description = "A leak we can see or reach: a drip from a joint under a sink, a valve weeping at a radiator, a pipe behind a bath panel, a stain spreading on the ceiling under a bathroom. We find where the water starts, stop it, and leave the repair dry under full pressure.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "tap-repairs", Name = "Tap Repairs", Description = "A tap that drips, will not turn off fully or has gone stiff nearly always has one worn part inside it, and that part can be changed without changing the tap. Taps with separate hot and cold heads close on a rubber washer; quarter-turn and most mixer taps close on a ceramic cartridge.\n\nWe fit new basin, bath and kitchen taps too, when the old one is past repair or you want a different one.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "toilet-repairs", Name = "Toilet Repairs", Description = "Running, slow to fill, a weak flush, water on the floor: most toilet faults come down to one part, either inside the cistern or behind the pan. It is the flush valve, the fill valve, the seal between cistern and pan, or the pan connector. We find which one has failed and change it, with the toilet left in place.\n\nA toilet that trickles into the pan all day is easy to miss and costly on a water meter: Waterwise puts the loss from a leaking toilet at 200 to 400 litres a day.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "pipe-repairs", Name = "Pipe Repairs", Description = "Pipes split in a frost, take a nail through a floorboard, wear thin, or are still the old lead. We cut out the damaged length and join in new copper or plastic pipe, then test the run under full mains pressure.\n\nLead pipe inside the house is replaced as far as the inside stop valve. Thames Water says the pipe from your outside stop valve to the inside one is yours, and that you are unlikely to have lead if the house was built after 1970.", Price = 90.00m, Duration = 90, CategoryId = plumbingCategory.Id },
                new { Slug = "shower-installation", Name = "Shower Installation", Description = "Which shower a home can take depends on how it makes its hot water. A combi boiler gives hot water at mains pressure, which Worcester Bosch says suits a mains-pressure mixer shower. A hot water cylinder fed from a cold tank in the loft gives low pressure, and that is what a pump-assisted power shower is made for.\n\nWe fit mixer, power and electric showers, with the pipework run to the valve, connected and tested. A new circuit for a shower is notifiable work under the Building Regulations, so the wiring for an electric shower or a shower pump is done by a registered electrician we bring in, who certifies it.", Price = 90.00m, Duration = 180, CategoryId = plumbingCategory.Id },
                new { Slug = "bathroom-plumbing", Name = "Bathroom Plumbing", Description = "A new basin, bath, bidet or toilet in the place of the old one, with the tiles and the layout staying as they are. We take the old piece out, fit the new one, connect it up, and run water through until every joint is dry.\n\nMoving things round, or retiling, is a new bathroom rather than a swap. That is Full Bathroom Refurbishment, priced after a free survey.", Price = 90.00m, Duration = 120, CategoryId = plumbingCategory.Id },
                new { Slug = "kitchen-plumbing", Name = "Kitchen Plumbing", Description = "The plumbing at the kitchen sink: a new or replacement sink with its waste and trap, the hot and cold feeds to the kitchen tap, and a valve on each feed so one part can be shut off while the rest of the house keeps its water.\n\nA new tap on its own, or a dripping one, is a Tap Repair; a washing machine or dishwasher at the sink is a Washing Machine & Dishwasher Install.", Price = 90.00m, Duration = 120, CategoryId = plumbingCategory.Id },
                new { Slug = "blocked-drains", Name = "Blocked Drains", Description = "We clear blockages inside the house: sinks, basins, baths, showers and toilets that drain slowly or not at all. We take the trap off, clear it and the waste pipe behind it, and run water through to show it flows again.\n\nWe do not clear outside drains, gullies or manholes.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "general-plumbing-maintenance", Name = "General Plumbing Maintenance", Description = "An hour spent going round the plumbing before something fails: the stop valve, the small valves under sinks and toilets, the tank in the loft if there is one, and the pipes that freeze. It is a chance to turn valves nobody has touched in years, and to catch parts that have started to drip.\n\nWe tell you what we found as we go. Small things, such as a valve that will not shut or a washer that drips, we can put right on the same visit. Anything bigger we price before we start.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "washing-machine-dishwasher-install", Name = "Washing Machine & Dishwasher Install", Description = "A new washing machine, washer-dryer or dishwasher connected to the water and the waste and set up the way its maker's instructions say. For a washing machine, Bosch's own steps are to take out the transit bolts and fit the blanks supplied, connect the fill hose to the cold supply, run the waste to a standpipe or the sink trap with no kinks in either hose, and level the machine on its feet.\n\nWhere there is no valve or waste for it yet, we add them: a valve on the cold pipe, and a waste connection under the sink or a standpipe of its own.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "radiator-trv-replacement", Name = "Radiator & TRV Replacement", Description = "A thermostatic radiator valve, or TRV, sits where the radiator meets its pipe and turns the radiator down as the room warms up, so each room holds the temperature its dial is set to. We change a manual valve for a TRV, replace a TRV that has stuck or started to weep, and swap an old radiator for a new one.\n\nWhen the pipes are already where the new radiator needs them, it goes straight on. When they are not, we alter the pipe ends to suit.", Price = 90.00m, Duration = 90, CategoryId = plumbingCategory.Id },
                new { Slug = "outside-garden-tap-installation", Name = "Outside Garden Tap Installation", Description = "We fit a tap to an outside wall and feed it from the cold supply inside the house, often the pipe under the kitchen sink, with the wall drilled through and sealed round the pipe.\n\nA garden hose can draw water back into the house's pipes: left in a pond, a water butt or a bucket of weedkiller, it can siphon that water into the drinking supply. Water Regs UK puts a hose used for watering a garden or washing a car in fluid category 3, which a double check valve protects against. We fit it indoors, next to a shut-off valve for winter, because one fitted outdoors can freeze and fail.", Price = 90.00m, Duration = 90, CategoryId = plumbingCategory.Id },

                // The same text as the migration that added it to the databases that already had
                // services (AddBoilerInstallationServicing). A blank line starts a new paragraph.
                new { Slug = "boiler-installation-servicing", Name = "Boiler Installation & Servicing", Description = BoilerDescription, Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },

                // Handyman Services
                new { Slug = "general-handyman-call-out", Name = "General Handyman Call-Out", Description = "Not sure which service you need? Book a handyman for an hour and tell us about the job. Good for small mixed jobs and anything not on the list.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "furniture-assembly", Name = "Furniture Assembly", Description = "Wardrobes, beds, chests of drawers, desks, tables and bookcases from the flat pack, built to the maker's instructions in the room where they will stand.\n\nA chest of drawers, a wardrobe or a tall bookcase is then fixed to the wall with the fitting the maker supplies. IKEA explains why: a pulled-out drawer works like a lever, and even a small child standing on it can tip a large chest that is not fixed.", Price = 60.00m, Duration = 90, CategoryId = handymanCategory.Id },
                new { Slug = "silicone-mastic-resealing", Name = "Silicone & Mastic Resealing", Description = "When the sealant round a bath, shower tray, basin or kitchen sink has split, lifted or gone black, we cut it out, clean the joint back to a bare, dry surface, and run a new bead of sanitary silicone.\n\nA failed seal lets water run down behind the bath or tray each time it is used. It shows later as a stain on the ceiling below, so the seal is worth redoing while it is only a black line.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "bath-shower-screen-fitting", Name = "Bath & Shower Screen Fitting", Description = "You buy the glass bath screen or shower screen, and we fit it: the wall profile drilled through the tiles and fixed into the wall behind, the glass hung and set upright, and the joints sealed on the side its maker names.\n\nIf an old screen is in the way, we take it down and cut its sealant out first.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "tv-mounting", Name = "TV Mounting", Description = "We fix a flat-screen TV to the wall on a bracket that matches the holes on the back of the set and carries its weight, level and at the height you choose, with the cables tidied into a cover on the wall.\n\nOn brick or block the bracket is fixed into the masonry. On a plasterboard wall it goes into the timber studs behind the board, which is RoSPA's advice for a TV on plasterboard.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "shelf-installation", Name = "Shelf Installation", Description = "The wall decides how a shelf is fixed, so we find out what it is before anything is drilled: solid brick or block, plasterboard on a timber frame, or plasterboard stuck to brick, where the fixing has to reach the brick behind.\n\nWe put up floating shelves, shelves on brackets, shelving on wall uprights, and shelves cut to fit an alcove, level and fixed for what will sit on them.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "curtain-and-blind-fitting", Name = "Curtain and Blind Fitting", Description = "Curtain poles and tracks, and roller, Roman, Venetian and vertical blinds, put up level and on fixings that suit what is above your window: brick, plasterboard, or the steel or concrete lintel that carries the wall over the frame.\n\nYou buy the poles, tracks or blinds, and we fit them. A blind with a cord or a chain gets its child-safety fitting on the wall before we leave.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "door-repairs", Name = "Door Repairs", Description = "When a door sticks in its frame, has dropped, rattles, will not latch or lets in a draught, we look first at its hinges, its latch and keep, and its edges, and put it right where it hangs. We also fit draught strips and adjust or fit door closers.\n\nA door that catches on a new floor is a different job: that is Door Trimming & Shaving.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "wall-mounting", Name = "Wall Mounting", Description = "Pictures, heavy mirrors, whiteboards, noticeboards and gallery walls go up on fixings chosen for the wall and for the weight: plugs and screws into brick or block, and on plasterboard either the timber studs behind it or cavity fixings that open out behind the board.\n\nFor a gallery wall we lay the frames out with you before a single hole is drilled.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "minor-electrical-tasks", Name = "Minor Electrical Tasks", Description = "This is the small electrical work a handyman visit covers: replacing light switches, dimmers, sockets and light fittings on the wiring you already have, and fitting smoke alarms.\n\nSwaps like these, on existing wiring, do not have to be notified under Part P of the Building Regulations in England. Work that does is listed below, and it is done by a registered electrician we bring in.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "minor-home-repairs", Name = "Minor Home Repairs", Description = "Addressing minor plaster cracks, squeaky floors, loose handles, or other general small repairs.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "painting-touch-ups", Name = "Painting Touch-Ups", Description = "When a shelf comes down, a bracket moves or furniture leaves its mark, the wall needs filling, sanding and painting before it looks right again. We do that on walls, ceilings, doors, frames and skirting boards.\n\nBefore we start, we tell you whether a patch will blend in, or whether the whole wall needs a coat to look even.", Price = 60.00m, Duration = 120, CategoryId = handymanCategory.Id },
                new { Slug = "property-maintenance", Name = "Property Maintenance", Description = "Comprehensive handyman checkups and general repairs across multiple areas of the property in a single visit.", Price = 60.00m, Duration = 180, CategoryId = handymanCategory.Id },
                new { Slug = "door-trimming-shaving", Name = "Door Trimming & Shaving", Description = "New carpet, laminate or tiles raise the floor, and the doors that cleared the old one start to catch. We take each door off its hinges, mark the cut from the new floor, trim the bottom edge, seal the bare wood and hang it back.\n\nWe leave a gap over the floor, so air still passes under the door as well as the door swinging clear.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "lock-handle-replacement", Name = "Lock & Handle Replacement", Description = "We replace door locks, latches and handles that are worn, broken or no longer to be trusted: euro cylinders in uPVC and composite doors, mortice locks and night latches in timber doors, and handles inside and out.\n\nWhen tenants change or a key goes missing, a new cylinder or lock is the quickest way to make every old key useless.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "gutter-clearing", Name = "Gutter Clearing", Description = "We clear gutters by hand: the leaves, moss and silt come out, the outlets and downpipes are freed, and water is run along each length to check it drains to its downpipe and not down your wall.\n\nSPAB, the building conservation charity, advises clearing gutters every autumn, and in spring as well.", Price = 60.00m, Duration = 90, CategoryId = handymanCategory.Id },

                // Small Building & Refurbishment Services
                new { Slug = "full-bathroom-refurbishment", Name = "Full Bathroom Refurbishment", Description = "A new bathroom from bare walls: the old bath or shower, basin, toilet and tiles taken out, the pipes and wastes moved where the new layout needs them, the walls and floor prepared and waterproofed, then tiled, and the new suite fitted, connected and sealed.\n\nIt starts with a survey at your home, where we measure the room and agree the layout and what goes in it.", Price = 650.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "kitchen-fitting-alterations", Name = "Kitchen Fitting & Alterations", Description = "Fitting kitchen units, worktops, sink plumbing, splashbacks, and integrated appliances for partial or complete kitchen renovations.", Price = 650.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "partition-walls-drylining", Name = "Partition Walls & Stud Work", Description = "Constructing timber or metal stud partition walls, acoustic insulation, plasterboarding, and room reconfigurations for home offices or layouts.", Price = 320.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "plastering-ceiling-repairs", Name = "Plastering & Ceiling Repairs", Description = "Plaster that has cracked, blown or been holed is cut back and made good; whole walls and ceilings are skimmed smooth; a ceiling a leak has brought down is replaced with new plasterboard; and coving is fitted, or a missing length matched to the old.\n\nWhere water did the damage, the leak is mended before any new plaster goes up.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "wall-floor-tiling", Name = "Wall & Floor Tiling", Description = "Tiling starts with what the tiles go on. We make the wall or the floor ready for the tile first, set the tiles out so the cut ones at each edge match, then fix and grout them, and seal with silicone where they meet a bath, a shower tray or a worktop.\n\nThat covers kitchen splashbacks, bathroom walls, and kitchen, bathroom and hallway floors, in ceramic, porcelain or natural stone.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "flooring-installation", Name = "Flooring Installation", Description = "Supplying and fitting laminate, luxury vinyl tiles (LVT), and engineered wood flooring, complete with underlay, beading, and threshold trims.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "external-brickwork-paving", Name = "External Brickwork & Paving", Description = "Mortar repointing, garden wall repairs, patio relaying, outdoor step rebuilds, and concrete shed bases built on a manageable domestic scale.", Price = 320.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "custom-carpentry-boxing-in", Name = "Custom Carpentry & Boxing-In", Description = "Bespoke alcove cupboards, floating shelving units, pipework boxing-in, and tailored interior woodwork crafted to suit your room dimensions.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
            };

            // The list above goes into an empty table only. Once the table holds any service, live
            // or deleted, the catalogue is the admin's and this seeder leaves it alone: a change
            // to the list reaches an existing database through the admin panel or a migration.
            //
            // It used to look each service up by name at every start and add the ones it could
            // not find. It cannot tell a renamed service from a missing one, so a renamed service
            // came back under its old name; and it could not see a deleted one, so its insert hit
            // the unique index on Slug and the site failed to start. It also wrote its own price,
            // duration and category over whatever an admin had set (PROJECT_STATE Section 3cb).
            if (!dbContext.Services.IgnoreQueryFilters().Any())
            {
                var firstInTheirCategory = new[] { "general-plumbing-maintenance", "general-handyman-call-out" };
                var popular = new[] { "full-bathroom-refurbishment", "kitchen-fitting-alterations", "emergency-plumbing", "furniture-assembly" };
                var gasSafeWork = new[] { "boiler-installation-servicing" };

                foreach (var item in services)
                {
                    var service = new Service
                    {
                        Slug = item.Slug,
                        Name = item.Name,
                        Description = item.Description,
                        BasePrice = item.Price,
                        EstimatedDurationMinutes = item.Duration,
                        CategoryId = item.CategoryId,
                        IsActive = true,
                        DisplayOrder = firstInTheirCategory.Contains(item.Slug) ? 0 : Service.DefaultDisplayOrder,
                        IsPopular = popular.Contains(item.Slug),
                        IsGasSafeWork = gasSafeWork.Contains(item.Slug),
                        IsQuoteOnly = item.CategoryId == smallBuildingCategory.Id || gasSafeWork.Contains(item.Slug),
                        IsEmergency = item.Slug == "emergency-plumbing",
                    };

                    // The page's own blocks and questions, for the services that have them so far
                    // (PROJECT_STATE.md Section 3eq).
                    if (ServicePageTexts.BySlug.TryGetValue(item.Slug, out ServicePageText page))
                    {
                        service.Summary = page.Summary;
                        service.CommonFindings = page.CommonFindings;
                        service.VisitSteps = page.VisitSteps;
                        service.BeforeWeArrive = page.BeforeWeArrive;
                        service.Included = page.Included;
                        service.NotIncluded = page.NotIncluded;
                        for (var i = 0; i < page.Faqs.Count; i++)
                        {
                            service.Faqs.Add(new ServiceFaq { Question = page.Faqs[i].Question, Answer = page.Faqs[i].Answer, DisplayOrder = i + 1 });
                        }
                    }

                    await dbContext.Services.AddAsync(service);
                }

                // One save for the whole list: a start that fails part-way must not leave a
                // part-filled table, which the next start would take for a finished catalogue.
                await dbContext.SaveChangesAsync();
            }

            // Sync ServiceImage entities for any service missing a ServiceImage record
            var allServices = dbContext.Services.ToList();
            foreach (Service service in allServices)
            {
                if (!dbContext.ServiceImages.Any(x => x.ServiceId == service.Id))
                {
                    await dbContext.ServiceImages.AddAsync(new ServiceImage
                    {
                        ServiceId = service.Id,
                        ImageUrl = $"/images/services/{service.Slug}-hero.webp",
                    });
                }
            }
        }
    }
}
