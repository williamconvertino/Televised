// Shared SVG helpers for the art pipeline scripts. See ART_PIPELINE.md at the repo root.
import { Resvg } from '@resvg/resvg-js';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const RepoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

const RootTag = /<svg\b[^>]*>/i;

/** Reads an SVG and returns its source, canvas viewBox [x, y, w, h] and optional data-pivot [x, y]. */
export function readSvg(file) {
    const src = fs.readFileSync(file, 'utf8');
    const root = src.match(RootTag)?.[0];
    if (!root) throw new Error(`${file}: no <svg> root element`);
    const attr = name => root.match(new RegExp(`\\s${name}\\s*=\\s*["']([^"']*)["']`, 'i'))?.[1];
    const nums = s => s?.trim().split(/[\s,]+/).map(Number);

    let viewBox = nums(attr('viewBox'));
    if (!viewBox) {
        const w = parseFloat(attr('width')), h = parseFloat(attr('height'));
        if (!(w > 0 && h > 0)) throw new Error(`${file}: needs a viewBox (or width and height)`);
        viewBox = [0, 0, w, h];
    }
    const pivot = nums(attr('data-pivot')) ?? null;
    return { file, src, viewBox, pivot };
}

/** Returns the SVG source with the root element's viewBox replaced and width/height removed. */
export function withViewBox(src, [x, y, w, h]) {
    return src.replace(RootTag, tag => tag
        .replace(/\s(viewBox|width|height)\s*=\s*["'][^"']*["']/gi, '')
        .replace(/^<svg\b/i, `<svg viewBox="${x} ${y} ${w} ${h}"`));
}

/**
 * Renders SVG source to PNG bytes. `scale` is output pixels per viewBox unit.
 * Scaling is done by setting the root width/height, because resvg-js ignores `fitTo` whenever `resourcesDir` is set.
 */
export function renderPng(src, scale, { background, resourcesDir } = {}) {
    const root = src.match(RootTag)?.[0] ?? '';
    const viewBox = root.match(/\sviewBox\s*=\s*["']([^"']*)["']/i)?.[1].trim().split(/[\s,]+/).map(Number);
    if (viewBox?.length === 4)
        src = src.replace(RootTag, tag => tag
            .replace(/\s(width|height)\s*=\s*["'][^"']*["']/gi, '')
            .replace(/^<svg\b/i, `<svg width="${viewBox[2] * scale}" height="${viewBox[3] * scale}"`));
    const resvg = new Resvg(src, {
        fitTo: viewBox?.length === 4 ? { mode: 'original' } : { mode: 'zoom', value: scale },
        background,
        resourcesDir,
        font: { loadSystemFonts: true },
    });
    return resvg.render().asPng();
}

/** Bounding box of the drawn content (including strokes) in viewBox units, or null if nothing is drawn. */
export function contentBBox(src) {
    const b = new Resvg(src).getBBox();
    return b && b.width > 0 && b.height > 0 ? [b.x, b.y, b.width, b.height] : null;
}

/** Stacks several SVGs that share a canvas into one SVG, first file at the bottom. */
export function stackSvgs(svgs) {
    const [x, y, w, h] = svgs[0].viewBox;
    const layers = svgs.map(s => {
        const inner = withViewBox(s.src.replace(/<\?xml[^>]*\?>/i, '').replace(/<!DOCTYPE[^>]*>/i, ''), s.viewBox)
            .replace(/^<svg\b/i, `<svg x="${x}" y="${y}" width="${w}" height="${h}"`);
        return `<!-- ${path.basename(s.file)} -->\n${inner}`;
    });
    return `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="${x} ${y} ${w} ${h}">\n${layers.join('\n')}\n</svg>\n`;
}

/** Reads <dir>/export.json, or {} if there is none. */
export function readConfig(dir) {
    const file = path.join(dir, 'export.json');
    return fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
}

/** Orders part names bottom-to-top: names listed in `layers` first, in that order, then the rest alphabetically. */
export function layerOrder(names, layers = []) {
    const rank = n => { const i = layers.indexOf(n); return i < 0 ? layers.length : i; };
    return [...names].sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
}

/** Expands file and directory arguments into .svg paths; directories follow their export.json `layers`. */
export function collectSvgs(args) {
    return args.flatMap(a => {
        if (!fs.statSync(a).isDirectory()) return [a];
        const names = fs.readdirSync(a).filter(f => f.toLowerCase().endsWith('.svg')).map(f => f.slice(0, -4));
        return layerOrder(names, readConfig(a).layers).map(n => path.join(a, n + '.svg'));
    });
}

/** Minimal `--flag value` / `--switch` parser. Returns { flags, positional }. */
export function parseArgs(argv, switches = []) {
    const flags = {}, positional = [];
    for (let i = 0; i < argv.length; i++) {
        const a = argv[i];
        if (!a.startsWith('--')) { positional.push(a); continue; }
        const key = a.slice(2);
        flags[key] = switches.includes(key) ? true : argv[++i];
    }
    return { flags, positional };
}

export const sameBox = (a, b) => a.length === b.length && a.every((v, i) => Math.abs(v - b[i]) < 1e-6);
