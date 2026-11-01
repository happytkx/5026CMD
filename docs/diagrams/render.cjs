// Optional local regeneration: npm install --no-save @viz-js/viz sharp
// Run from the repository root: node docs/diagrams/render.cjs
const fs = require('fs');
const { instance } = require('@viz-js/viz');
const sharp = require('sharp');
(async () => {
  const viz = await instance();
  for (const name of ['erd', 'class-diagram']) {
    const svg = viz.renderString(fs.readFileSync('docs/diagrams/' + name + '.dot', 'utf8'), { format: 'svg' });
    fs.writeFileSync('docs/diagrams/' + name + '.svg', svg);
    await sharp(Buffer.from(svg)).resize({ width: 3600 }).png().toFile('docs/diagrams/' + name + '.png');
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
