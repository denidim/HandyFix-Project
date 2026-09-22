// Phase 0 style-lock test batch. Reads GEMINI_API_KEY from .env, calls Nano Banana Pro
// (gemini-3-pro-image-preview), saves raw output to output/<name>-raw.jpg.
// Deliberately no brand-colour direction in any prompt -- colour is CSS-only (division
// overlay), never baked into the artwork. See PROJECT_STATE.md Section 4 Tier 1 item 1.

import fs from "node:fs";
import path from "node:path";

const envText = fs.readFileSync(new URL("./.env", import.meta.url), "utf8");
const apiKey = envText.match(/GEMINI_API_KEY=(.+)/)[1].trim();

const STYLE_PREFIX =
  "Soft watercolour and pastel gouache painting style, visible gentle brush strokes, " +
  "subtle paper texture, muted harmonious neutral warm pastel palette (no strong blue, " +
  "red or amber colour direction -- colour tinting is added afterward in CSS, never in " +
  "the artwork). Clean uncluttered composition, relaxed natural pose, not a heroic " +
  "stock-photo pose. No text, no logos, no lettering, no signage, no watermark of any " +
  "kind anywhere in the image.";

const jobs = [
  {
    name: "shelf-installation",
    aspectRatio: "1:1",
    scene:
      "A professional handyman mounting a floating wooden shelf on a living room wall, " +
      "using a cordless drill and a spirit level held up against the wall, bright tidy " +
      "British home interior, natural soft diffused daylight.",
  },
  {
    name: "wall-floor-tiling",
    aspectRatio: "1:1",
    scene:
      "A tradesperson kneeling and laying ceramic wall tiles in a bathroom, spreading " +
      "adhesive with a notched trowel, a neat stack of tiles waiting nearby, bright tidy " +
      "British bathroom, natural soft diffused daylight.",
  },
  {
    name: "chessington-area-hero",
    aspectRatio: "21:9",
    scene:
      "A gentle wide view of a leafy Surrey suburban street, British semi-detached homes " +
      "with driveways, mature trees lining the road, calm and welcoming, soft daylight, no " +
      "people needed.",
  },
  {
    name: "hero",
    aspectRatio: "16:9",
    scene:
      "A wide, calm view of a smart British home exterior with a plain unmarked work van " +
      "and a toolbox visible on the driveway, welcoming and professional feel, soft " +
      "daylight, suitable for a website hero banner.",
  },
  {
    name: "expert-david",
    aspectRatio: "1:1",
    scene:
      "A close head-and-shoulders portrait of a friendly professional plumber in his " +
      "30s-40s, warm approachable expression, wearing a plain collared work polo shirt " +
      "with no text or logo, softly blended painterly brush strokes that still clearly " +
      "preserve his individual facial structure and identity (representational, not " +
      "abstract or impressionistic), even soft studio-like lighting, plain neutral " +
      "background, portrait framing.",
  },
];

async function generate(job) {
  const body = {
    contents: [{ parts: [{ text: `${STYLE_PREFIX} Scene: ${job.scene}` }] }],
    generationConfig: {
      responseModalities: ["IMAGE"],
      imageConfig: { aspectRatio: job.aspectRatio },
    },
  };

  const res = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent?key=${apiKey}`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    },
  );

  const text = await res.text();
  if (!res.ok) {
    console.error(`[${job.name}] HTTP ${res.status}: ${text.slice(0, 500)}`);
    return null;
  }

  const data = JSON.parse(text);
  const part = data.candidates?.[0]?.content?.parts?.find((p) => p.inlineData);
  if (!part) {
    console.error(`[${job.name}] no image in response:`, text.slice(0, 500));
    return null;
  }

  const buf = Buffer.from(part.inlineData.data, "base64");
  const outPath = path.join("output", `${job.name}-raw.jpg`);
  fs.writeFileSync(outPath, buf);
  console.log(`[${job.name}] saved ${buf.length} bytes -> ${outPath}`);
  return buf.length;
}

for (const job of jobs) {
  await generate(job);
}
