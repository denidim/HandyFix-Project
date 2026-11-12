# ⭐ Reviews — Our Google Reviews on the Site, and the Review Button

The business's reviews live on Google. The site shows them as text, typed into the admin panel word
for word, and asks every paying customer for one. There is **no review form on the site itself**
(removed in `PROJECT_STATE.md` Section 3n), and no automatic import from Google: Google gives that
access only after an application, and at a few reviews a week, copying one takes a minute.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

---

## The routine: one new Google review

1. A customer leaves a review on Google.
2. A manager answers it on Google, naming the job and the town, within two days.
3. The same day, the manager adds it in the admin panel: **Reviews → Add Review**.
   - **Name**: the first name and the first letter of the surname, as on Google ("Sarah K.").
   - **Date on Google**: the day it went up. The site shows the month and year only, because
     Google shows "2 weeks ago" rather than a day.
   - **Town** (optional): pick one of our towns from the list the box offers, and the review also
     shows on that town's page.
   - **Job Done** (optional): a few words, "Boiler service".
   - **Stars** and **Review Text**: as on Google, the text copied without changes.
4. It is on the site at once: on `/Reviews`, on the home page (the six newest), and on its own
   town's area page (the three newest from that town).

A review added this way is approved on saving. **Edit** puts a typing mistake right; **Delete**
takes one off (a soft delete through `IDeletableEntityRepository`).

---

## Where each review shows

| Page | What it shows | With none |
| --- | --- | --- |
| `/Reviews` | Every approved review, newest on Google first (up to 50), beside the two Google buttons | The card with the two Google buttons alone |
| Home page | The six newest | No section |
| An area page | The three newest whose town is that area's name | No section |

Each card: the initials, the name, "{Town} · {Job}" (whichever is known), the stars, the text with its
paragraphs, and "Google review, {month year}". The order is the Google date, or the day it was saved
for a review that has none.

**No review data for search engines.** The pages carry no `Review`, `aggregateRating` or
`ratingValue` in their structured data: Google ignores, or penalises, stars a business serves about
itself. The stars on the cards are pictures for people. `ReviewsWebTests` keeps it that way.

**Code:** `Areas/Administration/Controllers/ReviewsController.cs` (Index, Create, Edit, Approve,
Delete), `ReviewsService` (`src/Services/HandyFix.Services.Data/Reviews/`), `Views/Shared/_ReviewCard.cshtml`,
`HomeController` (`Index`, `Reviews`), `AreasController` (`Details`).

---

## The two Google links

`appsettings.json`:

```json
"Business": {
  "GoogleReviewsUrl": "https://g.page/r/CRa4VR4BecsDEBM",
  "GoogleLeaveReviewUrl": "https://g.page/r/CRa4VR4BecsDEBM/review"
}
```

- **`GoogleReviewsUrl`** opens the Business Profile on Google Maps ("Read Our Google Reviews").
- **`GoogleLeaveReviewUrl`** opens Google's form for writing one ("Leave a Google Review"), on
  `/Reviews` and in the paid-in-full invoice email below.

They are the same link with and without `/review` at the end. Each button shows only when its link
is set. The links are public, which is why they live in `appsettings.json` rather than in the
server's settings. (`Business:ShowOnSiteReviews`, which hid the reviews until Section 3eh, is gone.)

---

## The review button in the paid-in-full invoice

When a job's invoice is paid in full, the customer gets it again marked "Paid in Full"
(`PROJECT_STATE.md` Sections 3dr and 3ds). That email, and only that one, ends with "Happy with the
work? A short Google review helps other homeowners find us." and the **Leave a Google Review**
button. Every customer whose invoice is paid gets it, without anyone choosing, so nobody is picked
for being happy: Google forbids asking only some customers. With no `GoogleLeaveReviewUrl`, the email
has no button.

**Code:** `BookingEmails.InvoiceAsync`; the link is read by `EmailOptions` from
`Business:GoogleLeaveReviewUrl`.

---

## Admin list summary cards

The Average Rating, Total Published and Approval Rate cards, and the Pending count beside the title,
are always worked out from the full list: the status filter on the table below them never changes
these numbers. `Review.IsApproved` is a plain bool; a review typed in is approved at once, so Pending
stays at nought unless a row is put in some other way.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| A review shows on `/Reviews` but not on its town's page | Its Town is not written as the area's name. Edit it and pick the town from the list. |
| A review is not on the home page | Only the six newest by their Google date are. |
| "Delete Review" seems to do nothing | This was a real bug once (Section 3o), now fixed. If you see it again, it's a regression. |
| No way for a customer to leave a review on the site | Correct, by design: reviews are left on Google (Section 3n). |

---

## Related

- Why the site's own review form was removed: `PROJECT_STATE.md` Section 3n.
- Reviews typed in, the review button, and "How did you hear about us?": Sections 3eh to 3ej.
