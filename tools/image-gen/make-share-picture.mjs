// Makes the pictures a chat or social app shows beside a link to the site (WhatsApp, Facebook):
// share.jpg for every page that has no picture of its own (PROJECT_STATE Section 3da), and
// areas/{slug}-share.jpg for each town's page, from the town's own picture (Section 3fi).
// Run it again after the home page's picture, a town's picture or the logo changes:
//   node make-share-picture.mjs
// - 1200x630, the shape these apps draw a link's picture in, cut from the picture's middle.
// - the logo on a white card in the top left corner, so the link reads as ours at a glance.
// - JPEG, because these apps do not all draw a WebP, and kept well under 300 KB: WhatsApp
//   shows no picture at all for a larger one.
import sharp from "sharp";
import fs from "node:fs";

const IMAGES = "../../src/Web/HandyFix.Web/wwwroot/images";

const logo = await sharp(`${IMAGES}/email/logo-header.png`).resize(440).toBuffer();
const { width: logoWidth, height: logoHeight } = await sharp(logo).metadata();
const padX = 28;
const padY = 22;
const cardWidth = logoWidth + padX * 2;
const cardHeight = logoHeight + padY * 2;
const card = Buffer.from(
  `<svg xmlns="http://www.w3.org/2000/svg" width="${cardWidth}" height="${cardHeight}">` +
  `<rect width="${cardWidth}" height="${cardHeight}" rx="22" fill="#ffffff" fill-opacity="0.94"/></svg>`);

async function makeSharePicture(source, out) {
  const picture = await sharp(source).resize(1200, 630, { fit: "cover", position: "centre" }).toBuffer();

  await sharp(picture)
    .composite([
      { input: card, left: 36, top: 36 },
      { input: logo, left: 36 + padX, top: 36 + padY },
    ])
    .jpeg({ quality: 82, mozjpeg: true })
    .toFile(out);

  console.log(`${out} (${Math.round(fs.statSync(out).size / 1024)} KB)`);
}

await makeSharePicture(`${IMAGES}/hero.webp`, `${IMAGES}/share.jpg`);

for (const file of fs.readdirSync(`${IMAGES}/areas`)) {
  if (!file.endsWith("-hero.webp")) continue;
  await makeSharePicture(`${IMAGES}/areas/${file}`, `${IMAGES}/areas/${file.replace(/-hero\.webp$/, "-share.jpg")}`);
}
