/* Compares a cache-tests results.json run against expected-results.json.
 *
 * Baseline stores only the outcome type per test id ("pass", "Assertion",
 * "Setup", ...) because failure messages contain per-run random values.
 *
 * Usage:
 *   node compare-results.mjs <results.json> <expected-results.json>          # gate
 *   node compare-results.mjs <results.json> --update <expected-results.json> # (re)write baseline
 *
 * Exit codes: 0 = no regressions, 1 = regressions found, 2 = usage/IO error.
 */
import { readFileSync, writeFileSync } from 'node:fs'

function outcomeType (value) {
  if (value === true) return 'pass'
  if (Array.isArray(value)) return String(value[0])
  return `unknown:${JSON.stringify(value)}`
}

const args = process.argv.slice(2)
if (args.length < 2) {
  console.error('Usage: node compare-results.mjs <results.json> [--update] <expected-results.json>')
  process.exit(2)
}

const resultsPath = args[0]
const update = args.includes('--update')
const baselinePath = args[args.length - 1]

const results = JSON.parse(readFileSync(resultsPath, 'utf8'))
const actual = {}
for (const [id, value] of Object.entries(results)) {
  actual[id] = outcomeType(value)
}

if (update) {
  const sorted = Object.fromEntries(Object.entries(actual).sort(([a], [b]) => a.localeCompare(b)))
  writeFileSync(baselinePath, JSON.stringify(sorted, null, 2) + '\n')
  const passes = Object.values(sorted).filter(v => v === 'pass').length
  console.log(`Baseline written to ${baselinePath}: ${Object.keys(sorted).length} tests, ${passes} passing.`)
  process.exit(0)
}

const baseline = JSON.parse(readFileSync(baselinePath, 'utf8'))

const regressions = []
const newPasses = []
const changed = []
const missing = []

for (const [id, expected] of Object.entries(baseline)) {
  const got = actual[id]
  if (got === undefined) {
    missing.push(id)
    continue
  }
  if (got === expected) continue
  if (expected === 'pass') {
    regressions.push({ id, expected, got, detail: results[id] })
  } else if (got === 'pass') {
    newPasses.push({ id, expected })
  } else {
    changed.push({ id, expected, got })
  }
}

const unknown = Object.keys(actual).filter(id => !(id in baseline))

const passCount = Object.values(actual).filter(v => v === 'pass').length
console.log(`Results: ${Object.keys(actual).length} tests, ${passCount} passing.`)

if (newPasses.length > 0) {
  console.log(`\n🎉 New passes (${newPasses.length}) — consider updating the baseline:`)
  for (const { id, expected } of newPasses) console.log(`  ${id} (was ${expected})`)
}

if (changed.length > 0) {
  console.log(`\nℹ️ Failure type changed (${changed.length}), not gating:`)
  for (const { id, expected, got } of changed) console.log(`  ${id}: ${expected} -> ${got}`)
}

if (unknown.length > 0) {
  console.log(`\nℹ️ Tests not in baseline (${unknown.length}), not gating:`)
  for (const id of unknown) console.log(`  ${id}: ${actual[id]}`)
}

if (missing.length > 0) {
  console.log(`\n⚠️ Baseline tests missing from results (${missing.length}):`)
  for (const id of missing) console.log(`  ${id}`)
}

if (regressions.length > 0) {
  console.log(`\n⛔ Regressions (${regressions.length}):`)
  for (const { id, got, detail } of regressions) {
    console.log(`  ${id}: pass -> ${got}: ${JSON.stringify(detail)}`)
  }
  process.exit(1)
}

console.log('\n✅ No regressions against baseline.')
