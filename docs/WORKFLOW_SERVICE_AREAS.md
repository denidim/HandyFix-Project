# 🗺️ Service Area Addition & Update Workflow

Adding a service area to HandyFix is **one step in the admin panel**: the record, its picture and
its place on the coverage diagram are all done there, with no code change (since `PROJECT_STATE.md`
Section 3dm; until then the diagram was a second step, a code change in the repo).

---

## The admin panel

Create or edit the area at **`/Administration/ServiceAreas`**.

> The admin area in this project is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

Fields: Town Name, Postcode Districts, Area Picture, About the Town, Local Streets and Landmarks,
the Featured flag, and a dynamic list of FAQs. Everything else the site works out by itself (since
`PROJECT_STATE.md` Section 3dn), so adding a town needs no technical terms:

- **The page address** (`/Areas/{slug}`) is made from the name when the town is added: lower case,
  `&` as "and", anything else but letters and digits as one hyphen ("Walton-on-Thames & Weybridge"
  gives `walton-on-thames-and-weybridge`). It never changes after; the Edit page shows it as a link.
  A name already taken is refused under the name box.
- **The postcode districts**, when the box is left empty, come from the town's name: postcodes.io's
  place search (`PostcodesIoLocator.FindTownDistrictAsync`) finds it; a place called exactly that
  wins, else the nearest to Chessington, so "Molesey" gives `KT8` and "Ashford" the one in Surrey.
  A name it cannot find saves nothing and says so under the name box. More districts can be typed
  in at any time.
- **The drive time** from Chessington is worked out once, when the town is added, by the public
  OSRM route service (`OsrmDriveTimeCalculator`: free, no key, no traffic), from the middle of its
  first district. With no answer in 5 seconds it is estimated from the straight-line distance
  (about 5 minutes plus 1.1 a kilometre). It is never 0, which the pages show as the base itself.
  The Edit page shows it; the 14 seeded areas keep the times set by hand.
- **The order** of the areas everywhere is featured first, then nearest, then by name. There is no
  order to set by hand.

### The area's picture

Upload it in the **Area Picture** box on the Create or Edit form (since `PROJECT_STATE.md` Section
3dl). JPEG, PNG or WebP, up to 20MB.

- **The server makes it fit.** `ImageStorageService.SaveAreaImageAsync` makes the picture cover
  1600×700 and keeps its middle, as the page draws it, then cuts the middle 928 pixels as the copy
  a phone is sent. Both are WebP at quality 80. A smaller picture is drawn larger rather than
  refused. Keep the town in the centre of what you upload.
- **It goes to the photo bucket (R2), never into `wwwroot`.** A file written into the running
  container is lost at the next deploy, which starts a new one from what git holds — the fault the
  service pictures had until Section 3cq. Each upload gets a new name,
  `areas/{random}_{slug}.webp`, with `...-phone.webp` beside it, and `ServiceArea.HeroImageUrl`
  holds its address.
- **A replaced picture leaves the bucket** after the area points at the new one, and an area's
  upload leaves it when the area is deleted, so nothing is left with no area pointing at it.
- **Which picture shows** is decided in one place, `PictureLookups.AreaPictureOf`: the upload,
  else the picture that came with the site for the slug (the 15 seeded areas have one in
  `wwwroot/images/areas/{slug}-hero.webp`, with its phone strip), else the general picture. The
  area page, the area cards and the picture a shared link shows all ask there. The Edit page says
  which of the three it is.
- **Making a picture:** below the box is a short text in the site's watercolour style, with the
  town's name, and a **Copy** button. Paste it into the Gemini app, check the result, and upload
  it. There is deliberately no "Generate" button on the server: an API key and the code around it
  would cost more than a job done about once a year.

### Postcode Districts decide where bookings are taken

A district is the first half of a postcode: the `KT9` of `KT9 2QN`. Each area lists the ones it
covers, with commas between them (`KT5, KT6, KT7`); the list page shows them in its Postcodes
column. Typing is tidied on save, and a box left empty is filled from the town's name (above).

The public booking page takes a booking **only for a postcode whose district is within 15 miles of
the Chessington base, or one some area lists**. The 15 miles are a fixed list of 251 districts in
`BookingRadius.cs`: every district with at least one postcode inside the circle (the company's
decision of 11 November 2026). Anyone else is shown an enquiry link and the phone number instead of
the payment step (`WORKFLOW_BOOKINGS.md`, Step 2). So:

- A town within the 15 miles is bookable before it has an area page: Twickenham's `TW1` and
  Richmond's `TW9` are taken with no area listing them.
- To take bookings past the 15 miles, add the district to the nearest area.
- Deleting an area removes only the districts it alone brought: one within the 15 miles stays.
- If the company changes the radius, the list is rebuilt from a new postcode download; it is not
  edited in the admin panel.

The 15 seeded areas start with the lists in `ServiceAreasSeeder.cs`; a database that already had
them got the same lists from the `AddPostcodeDistrictsToServiceArea` migration.

Saving auto-propagates to every consumer, because they all query `IServiceAreasService` live:

| Surface | Source |
| --- | --- |
| `/Areas` grid | `AreaCardViewComponent` |
| `/Areas/{slug}` detail page | `AreasController.Details` |
| Coverage diagram | `_AreaCoverageMap.cshtml`, from the area's place (below) |
| Footer "Service Areas" column | `FooterServiceAreasViewComponent` |
| Pricing & Category local-area badges | `ServicesController` |
| `Service` (provider: the shared business record) / `FAQPage` / `BreadcrumbList` JSON-LD | `Areas/Details.cshtml` |
| `sitemap.xml` | `SeoController.Sitemap` (generated, never static) |
| Which postcodes the booking page accepts | `IServiceAreasService.IsPostcodeServedAsync` |

### Things worth knowing

- **The two texts are drawn in paragraphs.** In "About the Town" and "Local Streets and Landmarks"
  a blank line starts a new paragraph on the town's page (`Paragraphs.Of`, as on a service page).
  A text with no blank line is one paragraph, as before.
- **A town's four service cards are worked out, not chosen.** The page shows two plumbing and two
  handyman services picked from the town's slug (`AreaServices.For`), so towns show different
  services, one town shows the same four on every visit, and a new town gets its own with nothing
  to set in the admin. Each card carries the start of the service's description
  (`Summaries.Of`: the first sentence, and the second when the first is very short), its price, a
  button that books it, and its name as a link to the service's own page, where the full
  description is. Adding a service, switching one off or moving one in its category's order
  changes what several towns show. Until `PROJECT_STATE.md` Section 3el every town printed the
  same four in full.
- **The business record's towns are not the area pages.** Every page writes one record of the
  business for Google (`IBusinessRecord`: type `Plumber`, open 24 hours, a link to the Google
  profile), and its `areaServed` is the fixed list in `HandyFix.Common/ServiceAreaTowns.cs`: the
  20 places of the Google Business Profile's service area, so the site and the profile name the
  same places. The service pages' "Do you cover my location?" answer reads the same list. Adding a
  town in the admin gives it a page but does not add it to that list: change the list together
  with the profile (`PROJECT_STATE.md` Section 3eg).
- **The slug is made once and never regenerated.** Renaming a town keeps its page address, unlike
  a service, whose slug follows its name. This is deliberate: the slug is load-bearing in two
  independent places (the public URL and the file name of a picture that came with the site), so
  regenerating it because someone fixed a typo in the name would break both at once. Until
  Section 3dn the slug was a box on the form and could be changed by hand.
- **Deletion is a hard delete.** `ServiceAreasService.DeleteAsync` removes the row and its FAQs
  permanently instead of soft-deleting. This is not an oversight — see the warning below.
- **FAQs are replaced wholesale on save**, not diffed. They carry no external references, so
  recreating them is simpler than reconciling by id, and hard-deleting the old rows stops
  soft-deleted orphans accumulating on every edit.

### ⚠️ Why areas are hard-deleted

`IX_ServiceAreas_Slug` is `UNIQUE` with **no filter on `IsDeleted`**, while `ApplicationDbContext`
applies a global `HasQueryFilter(e => !e.IsDeleted)` to every deletable entity. A soft-deleted area
therefore still occupies its slug at the database level while being invisible to every query.

If areas were soft-deleted, a deleted area's slug could never be used again: adding an area with
that slug would fail at the database, on a row no admin page can show. Recovery would mean
hand-deleting the row in SQL.

For the same reason, `ServiceAreasService.CreateAsync` checks that the slug is free with
`AllWithDeleted()`, not `All()`.

### The seeder fills an empty table, once

`ServiceAreasSeeder` adds its 15 areas and their questions **only when the ServiceAreas table is
empty**. After that it does nothing: an area deleted here stays deleted, and an area whose
questions were all removed stays without them. Editing the seeder's list has no effect on a
database that already has areas; use this admin page. (Until `PROJECT_STATE.md` Section 3cb it
added back any seeded area it could not find, at every start.)

---

## The coverage diagram places the town by itself

> There is **no** `wwwroot/images/coverage-map.svg`. The diagram is drawn in Razor, in
> `src/Web/HandyFix.Web/Views/Shared/_AreaCoverageMap.cshtml`, from three fields on each area:
> `DiagramBearing`, `DiagramLabelSide` and `DiagramLabel`.

- **Direction** comes from the area's first postcode district. On save, `ServiceAreasService` asks
  postcodes.io (`PostcodesIoLocator`: free, no key or account) where that district is and keeps
  the compass direction from the Chessington base (KT9) in whole degrees. It asks only for a new
  area, when the districts change, or when the area has no place yet, so saving again tries again
  and a place set by hand is kept.
- **Distance** comes from `DriveTimeMinutes`, worked out when the town is added (above). A town
  further than 27 minutes is drawn just outside the outer ring rather than off the picture.
- **Side of the name**: "Automatic" puts it right of the dot in the east half and left in the west
  half. The area's Edit page draws the diagram, says where the area is on it, and has a **Label on
  the Diagram** choice (right, left, above, below) for a name that runs into another one. Saving
  goes back to the list, so open the area again to see the result.
- **Shorter name**: `DiagramLabel` is shown instead of the full name when set. Only three seeded
  areas use it (Kingston, Worcester Park, Walton & Weybridge); the admin form has no box for it.
- **A first district that cannot be found** (no answer within 5 seconds, or a district postcodes.io
  does not know): a **new** town is not saved, and the sentence goes under the districts box, since
  its drive time needs the place too. When the districts of an **existing** town are changed, it is
  saved and the admin is told in one sentence that it is not on the diagram; its Edit page says the
  same. Check the districts and save again. Until then the town is simply not plotted; it still
  appears in the phone list, the areas grid and everywhere else.
- **The 14 seeded places** were hand-set in the view until Section 3dm and were moved into the
  database unchanged by the `AddDiagramPlaceToServiceArea` migration (a new database gets them
  from `ServiceAreasSeeder`). They are within a few degrees of what postcodes.io gives.
- **No test asks postcodes.io or OSRM**: `SqliteWebApplicationFactory` puts stand-ins in their
  place, so an area saved in a test is left off the diagram.

---

## Quick checklist

- [ ] Area created at `/Administration/ServiceAreas` with real, specific copy and at least one FAQ
- [ ] Its postcode districts right: the one found from the name, or typed, first the one at its centre
- [ ] Its picture uploaded in the Area Picture box
- [ ] Its Edit page says it is on the diagram, and its name runs into no other (else pick a side)
- [ ] `/Areas/{slug}` loads with its picture and FAQs
