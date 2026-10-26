// Run from any directory: node Tools/validate-world-map.cjs
// Data/resource validation only; this does not compile or execute Unity.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const read = p => fs.readFileSync(path.join(root, p));
const json = name => JSON.parse(read(`Assets/Resources/Data/${name}.json`));
const atlas = json('WorldMapAtlas');
const regions = json('RegionCatalog').regions;
const ids = new Set(regions.map(x => x.regionId));
assert.equal(atlas.pages.length, 4);
assert.equal(atlas.locations.length, regions.length);
assert.equal(new Set(atlas.locations.map(x => x.regionId)).size, regions.length);
const guids = new Set();
for (const p of atlas.pages) {
  assert.match(p.resource, /^Art\/WorldMap\/Acts\d\d$/);
  const filename = `Assets/Resources/${p.resource}.png`;
  const png = read(filename);
  assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
  const width = png.readUInt32BE(16), height = png.readUInt32BE(20);
  assert(width >= 1024 && height >= 640 && width <= 2048 && height <= 2048);
  const meta = read(filename + '.meta').toString();
  const guid = meta.match(/^guid: ([a-f0-9]{32})$/m)?.[1];
  assert(guid && !guids.has(guid)); guids.add(guid);
  assert.match(meta, /textureType: 0/); // RawImage loads Texture2D, not sprite slices.
}
for (const l of atlas.locations) {
  assert(ids.has(l.regionId), `unknown region ${l.regionId}`);
  assert(Number.isInteger(l.page) && atlas.pages[l.page]);
  assert(l.x > .05 && l.x < .95 && l.y > .05 && l.y < .95);
  const others = atlas.locations.filter(x => x.page === l.page && x.regionId !== l.regionId);
  assert(others.every(x => Math.hypot(x.x - l.x, x.y - l.y) > .09), `overlapping pins: ${l.regionId}`);
}
for (const r of regions) for (const id of r.connectedRegionIds || []) assert(ids.has(id), `${r.regionId} -> ${id}`);
let reach = 0;
for (const q of json('QuestCatalog').quests) for (const o of q.objectives || []) {
  if (o.type !== 'Reach') continue;
  reach++;
  assert(ids.has(o.targetId.replace(/^area_/, '')) || regions.some(x => x.sceneName === o.targetId), o.targetId);
}
const html = read('Prototype/world-map-atlas.html').toString();
new vm.Script(html.match(/<script>([\s\S]*?)<\/script>/)[1]);
console.log(`PASS: ${regions.length} atlas locations, 4 PNGs/import settings, unique GUIDs, coordinate spacing, catalog links, ${reach} Reach targets, preview JS syntax`);
