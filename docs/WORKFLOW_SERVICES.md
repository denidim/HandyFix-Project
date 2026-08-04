# 🛠️ Services & Categories — Admin CRUD and the Image Pipeline

How a service gets created, priced, and given a hero image — and the one part of this area that has
**no admin UI at all**: categories.

---

## Step 1 — Categories are seed-only, not admin-managed

> **There is no Categories admin page or controller.** `Areas/Administration/Controllers/` has no
> `CategoriesController` — only `ServicesController` exists, and it only lets you *pick* a category
> for a service via a dropdown.

The two categories that exist (`Plumbing`, `Handyman`) come from
`src/Data/HandyFix.Data/Seeding/ServiceCategoriesSeeder.cs`, which only **inserts** — it never
updates a row that already exists by name. Changing a category's name, description, or adding a
third category means **editing the seeder and restarting the app**, not a form submission.

`ICategoriesService.CreateAsync(name, description)` exists in the service layer but has **no caller
anywhere in the app** — it's reachable from a test, not from any admin action. Don't assume it's
wired to a UI because the method exists.

**Code:** `Areas/Administration/Controllers/ServicesController.cs` (category dropdown only),
`src/Data/HandyFix.Data/Seeding/ServiceCategoriesSeeder.cs`, `ICategoriesService`.

---

## Step 2 — Services: full CRUD at `/Administration/Services`

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

Standard Create / Edit / Delete via `ServicesController`. Fields: Name, Description, Base Price,
Estimated Duration, Category, and an optional hero image upload.

| Rule | Value |
| --- | --- |
| Name | required, 3–100 chars |
| Description | required, 20–3000 chars |
| Base Price | 0.01–10,000.00 |
| Duration | 15–1440 minutes |
| Image upload | optional, **20 MB max**, `.jpg`/`.jpeg`/`.png`/`.webp` only |

**Delete is a real hard delete**, unlike Technicians (soft-delete-if-no-bookings) and Reviews/
Enquiries (soft delete). `ServicesController.Delete` removes the image file from disk *before*
deleting the row, so a failed image delete doesn't leave an orphaned file behind silently.

**The slug is auto-regenerated from the Name on every save**, unlike Service Areas (explicit,
editable field — see `WORKFLOW_SERVICE_AREAS.md`). This matters because the slug is load-bearing for
the image filename (Step 3) — renaming a service's Name mid-life renames its image file too.

**Code:** `Areas/Administration/Controllers/ServicesController.cs`,
`Areas/Administration/Views/Services/{Index,Create,Edit}.cshtml`, `ServicesService`.

---

## Step 3 — The image pipeline: local WebP, not R2

Service hero images are the **only** admin-uploaded images that go through
`ImageStorageService` (SkiaSharp), writing straight to local disk under
`wwwroot/images/services/` — a completely different path from the Cloudflare R2 pipeline used for
booking/enquiry customer photo uploads (`ImageService`/`ICloudflareR2Service`, capped at 5 files ×
15MB, no format conversion). Don't reach for one when you mean the other.

| Step | Behaviour |
| --- | --- |
| Resize | only if source width > **1920px** — resized to 1920 wide, proportional height |
| Encode | always WebP, quality **80** |
| Output path | `wwwroot/images/services/{slug}-hero.webp` (always, regardless of input format) |
| Legacy cleanup | `DeleteLegacyImages` removes any leftover `{slug}-hero.jpg/.jpeg/.png` after a successful WebP write |
| Slug validation | rejects empty slugs, `..`, or invalid filename characters before touching disk |

### The `{slug}-hero.webp` convention is hardcoded in **five** places

Change the naming convention and all five need updating together:

1. `ImageStorageService.GetServiceImagePath` / `SaveServiceImageAsync`'s return URL.
2. `src/Data/HandyFix.Data/Seeding/ServicesSeeder.cs:79` — back-fills `ServiceImage` rows for seeded
   services that don't have one yet.
3. `Areas/Administration/Controllers/ServicesController.cs:168` — the "slug changed, no new file
   uploaded" branch of `Edit`, which has to rebuild the URL itself since no upload triggered
   `SaveServiceImageAsync`.
4. `ServiceViewModel` / `ServiceDetailsViewModel`'s Mapster fallback — if a service has no
   `ImageUrl` row at all, falls back to this path by convention.
5. `Areas/Administration/Views/Services/Edit.cshtml` — builds the same path client-side (paired with
   `ImageStorageService.ServiceImageExists`) to decide whether to show a "current image" preview.

**Category hero tiles break this pattern entirely**: they use `{slug}-category-hero.webp` (note
`-category-` inserted), and there is **no admin upload path for them at all** — `ServiceCategory` has
no image column, so category images are a pure file-drop-by-convention, same as area hero images.

---

## Known issue: `Slugify` doesn't fully collapse triple hyphens

`ServicesService.Slugify` (duplicated verbatim in `CategoriesService`):

```csharp
name.Replace(" ", "-").Replace("/", "-").Replace("&", "-").Replace("--", "-").Trim('-').ToLower();
```

`Replace("--", "-")` runs **once**, not repeatedly, so a name that produces three or more
consecutive hyphens after the earlier replacements doesn't fully collapse. A name shaped like
`"Walton-on-Thames & Weybridge"` becomes `walton-on-thames--weybridge` (double hyphen survives) —
this is exactly why `ServiceAreasService` uses an explicit, admin-typed slug field instead of
deriving one (see `WORKFLOW_SERVICE_AREAS.md`). If you ever give a service or category a name with
an ampersand or a multi-word hyphenated place name, check the resulting slug by hand.

`CategoryViewModel.Slug` has its own, even simpler computed slug (`Name.Replace(" ", "-").ToLower()`
— no `&`/`/` handling at all) used purely for display routing, a second independent place the same
class of bug can show up.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| "I need a new category" has no obvious button | Correct — there isn't one. Edit `ServiceCategoriesSeeder.cs` and restart. |
| A service's image doesn't update after upload | Check the upload succeeded (`ModelState` errors re-render the form); if it did, the browser may be caching the old WebP — hard refresh. |
| An uploaded file was rejected outright | Only `.jpg`/`.jpeg`/`.png`/`.webp` under 20MB are accepted. |
| A service's slug looks wrong after a name edit with `&` or multiple hyphens | Known `Slugify` limitation above — fix the slug by renaming with a cleaner name, or expect a manual DB correction. |
| Category tile image doesn't show | It's a manual file drop (`{slug}-category-hero.webp`), not an admin upload — confirm the file actually exists on disk. |

---

## Related

- Adding/editing a service area (a separate, explicit-slug pattern deliberately different from
  this one): `docs/WORKFLOW_SERVICE_AREAS.md`.
- Booking/enquiry photo uploads (the *other* image pipeline, R2-backed): see `ImageService` in
  `src/Services/HandyFix.Services/ImageService.cs`.
- Full architectural history: `PROJECT_STATE.md` Section 3j (hero WebP migration), 3k (legacy sweep
  removal), Section 4 Tier 1 item 1 (bulk image generation for launch).
