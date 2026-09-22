// The full L5 image batch: one entry per image the site uses. Shared by generate-batch.mjs
// (calls the model) and the later convert step (WebP at the real wwwroot paths).
//
//   name    raw output file name (output/batch/<name>.jpg)
//   out     final path under wwwroot/images, per the existing naming conventions
//   w, h    final WebP size (services/categories 1024x1024, areas 1600x700, page heroes 1920x1072)
//   refs    which style-reference pair to send: "interior", "exterior" or "outdoorJob"
//   reuse   already-approved image from the style test round; generation is skipped for it
//
// Area seasons are spread across the year (4 spring, 4 summer, 4 autumn, 3 winter), always in
// bright light. Workers vary in age and appearance on purpose: no recurring "character" that
// could read as a real, named member of staff.

const service = (name, scene, refs = "interior") => ({
  name, out: `services/${name}-hero.webp`, w: 1024, h: 1024, aspectRatio: "1:1", refs, scene,
});

// Busy street scenes were the riskiest in the first batch run: the model put badges on vans and
// logo text on passers-by wearing the navy "tradesperson" polo. Areas need neither.
const AREA_EXTRAS =
  "Any people are small, in the distance, in casual everyday clothes (no navy polo shirts). " +
  "No cars, vans or other road vehicles.";

const area = (slug, scene) => ({
  name: `area-${slug}`, out: `areas/${slug}-hero.webp`, w: 1600, h: 700, aspectRatio: "21:9",
  refs: "exterior", scene: `${scene} ${AREA_EXTRAS}`,
});

const page = (name, scene, refs) => ({
  name, out: `${name}.webp`, w: 1920, h: 1072, aspectRatio: "16:9", refs, scene,
});

const TEXT_SPACE = "Calm, uncluttered composition with open space, suitable as a background behind overlaid text.";

export const jobs = [
  // ---- Page backgrounds ----
  page("hero",
    "A wide view of a smart British detached home on a sunny summer morning, a plain navy work " +
    "van with no writing and no manufacturer badge or emblem parked on the driveway, a tradesman " +
    "carrying a toolbox up the front path, colourful front garden, welcoming and professional.",
    "exterior"),
  page("services-hero",
    "A bright, airy open-plan kitchen and living room in a renovated Victorian house, sunlight " +
    "pouring through large rear glazing onto a wooden floor, a neat navy tool bag, spirit level " +
    `and cordless drill resting in the foreground, no people. ${TEXT_SPACE}`,
    "interior"),
  page("areas-hero",
    "A wide panoramic view across the Surrey Hills: rolling green fields, woodland, villages with " +
    "church spires and red-brick homes nestled in the valleys, a winding river, bright summer " +
    `sunshine with big white clouds. ${TEXT_SPACE}`,
    "exterior"),
  page("bg-cta-wide",
    "A sunlit British living room partway through a refresh: freshly painted pale walls, a " +
    "stepladder, a folded dust sheet and a few neat tools on the floor, a large window with a green " +
    `garden beyond, no people. Very calm and low in detail. ${TEXT_SPACE}`,
    "interior"),

  // ---- Category tiles ----
  service("plumbing-category",
    "A Black British plumber in his 50s with short grey hair and a short grey beard kneeling beside a new freestanding bath, connecting gleaming chrome taps " +
    "and pipework, an open tool bag of wrenches beside him, bright white bathroom full of sunlight."),
  service("handyman-category",
    "A cheerful, broad-shouldered handyman in his 40s with a shaved head and a thick dark beard on a small stepladder hanging a framed abstract painting in a " +
    "bright living room, tool belt with a hammer and screwdriver, freshly fitted shelves nearby."),
  service("small-building-category",
    "A builder in his 50s with a weathered tanned face, short grey hair and a grey moustache, in a bright room mid-renovation: freshly plastered walls, a new timber stud wall " +
    "partly boarded, materials and tools neatly stacked, a big window with sunlight streaming in, " +
    "a clear sense of transformation."),

  // ---- Plumbing ----
  service("emergency-plumbing",
    "A calm plumber in his 50s with short grey hair kneeling in the hallway of a Victorian terraced " +
    "house, turning off the main stopcock in an open low cupboard, a few folded towels on the " +
    "floorboards, bright morning sunlight through a stained-glass front door panel. In control, reassuring."),
  service("leak-repairs",
    "A South Asian British plumber in his 40s with a torch, inspecting and tightening a pipe joint " +
    "under a modern bathroom basin, a small bowl catching drips, winter sunshine through a frosted window."),
  { name: "tap-repairs", out: "services/tap-repairs-hero.webp", w: 1024, h: 1024, reuse: "output/tap-repairs-v2.jpg" },
  service("toilet-repairs",
    "A young plumber with dark curly hair adjusting the fill valve inside an open toilet cistern, the " +
    "lid set safely aside, clean bright white bathroom in a 1930s house, spring blossom outside the window."),
  service("pipe-repairs",
    "A stocky plumber with a shaved head cutting and fitting a new section of copper pipe with a pipe " +
    "cutter in a bright utility room, neat copper pipework along the wall, afternoon sunlight."),
  service("shower-installation",
    "An olive-skinned plumber in his 30s with dark curly hair and stubble fitting a chrome thermostatic shower mixer and riser rail on a freshly tiled " +
    "wall of a modern walk-in shower, bright clean bathroom with a skylight."),
  service("bathroom-plumbing",
    "A tall plumber with sandy hair connecting the waste pipe of a new white pedestal basin in a " +
    "Victorian bathroom with a sash window and a clawfoot bath, bright summer sunshine."),
  service("kitchen-plumbing",
    "A Black British plumber in his 30s installing a new ceramic sink into an oak worktop in a " +
    "cottage kitchen with exposed beams and sage-green units, autumn leaves outside the window, warm sunshine."),
  service("blocked-drains",
    "A plumber in his 50s with glasses, a receding hairline and grey stubble clearing a blocked bathroom basin with a hand drain auger, a tidy bucket " +
    "beside him, gleaming bright bathroom, everything clean and fresh."),
  service("general-plumbing-maintenance",
    "A friendly plumber in his 60s with a white beard checking the valves beneath a plain white wall-" +
    "mounted boiler with no logo and no display, inside a bright kitchen cupboard, morning sunlight."),
  service("washing-machine-dishwasher-install",
    "A stocky plumber in his 30s with a dark buzz cut and a neat beard sliding a new plain white washing machine with no logo into place under a " +
    "worktop, its hoses running behind it to a valve on the wall, bright utility room with a garden view, spring sunshine."),
  service("radiator-trv-replacement",
    "An East Asian British plumber in his 30s fitting a plain white thermostatic valve to a white " +
    "panel radiator beneath a living-room window, frost and bright winter sunshine outside."),
  service("outside-garden-tap-installation",
    "A South Asian British plumber in his 50s with silver hair and a moustache fitting a brass outside tap to the brick wall of a house beside a sunny " +
    "garden, a coiled green hose and flowering borders nearby, bright summer day.",
    "outdoorJob"),

  // ---- Handyman ----
  service("general-handyman-call-out", // slug assumed; the service is seeded in Launch Sprint L2 item 2
    "A friendly, stocky handyman in his 50s with a bald head and a warm smile, carrying a toolbox, being welcomed at a sunny front door by a smiling older " +
    "couple, colourful front garden, bright morning. Warm first impression.",
    "outdoorJob"),
  service("furniture-assembly",
    "A handyman in his 30s with long dark hair tied back and a dark beard assembling a flat-pack wardrobe in a bright bedroom, parts laid out neatly " +
    "on the floor with an Allen key and screwdriver, an older woman in the doorway bringing him a mug of tea."),
  service("silicone-mastic-resealing",
    "A handyman in his 60s with neat grey hair and reading glasses running a neat, clean white silicone bead along the edge of a bath with a sealant gun " +
    "with no label, gleaming bright bathroom, sunlight through a frosted window."),
  service("bath-shower-screen-fitting",
    "A slim young East Asian British handyman in his 20s with short black hair fitting a clear glass shower screen onto a bath, checking it with a spirit level, " +
    "modern bright bathroom with white metro tiles."),
  { name: "shelf-installation", out: "services/shelf-installation-hero.webp", w: 1024, h: 1024, reuse: "output/shelf-installation-v2.jpg" },
  service("tv-mounting",
    "A handyman with a short beard mounting a large flat-screen TV, switched off with a blank black " +
    "screen, on a bracket above a Victorian fireplace, tidy cable cover, bright living room."),
  service("curtain-and-blind-fitting",
    "A handyman in his 50s on a small stepladder fitting a curtain pole above a large bay window of " +
    "a 1930s house, soft floral curtains ready to hang, spring sunshine."),
  service("door-repairs",
    "A Black British handyman in his 40s with short black hair and a neat goatee adjusting the hinge of a white panelled internal door with a screwdriver, bright " +
    "hallway with wooden floors and sunlight."),
  service("wall-mounting",
    "A tall young Black British handyman in his 20s with short locs hanging a large round mirror on a hallway wall, using a spirit level, a gallery of " +
    "framed abstract prints already hung beside it, bright hallway."),
  service("minor-electrical-tasks",
    "A mixed-race handyman in his 30s with short curly black hair, clean-shaven, on a stepladder fitting a modern pendant light in a bright dining room, a plain " +
    "voltage tester in his tool belt, sunshine through French doors."),
  service("minor-home-repairs",
    "A slim young handyman in his 20s with a blond buzz cut kneeling in a Victorian hallway, screwing down a creaky floorboard with a cordless " +
    "screwdriver, runner rug rolled back neatly, bright morning light."),
  service("painting-touch-ups",
    "A South Asian British handyman in his 20s with a short neat beard repainting a skirting board with a small brush and fresh white paint, a dust sheet " +
    "on the floor, bright freshly painted pastel room."),
  service("property-maintenance",
    "A handyman in his 40s with sandy hair and a ginger beard fixing the latch of a painted wooden garden gate at the front of a pretty " +
    "Victorian house, tool bag at his feet, colourful front garden, bright summer day.",
    "outdoorJob"),
  service("door-trimming-shaving",
    "A red-haired handyman in his 30s with a hand plane trimming the bottom edge of a door laid across two trestles, wood " +
    "shavings curling, a room with new soft carpet behind, bright light."),
  service("lock-handle-replacement",
    "A close view of a handyman in his 60s with short white hair fitting a new brass handle and lock to a sage-green front door, " +
    "screwdriver in hand, sunny porch with potted plants.",
    "outdoorJob"),
  service("gutter-clearing",
    "A young handyman in his 20s with short dark hair and stubble on a ladder clearing autumn leaves from the gutter of a 1930s semi-detached house, a " +
    "bucket hooked to the ladder, bright blue sky, golden trees.",
    "outdoorJob"),

  // ---- Small Building & Refurbishments ----
  service("full-bathroom-refurbishment",
    "A beautifully finished modern bathroom with a freestanding bath, white metro tiles, plants and " +
    "sunlight streaming in, a South Asian British tradesman in his 30s fitting the last chrome towel rail. Aspirational, fresh and bright."),
  service("kitchen-fitting-alterations",
    "A fitter in his 40s with a neat dark beard fitting an oak worktop onto new sage-green Shaker kitchen units, bright " +
    "modern kitchen with large windows, sunshine."),
  service("partition-walls-drylining",
    "A tall Black British builder in his 20s fixing a plasterboard sheet onto a new timber stud wall frame, insulation visible in " +
    "the open section, bright spacious room with sunlight."),
  service("plastering-ceiling-repairs",
    "A plasterer in his 50s with short white hair and a tanned face skimming a wall smooth with a trowel and hawk, fresh smooth plaster, bright room " +
    "with a big window and sunlight."),
  service("wall-floor-tiling",
    "An East Asian British tiler in his 40s with glasses kneeling and laying ceramic wall tiles in a bathroom, spreading adhesive with a " +
    "notched trowel, a neat stack of tiles nearby, bright clean British bathroom, sunlight through a " +
    "frosted window."),
  service("flooring-installation",
    "A fitter in his 20s with curly blond hair kneeling and clicking together light oak engineered wood floorboards in a bright empty " +
    "room, spacers along the wall, sunlight through tall windows."),
  service("external-brickwork-paving",
    "A broad-shouldered builder in his 40s with a salt-and-pepper beard laying natural stone patio slabs in a sunny back garden, a low red-brick garden wall " +
    "behind, flowering borders, bright summer day.",
    "outdoorJob"),
  service("custom-carpentry-boxing-in",
    "A carpenter in his 60s with a neat grey beard standing in a bright Victorian living room, clearly in front of " +
    "(not inside) newly fitted bespoke painted alcove cupboards and shelves beside a chimney breast, checking a " +
    "shelf with a spirit level, full figure visible. Above the fireplace hangs a framed painting of Bulgarian Kukeri: " +
    "folk dancers in huge shaggy fur costumes, tall carved wooden masks and large brass bells, in a snowy village. " +
    "Bright sunshine."),

  // ---- Areas ----
  { name: "area-chessington", out: "areas/chessington-hero.webp", w: 1600, h: 700, reuse: "output/chessington-area-hero-autumn-rain-v2.jpg" },
  area("surbiton",
    "A leafy avenue of large Victorian and Edwardian villas in Surbiton, bay windows, tiled front " +
    "paths and mature plane trees, bright high-summer sunshine and deep blue sky."),
  area("kingston-upon-thames",
    "The historic market place in Kingston upon Thames: an elegant old market hall with a plain " +
    "facade and no clock, timber-framed and Georgian buildings, colourful flower stalls with plain " +
    "striped awnings, a few shoppers, spring blossom, bright morning sunshine."),
  area("worcester-park-ewell",
    "A village pond in Ewell with ducks, old Georgian cottages and a historic flint church tower among " +
    "bare trees, crisp frosty winter morning, bright low sunshine and clear blue sky."),
  area("epsom",
    "Epsom Downs: wide open rolling green downland with long white racecourse rails curving across it, " +
    "scattered wildflowers, trees and houses on the horizon, huge bright summer sky with white clouds."),
  area("sutton",
    "A row of charming Victorian terraced houses in Sutton seen straight-on from across a quiet road, " +
    "each with a different brightly painted front door (red, yellow, teal), bay windows and little " +
    "front gardens, a street tree in golden autumn colour, bright crisp autumn afternoon sunshine."),
  area("banstead",
    "A village green in Banstead on the edge of the North Downs, a traditional well with a pitched " +
    "timber canopy, cottages and a flint church, spring daffodils, bright morning sunshine."),
  area("esher",
    "A tranquil landscape-garden lake in Esher with a small classical pavilion on an island, lawns " +
    "sloping down to the water, trees in rich autumn colour reflected in the lake, bright sunny day."),
  area("leatherhead",
    "The River Mole in Leatherhead with an old multi-arched red-brick bridge, willows along the banks, " +
    "a light dusting of snow on the banks and rooftops, sparkling water, bright clear winter sunshine."),
  area("wimbledon",
    "Wimbledon Common in high summer: open grassland and woodland with the historic windmill, a few " +
    "people strolling in the distance, bright sunshine and blue sky."),
  area("cobham",
    "A historic red-brick watermill beside the River Mole in Cobham, a small weir, willows and spring " +
    "blossom, ducks on the water, morning mist lifting in bright sunshine."),
  area("walton-on-thames-weybridge",
    "The Thames towpath near Walton-on-Thames and Weybridge: moored colourful narrowboats with no " +
    "names painted on them, riverside willows, sparkling water, warm golden late-afternoon summer sun."),
  area("reigate",
    "The view from the top of Reigate Hill across a patchwork of fields, woods and the rooftops of " +
    "Reigate below, a bench and a winding chalk path in the foreground, crisp frosty winter morning, " +
    "bright clear sky."),
  area("dorking",
    "The Surrey Hills near Dorking: rolling vineyard rows in golden autumn colour on gentle slopes, " +
    "wooded Box Hill behind, a tall church spire in the distance, bright autumn sunshine."),
  area("guildford",
    "The Norman castle keep in Guildford's castle gardens, bright spring flower beds of tulips, " +
    "lawns and blossom trees, the town's rooftops beyond, bright spring sunshine."),
];
