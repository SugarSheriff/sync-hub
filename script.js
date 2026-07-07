const triggerBtn = document.getElementById('triggerSync');
const failureBtn = document.getElementById('injectFailure');
const statusDot = document.getElementById('statusDot');
const statusText = document.getElementById('statusText');
const packetA = document.getElementById('packetA');
const packetB = document.getElementById('packetB');
const consoleBody = document.getElementById('consoleBody');
const recordCount = document.getElementById('recordCount');

let processed = 0;
let forceFailure = false;

const sampleOrders = ['SO-10231', 'SO-10232', 'SO-10233', 'SO-10234', 'SO-10235'];

function log(message, level = 'muted') {
  const line = document.createElement('div');
  line.className = `log-${level}`;
  const ts = new Date().toLocaleTimeString('en-US', { hour12: false });
  line.textContent = `[${ts}] ${message}`;
  consoleBody.appendChild(line);
  consoleBody.scrollTop = consoleBody.scrollHeight;
}

function setStatus(state, text) {
  statusDot.className = `dot ${state}`;
  statusText.textContent = text;
}

function wait(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function runPacket(el, willFail) {
  el.classList.remove('moving', 'failed');
  void el.offsetWidth; // restart animation
  if (willFail) el.classList.add('failed');
  el.classList.add('moving');
  await wait(900);
  el.classList.remove('moving');
}

async function syncOne(orderId, shouldFail) {
  log(`Polling SAP B1 for order ${orderId}...`);
  await wait(200);
  log(`Found order ${orderId}, mapping fields → Wrike task shape`, 'muted');
  await runPacket(packetA, false);
  log(`Order ${orderId} queued and validated`, 'ok');

  if (shouldFail) {
    await runPacket(packetB, true);
    log(`Wrike API returned 503 for ${orderId} — retrying (1/3)`, 'warn');
    await wait(500);
    log(`Retry succeeded for ${orderId} on attempt 2`, 'ok');
  }

  await runPacket(packetB, false);
  log(`Task created in Wrike for ${orderId}`, 'ok');
  processed += 1;
  recordCount.textContent = `${processed} records processed`;
}

async function runBatch(withFailure) {
  triggerBtn.disabled = true;
  failureBtn.disabled = true;
  setStatus('busy', 'Syncing...');
  log('--- sync batch started ---');

  for (let i = 0; i < sampleOrders.length; i++) {
    const shouldFail = withFailure && i === Math.floor(sampleOrders.length / 2);
    await syncOne(sampleOrders[i], shouldFail);
  }

  log('--- sync batch complete ---');
  setStatus('ok', 'Idle');
  triggerBtn.disabled = false;
  failureBtn.disabled = false;
}

triggerBtn.addEventListener('click', () => {
  const withFailure = forceFailure;
  forceFailure = false;
  runBatch(withFailure);
});

failureBtn.addEventListener('click', () => {
  forceFailure = true;
  log('Next batch will simulate a transient Wrike API failure', 'warn');
});

log('Sync Hub ready. Click "Trigger sync batch" to simulate a run.');
