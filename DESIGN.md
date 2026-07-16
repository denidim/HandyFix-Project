# Design DNA System - Handy Fix

This document defines the core visual design guidelines, typography, color palette, spacing, and component behaviors extracted from the existing Handy Fix frontend files.

---

## 🎨 Color Palette

The theme uses a premium, modern slate-based color scheme with cyan accents and emerald indicators for actions.

### Core Variables (`:root`)
| Variable | Hex Code | Description | Visual Sample |
| :--- | :--- | :--- | :--- |
| `--primary` | `#0f172a` | Dark slate primary brand color | Dark Slate |
| `--primary-light` | `#1e293b` | Medium slate for hover states, borders, and gradients | Medium Slate |
| `--accent` | `#0ea5e9` | Cyan accent for highlights, focus borders, and hovers | Cyan |
| `--accent-hover` | `#0284c7` | Darker cyan for active accent hover states | Dark Cyan |
| `--success` | `#10b981` | Emerald green for successful booking actions & active states | Emerald |
| `--background` | `#f8fafc` | Soft slate layout background | Soft Gray-Blue |
| `--text-main` | `#334155` | Slate gray for standard body copy | Muted Gray |
| `--text-dark` | `#0f172a` | Solid deep slate for headings and bold titles | Deep Slate |
| `--border` | `#e2e8f0` | Cool light gray for dividing borders and container edges | Light Gray |

### Specialty Backgrounds & Gradients
* **Hero Background / Callout Gradient** (`.bg-gradient-cyan`):
  ```css
  background: linear-gradient(135deg, var(--primary), var(--primary-light));
  ```
* **Glassmorphism** (`.booking-widget-card`):
  ```css
  background-color: rgba(255, 255, 255, 0.65);
  border: 1px solid rgba(255, 255, 255, 0.4);
  backdrop-filter: blur(10px);
  ```

---

## ✍️ Typography & Hierarchy

The application relies on a single high-quality sans-serif font family.

* **Primary Font Family**: `'Outfit', sans-serif`
  * **Import URL**: `https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700;800&display=swap`
* **Heading Styling** (`h1, h2, h3, h4, h5, h6, .navbar-brand`):
  * **Color**: `var(--text-dark)`
  * **Weight**: `700`
* **Brand Logo** (`.navbar-brand`):
  * **Weight**: `800`
  * **Spacing**: `letter-spacing: -0.5px`
  * **Accent Dot**: Trailing cyan dot (`.navbar-brand:after { content: " ."; color: var(--accent); }`)
* **Navigation Links** (`.nav-link`):
  * **Weight**: `500`
  * **Transition**: `color 0.2s ease`
* **Primary Button Text** (`.btn-primary`, `.btn-success`):
  * **Weight**: `600`

---

## 🧱 Layout & Spacing Rules

### Grid Layouts
* **Service Overview Grid**:
  * Uses a two-column grid on mobile (`col-6`) transitioning to larger columns on desktop.
  * Mobile gap uses `g-2` to maximize screen real estate, upgrading to `g-md-4` on larger viewports.

### Mobile-First Sticky Action Bar
To ensure high conversion rates on mobile devices, a persistent floating action footer is implemented for screens smaller than `768px`:
* **Container Class**: `.mobile-sticky-cta-footer`
* **CSS Properties**:
  ```css
  display: flex;
  position: fixed;
  bottom: 0;
  left: 0;
  width: 100%;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(10px);
  border-top: 1px solid var(--border);
  padding: 10px 16px;
  z-index: 1030;
  box-shadow: 0 -4px 12px rgba(0, 0, 0, 0.08);
  ```
* **Page Adjustments**: When active, the body margin-bottom is expanded (`80px` on mobile, `70px` on desktop) to prevent content from being clipped behind the footer bar.

---

## ✨ Micro-Animations & Hover Effects

* **Scale Grow Effect** (`.hover-grow`):
  ```css
  transition: transform 0.2s ease;
  &:hover {
      transform: scale(1.02);
  }
  ```
* **Interactive Category Cards** (`.category-card`):
  * Transition style: `all 0.3s cubic-bezier(0.4, 0, 0.2, 1)`
  * Hover transform: `translateY(-5px)`
  * Hover shadow: `box-shadow: 0 10px 20px rgba(15, 23, 42, 0.08) !important`
  * Hover border-color: `var(--accent)`
* **Primary Buttons**:
  * Border Radius: `8px`
  * Hover transition: `all 0.2s ease`
  * Hover state: `transform: translateY(-1px)` with background changing from `--primary` to `--primary-light`.
