import { readdir, readFile } from 'node:fs/promises';
import { dirname, extname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import postcss from 'postcss';

const featureRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../src/app/features');
const allowed = new Set([
  // MESP-186 owns the authentication styles in parallel; remove the exception once that work is integrated.
  'auth',
]);
const blockedProperties = /^(?:border-radius|height|min-height|font-family|font-size|font-weight|background(?:-.+)?)$/i;
const hardCodedColor = /(?:#[\da-f]{3,8}\b|rgba?\s*\()/i;
const controlSelector = /^(?:button|input|select|textarea)(?=$|[.:#[>+~])/i;
const sharedClassSelector = /^\.(?:button(?:--[\w-]+)?|[\w-]+-button(?:--[\w-]+)?|grid-sort|grid-row-menu-trigger|grid-clear-all|rail-tile|theme-trigger|theme-option|language-button|scheme-toggle|sign-out|[\w-]+-tabs?(?:__[\w-]+)?|tabs?(?:__[\w-]+)?)(?=$|[.:#[>+~])/i;
const tabRoleSelector = /\[role\s*=\s*["']?tab(?:list)?["']?\s*\]/i;
const violations = [];

function isAllowed(file) {
  const path = relative(featureRoot, file).split(sep).join('/');
  return [...allowed].some((entry) => entry === path || (entry === 'auth' && path.startsWith('auth/')));
}

function isControlRule(selector, classes) {
  const compound = selector.replace(/:has\([^)]*\)/gi, '').split(/[\s>+~]+/).pop() ?? '';
  if (compound.includes('::')) return false;
  const buttonClass = [...classes].some((name) => new RegExp(`(?:^|[^\\w-])\\.${name}(?=$|[^\\w-])`).test(compound));
  return controlSelector.test(compound) || sharedClassSelector.test(compound) || buttonClass || tabRoleSelector.test(compound);
}

function classesOnButtons(source) {
  const classes = new Set();
  for (const match of source.matchAll(/<(button|a)\b([^>]*)>/gi)) {
    const attributes = match[2];
    const classValue = attributes.match(/\bclass=(['"])(.*?)\1/i)?.[2] ?? '';
    const names = classValue.split(/\s+/).filter(Boolean);
    const buttonLikeLink = match[1].toLowerCase() === 'a' && (names.some((name) => /(?:^button(?:--|$)|-button$)/i.test(name)) || /\brole=(['"])tab\1/i.test(attributes));
    if (match[1].toLowerCase() === 'button' || buttonLikeLink) for (const name of names) classes.add(name);
  }
  return classes;
}

function inspectCss(css, file, classes = new Set()) {
  const root = postcss.parse(css, { from: file });
  root.walkRules((rule) => {
    for (const selector of rule.selectors) {
      if (!isControlRule(selector, classes)) continue;
      rule.walkDecls((declaration) => {
        if (blockedProperties.test(declaration.prop) || hardCodedColor.test(declaration.value)) {
          violations.push(`${relative(featureRoot, file)}:${declaration.source?.start?.line ?? 1} ${selector} { ${declaration.prop}: ${declaration.value} }`);
        }
      });
    }
  });
}

async function scan(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const file = resolve(directory, entry.name);
    if (entry.isDirectory()) { await scan(file); continue; }
    if (isAllowed(file)) continue;
    const extension = extname(file).toLowerCase();
    if (!['.ts', '.scss', '.html'].includes(extension)) continue;
    const source = await readFile(file, 'utf8');
    if (extension === '.scss') inspectCss(source, file);
    if (extension === '.ts') {
      const classes = classesOnButtons(source);
      for (const match of source.matchAll(/styles\s*:\s*(?:\[\s*)?`([\s\S]*?)`/g)) inspectCss(match[1], file, classes);
      for (const match of source.matchAll(/template\s*:\s*`([\s\S]*?)`/g)) inspectMarkup(match[1], file, classes);
    }
    if (extension === '.html') inspectMarkup(source, file, classesOnButtons(source));
  }
}

function inspectMarkup(markup, file, classes) {
  for (const match of markup.matchAll(/<(button|input|select|textarea|a)\b([^>]*)\sstyle=(['"])(.*?)\3/gi)) {
    const [, tag, attributes, , declarations] = match;
    const names = attributes.match(/\bclass=(['"])(.*?)\1/i)?.[2]?.split(/\s+/) ?? [];
    const tab = /\brole=(['"])tab\1/i.test(attributes);
    const buttonLike = tag.toLowerCase() !== 'a' || tab || names.some((name) => /(?:^button(?:--|$)|-button$)/i.test(name));
    if (!buttonLike) continue;
    const selector = tag.toLowerCase() === 'a' ? (names.map((name) => `.${name}`).join(',') || '[role="tab"]') : tag;
    inspectCss(`${selector} { ${declarations} }`, file, classes);
  }
  for (const match of markup.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi)) inspectCss(match[1], file, classes);
}

await scan(featureRoot);
if (violations.length) {
  console.error(`Feature control style violations: ${violations.length}`);
  for (const violation of violations) console.error(violation);
  process.exitCode = 1;
} else {
  console.log('Feature control style guard: 0 violations; MESP-186-owned authentication styles remain allowlisted.');
}
