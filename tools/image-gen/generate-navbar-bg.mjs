// Header background: a pale watercolour strip in the style of bg-services-category.webp, sent as
// the style reference. Raw output goes to output/navbar/<name>.jpg (PROJECT_STATE Section 3bj).
//
//   node generate-navbar-bg.mjs                      all variants not generated yet
//   node generate-navbar-bg.mjs nav-bg-2             only these, regenerating even if the file exists
//   node generate-navbar-bg.mjs --convert nav-bg-3   crop the chosen one to wwwroot/images/bg-navbar.webp

import fs from "node:fs";
import path from "node:path";
import sharp from "sharp";

// The header is about 1920x125, so a band that shape is cut from the middle of the 3168x1344
// output, where the wash runs left to right, and saved at 2400px wide for sharper screens.
if (process.argv[2] === "--convert") {
  const src = path.join("output", "navbar", `${process.argv[3]}.jpg`);
  const out = new URL("../../src/Web/HandyFix.Web/wwwroot/images/bg-navbar.webp", import.meta.url);
  const { width, height } = await sharp(src).metadata();
  const band = Math.round(width * 125 / 1920);
  await sharp(src)
    .extract({ left: 0, top: Math.round((height - band) / 2), width, height: band })
    .resize(2400, Math.round(2400 * band / width))
    .webp({ quality: 70, effort: 6 })
    .toFile(out.pathname.slice(1));
  console.log(`${src} -> wwwroot/images/bg-navbar.webp`);
  process.exit(0);
}

const envText = fs.readFileSync(new URL("./.env", import.meta.url), "utf8");
const apiKey = envText.match(/GEMINI_API_KEY=(.+)/)[1].trim();

const REF = fs.readFileSync(new URL("../../src/Web/HandyFix.Web/wwwroot/images/bg-services-category.webp", import.meta.url));

const REF_INSTRUCTION =
  "The attached image is a style reference only. Match its watercolour technique: soft wet-in-wet " +
  "washes, feathered blooms, visible cold-press paper texture and its colour family of sky blue, " +
  "rose pink and warm yellow. Do not copy its layout.";

const BASE =
  "Abstract watercolour wash on white paper, no objects, no scene. A long horizontal band where " +
  "soft colour flows from left to right: pale sky blue, then soft rose pink, then warm pale yellow, " +
  "then back to pale sky blue at the far right, so the left and right edges match and copies can " +
  "sit side by side without a seam. Light and airy, gentle translucent pigment with white paper " +
  "showing through. Full-bleed, no border or unpainted margin. No text, no logos, no lettering, " +
  "no watermark of any kind.";

const variants = [
  ["nav-bg-1", ""],
  ["nav-bg-2", "Even paler: the pigment gathers softly along the top and bottom edges and the middle band is almost white paper."],
  ["nav-bg-3", "The left fifth of the image is almost plain white paper, with the colour starting gently after it."],
  ["nav-bg-4", "Loose horizontal brush strokes rather than blooms, slightly more colour but still pale."],
];

const OUT_DIR = path.join("output", "navbar");
fs.mkdirSync(OUT_DIR, { recursive: true });

async function callModel(extra) {
  const body = {
    contents: [{
      parts: [
        { text: REF_INSTRUCTION },
        { inlineData: { mimeType: "image/webp", data: REF.toString("base64") } },
        { text: `${BASE} ${extra}`.trim() },
      ],
    }],
    generationConfig: {
      responseModalities: ["IMAGE"],
      imageConfig: { aspectRatio: "21:9", imageSize: "2K" },
    },
  };
  const res = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent?key=${apiKey}`,
    { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) },
  );
  const text = await res.text();
  if (!res.ok) return { error: `HTTP ${res.status}: ${text.slice(0, 300)}` };
  const part = JSON.parse(text).candidates?.[0]?.content?.parts?.find((p) => p.inlineData);
  return part ? { buf: Buffer.from(part.inlineData.data, "base64") } : { error: `no image: ${text.slice(0, 300)}` };
}

const only = process.argv.slice(2);
for (const [name, extra] of variants) {
  const out = path.join(OUT_DIR, `${name}.jpg`);
  if (only.length ? !only.includes(name) : fs.existsSync(out)) continue;
  const r = await callModel(extra);
  if (r.buf) { fs.writeFileSync(out, r.buf); console.log(`[${name}] saved ${r.buf.length} bytes`); }
  else console.error(`[${name}] ${r.error}`);
}
