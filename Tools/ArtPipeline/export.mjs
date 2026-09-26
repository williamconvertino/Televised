// Exports approved vector masters to Unity sprites. Overwrites production sprites, so only run it when asked.
//
//   node export.mjs <Category/Asset> [part...] [--dry]
//       e.g. node export.mjs Characters/Hungry
//            node export.mjs Characters/Hungry hungry_brow_L hungry_brow_R
//
// Reads   ArtSource/Vector/<Category/Asset>/*.svg   (+ optional export.json)
// Writes  Assets/Art/<Category/Asset>/Sprites/<part>.png and sprite_layout.json
//
// Every part of an asset is drawn on the same canvas (identical viewBox), so parts line up when stacked.
// Each PNG is cropped to its content; the crop, pivot and position go into sprite_layout.json, which
// Assets/Art/Editor/SpriteLayoutPostprocessor.cs applies on import (sprite pivot + pixels per unit).
//
// export.json (all optional):
//   scale             output pixels per canvas unit                     (default 2)
//   unitsPerWorldUnit canvas units per Unity world unit                 (default 100)
//   padding           transparent margin around each part, canvas units (default 2)
//   origin            [x, y] canvas point that maps to the asset's local (0, 0)  (default canvas centre)
//   layers            part names bottom-to-top; sets `order` in the layout and preview stacking
//                     (unlisted parts go on top, alphabetically)
import fs from 'node:fs';
import path from 'node:path';
import { RepoRoot, readSvg, withViewBox, renderPng, contentBBox, parseArgs, sameBox, readConfig, layerOrder } from './lib.mjs';

const { flags, positional } = parseArgs(process.argv.slice(2), ['dry']);
const [asset, ...only] = positional;
if (!asset) {
    console.error('usage: node export.mjs <Category/Asset> [part...] [--dry]');
    process.exit(1);
}

const srcDir = path.join(RepoRoot, 'ArtSource/Vector', asset);
const outDir = path.join(RepoRoot, 'Assets/Art', asset, 'Sprites');
const layoutPath = path.join(outDir, 'sprite_layout.json');
if (!fs.existsSync(srcDir)) { console.error(`no vector masters at ${path.relative(RepoRoot, srcDir)}`); process.exit(1); }

const config = readConfig(srcDir);
const scale = config.scale ?? 2;
const unitsPerWorldUnit = config.unitsPerWorldUnit ?? 100;
const padding = config.padding ?? 2;

let files = fs.readdirSync(srcDir).filter(f => f.toLowerCase().endsWith('.svg')).sort();
if (only.length) {
    const missing = only.filter(p => !files.includes(p + '.svg'));
    if (missing.length) { console.error(`not found in ${asset}: ${missing.join(', ')}`); process.exit(1); }
    files = only.map(p => p + '.svg');
}
if (!files.length) { console.error(`no .svg files in ${path.relative(RepoRoot, srcDir)}`); process.exit(1); }

const svgs = files.map(f => readSvg(path.join(srcDir, f)));
const canvas = svgs[0].viewBox;
for (const s of svgs)
    if (!sameBox(s.viewBox, canvas))
        throw new Error(`${path.basename(s.file)}: viewBox ${s.viewBox} differs from ${canvas}; all parts must share one canvas`);
const origin = config.origin ?? [canvas[0] + canvas[2] / 2, canvas[1] + canvas[3] / 2];

const previous = fs.existsSync(layoutPath) ? JSON.parse(fs.readFileSync(layoutPath, 'utf8')) : null;
if (previous && !sameBox(previous.canvas, canvas))
    console.warn('warning: canvas changed since the last export; re-export every part so positions stay consistent');
const parts = new Map((previous?.parts ?? []).map(p => [p.name, p]));

const snapDown = v => Math.floor(v * scale) / scale;
const snapUp = v => Math.ceil(v * scale) / scale;
const round = v => Math.round(v * 1e4) / 1e4;

if (!flags.dry) fs.mkdirSync(outDir, { recursive: true });
for (const s of svgs) {
    const name = path.basename(s.file, '.svg');
    const box = contentBBox(s.src);
    if (!box) { console.warn(`skip ${name}: nothing drawn`); continue; }

    const x0 = snapDown(box[0] - padding), y0 = snapDown(box[1] - padding);
    const x1 = snapUp(box[0] + box[2] + padding), y1 = snapUp(box[1] + box[3] + padding);
    const rect = [x0, y0, x1 - x0, y1 - y0];
    const [px, py] = s.pivot ?? [x0 + rect[2] / 2, y0 + rect[3] / 2];

    const out = path.join(outDir, name + '.png');
    const verb = fs.existsSync(out) ? 'update' : 'new   ';
    const size = `${Math.round(rect[2] * scale)}x${Math.round(rect[3] * scale)}`;
    console.log(`${flags.dry ? '[dry] ' : ''}${verb} ${path.relative(RepoRoot, out)} (${size})`);
    if (flags.dry) continue;

    fs.writeFileSync(out, renderPng(withViewBox(s.src, rect), scale, { resourcesDir: srcDir }));
    parts.set(name, {
        name,
        rect: rect.map(round),
        // Unity sprite pivot: normalized, origin bottom-left.
        pivot: { x: round((px - x0) / rect[2]), y: round(1 - (py - y0) / rect[3]) },
        // Pivot position in the asset's local space, Unity units, y up.
        position: { x: round((px - origin[0]) / unitsPerWorldUnit), y: round(-(py - origin[1]) / unitsPerWorldUnit) },
    });
}

if (!flags.dry) {
    const order = layerOrder([...parts.keys()], config.layers);
    for (const p of parts.values()) p.order = order.indexOf(p.name);
    const layout = {
        source: path.relative(RepoRoot, srcDir).replaceAll('\\', '/'),
        canvas, origin, scale, unitsPerWorldUnit,
        pixelsPerUnit: scale * unitsPerWorldUnit,
        parts: order.map(n => parts.get(n)),
    };
    fs.writeFileSync(layoutPath, JSON.stringify(layout, null, 2) + '\n');
    console.log(`wrote ${path.relative(RepoRoot, layoutPath)}`);
}
