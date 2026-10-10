const { chromium } = require('../../.tools/ui-preview/node_modules/playwright-core');
const path = require('path');
const { pathToFileURL } = require('url');
(async () => {
  const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1400, height: 1050 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.resolve(__dirname, '../Assets/Gebaeudevorschau.html')).href);
    const initial = await page.evaluate(() => [...document.querySelectorAll('canvas')].map(canvas => {
      const bytes = canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;
      let count = 0;
      for(let n=0;n<bytes.length;n+=4) if(bytes[n]<220) count++;
      return count;
    }));
    if(initial.length !== 25 || initial.some(count => count < 1500)) throw new Error('A building preview is empty');
    const before = await page.locator('canvas').first().evaluate(c => c.toDataURL());
    await page.locator('#yaw').evaluate(e => { e.value = 120; e.dispatchEvent(new Event('input')); });
    const after = await page.locator('canvas').first().evaluate(c => c.toDataURL());
    if(before === after) throw new Error('Rotation control does not update the geometry');
    await page.locator('#yaw').evaluate(e => { e.value = 35; e.dispatchEvent(new Event('input')); });
    await page.screenshot({ path: path.resolve(__dirname, '../Assets/Gebaeudeuebersicht.png'), fullPage: true });
    if(errors.length) throw new Error(errors.join('\n'));
    console.log('PASS: all 25 previews render, rotation works, no JavaScript errors.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
