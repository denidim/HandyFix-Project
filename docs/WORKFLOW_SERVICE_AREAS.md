# 🗺️ Service Area Addition & Update Workflow

Adding a service area to HandyFix takes **two steps**. The first, the record with its picture, is
done in the admin panel; the second, the coverage diagram, is a code change in the repo. An area
created without step 2 still works — it is simply not plotted on the diagram.

---

## Step 1 — Database record (Admin panel)

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
| Coverage diagram | `_AreaCoverageMap.cshtml` — **only if step 2 is done** |
| Footer "Service Areas" column | `FooterServiceAreasViewComponent` |
| Pricing & Category local-area badges | `ServicesController` |
| `LocalBusiness` / `FAQPage` / `BreadcrumbList` JSON-LD | `Areas/Details.cshtml` |
| `sitemap.xml` | `SeoController.Sitemap` (generated, never static) |
| Which postcodes the booking page accepts | `IServiceAreasService.IsPostcodeServedAsync` |

### Things worth knowing

- **The slug is not auto-generated on save.** It is an explicit, editable field. On *create* the
  form suggests one from the name via JS, but stops the moment you type in the slug box. On *edit*
  it is never touched automatically. This is deliberate: the slug is load-bearing in three
  independent places (the public URL, the file name of a picture that came with the site, the
  coverage-diagram key), so regenerating it because someone fixed a typo in the name would break
  all three at once.
- **Changing a slug** means updating the geometry key in `_AreaCoverageMap.cshtml` too. An
  uploaded picture keeps its address; a picture that came with the site is named after the old
  slug, so such an area needs a picture uploaded. The admin form says both when you do this.
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

## Step 2 — Coverage diagram entry (code change)

> ⚠️ There is **no** `wwwroot/images/coverage-map.svg`. The coverage map is not a static asset and
> has no per-area `<path>` or `data-slug` elements. It is generated in Razor.

Open **`src/Web/HandyFix.Web/Views/Shared/_AreaCoverageMap.cshtml`** and add an entry to the
`geometry` dictionary, keyed by slug:

```csharp
["kingston-upon-thames"] = (10, "start", 13, 5, "Kingston"),
//                          │     │      │   │   └─ short display label (long names overflow)
//                          │     │      └───┴───── label offset from the dot, in viewBox units
//                          │     └──────────────── text-anchor: "start" | "middle" | "end"
//                          └────────────────────── compass bearing from Chessington, degrees
```

How a point is placed:

- **Angle** comes from the bearing you supply — the true compass direction from the Chessington
  base.
- **Radius** is *not* in the table. It is computed from the area's `DriveTimeMinutes`, so editing
  the drive time in the admin panel moves the point automatically.

### Then check it visually

Label collision is the real difficulty here — several towns sit only a few degrees apart (Kingston
and Surbiton are 5° apart; Dorking and Leatherhead 7°). Adjust the **anchor and offsets**, not the
bearing, so the geography stays honest.

To check without guessing, run the app and screenshot the page:

```bash
dotnet run --project src/Web/HandyFix.Web
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" \
  --headless=new --disable-gpu --virtual-time-budget=5000 \
  --screenshot=map.png --window-size=1440,1400 http://localhost:5000/Areas
```

> Don't screenshot at `--window-size=390` to check mobile: Chromium on Windows clamps window width
> to roughly 500px, so narrow captures clip content on every page whether it's broken or not. Use
> ≥700px, or real device emulation.

**Omitting an area from the dictionary is safe.** It is skipped on the diagram but still appears in
the mobile pill list, the areas grid, and everywhere else. Nothing breaks — the town just isn't
plotted.

---

## Quick checklist

- [ ] Area created at `/Administration/ServiceAreas` with real, specific copy and at least one FAQ
- [ ] Its picture uploaded in the Area Picture box
- [ ] Geometry entry added to `_AreaCoverageMap.cshtml`
- [ ] `/Areas` rendered and visually checked for label collisions
- [ ] `/Areas/{slug}` loads with its picture and FAQs
- [ ] `dotnet build` clean and the test suite passing
