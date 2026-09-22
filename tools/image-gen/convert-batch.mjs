// Converts the reviewed batch to WebP at the real wwwroot paths (sizes per batch-jobs.mjs).
// Run only after the review sheets are approved: this overwrites the live site images.
//
// Quality 70 at effort 6, not the q80 used elsewhere: these detailed, saturated paintings came out
// ~30% smaller with no difference visible at 100% zoom (hero 471KB -> 330KB), and the hero is the
// homepage's LCP image.
//
//   node convert-batch.mjs              every job whose source image exists
//   node convert-batch.mjs hero epsom   only these

import fs from "node:fs";
import path from "node:path";
import sharp from "sharp";
import { jobs } from "./batch-jobs.mjs";

const WWWROOT = "../../src/Web/HandyFix.Web/wwwroot/images";
const only = process.argv.slice(2);

for (const job of jobs) {
  if (only.length > 0 && !only.includes(job.name)) continue;

  const src = job.reuse ?? path.join("output", "batch", `${job.name}.jpg`);
  if (!fs.existsSync(src)) {
    console.warn(`[${job.name}] skipped: ${src} does not exist`);
    continue;
  }

  const out = path.join(WWWROOT, job.out);
  fs.mkdirSync(path.dirname(out), { recursive: true });
  await sharp(src)
    .resize(job.w, job.h, { fit: "cover", position: "attention" })
    .webp({ quality: 70, effort: 6 })
    .toFile(out);
  console.log(`${src} -> ${out} (${job.w}x${job.h})`);
}
