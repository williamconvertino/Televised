// Renders SVGs to PNG previews. Never writes into ArtSource/Vector or Assets.
//
//   node render.mjs <svg|dir>... [--out <dir>] [--scale <n>] [--bg <color>]
//       One PNG per SVG (default out: ArtSource/AI_Workspace/previews).
//   node render.mjs --stack <out.png> <svg|dir>... [--scale <n>] [--bg <color>]
//       Stacks parts that share a canvas into one preview, first file at the bottom.
//       Also writes the combined .svg next to the PNG.
import fs from 'node:fs';
import path from 'node:path';
import { RepoRoot, readSvg, renderPng, stackSvgs, collectSvgs, parseArgs, sameBox } from './lib.mjs';

const { flags, positional } = parseArgs(process.argv.slice(2));
if (!positional.length) {
    console.error('usage: node render.mjs <svg|dir>... [--out dir] [--scale n] [--bg color]\n' +
                  '       node render.mjs --stack out.png <svg|dir>... [--scale n] [--bg color]');
    process.exit(1);
}
const scale = Number(flags.scale ?? 1);
const background = flags.bg;
const svgs = collectSvgs(positional).map(readSvg);

if (flags.stack) {
    const mismatched = svgs.filter(s => !sameBox(s.viewBox, svgs[0].viewBox));
    if (mismatched.length)
        console.warn(`warning: canvas differs from ${path.basename(svgs[0].file)}: ` +
                     mismatched.map(s => path.basename(s.file)).join(', '));
    const out = path.resolve(flags.stack);
    const combined = stackSvgs(svgs);
    fs.mkdirSync(path.dirname(out), { recursive: true });
    fs.writeFileSync(out.replace(/\.png$/i, '') + '.svg', combined);
    fs.writeFileSync(out, renderPng(combined, scale, { background }));
    console.log(`stacked ${svgs.length} -> ${path.relative(RepoRoot, out)}`);
} else {
    const outDir = path.resolve(flags.out ?? path.join(RepoRoot, 'ArtSource/AI_Workspace/previews'));
    fs.mkdirSync(outDir, { recursive: true });
    for (const s of svgs) {
        const out = path.join(outDir, path.basename(s.file, path.extname(s.file)) + '.png');
        fs.writeFileSync(out, renderPng(s.src, scale, { background, resourcesDir: path.dirname(s.file) }));
        console.log(`${path.basename(s.file)} -> ${path.relative(RepoRoot, out)}`);
    }
}
