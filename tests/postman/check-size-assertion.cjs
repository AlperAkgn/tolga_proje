"use strict";
// Test the size assertion itself without HTTP, accounts or database writes.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const collection = JSON.parse(fs.readFileSync(path.resolve(__dirname, "../../postman/Afterform.Step9.postman_collection.json"), "utf8"));
const item = collection.item.flatMap(folder => folder.item).find(request => request.name.includes("B: oversized request"));
assert.ok(item, "The size request must exist.");
const code = item.event.find(event => event.listen === "test").script.exec.join("\n");
const scenarios = [
  { name: "400 with multipart size limit", status: 400, messages: ["Failed to read the request form. Multipart body length limit 104857600 exceeded."], pass: true },
  { name: "400 with Kestrel size limit", status: 400, messages: ["Request body too large. The max request body size is 105119744 bytes."], pass: true },
  { name: "413 payload too large", status: 413, messages: [], pass: true },
  { name: "400 with unrelated CSRF error", status: 400, messages: ["CSRF validation failed."], pass: false },
  { name: "400 with unrelated audio format error", status: 400, messages: ["The file content does not match a supported audio format."], pass: false },
  { name: "201 accepts oversized file", status: 201, messages: [], pass: false },
  { name: "429 has already reached limiter", status: 429, messages: [], pass: false }
];
for (const scenario of scenarios) {
  const results = [];
  const pm = {
    response: { code: scenario.status, json: () => ({ errors: { AudioFile: scenario.messages } }) },
    test: (_name, callback) => { try { callback(); results.push(true); } catch { results.push(false); } },
    expect: value => ({ to: {
      eql: expected => assert.deepEqual(value, expected),
      be: { oneOf: allowed => assert.ok(allowed.includes(value)) }
    } })
  };
  vm.runInNewContext(code, { pm });
  assert.equal(results.length, 2);
  assert.equal(results.every(Boolean), scenario.pass, scenario.name);
  console.log(`PASS size-assertion check: ${scenario.name}`);
}
