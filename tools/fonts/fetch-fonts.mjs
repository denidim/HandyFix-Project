// Downloads the site's two web fonts from Google Fonts into wwwroot/fonts, so the pages load
// them from the site itself and never wait on Google's servers. Run it from this folder after
// adding an icon to icon-names.txt:
//   node fetch-fonts.mjs
// - Outfit, the text font: Google's two files for it, Latin and the extra Latin letters. Each is
//   a variable font holding every weight from 300 to 800.
// - Material Symbols Outlined, the icon font: only the icons in icon-names.txt, at the one size,
//   weight and grade icons.css uses, with fill on and off.
// No packages needed: Node 18 or later has fetch built in.
import fs from "node:fs";

const FONTS = "../../src/Web/HandyFix.Web/wwwroot/fonts";

// Google sends woff2 files only to a browser it knows can read them.
const BROWSER = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

const names = fs.readFileSync("icon-names.txt", "utf8")
  .split("\n")
  .map((line) => line.trim())
  .filter((line) => line && !line.startsWith("#"));

const sorted = [...names].sort();
if (names.join() !== sorted.join()) {
  throw new Error("icon-names.txt is not in alphabetical order, which Google asks for");
}

async function css(url) {
  const response = await fetch(url, { headers: { "User-Agent": BROWSER } });
  if (!response.ok) throw new Error(`${url}: ${response.status}`);
  return response.text();
}

// The file named in the @font-face block that follows the given /* subset */ comment, or the
// only one in the stylesheet when no subset is given.
function fileIn(stylesheet, subset) {
  const from = subset ? stylesheet.indexOf(`/* ${subset} */`) : 0;
  const match = /url\((https:[^)]+)\)/.exec(stylesheet.slice(from));
  if (from < 0 || !match) throw new Error(`no font file for ${subset ?? "the icons"}`);
  return match[1];
}

async function save(url, file) {
  const response = await fetch(url, { headers: { "User-Agent": BROWSER } });
  if (!response.ok) throw new Error(`${url}: ${response.status}`);
  fs.writeFileSync(`${FONTS}/${file}`, Buffer.from(await response.arrayBuffer()));
  console.log(`${file} (${Math.round(fs.statSync(`${FONTS}/${file}`).size / 1024)} KB)`);
}

fs.mkdirSync(FONTS, { recursive: true });

const outfit = await css("https://fonts.googleapis.com/css2?family=Outfit:wght@300..800&display=swap");
await save(fileIn(outfit, "latin"), "outfit-latin.woff2");
await save(fileIn(outfit, "latin-ext"), "outfit-latin-ext.woff2");

const icons = await css(
  "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0..1,0" +
  `&icon_names=${names.join(",")}&display=block`);
await save(fileIn(icons), "material-symbols-outlined.woff2");
console.log(`${names.length} icons`);
