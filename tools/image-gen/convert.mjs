// Converts Phase 0 raw output to WebP q80 at the correct target dimensions and copies
// straight into wwwroot at the real convention paths, for live preview on dev.
import sharp from "sharp";
import fs from "node:fs";

const WWWROOT = "../../src/Web/HandyFix.Web/wwwroot/images";

const jobs = [
  { raw: "output/hero-raw.jpg", out: `${WWWROOT}/hero.webp`, w: 1920, h: 1072 },
  { raw: "output/tap-repairs-raw.jpg", out: `${WWWROOT}/services/tap-repairs-hero.webp`, w: 1024, h: 1024 },
  { raw: "output/shelf-installation-raw.jpg", out: `${WWWROOT}/services/shelf-installation-hero.webp`, w: 1024, h: 1024 },
  { raw: "output/wall-floor-tiling-raw.jpg", out: `${WWWROOT}/services/wall-floor-tiling-hero.webp`, w: 1024, h: 1024 },
  { raw: "output/chessington-area-hero-raw.jpg", out: `${WWWROOT}/areas/chessington-hero.webp`, w: 1600, h: 700 },
];

for (const job of jobs) {
  fs.mkdirSync(job.out.substring(0, job.out.lastIndexOf("/")), { recursive: true });
  await sharp(job.raw)
    .resize(job.w, job.h, { fit: "cover", position: "attention" })
    .webp({ quality: 80 })
    .toFile(job.out);
  console.log(`${job.raw} -> ${job.out} (${job.w}x${job.h})`);
}
