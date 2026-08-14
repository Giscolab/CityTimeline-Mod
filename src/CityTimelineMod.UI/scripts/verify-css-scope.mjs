import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const stylesheetPath = path.resolve(
  scriptDirectory,
  "..",
  "src",
  "citytimeline-hud.css",
);
const stylesheet = readFileSync(stylesheetPath, "utf8");

if (!/src\s*:\s*url\(["']?\.\/assets\/fonts\/overpass-Regular\.ttf["']?\)/i.test(stylesheet)) {
  console.error("CTM must keep the source Overpass URL as ./assets/fonts/Overpass-Regular.ttf.");
  process.exitCode = 1;
}

if (/\[object(?:%20|\s)+object\]/i.test(stylesheet)) {
  console.error("Invalid [object Object] URL detected in CTM CSS.");
  process.exitCode = 1;
}

const themeContract = /\.ctm-hud,\s*\n\.ctm-launcher,\s*\n\.ctm-hud-error\s*\{/;
if (!themeContract.test(stylesheet)) {
  console.error(
    "CTM theme variables must be shared by .ctm-hud, .ctm-launcher, and .ctm-hud-error.",
  );
  process.exitCode = 1;
}

const allowedRootSelectors = [
  ".ctm-hud-host",
  ".ctm-hud-host-closed",
  ".ctm-hud,",
  ".ctm-hud {",
  ".ctm-hud *",
  ".ctm-hud ",
  ".ctm-launcher",
  ".ctm-hud-error",
];

const unscopedSelectors = stylesheet
  .split(/\r?\n/)
  .map((line, index) => ({ line, number: index + 1 }))
  .filter(({ line }) => {
    if (!line || /^\s/.test(line) || line.startsWith("@") || line === "}") {
      return false;
    }

    return !allowedRootSelectors.some((selector) => line.startsWith(selector));
  });

if (unscopedSelectors.length > 0) {
  console.error("Unscoped CTM CSS selectors detected:");
  for (const selector of unscopedSelectors) {
    console.error(`  ${selector.number}: ${selector.line}`);
  }
  process.exitCode = 1;
}

const unsupportedCohtmlPatterns = [
  { label: "display: grid", pattern: /display\s*:\s*grid\b/i },
  {
    label: "unsupported display mode",
    pattern: /display\s*:\s*(?:table|inline-block)\b/i,
  },
  { label: "grid-template", pattern: /grid-template(?:-columns|-rows)?\s*:/i },
  { label: "gap", pattern: /(?:^|[;{]\s*|\n\s*)gap\s*:/i },
  { label: "max-content", pattern: /\bmax-content\b/i },
  { label: "justify-content: space-evenly", pattern: /justify-content\s*:\s*space-evenly\b/i },
  { label: "font-size: x-small", pattern: /font-size\s*:\s*x-small\b/i },
  { label: "hsla color", pattern: /\bhsla\s*\(/i },
  {
    label: "dashed border",
    pattern: /border(?:-(?:top|right|bottom|left))?-style\s*:\s*dashed\b/i,
  },
  {
    label: "custom property in border shorthand",
    pattern: /border(?:(?:-(?:top|right|bottom|left))|-(?:width|color))?\s*:[^;{}]*var\s*\(/i,
  },
  {
    label: "custom property in flex shorthand",
    pattern: /flex\s*:[^;{}]*var\s*\(/i,
  },
  {
    label: "custom property in box shorthand",
    pattern: /(?:background|margin|padding|outline|transition)\s*:[^;{}]*var\s*\(/i,
  },
  { label: "variable font-weight range", pattern: /font-weight\s*:\s*\d+\s+\d+/i },
  {
    label: "unsupported pseudo selector",
    pattern: /:(?:not|focus-within|nth-child|nth-of-type|placeholder|first-child|last-child|first-of-type|disabled)\b/i,
  },
  { label: "scrollbar pseudo-element", pattern: /::?-webkit-scrollbar\b/i },
];

for (const check of unsupportedCohtmlPatterns) {
  if (check.pattern.test(stylesheet)) {
    console.error(`Unsupported CoHTML CSS detected: ${check.label}.`);
    process.exitCode = 1;
  }
}

if (!process.exitCode) {
  console.log("CTM CSS scope and CoHTML compatibility verified.");
}
