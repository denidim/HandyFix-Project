// Bright-style test round. Same five Phase 0 subjects as generate-phase0.mjs, re-run with the
// new bright/saturated style, plus three extra Chessington seasons to test area variety.
// Saves raw output to output/<name>-v2.jpg so the old -raw.jpg files stay for side-by-side
// comparison. Still no brand colour baked into scenes and no text/logos (vision doc, Section 2);
// the only fixed brand element is the plain navy uniform.

import fs from "node:fs";
import path from "node:path";

const envText = fs.readFileSync(new URL("./.env", import.meta.url), "utf8");
const apiKey = envText.match(/GEMINI_API_KEY=(.+)/)[1].trim();

const STYLE_PREFIX =
  "Bright, vibrant, contemporary watercolour illustration with clear, confident definition. " +
  "Rich saturated fresh colours, crisp contrast, bright direct natural sunlight with clean " +
  "shadows. Visible paper texture, clean washes, controlled edges. Optimistic, modern and " +
  "welcoming, never dark, sepia, faded or nostalgic. Any tradesperson wears a plain navy polo " +
  "shirt with no text or logo. No text, no logos, no lettering, no signage, no number plates, " +
  "no watermark of any kind anywhere in the image.";

const STREET =
  "A wide view of a leafy Surrey suburban street in Chessington, British semi-detached homes " +
  "with front gardens and driveways, mature trees lining the road, calm and welcoming, no " +
  "people needed.";

const jobs = [
  {
    name: "hero",
    aspectRatio: "16:9",
    scene:
      "A wide view of a smart British home exterior on a sunny summer morning, a plain " +
      "unmarked navy work van with no writing on the driveway and a toolbox beside it, " +
      "welcoming and professional, suitable for a website hero banner.",
  },
  {
    name: "tap-repairs",
    aspectRatio: "1:1",
    scene:
      "A plumber fitting a new chrome mixer tap on a kitchen sink, spanner in hand, bright " +
      "modern British kitchen, sunlight streaming through the window.",
  },
  {
    name: "shelf-installation",
    aspectRatio: "1:1",
    scene:
      "A handyman mounting a floating wooden shelf on a living room wall with a cordless " +
      "drill and a spirit level, bright tidy British home interior, sunlight through the window.",
  },
  {
    name: "wall-floor-tiling",
    aspectRatio: "1:1",
    scene:
      "A tradesperson kneeling and laying ceramic wall tiles in a bathroom, spreading adhesive " +
      "with a notched trowel, a neat stack of tiles nearby, bright clean British bathroom, " +
      "sunlight through a frosted window.",
  },
  {
    name: "chessington-area-hero-summer",
    aspectRatio: "21:9",
    scene: `${STREET} High summer, lush green trees, deep blue sky, strong midday sun.`,
  },
  {
    name: "chessington-area-hero-spring",
    aspectRatio: "21:9",
    scene: `${STREET} Spring, pink and white blossom trees in full bloom, fresh green lawns, bright morning sun.`,
  },
  {
    name: "chessington-area-hero-autumn-rain",
    aspectRatio: "21:9",
    scene:
      `${STREET} Autumn, just after a rain shower: golden and red leaves, wet road with ` +
      "colourful reflective puddles, sun breaking through, bright blue sky, clearly sunny not gloomy.",
  },
  {
    name: "chessington-area-hero-winter",
    aspectRatio: "21:9",
    scene:
      `${STREET} Crisp frosty winter morning: bare trees, light frost on lawns and rooftops, ` +
      "clear bright blue sky, low golden sunshine, fresh and cheerful not grey.",
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
  const outPath = path.join("output", `${job.name}-v2.jpg`);
  fs.writeFileSync(outPath, buf);
  console.log(`[${job.name}] saved ${buf.length} bytes -> ${outPath}`);
  return buf.length;
}

// Optional filter: `node generate-style-test.mjs hero tap-repairs` runs only those jobs,
// so a single bad image can be redone without paying for the whole round again.
const only = process.argv.slice(2);
for (const job of jobs) {
  if (only.length === 0 || only.includes(job.name)) {
    await generate(job);
  }
}
