import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const uiRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(uiRoot, "..", "..");
const csharpUiRoot = path.join(repositoryRoot, "src", "CityTimelineMod", "UI");
const typescriptRoot = path.join(uiRoot, "src");

function readSourceFiles(root, extensions) {
  const result = [];

  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const absolutePath = path.join(root, entry.name);

    if (entry.isDirectory()) {
      result.push(...readSourceFiles(absolutePath, extensions));
    } else if (extensions.has(path.extname(entry.name))) {
      result.push({
        path: absolutePath,
        text: readFileSync(absolutePath, "utf8"),
      });
    }
  }

  return result;
}

function collectMatches(files, expressions) {
  const values = new Set();

  for (const file of files) {
    for (const expression of expressions) {
      expression.lastIndex = 0;

      for (const match of file.text.matchAll(expression)) {
        values.add(match[1]);
      }
    }
  }

  return values;
}

function difference(left, right) {
  return [...left].filter((value) => !right.has(value)).sort();
}

function reportMismatch(label, onlyInCsharp, onlyInTypescript) {
  if (onlyInCsharp.length === 0 && onlyInTypescript.length === 0) {
    return false;
  }

  console.error(`CoHTML ${label} contract mismatch.`);

  if (onlyInCsharp.length > 0) {
    console.error(`  C# only: ${onlyInCsharp.join(", ")}`);
  }

  if (onlyInTypescript.length > 0) {
    console.error(`  TypeScript only: ${onlyInTypescript.join(", ")}`);
  }

  return true;
}

const csharpFiles = readSourceFiles(csharpUiRoot, new Set([".cs"]));
const typescriptFiles = readSourceFiles(
  typescriptRoot,
  new Set([".ts", ".tsx"]),
);

const csharpValues = collectMatches(csharpFiles, [
  /new\s+ValueBinding<[^>]+>\s*\(\s*BindingGroup\s*,\s*"([^"]+)"/gs,
]);

const typescriptValues = collectMatches(typescriptFiles, [
  /bindValue<[^>]+>\s*\(\s*BINDING_GROUP\s*,\s*"([^"]+)"/gs,
]);

const csharpTriggers = collectMatches(csharpFiles, [
  /new\s+TriggerBinding(?:<[^>]+>)?\s*\(\s*BindingGroup\s*,\s*"([^"]+)"/gs,
  /Create[A-Za-z0-9_]*Trigger\s*\(\s*"([^"]+)"/gs,
]);

const typescriptTriggers = collectMatches(typescriptFiles, [
  /"(set[A-Z][A-Za-z0-9]+|toggleCohtmlHud|closeCohtmlHud|clearAllRoadHighways)"/g,
]);

// Kept as a public backend capability for external CoHTML callers. The
// permanent CTM HUD currently uses the dedicated toggle and close triggers.
const backendOnlyTriggers = new Set(["setCohtmlHudVisible"]);
const csharpHudTriggers = new Set(
  [...csharpTriggers].filter((name) => !backendOnlyTriggers.has(name)),
);

const valueMismatch = reportMismatch(
  "value binding",
  difference(csharpValues, typescriptValues),
  difference(typescriptValues, csharpValues),
);

const triggerMismatch = reportMismatch(
  "trigger binding",
  difference(csharpHudTriggers, typescriptTriggers),
  difference(typescriptTriggers, csharpHudTriggers),
);

if (valueMismatch || triggerMismatch) {
  process.exitCode = 1;
} else {
  console.log(
    `CoHTML binding contract verified: ${csharpValues.size} values, ` +
      `${csharpHudTriggers.size} HUD triggers, ` +
      `${backendOnlyTriggers.size} backend-only trigger.`,
  );
}
