# Design DNA System — HandyFix

This document is a reference, not a history: it describes the design tokens, shared classes, and
page-shell patterns that actually exist in `wwwroot/css/` today, organized so a future contributor
checks here **before** inventing a one-off class or reaching for an inline `style=`. See
`PROJECT_STATE.md` for the reasoning and dates behind individual pieces of this system.

---

## 1. Design Tokens (`base/variables.css`)

Everything below is a CSS custom property on `:root`. Two token generations coexist and are both
live — the original hand-named set (`--primary`, `--accent`, `--success`, `--border-subtle`, …) and
a larger, later "Material You"-style extended palette (`--surface-*`, `--on-*`, `--secondary`,
`--tertiary`, `--error`, `--*-fixed`, …). Newer components (Services/Areas/Pricing pages, the admin
panel) lean on the extended palette — in particular **`--secondary` (`#006591`) is the accent color
used throughout current pages**, not the older `--accent` (`#0ea5e9`). When in doubt which one a
given component uses, check `variables.css` directly rather than assuming.

> Note: the original version of this doc named the border token `--border`. It is now
> **`--border-subtle`** (same hex value, `#e2e8f0`) — the rename predates this rewrite.

### Core colors (original set)
| Variable | Value | Notes |
| :--- | :--- | :--- |
| `--primary` | `#0f172a` | Dark slate — headings, admin sidebar |
| `--primary-light` | `#1e293b` | Hover/gradient partner for `--primary` |
| `--accent` | `#0ea5e9` | Cyan — original nav/button accent |
| `--accent-hover` | `#0284c7` | Darker cyan hover state |
| `--success` | `#10b981` | Emerald — success/active states |
| `--background` | `#f8fafc` | Page background |
| `--text-main` | `#334155` | Body copy |
| `--text-dark` | `#0f172a` | Headings/bold titles |
| `--border-subtle` | `#e2e8f0` | Dividers, container borders |

### Extended palette (current accent + surfaces)
| Variable | Value | Variable | Value |
| :--- | :--- | :--- | :--- |
| `--secondary` | `#006591` | `--secondary-container` | `#39b8fd` |
| `--secondary-fixed` | `#c9e6ff` | `--secondary-fixed-dim` | `#89ceff` |
| `--tertiary` | `#000000` | `--tertiary-container` | `#002113` |
| `--tertiary-fixed` | `#6ffbbe` | `--tertiary-fixed-dim` | `#4edea3` |
| `--error` | `#ba1a1a` | `--error-container` | `#ffdad6` |
| `--primary-container` | `#131b2e` | `--primary-fixed` / `-dim` | `#dae2fd` / `#bec6e0` |
| `--outline` | `#76777d` | `--outline-variant` | `#c6c6cd` |
| `--surface-container-lowest` | `#ffffff` | `--surface-container` | `#eceef0` |
| `--surface-container-low/high/highest` | `#f2f4f6` / `#e6e8ea` / `#e0e3e5` | `--on-surface` / `-variant` | `#191c1e` / `#45464d` |
| `--on-secondary-container` | `#004666` | `--on-tertiary-container` | `#009668` |

Every `--on-*` token is the readable text/icon color for its matching container. Full list (all
`surface`/`on-*`/`*-fixed` pairs) lives in `variables.css` — not reproduced exhaustively here.

### Glass, spacing, radii, shadows
| Group | Values |
| :--- | :--- |
| Glass | `--glass-bg: rgba(255,255,255,0.65)`, `--glass-border: rgba(255,255,255,0.4)` |
| Spacing scale | `--spacing-1` … `--spacing-32` = `0.25rem` → `8rem` (1=0.25rem, 2=0.5rem, 3=0.75rem, 4=1rem, 6=1.5rem, 8=2rem, 10=2.5rem, 12=3rem, 16=4rem, 20=5rem, 24=6rem, 32=8rem) |
| Radii | `--radius-sm:0.25rem` `-md:0.5rem` `-lg:0.75rem` `-xl:1rem` `-2xl:1.5rem` `-full:9999px` |
| Shadows | `--shadow-sm` → `--shadow-2xl`, plus `--shadow-ambient` for the original card hover effect |
| Transitions | `--transition-fast: 0.2s ease`, `-normal: 0.3s cubic-bezier(0.4,0,0.2,1)`, `-slow: 0.5s ease`, `-hero: 0.7s ease` |
| Z-index | base `1`, dropdown `40`, sticky `50`, overlay `90`, modal `100`, mobile-cta `1030` |

---

## 2. Typography (`base/typography.css`)

**Font**: `'Outfit', sans-serif` for both body and display (`--font-family-base` /
`--font-family-display`), loaded via Google Fonts in `site.css`.

Headings (`h1`–`h6`) default to `var(--font-family-display)` + `color: var(--primary)`.

### Font-size scale (`.font-*`)
Each class sets font-size + line-height + weight together — don't combine `fs-*` (utilities.css,
raw pixel size only) with these when a semantic size already exists.

| Class | Size / Line-height / Weight | Responsive? |
| :--- | :--- | :--- |
| `.font-display-brand` | 24px / 32px / 800, `letter-spacing:-0.5px` | — |
| `.font-headline-xl` | 32px/40px/700 → **48px/56px/700 at ≥768px** | yes |
| `.font-headline-lg` | 28px/36px → **32px/40px at ≥768px** | yes |
| `.font-headline-md` | 24px / 32px / 700 | — |
| `.font-headline-sm` | 20px / 28px / 600 | — |
| `.font-body-lg` | 18px / 28px / 400 | — |
| `.font-body-md` | 16px / 24px / 400 | — |
| `.font-nav-link` | 16px / 24px / 500 | — |
| `.font-button-text` | 16px / 24px / 600 | — |
| `.font-label-sm` | 14px / 20px / 500 | — |

### Color-token text classes (all `!important`)
`.text-primary`, `.text-secondary`, `.text-text-main`, `.text-on-surface-variant`, `.text-white`
(+ `-80`/`-90` opacity variants), `.text-on-primary-container`, `.text-on-surface`,
`.text-on-secondary-container`, `.text-on-tertiary-container`, `.text-outline`, `.text-error`.

---

## 3. Utility Classes (`base/utilities.css`)

Check this list before adding a new one-off class or `style=` attribute — most spacing/sizing needs
are already covered.

| Category | Classes |
| :--- | :--- |
| Margin | `.mt-0/1/2/3/4/6/8/10/12/16`, `.mb-0/1/2/3/4/6/8/10/12/16` (values from the spacing scale) |
| Padding | `.pt-4`, `.pt-6`, `.pb-4`, `.pb-6`, `.pl-4` — **only these five exist**, no `pr-*` |
| Line-height | `.lh-tight` (1.25), `.lh-snug` (1.375), `.lh-normal` (1.5), `.lh-relaxed` (1.625), `.lh-24` (24px) |
| Font size | `.fs-9` … `.fs-48` (raw px), `.fs-headline-lg` (token-based) |
| Font weight / align | `.font-bold` (700), `.font-medium` (500), `.text-right`, `.text-left` |
| Max width | `.max-w-xl` (36rem), `-2xl` (42rem), `-3xl` (48rem), `-7xl` (80rem) |
| Border radius (`!important`) | `.rounded-lg`, `.rounded-xl`, `.rounded-full` |
| Opacity | `.opacity-70/80/90` (fills the gap above Bootstrap's 25/50/75/100) |
| Fixed dimensions | `.dim-32/36/40/48/56` (square, e.g. avatars/icon boxes) |
| Tint backgrounds | `.tint-neutral`, `.tint-error(-soft)`, `.tint-success`, `.tint-secondary(-soft/-faint)`, `.tint-info`, `.tint-neutral-soft`, `.tint-black-soft`, `.tint-tertiary-fill`, `.tint-secondary-fixed`, `.tint-primary-fixed-dim`, `.tint-surface-high` |
| Icon variation | `.icon-fill`, `.icon-unfill`, `.icon-medium-weight` (Material Symbols `FILL`/`wght` axes) |
| Tailwind-parity misc | `.uppercase`, `.italic`, `.leading-relaxed`, `.select-all`, `.block`, `.text-xs`, `.tracking-wide`, `.tracking-wider` |
| Behavior | `.resize-none`, `.pre-wrap`, `.text-no-decoration`, `.inline-form-reset`, `.btn-link-reset`, `.z-1` |

**Why `font-bold`/`rounded-*`/`max-w-*`/etc. exist here at all**: every one of these was referenced
across views under the assumption it was a Bootstrap or Tailwind class, but wasn't defined anywhere
— a silent no-op sitewide until each was added for real (see `PROJECT_STATE.md` Section 3a). Check
this file before assuming a class "looks standard enough" to already work.

---

## 4. Page Shells

Two shell patterns cover nearly every page. Pick the one that matches the page's shape rather than
composing a new one.

### Template A — hero + breadcrumb (`pages/services.css`)
Used by: Services Index/Category/Pricing, Areas Index, About, Contact, Terms, Privacy, FAQ, Reviews.

```css
.page-container      /* base/layout.css: width: min(1320px, calc(100% - 48px)); margin-inline: auto; */
.breadcrumb-nav       /* flex row, gap 0.5rem, .breadcrumb-current is bold + --secondary */
.services-header-content        /* max-width: 800px */
.services-header-content.text-center   /* modifier: centers the block — used by Contact/Terms/Privacy/FAQ */
.services-hero-title  /* --fs-headline-xl, drops to -xl-mobile below 768px; <span> inside is --secondary */
.services-subtitle
```
Left-aligned by default (Services/Areas/Pricing/About); add `.text-center` for the pages that want
the hero centered instead (Contact/Terms/Privacy/FAQ) rather than inventing a second hero class.

### Template B — thin, centered, no breadcrumb
There is no single shared class pair for this shape — each thin form-style page has its own
page-scoped classes following the same *concept* (centered card, capped width, no breadcrumb):

| Page | Classes | File |
| :--- | :--- | :--- |
| Login / Register | `.auth-page-container` / `.auth-card` (max 450px, `.wide` → 550px) | `pages/auth.css` |
| Payment result | `.payment-page-container` / `.payment-status-card` (max 576px) | `pages/payment.css` |
| Booking confirmed | `.confirmed-page-container` / `.success-container` (max 600px) | `pages/booking.css` |

If adding a new thin-page, follow this pattern (own page-scoped class, capped `max-width`, centered)
rather than trying to force-fit one of the three existing class names.

### `.info-canvas` (`pages/pages-info.css`)
Plain positive-margin content wrapper (`max-width:800px; margin:3rem auto`) used inside Template-A
info pages for the body content below the hero. `.info-canvas.wide` is meant to widen this to
`--max-width-desktop` — **that variable is never defined**, so the modifier is currently a no-op
(see Known Issues).

---

## 5. Admin Design System (`pages/admin.css`)

886 lines, and **loaded outside the `site.css` `@import` chain** — it's not one of the partials
listed in Section 8 below. It ships via its own `<link>` in the admin layout, so a class added here
never appears on public pages and vice versa; don't expect `@import` order rules from Section 8 to
apply to it.

### Shared admin vocabulary
| Class | Purpose |
| :--- | :--- |
| `.admin-card` | Standard bordered card (`surface-container-lowest` + `border-subtle` + `radius-xl`) — replaces the repeated inline `border-radius`/`border` pair |
| `.detail-label` | Uppercase field label, `letter-spacing:0.1em` (fixes a past bug: the inline version used the invalid CSS property `tracking` instead of `letter-spacing`) |
| `.status-badge` | Pill badge, pair with a `.tint-*` utility for color |
| `.admin-table-head` / `.admin-table-row` / `.admin-table` | List table chrome |
| `.avatar-badge` | Circular badge, pair with `.dim-*` (size) + `.tint-*` (color) |
| `.financial-summary-card` | Dark sidebar summary card (booking details page) |
| `.admin-form-control` | Form input chrome matching admin card styling |
| `.help-card` | Sidebar help/info card |
| `.admin-status-filter` | The status `<select>` used on Bookings/Reviews list pages |
| `.admin-sort-link` | Clickable sortable column header, `.active` state → `--secondary` |
| `.faq-row` | Dynamic FAQ-builder row (Service Area form) |

Also present: the full sidebar/topbar shell (`.admin-sidebar`, `.admin-topbar`, `.admin-nav-link`,
mobile drawer at `≤768px`), the Calendar page's slot/legend classes, the Dashboard's
`.bento-grid`/`.bento-card`/`.health-progress-*` family (note the `.bento-card` name collision below),
and page-specific form/button helpers (`.admin-btn-compact`, `.form-control-price-affix`, etc.) — see
the file directly for the full list; the table above is the reusable core worth checking first.

---

## 6. Card Components (`components/cards.css`)

```css
.glass-card { background: var(--glass-bg); backdrop-filter: blur(12px);
              border: 1px solid var(--glass-border); border-radius: var(--radius-xl);
              box-shadow: var(--shadow-2xl); }
```
Single real definition (a past duplicate in `pages-info.css` was removed — see `PROJECT_STATE.md`
Section 3g). Use for hero/overlay translucent contexts only.

**`.pricing-card` / `.pricing-cards-grid` do not live in this file** — despite the name suggesting
otherwise, both are defined in `pages/pricing.css`. `.pricing-card` is an *opaque* content card
(`surface-container-lowest` background, `shadow-lg` → `shadow-xl` on hover) — deliberately not the
translucent `.glass-card` treatment.

Also here: `.division-card` (category tiles), `.testimonial-card` (translucent, dark-background
contexts), and two class names that collide with a second, different definition elsewhere — see
Known Issues.

---

## 7. Area Components (`pages/areas.css`)

**`.area-card` / `.area-card-grid`** — the card used by `AreaCardViewComponent` everywhere areas are
listed (Areas Index, Area Details "Nearby Areas", the footer). Bordered card, `border-color` →
`--secondary` + `translateY(-2px)` on hover, `.area-card-badge` for the "Featured" pill.

**Coverage-map SVG classes** — back `_AreaCoverageMap.cshtml`: `.coverage-ring`/`-spoke` (dashed
guide lines), `.coverage-node`/`.coverage-dot`/`.coverage-label` (each plotted town, `--secondary`
stroke, bolds + thickens on hover/focus), `.coverage-hub`/`.coverage-hub-halo` (the Chessington
base), and the mobile fallback `.coverage-list-*`/`.coverage-pill`. The two layouts swap at exactly
`768px`: pill list by default, SVG diagram (`display:block`) at `≥768px`. The SVG itself is capped
`max-width:880px` — its `viewBox` is 780 units wide, so an uncapped diagram on a large screen would
render its 15px labels at roughly double size.

Note: `pages-info.css` independently defines an unrelated `.coverage-grid`/`.coverage-card` pair
(plain content cards on the info/service-areas page) — same word, no actual class-name collision,
just easy to confuse when searching for "coverage".

---

## 8. Stylesheet Load Order (`site.css`)

```
base/variables.css → base/reset.css → base/typography.css → base/layout.css → base/utilities.css
components/navbar.css → footer.css → buttons.css → cards.css → forms.css → image-upload.css
pages/home.css → services.css → pricing.css → service-details.css → booking.css
      → service-category.css → areas.css → payment.css → auth.css → pages-info.css
```
`pages/admin.css` is **not** in this chain (Section 5). Later files can override earlier ones on
equal specificity — this is exactly how the old duplicate `.glass-card` in `pages-info.css` won
silently before it was removed, so a genuine duplicate class name in two `@import`ed files is a real
footgun, not just untidiness.

---

## 9. Known Issues (documented, not fixed here)

- **`.bento-card` is defined twice** with different rules: `components/cards.css` (public image
  bento tiles, hover zoom + gradient overlay) vs. `pages/admin.css` (Dashboard stat cards, plain
  card). They don't collide in practice only because `admin.css` isn't in the `site.css` chain — if
  that ever changes, one definition will silently win over the other.
- **`.trust-card` is defined twice** with different rules: `components/cards.css` (flex row, icon +
  text, opaque `surface-container-lowest`) vs. `pages-info.css` (centered column layout). Both are
  in the `site.css` chain, so `pages-info.css` currently wins wherever both could apply, per import
  order.
- **`--max-width-desktop`** is referenced by `.info-canvas.wide` but never defined anywhere in
  `variables.css` — the modifier currently has no effect.
