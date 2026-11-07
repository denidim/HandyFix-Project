# 🗺️ Service Area Addition & Update Workflow

Adding a service area to HandyFix is **one step in the admin panel**: the record, its picture and
its place on the coverage diagram are all done there, with no code change (since `PROJECT_STATE.md`
Section 3dm; until then the diagram was a second step, a code change in the repo).

---

## The admin panel

Create or edit the area at **`/Administration/ServiceAreas`**.

> The admin area in this project is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

Fields: Name, Slug, Region, Drive Time, Display Order, Postcode Districts, Area Picture, Featured
flag, Intro Copy, Local Neighbourhoods Copy, and a dynamic list of FAQs.

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
column. Typing is tidied on save, and the field can be left empty.

The public booking page takes a booking **only for a postcode whose district some area lists**.
Anyone else is shown an enquiry link and the phone number instead of the payment step
(`WORKFLOW_BOOKINGS.md`, Step 2). So:

- To start taking bookings somewhere new, add its district to the nearest area. It does not need
  an area page of its own: Ashtead's `KT21` sits on the Epsom area.
- Deleting an area removes its districts from the check with it.
- If no area lists any district, the check is off and every postcode is accepted.

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
| `LocalBusiness` / `FAQPage` / `BreadcrumbList` JSON-LD | `Areas/Details.cshtml` |
| `sitemap.xml` | `SeoController.Sitemap` (generated, never static) |
| Which postcodes the booking page accepts | `IServiceAreasService.IsPostcodeServedAsync` |

### Things worth knowing

- **The slug is not auto-generated on save.** It is an explicit, editable field. On *create* the
  form suggests one from the name via JS, but stops the moment you type in the slug box. On *edit*
  it is never touched automatically. This is deliberate: the slug is load-bearing in two
  independent places (the public URL and the file name of a picture that came with the site), so
  regenerating it because someone fixed a typo in the name would break both at once.
- **Changing a slug:** an uploaded picture keeps its address; a picture that came with the site is
  named after the old slug, so such an area needs a picture uploaded. The admin form says so when
  you do this. The diagram does not depend on the slug.
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

For the same reason, the admin form validates slug uniqueness with `AllWithDeleted()`, not `All()`.

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
- **Distance** comes from `DriveTimeMinutes`, so editing the drive time moves the point. A town
  further than 27 minutes is drawn just outside the outer ring rather than off the picture.
- **Side of the name**: "Automatic" puts it right of the dot in the east half and left in the west
  half. The area's Edit page draws the diagram, says where the area is on it, and has a **Label on
  the Diagram** choice (right, left, above, below) for a name that runs into another one. Saving
  goes back to the list, so open the area again to see the result.
- **Shorter name**: `DiagramLabel` is shown instead of the full name when set. Only three seeded
  areas use it (Kingston, Worcester Park, Walton & Weybridge); the admin form has no box for it.
- **No district, or none found** (no answer within 5 seconds, or a district postcodes.io does not
  know): the area is still saved and the admin is told in one sentence that it is not on the
  diagram; its Edit page says the same. Check the districts and save again. Until then the town is
  simply not plotted; it still appears in the phone list, the areas grid and everywhere else.
- **The 14 seeded places** were hand-set in the view until Section 3dm and were moved into the
  database unchanged by the `AddDiagramPlaceToServiceArea` migration (a new database gets them
  from `ServiceAreasSeeder`). They are within a few degrees of what postcodes.io gives.
- **No test asks postcodes.io**: `SqliteWebApplicationFactory` puts a stand-in in its place, so an
  area saved in a test is left off the diagram.

---

## Quick checklist

- [ ] Area created at `/Administration/ServiceAreas` with real, specific copy and at least one FAQ
- [ ] Its postcode districts filled in, first the one at its centre
- [ ] Its picture uploaded in the Area Picture box
- [ ] Its Edit page says it is on the diagram, and its name runs into no other (else pick a side)
- [ ] `/Areas/{slug}` loads with its picture and FAQs
