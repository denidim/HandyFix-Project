# 🗺️ Service Area Addition & Update Workflow

Adding a service area to HandyFix takes **three steps**. Only the first is self-service; the other
two are file changes in the repo. An area created without steps 2 and 3 still works — it degrades
gracefully rather than breaking — but it will look unfinished.

---

## Step 1 — Database record (Admin panel)

Create or edit the area at **`/Administration/ServiceAreas`**.

> The admin area in this project is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

Fields: Name, Slug, Region, Drive Time, Display Order, Featured flag, Intro Copy, Local
Neighbourhoods Copy, and a dynamic list of FAQs.

Saving auto-propagates to every consumer, because they all query `IServiceAreasService` live:

| Surface | Source |
| --- | --- |
| `/Areas` grid | `AreaCardViewComponent` |
| `/Areas/{slug}` detail page | `AreasController.Details` |
| Coverage diagram | `_AreaCoverageMap.cshtml` — **only if step 3 is done** |
| Footer "Service Areas" column | `FooterServiceAreasViewComponent` |
| Pricing & Category local-area badges | `ServicesController` |
| `LocalBusiness` / `FAQPage` / `BreadcrumbList` JSON-LD | `Areas/Details.cshtml` |
| `sitemap.xml` | `SeoController.Sitemap` (generated, never static) |

### Things worth knowing

- **The slug is not auto-generated on save.** It is an explicit, editable field. On *create* the
  form suggests one from the name via JS, but stops the moment you type in the slug box. On *edit*
  it is never touched automatically. This is deliberate: the slug is load-bearing in three
  independent places (the public URL, the hero image filename, the coverage-diagram key), so
  regenerating it because someone fixed a typo in the name would break all three at once.
- **Changing a slug is a three-file operation.** Update the record, rename
  `wwwroot/images/areas/{old}-hero.webp`, and update the geometry key in `_AreaCoverageMap.cshtml`.
  The admin form warns you when you do this.
- **Deletion is a hard delete.** `ServiceAreasService.DeleteAsync` removes the row and its FAQs
  permanently instead of soft-deleting. This is not an oversight — see the warning below.
- **FAQs are replaced wholesale on save**, not diffed. They carry no external references, so
  recreating them is simpler than reconciling by id, and hard-deleting the old rows stops
  soft-deleted orphans accumulating on every edit.

### ⚠️ Why areas are hard-deleted

`IX_ServiceAreas_Slug` is `UNIQUE` with **no filter on `IsDeleted`**, while `ApplicationDbContext`
applies a global `HasQueryFilter(e => !e.IsDeleted)` to every deletable entity. A soft-deleted area
therefore still occupies its slug at the database level while being invisible to every query.

If areas were soft-deleted, then on the next boot `ServiceAreasSeeder` — which looks the area up by
slug, sees nothing, and inserts — would hit a unique-index violation, and **the app would fail to
start** because seeding runs inside `Program.Configure`. Recovery would mean hand-deleting the row
in SQL.

For the same reason, the admin form validates slug uniqueness with `AllWithDeleted()`, not `All()`.

---

## Step 2 — Hero image (manual file drop)

Place a WebP hero at:

```
src/Web/HandyFix.Web/wwwroot/images/areas/{slug}-hero.webp
```

- **Dimensions:** 1600×700 (the `<img>` in `Areas/Details.cshtml` declares these).
- **Format:** WebP, quality 80, to match what `ImageStorageService` produces for service images.
- **Convert before copying it in.** Nothing in the app converts images on boot any more — the old
  `ConvertExistingJpgServiceImages` sweep was removed (see `PROJECT_STATE.md` Section 3k). A `.jpg` or
  `.png` dropped here is simply ignored.
- **There is no upload UI for area images.** Unlike services, `ServiceArea` has no image column —
  the path is resolved by convention from the slug at the view layer.
- **Fallback:** if the file is missing, the `onerror` handler degrades to `/images/hero.webp`. The
  page works, it just shows the generic hero. The admin edit screen tells you which state you're in.

---

## Step 3 — Coverage diagram entry (code change)

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
- [ ] `wwwroot/images/areas/{slug}-hero.webp` added at 1600×700, WebP q80
- [ ] Geometry entry added to `_AreaCoverageMap.cshtml`
- [ ] `/Areas` rendered and visually checked for label collisions
- [ ] `/Areas/{slug}` loads with the hero image and FAQs
- [ ] `dotnet build` clean and the test suite passing
