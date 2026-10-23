// Makes the smaller copies of the site's heaviest pictures that phones are sent instead
// (PROJECT_STATE Section 3cn). Run it again after any of these pictures is replaced:
//   node make-phone-copies.mjs
// - every service picture: a copy 320 wide, {slug}-hero-320.webp, for the category page's cards
//   (88px on a phone). ImageStorageService writes the same copy for a picture uploaded in the
//   admin panel.
// - every service picture: a copy 640 wide, {slug}-hero-640.webp, for the service cards on the
//   home, area, pricing and service pages (about 340px on a phone, at most 422 on a computer;
//   PROJECT_STATE Section 3cz). A phone with a sharper screen still takes the picture itself.
// - every area's main picture: a crop of its middle, 928 wide, {slug}-hero-phone.webp. On a
//   phone the picture fills a box 400px tall, so it is drawn 914px wide and only the middle of it
//   shows: 928 of its 1600 pixels at the widest phone (575px), the same pixels as before.
// - the home page's main picture and the wide background: a crop of their middle, 720 wide. A
//   phone shows only a narrow strip from the middle of each (object-fit/background-size: cover,
//   centred), so the crop keeps every pixel a phone was shown.
// - the category pages' background: a copy 1080 wide, as wide as a phone screen draws it.
import sharp from "sharp";
import fs from "node:fs";

const WWWROOT = "../../src/Web/HandyFix.Web/wwwroot/images";
const report = (file, out) => console.log(`${file} -> ${out} (${Math.round(fs.statSync(out).size / 1024)} KB)`);

for (const file of fs.readdirSync(`${WWWROOT}/services`)) {
  if (!file.endsWith("-hero.webp")) continue;
  for (const width of [320, 640]) {
    const out = `${WWWROOT}/services/${file.replace(/\.webp$/, `-${width}.webp`)}`;
    await sharp(`${WWWROOT}/services/${file}`).resize({ width }).webp({ quality: 80 }).toFile(out);
    report(file, out);
  }
}

for (const file of fs.readdirSync(`${WWWROOT}/areas`)) {
  if (!file.endsWith("-hero.webp")) continue;
  const source = `${WWWROOT}/areas/${file}`;
  const { width, height } = await sharp(source).metadata();
  const out = `${WWWROOT}/areas/${file.replace(/\.webp$/, "-phone.webp")}`;
  await sharp(source)
    .extract({ left: Math.round((width - 928) / 2), top: 0, width: 928, height })
    .webp({ quality: 80 })
    .toFile(out);
  report(file, out);
}

for (const name of ["hero", "bg-cta-wide"]) {
  const source = `${WWWROOT}/${name}.webp`;
  const { width, height } = await sharp(source).metadata();
  const out = `${WWWROOT}/${name}-phone.webp`;
  await sharp(source)
    .extract({ left: Math.round((width - 720) / 2), top: 0, width: 720, height })
    .webp({ quality: 80 })
    .toFile(out);
  report(`${name}.webp`, out);
}

const out = `${WWWROOT}/bg-services-category-phone.webp`;
await sharp(`${WWWROOT}/bg-services-category.webp`).resize({ width: 1080 }).webp({ quality: 80 }).toFile(out);
report("bg-services-category.webp", out);
