# Design DNA System — HandyFix

This document is a reference, not a history: it describes the sizes, design tokens, shared classes
and page patterns that actually exist in `wwwroot/css/` today, organised so a contributor checks
here **before** inventing a one-off class or reaching for an inline `style=`. See
`PROJECT_STATE.md` and `docs/history/` for the reasoning and dates behind individual pieces.

Contents: 1. Sizes and screens · 2. Design tokens · 3. Typography · 4. Utility classes ·
5. Layout and page shells · 6. Shared components · 7. Page stylesheets · 8. Admin design system ·
9. Stylesheet load order

---

## 1. Sizes and Screens

### Every size is in `rem`

The public stylesheets write every size in `rem`: font sizes, line heights, spacing, widths,
heights, radii, shadows. A `rem` is counted from the font size of `<html>`, which is 16px unless
Section "One page for every computer screen" below changes it, so **a size in px divided by 16 is
the size in rem**:

| px | 14 | 16 | 17 | 18 | 20 | 24 | 32 | 48 | 600 | 1320 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| rem | 0.875 | 1 | 1.0625 | 1.125 | 1.25 | 1.5 | 2 | 3 | 37.5 | 82.5 |

When this document, a comment or a class name gives a size in px (`18px`, `.fs-18`, `.dim-48`,
`.lh-24`), it means the size on a screen 1920 wide, where 1rem is 16px.

**Write new sizes in rem.** A size written in px does not shrink on a laptop while everything
around it does. These stay in px, on purpose:

| Stays in px | Why |
| :--- | :--- |
| `1px` borders and dividers | A hairline should stay one crisp line at every size |
| `9999px` (pill radius), `-10000px` (off the page) | They mean "all the way", not a size |
| Widths in `@media` conditions | A breakpoint is about the screen, not about the text size |
| Sizes inside an SVG drawing (the coverage diagram's labels) | There a px is one unit of the drawing, which already shrinks as a whole |
| `print.css` | Paper has no screen width |
| `pages/admin.css` | The admin panel does not scale (Section 8) |

### One page for every computer screen (`base/scale.css`)

The public site is drawn for a screen **1920 wide**. On a narrower computer screen every size
shrinks with the width, so a laptop shows the page a wide monitor shows, smaller, and not a page
that looks zoomed in.

| Screen width | Size of everything |
| :--- | :--- |
| 1920 and over | 100%, the sizes as written |
| 1536 to 1920 | In step with the width (1728 wide is 90%) |
| 1280 to 1536 | 80%, and never under it |
| Under 1280 | Not touched: tablets and phones have layouts of their own |

A 15.6-inch laptop at Windows' usual 125% is 1536 wide, so it draws the page at exactly 80%.

How it is built:

- `_Layout.cshtml` puts `class="site-scale"` on `<html>`.
- `base/scale.css` sets that element's font size from 1280 wide up:
  `font-size: clamp(80%, calc(100vw / 120), 100%)`. `100vw / 120` is 16px on a screen 1920 wide.
  The two ends are percentages, not px, so a larger font size set in the visitor's browser still
  counts.
- Bootstrap's `.container` has its width in px, so the same file gives it the same 1320px in rem
  (`82.5rem`).
- The admin layout has no `site-scale` class. The admin panel keeps one size on every screen.

**To change how small a laptop goes, change the `80%` in `base/scale.css` and nothing else.**

What follows from it:

- **Text sizes.** "Nothing under 14px" (Section 3) is measured on the design width. On a laptop at
  80% the smallest text is 11.2px.
- **Browser zoom on a wide screen.** Zooming in makes the window narrower in CSS px, so on a 1920
  screen the steps up to 125% are given back by the scale; from 150% the text grows, and from 175%
  the window is under 1280 and the page is the menu-button layout at full size.
- **Checking a change of sizes.** Look at 1920 (as designed), 1536 (80%), 1024 and 390. At 1536
  every box should be the same box as at 1920, 0.8 times the size.

### Breakpoints

All `@media` widths are px. The ones in use:

| Width | What changes there |
| :--- | :--- |
| `575.98px` / `576px` | Phone-only details: phone copies of wide pictures, two-column home gallery strip |
| `640px` | The Gallery page's grid goes from one column to two |
| `767.98px` / `768px` | **The phone line.** Most files switch here: headline sizes, section padding, grids, the coverage diagram in place of its pill list |
| `991.98px` / `992px` | Two columns with a sidebar (booking, category, service details, services, info pages); the home gallery strip in one row; the Gallery page's third column |
| `1024px` | The home hero's two columns |
| `1279.98px` / `1280px` | **The header line.** Under it the header shows the menu button; from it, the full link row. The scale above starts here too |
| `380px`, `440px` | Header details on narrow phones (`components/navbar.css`) |
| `701px` | Two blocks on the service page go to two columns (`pages/service-details.css`) |

`pages/admin.css` has its own: `576px`, `768px`, `992px`, and `1536px`, under which the sidebar
folds to icons.

---

## 2. Design Tokens (`base/variables.css`)

Everything below is a CSS custom property on `:root`. Two token generations coexist and are both
live: the original hand-named set (`--primary`, `--accent`, `--success`, `--border-subtle`, …) and
a larger "Material You"-style extended palette (`--surface-*`, `--on-*`, `--secondary`,
`--tertiary`, `--error`, `--*-fixed`, …). Newer components lean on the extended palette: in
particular **`--secondary` (`#006591`) is the accent colour used throughout current pages**, not
the older `--accent` (`#0ea5e9`). When in doubt which one a component uses, check `variables.css`.

### Core colours (original set)
| Variable | Value | Notes |
| :--- | :--- | :--- |
| `--primary` | `#0f172a` | Dark slate: headings, primary buttons, admin sidebar |
| `--primary-light` | `#1e293b` | Hover and gradient partner for `--primary` |
| `--accent` | `#0ea5e9` | Cyan: the original accent, still behind the cookie notice |
| `--accent-hover` | `#0284c7` | Darker cyan hover state |
| `--success` | `#10b981` | Emerald: ticks and success marks, not behind white text |
| `--success-strong` | `#047857` | The green behind white text (contrast 5.5; `--success` gave 2.5) |
| `--warning-strong` | `#b45309` | Amber: a limit, not an error (the "what is not included" mark on a service page) |
| `--background` | `#f8fafc` | Page background |
| `--text-main` | `#334155` | Body copy |
| `--text-dark` | `#0f172a` | Headings and bold titles |
| `--border-subtle` | `#e2e8f0` | Dividers, container borders |

### Division colours
One colour family per division, each used by several components (the home division cards, the
category page's service cards, the pricing cards, the badges). The values live here once.

| Division | Ink | Border | Wash (card background) |
| :--- | :--- | :--- | :--- |
| Plumbing | `--division-plumbing` `#0ea5e9` | `--division-plumbing-border` `#bae6fd` | `--division-plumbing-wash` (pale blue gradient) |
| Handyman | `--division-handyman` `#e11d48` | `--division-handyman-border` `#fecdd3` | `--division-handyman-wash` (pale rose gradient) |
| Small building | `--division-building` `#b45309` | `--division-building-border` `#fde68a` | `--division-building-wash` (pale amber gradient) |

A component takes its division from a modifier class: `.division-card-plumbing`,
`.service-horizontal-card-handyman`, `.pricing-card-building`, `.bento-badge-plumbing`, and so on.

### Extended palette (current accent and surfaces)
| Variable | Value | Variable | Value |
| :--- | :--- | :--- | :--- |
| `--secondary` | `#006591` | `--secondary-container` | `#39b8fd` |
| `--secondary-fixed` | `#c9e6ff` | `--secondary-fixed-dim` | `#89ceff` |
| `--tertiary` | `#000000` | `--tertiary-container` | `#002113` |
| `--tertiary-fixed` | `#6ffbbe` | `--tertiary-fixed-dim` | `#4edea3` |
| `--error` | `#ba1a1a` | `--error-container` | `#ffdad6` |
| `--primary-container` | `#131b2e` | `--primary-fixed` / `-dim` | `#dae2fd` / `#bec6e0` |
| `--outline` | `#696a70` | `--outline-variant` | `#c6c6cd` |
| `--surface-container-lowest` | `#ffffff` | `--surface-container` | `#eceef0` |
| `--surface-container-low/high/highest` | `#f2f4f6` / `#e6e8ea` / `#e0e3e5` | `--on-surface` / `-variant` | `#191c1e` / `#45464d` |
| `--on-secondary-container` | `#004666` | `--on-tertiary-container` | `#009668` |

Every `--on-*` token is the readable text or icon colour for its matching container. `--outline`
is a text colour too (captions, placeholders, diagram labels), which is why it is dark enough to
read on white at 4.5 to 1. The full list of `surface`, `on-*` and `*-fixed` pairs is in
`variables.css`.

### Glass, spacing, radii, shadows
| Group | Values |
| :--- | :--- |
| Glass | `--glass-bg: rgba(255,255,255,0.65)`, `--glass-border: rgba(255,255,255,0.4)` |
| Spacing scale | `--spacing-1` … `--spacing-32` = `0.25rem` → `8rem` (1=0.25rem, 2=0.5rem, 3=0.75rem, 4=1rem, 5=1.25rem, 6=1.5rem, 8=2rem, 10=2.5rem, 12=3rem, 16=4rem, 20=5rem, 24=6rem, 32=8rem) |
| Layout | `--padding-section` (= `--spacing-16`), `--gap-mobile`, `--gap-desktop`, `--height-footer-mobile: 5rem`, `--height-footer-desktop: 4.375rem` |
| Radii | `--radius-sm:0.25rem` `-md:0.5rem` `-lg:0.75rem` `-xl:1rem` `-2xl:1.5rem` `-full:9999px` |
| Shadows | `--shadow-sm` → `--shadow-2xl`, plus `--shadow-ambient` for the original card hover effect |
| Transitions | `--transition-fast: 0.2s ease`, `-normal: 0.3s cubic-bezier(0.4,0,0.2,1)`, `-slow: 0.5s ease`, `-hero: 0.7s ease` |
| Z-index | base `1`, dropdown `40`, sticky `50`, overlay `90`, modal `100`, mobile-cta `1030` |

---

## 3. Typography (`base/typography.css`)

**Font**: `'Outfit', sans-serif` for both body and display (`--font-family-base` /
`--font-family-display`). It and the Material Symbols icon font are the site's own files in
`wwwroot/fonts`, downloaded from Google Fonts by `tools/fonts/fetch-fonts.mjs` and declared in the
page head by `Views/Shared/_WebFonts.cshtml`, which both layouts include. The icon font is cut to the
icons listed in `tools/fonts/icon-names.txt`, at one weight (400), size (24) and grade (0), with
fill on and off: a weight other than 400 draws at 400, and **an icon not on that list shows as its
name**. Add a new icon's name there and run the script again; a test fails until it is
(`PROJECT_STATE.md` Sections 3cn and 3cz).

Headings (`h1`–`h6`) default to `var(--font-family-display)` + `color: var(--primary)`. Text with
no size class of its own takes the body size (`base/reset.css`), not Bootstrap's 16px.

### Font-size scale (`.font-*`)
Each class sets font size, line height and weight together, from the `--fs-*`, `--lh-*` and
`--fw-*` tokens. Do not combine an `fs-*` utility (size only) with one of these when a semantic
size already exists. Sizes are given in px on the design width; the tokens hold them in rem.

| Class | Size / Line height / Weight | Responsive? |
| :--- | :--- | :--- |
| `.font-display-brand` | 24px / 32px / 800, `letter-spacing:-0.5px` | — |
| `.font-headline-xl` | 34px / 42px / 700 → **52px / 60px / 700 at ≥768px** | yes |
| `.font-headline-lg` | 30px / 38px → **36px / 44px at ≥768px** | yes |
| `.font-headline-md` | 26px / 34px / 700 | — |
| `.font-headline-sm` | 22px / 30px / 600 | — |
| `.font-body-lg` | 20px / 30px / 400 | — |
| `.font-body-md` | 18px / 28px / 400 (also the `body` default) | — |
| `.font-nav-link` | 17px / 26px / 500 | — |
| `.font-button-text` | 17px / 26px / 600 | — |
| `.font-label-sm` | 16px / 24px / 500 | — |

**Nothing on a public page goes below 14px on the design width** (older customers; scaled up
2026-09-22, `PROJECT_STATE.md` Section 3bf). The `fs-9` … `fs-13` utilities exist for the admin
panel only. On a laptop the whole page is drawn smaller (Section 1), and 14px becomes 11.2px.

### Colour-token text classes (all `!important`)
`.text-primary`, `.text-secondary`, `.text-secondary-container`, `.text-text-main`,
`.text-on-surface-variant`, `.text-white` (+ `-80`/`-90` opacity variants),
`.text-on-primary-container`, `.text-on-surface`, `.text-on-secondary-container`,
`.text-on-tertiary-container`, `.text-outline`, `.text-error`.

---

## 4. Utility Classes (`base/utilities.css`)

Check this list before adding a new one-off class or `style=` attribute: most spacing and sizing
needs are already covered.

| Category | Classes |
| :--- | :--- |
| Margin | `.mt-0/1/2/3/4/6/8/10/12/16`, `.mb-0/1/2/3/4/6/8/10/12/16` (values from the spacing scale) |
| Padding | `.pt-4`, `.pt-6`, `.pb-4`, `.pb-6`, `.pl-4`: **only these five exist**, no `pr-*` |
| Line height | `.lh-tight` (1.25), `.lh-snug` (1.375), `.lh-normal` (1.5), `.lh-relaxed` (1.625), `.lh-24` (24px) |
| Font size | `.fs-9`, `-10`, `-11`, `-12`, `-13`, `-14`, `-15`, `-16`, `-18`, `-20`, `-24`, `-36`, `-48` (the number is the size in px on the design width), `.fs-headline-lg` (token-based) |
| Font weight / align | `.font-bold` (700), `.font-medium` (500), `.text-right`, `.text-left` |
| Max width | `.max-w-xl` (36rem), `-2xl` (42rem), `-3xl` (48rem), `-7xl` (80rem) |
| Border radius (`!important`) | `.rounded-lg`, `.rounded-xl`, `.rounded-full` |
| Opacity | `.opacity-70/80/90` (fills the gap above Bootstrap's 25/50/75/100) |
| Fixed dimensions | `.dim-32/36/40/48/56` (square, e.g. avatars and icon boxes) |
| Tint backgrounds | `.tint-neutral`, `.tint-neutral-soft`, `.tint-error(-soft)`, `.tint-success`, `.tint-secondary(-soft/-faint)`, `.tint-info`, `.tint-waiting`, `.tint-black-soft`, `.tint-secondary-fixed`, `.tint-primary-fixed-dim`, `.tint-surface-high` |
| Icon variation | `.icon-fill`, `.icon-unfill`, `.icon-medium-weight` (Material Symbols `FILL`/`wght` axes) |
| Tailwind-parity misc | `.uppercase`, `.italic`, `.leading-relaxed`, `.select-all`, `.block`, `.text-xs`, `.tracking-wide`, `.tracking-wider` |
| Behaviour | `.resize-none`, `.pre-wrap`, `.text-no-decoration`, `.inline-form-reset`, `.btn-link-reset`, `.z-1` |

**Why `font-bold`, `rounded-*`, `max-w-*` and the like exist here at all**: every one of them was
referenced across views under the assumption it was a Bootstrap or Tailwind class, but was defined
nowhere: a silent no-op sitewide until each was added for real (`PROJECT_STATE.md` Section 3a).
Check this file before assuming a class "looks standard enough" to already work.

---

## 5. Layout and Page Shells

### Layout pieces (`base/layout.css`)
| Class | Purpose |
| :--- | :--- |
| `.page-container` | The boxed width every public page shares: `width: min(82.5rem, calc(100% - 3rem))` (1320px, or the screen less 24px each side), centred |
| `.section` | Top and bottom padding of `--padding-section` |
| `.section-header`, `.section-header-left`, `.section-header-divider` | A section's centred or left heading block and the short rule under it |
| `.bg-background`, `.bg-surface`, `.bg-surface-container`, `.bg-primary-container`, `.bg-glass-bg` | Background helpers from the tokens |
| `.environment-strip` | The strip across the top of every page of a copy of the site that is not the live one |

`body` carries a pale blue-to-rose gradient down its first 700px (43.75rem). The three category
pages set `body.page-bg-services-category` (through `ViewData["BodyClass"]`), a watercolour picture
drawn at the screen's width and repeated down the page; a phone is sent a narrower copy.

Bootstrap's `.container` is used inside many views as well; on scaled screens its width follows
the page (Section 1).

Two shell patterns cover nearly every page. Pick the one that matches the page's shape rather than
composing a new one.

### Template A: hero + breadcrumb (`pages/services.css`)
Used by: Services Index/Category/Pricing, Areas Index, Gallery, About, Contact, Terms, Privacy,
FAQ, Reviews.

```css
.page-container                         /* base/layout.css */
.breadcrumb-nav                         /* flex row; .breadcrumb-current is bold + --secondary */
.services-header-content                /* max-width: 50rem (800px) */
.services-header-content.text-center    /* modifier: centres the block */
.services-hero-title                    /* --fs-headline-xl, the mobile size below 768px; a <span> inside is --secondary */
.services-subtitle
```
Left-aligned by default (Services, Areas, Pricing, About); add `.text-center` for the pages that
want the hero centred (Contact, Terms, Privacy, FAQ) rather than inventing a second hero class.

### Template B: thin, centred, no breadcrumb
There is no single shared class pair for this shape. Each thin form-style page has its own
page-scoped classes following the same concept (centred card, capped width, no breadcrumb):

| Page | Classes | File |
| :--- | :--- | :--- |
| Login and the other account pages | `.auth-page-container` / `.auth-card` (max 450px, `.wide` → 550px) | `pages/auth.css` |
| Payment result | `.payment-page-container` / `.payment-status-card` (max 576px) | `pages/payment.css` |
| Booking confirmed | `.confirmed-page-container` / `.success-container` (max 600px) | `pages/booking.css` |

If adding a new thin page, follow this pattern (own page-scoped class, capped `max-width`, centred)
rather than force-fitting one of the three existing class names.

### `.info-canvas` (`pages/pages-info.css`)
The content wrapper under the hero of a Template A info page: `max-width: 50rem` (800px),
`margin: var(--spacing-12) auto`. There is no `.wide` modifier. If a wider variant is ever needed,
add it with a real, deliberate width at the point something uses it.

---

## 6. Shared Components (`components/`)

### Header (`navbar.css`) and logo (`logo.css`)
- `.header-docked` is plain white, so the logo is the only colour in it. From 1280 wide it is
  sticky and shows the full link row (`.nav-links`, `.nav-link`, `.navbar-actions`,
  `.navbar-call-btn`, `.navbar-whatsapp-btn`). Under 1280 it is fixed and shows the menu button
  (`.navbar-mobile-toggle`), which opens `.mobile-nav-dropdown`.
- A fixed header takes no room in the page, so `site.js` measures it and writes
  `--header-height` on `<html>`; the page's top padding and the open menu's height follow it. Do
  not hard-code the header's height anywhere.
- `.mobile-cta-bar` / `.btn-mobile-cta`: the floating "Book Now" button on phones.
- `.site-logo` is an inline SVG sized by its height: 70px in the header, 48px on phones, 84px in
  the footer. In the footer its navy parts turn white (`.logo-ink`, `.logo-gold`, `.logo-teal-*`),
  on screens only: on paper the footer is white and the logo keeps its colours.

### Footer (`footer.css`)
`.footer-main`, `.footer-content`, `.footer-column`, `.footer-title`, `.footer-link`,
`.footer-areas-grid`, `.footer-social` / `.footer-social-icon`, `.footer-copyright`.

### Buttons (`buttons.css`)
`.btn` takes its text from the button tokens. Variants: `.btn-primary` (navy, a shade lighter and
1px up on hover), `.btn-secondary`, `.btn-success`, `.btn-outline-primary`,
`.btn-outline-secondary`; sizes `.btn-lg` and `.btn-block`. `button.is-submitting` marks a button
whose form is being sent; its animation runs slower under `prefers-reduced-motion`.

### Forms (`forms.css`, `image-upload.css`)
`.form-label`, `.form-control`, `.form-field`, `.input-with-icon` + `.field-icon`,
`.form-checkbox-field`, `.validation-summary`, `.validation-error`. `.form-extra`,
`.form-turnstile` and `.form-turnstile-hint` belong to the form guard every public form renders
(`docs/WORKFLOW_FORMS.md`). Photo pickers use `.upload-dropzone`, `.border-dashed-upload`,
`.image-preview-grid`, `.image-preview-item`, `.image-preview-remove`.

### Cards (`cards.css`)
```css
.glass-card { background: var(--glass-bg); backdrop-filter: blur(0.75rem);
              border: 1px solid var(--glass-border); border-radius: var(--radius-xl);
              box-shadow: var(--shadow-2xl); }
```
The single definition of the translucent card. Use it for hero and overlay contexts only.

Also here: `.division-card` (+ `.img-container`, `.division-card-content`), the home page's
category tiles; `.bento-card` (+ `-overlay`, `-content`, `-large`), the picture tiles of Popular
Services; `.testimonial-card` (translucent, for dark backgrounds); and `.card-image-link`, which
makes a card's picture a link only where there is a mouse (`hover: hover` and `pointer: fine`).

Defined elsewhere, despite the names:
- **`.pricing-card` / `.pricing-cards-grid`** are in `pages/pricing.css`. `.pricing-card` is an
  opaque content card (`surface-container-lowest`, `shadow-lg` → `shadow-xl` on hover),
  deliberately not the translucent `.glass-card` treatment.
- **`.trust-card`** is in `pages/pages-info.css`; the home page's trust section uses it with
  `.trust-card-icon` from `pages/home.css`.

### Cookie notice (`cookie-notice.css`)
`#cookieConsent` is one slim line fixed to the bottom of the screen until the visitor presses
`.cookie-notice-ok`. The site sets only essential cookies, so it informs and asks for no choice.

### Icons (`icons.css`, last in the bundle)
`.material-symbols-outlined` and `.material-symbols-filled` set the icon font's axes. An icon is
1.5rem (24px) unless an `fs-*` class says otherwise. `.whatsapp-icon` is a drawing, because the
icon font has no brand icons; it takes the text colour and scales with the text around it.

---

## 7. Page Stylesheets (`pages/`)

Anything used by one page lives in that page's file. What is in each:

| File | What it holds |
| :--- | :--- |
| `home.css` | The hero (`.hero-section`: at least 600px tall, photo under a white wash, two columns from 1024 wide) with the booking card (`.hero-booking-card`, its calendar and time grid); the division cards' colours and lists (`.divisions-grid`, `.division-card-*`); Popular Services (`.bento-*`, one card per row on phones in `.bento-2x2-mode`); the trust section with the Join Our Team card (`.trust-*`, `.join-team-*`); testimonials (`.testimonials-*`); the closing call to action (`.cta-card`, `.cta-*`) |
| `services.css` | Template A's hero classes, the Services index cards (`.service-category-card`, `.service-subitem`) and the quality band (`.quality-proof-*`) |
| `service-category.css` | A category page: its hero and badges (`.category-hero-*`), the list of services as wide cards (`.service-horizontal-card`, in each division's colours), the local block with the coverage diagram (`.seo-local-*`) and the sidebar call to action (`.sidebar-cta-*`) |
| `service-details.css` | A service page: hero (`.service-hero-*`), the facts tiles (`.specs-bento-grid`, `.spec-bento-card`), the text blocks (`.service-block`, `.service-points`, `.service-steps`, `.service-scope-*`, `.service-same-visit`, `.service-towns`), the questions (`.faq-accordion-*`), the technician (`.local-expert-*`) and the sticky booking sidebar (`.sidebar-booking-*`, `.sidebar-trust-*`) |
| `pricing.css` | Rate cards (`.rate-card-*`), the notes (`.pricing-bento-*`, `.pricing-note-box`) and the service price cards (`.pricing-card-*`, in each division's colours) |
| `areas.css` | Section "Areas" below |
| `booking.css` | The booking wizard: the stepper (`.vertical-stepper`, `.v-step-*`), category choice (`.category-radio-*`), the calendar (`.custom-calendar-card`, `.cal-day-cell`), time slots (`.slot-selection-btn`), the summary (`.summary-glow-card`, `.summary-*`) and the confirmation page (`.success-*`, `.confetti-canvas`) |
| `gallery.css` | Section "Gallery" below |
| `payment.css` | The payment result card (`.payment-status-*`) |
| `auth.css` | The account pages' card (`.auth-*`) |
| `pages-info.css` | The info pages: `.info-canvas`, `.info-card`, `.info-section*`, the FAQ accordion (`.accordion-*`), Contact (`.contact-info-*`), Reviews (`.review-*`, `.reviews-*`), About (`.narrative-*`, `.guarantee*`, `.hub-pillar-*`, `.about-*`) |

### Areas (`areas.css`)
**`.area-card` / `.area-card-grid`**: the card used wherever areas are listed (Areas index, a
town's "Nearby Areas"). Bordered card with the town's picture (`.area-card-media`,
`.area-card-image`), lifted 2px with a `--secondary` border on hover, `.area-card-badge` for the
"Featured" pill.

**Coverage diagram** (`_AreaCoverageMap.cshtml`): `.coverage-ring` / `-spoke` (dashed guide
lines), `.coverage-node` / `.coverage-dot` / `.coverage-label` (each town; bolder and thicker on
hover and focus), `.coverage-hub` / `.coverage-hub-halo` (the base), and the phone fallback
`.coverage-list-*` / `.coverage-pill`. The two swap at exactly `768px`: pill list by default, the
SVG from 768 wide. The SVG is capped at `max-width: 55rem` (880px): its `viewBox` is 780 units
wide, so uncapped it would draw its labels far too large on a wide screen. **The label sizes
inside it are px on purpose** (Section 1): they are units of the drawing.

`pages-info.css` has an unrelated `.coverage-grid` / `.coverage-card` pair: same word, no
collision, easy to confuse when searching for "coverage".

### Gallery (`gallery.css`)
**The home page strip** (`.home-gallery-strip`): six photos in glass frames
(`.home-gallery-crystal-frame`, `.home-gallery-thumb-box`) with the "View Gallery" button between
the third and fourth, over the watercolour background. From 992 wide it runs from one edge of the
screen to the other, the photos are 4 to 5, and the strip stands about half as tall as the hero.
Under 992 the photos are 4 to 3 in three columns with the button below, and in two columns on a
phone. `home.js` swaps photos in and out of the frames, unless the visitor asked for reduced
motion.

**The Gallery page**: `.gallery-grid` of `.gallery-polaroid-card` (one column, two from 640 wide,
three from 992), each in one of three pale tints (`.gallery-frame-0/1/2`: blue, rose, amber), with
a caption plate and a before/after badge (`.gallery-stage-badge`). A photo opens in `.gallery-lightbox` (`gallery.js`),
whose close button and caption stay on screen on a phone.

---

## 8. Admin Design System (`pages/admin.css`)

**Loaded outside the site stylesheet bundle**: it is not one of the files listed in Section 9. It
ships through its own `<link>` in the admin layout, so a class added here never appears on public
pages, and the load-order rules of Section 9 do not apply to it.

**The admin panel does not scale** (Section 1). Its layout has no `site-scale` class, so 1rem is
16px on every screen, and `admin.css` keeps its sizes in px. The shared files it also loads
(buttons, forms, utilities) are in rem and draw at their written size there.

### Shared admin vocabulary
| Class | Purpose |
| :--- | :--- |
| `.admin-card` | Standard bordered card (`surface-container-lowest` + `border-subtle` + `radius-xl`) |
| `.detail-label` | Uppercase field label, `letter-spacing:0.1em` |
| `.status-badge` | Pill badge, pair with a `.tint-*` utility for colour |
| `.admin-table-head` / `.admin-table-row` / `.admin-table` | List table chrome |
| `.avatar-badge` | Circular badge, pair with `.dim-*` (size) + `.tint-*` (colour) |
| `.financial-summary-card` | Dark sidebar summary card (job page) |
| `.admin-form-control` | Form input chrome matching the admin cards |
| `.help-card` | Sidebar help and info card |
| `.admin-status-filter` | The status `<select>` on the list pages |
| `.admin-sort-link` | Clickable sortable column header, `.active` → `--secondary` |
| `.faq-row` | A row of the FAQ builder (Service Area form) |

Also present: the sidebar and topbar shell (`.admin-sidebar`, `.admin-topbar`,
`.admin-nav-link`; the sidebar folds to icons under 1536 wide and becomes a drawer at 768 and
under), the Calendar page's slot and legend classes, the Dashboard's `.bento-grid` /
`.dashboard-stat-card` family (named `.dashboard-stat-card` to stay clear of the public
`.bento-card`), and page-specific helpers (`.admin-btn-compact`, `.form-control-price-affix`, and
others). See the file for the full list; the table above is the reusable core worth checking
first.

---

## 9. Stylesheet Load Order (`SiteStylesheets.cs`)

The list lives in `src/Web/HandyFix.Web/SiteStylesheets.cs`, not in a CSS file. On the live site
WebOptimizer combines these files, in this order, into one minified `/css/site.min.css` whose
address carries a hash of its content; in Development each file is linked by itself, in the same
order (`PROJECT_STATE.md` Section 3bs).

```
base/variables.css → reset.css → typography.css → layout.css → utilities.css → scale.css
components/navbar.css → footer.css → cookie-notice.css → logo.css → buttons.css → cards.css
          → forms.css → image-upload.css
pages/home.css → services.css → pricing.css → service-details.css → booking.css
      → service-category.css → areas.css → payment.css → auth.css → pages-info.css → gallery.css
print.css                (paper styles, after everything above)
icons.css                (Material Symbols defaults)
```
**A new stylesheet has to be added to that list**, at the end of its group. A file under
`wwwroot/css` that is not listed is not loaded at all; a test (`WebTests`) fails if one is left out.
Do not chain stylesheets with `@import`: an imported file has no version in its address, so browsers
keep old copies of it after a deploy.

`pages/admin.css` is **not** in this list (Section 8). Later files override earlier ones on equal
specificity, so the same class name defined in two listed files is a real hazard, not just
untidiness: the later one wins in silence.

`print.css` hides the header, the menus, the floating button and the sidebars on paper, and draws
the footer white.
