// Builds labelled contact sheets of the batch for review: output/review/<group>.jpg.
// One sheet per group, so ~60 images can be checked by opening five files.

import fs from "node:fs";
import path from "node:path";
import sharp from "sharp";
import { jobs } from "./batch-jobs.mjs";

const TILE_W = 520;
const COLS = 3;
const LABEL_H = 34;

const group = (job) =>
  job.out.startsWith("areas/") ? "areas"
    : !job.out.startsWith("services/") ? "pages"
      : job.name.endsWith("-category") ? "pages"
        : "services";

const source = (job) => job.reuse ?? path.join("output", "batch", `${job.name}.jpg`);

const escapeXml = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;");

async function tile(job, tileH) {
  const src = source(job);
  const img = fs.existsSync(src)
    ? await sharp(src).resize(TILE_W, tileH, { fit: "contain", background: "#ffffff" }).toBuffer()
    : await sharp({ create: { width: TILE_W, height: tileH, channels: 3, background: "#f3c4c4" } }).png().toBuffer();
  const label = Buffer.from(
    `<svg width="${TILE_W}" height="${LABEL_H}"><rect width="100%" height="100%" fill="#1e293b"/>` +
    `<text x="10" y="23" font-family="Arial" font-size="18" fill="#fff">${escapeXml(job.name)}${fs.existsSync(src) ? "" : " (MISSING)"}</text></svg>`,
  );
  return sharp({ create: { width: TILE_W, height: tileH + LABEL_H, channels: 3, background: "#ffffff" } })
    .composite([{ input: label, top: 0, left: 0 }, { input: img, top: LABEL_H, left: 0 }])
    .png()
    .toBuffer();
}

const OUT_DIR = path.join("output", "review");
fs.mkdirSync(OUT_DIR, { recursive: true });

const groups = {};
for (const job of jobs) (groups[group(job)] ??= []).push(job);

for (const [name, list] of Object.entries(groups)) {
  // Services are split into sheets of 15 so each tile stays big enough to spot stray text.
  const chunkSize = name === "services" ? 15 : list.length;
  for (let i = 0; i < list.length; i += chunkSize) {
    const chunk = list.slice(i, i + chunkSize);
    const tileH = name === "areas" ? 228 : name === "pages" ? 300 : TILE_W;
    const tiles = await Promise.all(chunk.map((job) => tile(job, tileH)));
    const rows = Math.ceil(tiles.length / COLS);
    const sheetName = chunkSize < list.length ? `${name}-${i / chunkSize + 1}` : name;
    await sharp({ create: { width: TILE_W * COLS, height: rows * (tileH + LABEL_H), channels: 3, background: "#cbd5e1" } })
      .composite(tiles.map((input, n) => ({ input, left: (n % COLS) * TILE_W, top: Math.floor(n / COLS) * (tileH + LABEL_H) })))
      .jpeg({ quality: 85 })
      .toFile(path.join(OUT_DIR, `${sheetName}.jpg`));
    console.log(`${sheetName}: ${chunk.length} images`);
  }
}
