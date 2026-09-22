// Full L5 batch generator. Jobs live in batch-jobs.mjs; raw output goes to output/batch/<name>.jpg.
// Every call sends two approved images from the style test round (output/refs/) as style
// references, which is what keeps ~56 separate calls looking like one set.
//
//   node generate-batch.mjs              every job not generated yet (safe to re-run after a failure)
//   node generate-batch.mjs hero epsom   only these, regenerating even if the file exists
//
// Pass about five names at a time and check every image before the next batch: each call costs
// real money, and one unchecked full run repeated a face across ~15 images (PROJECT_STATE 3bd).

import fs from "node:fs";
import path from "node:path";
import { jobs } from "./batch-jobs.mjs";

const envText = fs.readFileSync(new URL("./.env", import.meta.url), "utf8");
const apiKey = envText.match(/GEMINI_API_KEY=(.+)/)[1].trim();

// Same bright style as generate-style-test.mjs, plus fixes for what that round got wrong:
// a real car badge on the hero van, and a painted paper border on one street scene. The shadow and
// face lines came from the first batch run: the man in the references kept reappearing.
const STYLE_PREFIX =
  "Bright, vibrant, contemporary watercolour illustration with clear, confident definition. " +
  "Rich saturated fresh colours, crisp contrast, bright direct natural sunlight with clean " +
  "shadows that match the people and objects casting them. Visible paper texture, clean " +
  "washes, controlled edges. Optimistic, modern and welcoming, never dark, sepia, faded or " +
  "nostalgic. Full-bleed: the painting fills the whole " +
  "frame edge to edge, with no border, margin, frame or unpainted paper edge. Any tradesperson " +
  "wears a plain navy polo shirt with no text or logo. Tools, appliances and vehicles are " +
  "unbranded, with no manufacturer badges or emblems. No text, no logos, no lettering, no " +
  "signage, no number plates, no watermark of any kind anywhere in the image.";

const REF_INSTRUCTION =
  "The attached images are style references only. Match their painting technique, colour " +
  "saturation, bright sunny lighting, line quality and level of detail, so the new image looks " +
  "like part of the same illustrated set. Do not copy their subjects, people, objects, buildings " +
  "or composition. In particular, never reuse the face or look of the man in the reference " +
  "images: draw every person exactly as the scene describes them.";

const REFS = {
  interior: ["interior-tap.jpg", "interior-shelf.jpg"],
  exterior: ["street-summer.jpg", "street-autumn.jpg"],
  outdoorJob: ["interior-shelf.jpg", "street-summer.jpg"],
};

const refData = Object.fromEntries(
  Object.entries(REFS).map(([key, files]) => [
    key,
    files.map((f) => ({
      inlineData: { mimeType: "image/jpeg", data: fs.readFileSync(path.join("output", "refs", f)).toString("base64") },
    })),
  ]),
);

const OUT_DIR = path.join("output", "batch");
fs.mkdirSync(OUT_DIR, { recursive: true });

async function callModel(job) {
  const body = {
    contents: [{
      parts: [
        { text: REF_INSTRUCTION },
        ...refData[job.refs],
        { text: `${STYLE_PREFIX} Scene: ${job.scene}` },
      ],
    }],
    generationConfig: {
      responseModalities: ["IMAGE"],
      imageConfig: { aspectRatio: job.aspectRatio, imageSize: "2K" },
    },
  };

  const res = await fetch(
    `https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent?key=${apiKey}`,
    { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) },
  );
  const text = await res.text();
  if (!res.ok) {
    return { error: `HTTP ${res.status}: ${text.slice(0, 300)}`, retry: [429, 500, 503].includes(res.status) };
  }

  const part = JSON.parse(text).candidates?.[0]?.content?.parts?.find((p) => p.inlineData);
  if (!part) {
    return { error: `no image in response: ${text.slice(0, 300)}`, retry: true };
  }
  return { buf: Buffer.from(part.inlineData.data, "base64") };
}

async function generate(job) {
  const outPath = path.join(OUT_DIR, `${job.name}.jpg`);
  for (let attempt = 1; attempt <= 3; attempt++) {
    const result = await callModel(job);
    if (result.buf) {
      fs.writeFileSync(outPath, result.buf);
      console.log(`[${job.name}] saved ${result.buf.length} bytes -> ${outPath}`);
      return true;
    }
    console.error(`[${job.name}] attempt ${attempt}: ${result.error}`);
    if (!result.retry) break;
    await new Promise((r) => setTimeout(r, 30000));
  }
  return false;
}

const only = process.argv.slice(2);
const failed = [];
let made = 0;

for (const job of jobs) {
  if (job.reuse) continue;
  if (only.length > 0 && !only.includes(job.name)) continue;
  if (only.length === 0 && fs.existsSync(path.join(OUT_DIR, `${job.name}.jpg`))) continue;

  if (await generate(job)) made++;
  else failed.push(job.name);
}

console.log(`\nDone: ${made} generated, ${failed.length} failed${failed.length ? ` (${failed.join(" ")})` : ""}.`);
