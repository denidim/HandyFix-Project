# ⭐ Reviews — Admin Approve/Delete and Public Display

There is **no public review-submission form any more** (removed — see `PROJECT_STATE.md`
Section 3n). Every `Review` row today exists because someone put it there directly — through the
admin panel, a manual DB insert, or eventually a Google Business Profile import that hasn't been
built. This doc covers the two things that remain: admin moderation, and the config gate that
controls whether reviews show up on the public site at all.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

---

## Admin: approve, filter, delete — `/Administration/Reviews`

`Review.IsApproved` is a plain bool, not a status enum. The admin Index list is sortable
(`CreatedOn`/`CustomerName`/`Rating`) and filterable by a `statusFilter` string matched against
`"Approved"`/`"Pending"` — same "Order by" pattern as Bookings/Enquiries, just without a real status
entity behind it.

| Action | Effect |
| --- | --- |
| **Approve** | Sets `IsApproved = true`. This is the only thing that makes a review eligible for public display (subject to the config gate below). |
| **Delete** | Soft delete via the standard `IDeletableEntityRepository` pattern. |

**Summary stat cards (Average Rating, Total Published, Approval Rate, Pending count) are always
computed from the full, unfiltered review list** — applying the status filter to the table below
them never skews these numbers. Same reasoning as the Bookings and Enquiries summary cards: a filter
on what you're looking at shouldn't change what the dashboard reports.

**Code:** `Areas/Administration/Controllers/ReviewsController.cs`, `ReviewsService`
(`src/Services/HandyFix.Services.Data/Reviews/`), `ReviewSortField` enum.

---

## The public-display gate is separate from approval

Two config keys, read as raw `IConfiguration` values (not a typed options class) — `appsettings.json`:

```json
"Business": { "GoogleReviewsUrl": "", "ShowOnSiteReviews": false }
```

- **`Business:ShowOnSiteReviews`** — when `false`, the homepage slider, the full `/Reviews` page, and
  each Area Details page **never call `GetLatestApprovedAsync` at all** — reviews aren't fetched and
  hidden, they're simply not queried. Toggling this to `true` is what actually makes approved
  reviews visible anywhere public.
- **`Business:GoogleReviewsUrl`** — passed straight through to the Reviews page view model
  regardless of the toggle above, for a "See more reviews on Google" link once a real profile
  exists.

**This gate does not touch the admin panel.** Approve/Delete work identically whether
`ShowOnSiteReviews` is on or off — it's purely a public-display switch, easy to conflate with
approval itself since both gate the same content.

**Code:** `Controllers/HomeController.cs` (`Index`, `Reviews` actions), `Controllers/AreasController.cs`
(`Details` action) — all three read the same two keys independently.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| An approved review still doesn't show on the public site | Check `Business:ShowOnSiteReviews` — approval alone isn't enough. |
| "Delete Review" seems to do nothing | This was a real bug once (Section 3o), now fixed. If you see it again, it's a regression, not the known issue. |
| Filtering the admin list changes the stat cards | It shouldn't — if it does, something regressed the always-unfiltered summary query. |
| No way for a customer to leave a review | Correct, by design — see `PROJECT_STATE.md` Section 3n for why the public form was removed. |

---

## Related

- Full reasoning for removing public submission and what replaced it as the trust signal:
  `PROJECT_STATE.md` Section 3n.
- The admin "Delete silently did nothing" bug and its fix: `PROJECT_STATE.md` Section 3o.
