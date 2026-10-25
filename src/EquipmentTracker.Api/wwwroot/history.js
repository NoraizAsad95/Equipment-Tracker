const $ = id => document.getElementById(id);

// Builds an element; text is set with textContent so names can never inject HTML.
function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

// Shows a banner at the bottom of the screen. Success clears itself; errors stay until the next action.
let messageTimer;
function show(text, isError) {
  $('message').textContent = text;
  $('message').className = isError ? 'error' : 'ok';
  clearTimeout(messageTimer);
  if (text && !isError) messageTimer = setTimeout(() => show(''), 6000);
}

// Sends the chosen person in a header and their PIN in the body (the PIN is never stored).
async function post(url, body) {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Person-Id': $('actor').value },
    body: JSON.stringify({ ...body, pin: $('pin').value })
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.detail || problem.title || 'Something went wrong.');
  }
  return response;
}

function entryText(e) {
  const what = e.correctsEventId ? `Correction to entry #${e.correctsEventId}` : e.type;
  const qty = e.quantity ? ` ×${e.quantity}` : '';
  const condition = e.condition === 'Damaged' ? ' (damaged)' : '';
  return `#${e.id} ${what}${qty}${condition} · ${e.on} · by ${e.by}` + (e.note ? ` · ${e.note}` : '');
}

// A "Correct" button that asks for a note, then adds a correction beside the entry (Owner only).
function correctButton(entry) {
  const button = el('button', 'secondary', 'Correct');
  button.onclick = () => {
    const note = el('input');
    note.placeholder = 'What was wrong?';
    const save = el('button', '', 'Save correction');
    save.onclick = async () => {
      try {
        await post(`/api/history/${entry.id}/corrections`, { note: note.value });
        show('Correction saved.');
        await search();
      } catch (error) {
        show(error.message, true);
      }
    };
    button.replaceWith(note, ' ', save);
  };
  return button;
}

function renderRow(r) {
  const qty = r.quantity > 1 ? ` ×${r.quantity}` : '';
  const li = el('li', 'item history-row');

  const top = el('div', 'top');
  top.append(
    el('span', r.stillOut ? 'name overdue' : 'name', `${r.borrower} · ${r.equipment}${qty}`),
    el('span', `badge ${r.stillOut ? 'CheckedOut' : 'Available'}`, r.stillOut ? 'Still out' : 'Returned'));
  li.append(top);

  const end = r.stillOut
    ? (r.returnedQuantity ? ` · ${r.returnedQuantity} of ${r.quantity} returned` : '')
    : ` · returned ${r.returnedOn}`;
  li.append(el('div', 'detail', `Out ${r.checkedOutOn} · due ${r.dueDate}${end}`));

  const entries = el('div', 'history-entries');
  r.entries.forEach(e => {
    const line = el('div', 'entry', entryText(e));
    if (e.type !== 'Corrected') line.append(' ', correctButton(e));
    entries.append(line);
  });
  li.append(entries);
  return li;
}

async function search() {
  const response = await post('/api/history/search', { from: $('from').value, to: $('to').value });
  const rows = await response.json();
  $('rows').replaceChildren(...rows.map(renderRow));
  if (!rows.length) show('Nothing was out in that period.');
}

$('history-form').onsubmit = async event => {
  event.preventDefault();
  try {
    show('');
    await search();
  } catch (error) {
    $('rows').replaceChildren();
    show(error.message, true);
  }
};

async function init() {
  const people = await (await fetch('/api/people')).json();
  const select = $('actor');
  people.filter(p => p.role !== 'Staff').forEach(p => {
    const option = el('option', '', `${p.name} (${p.role})`);
    option.value = p.id;
    select.append(option);
  });
  // The server works in Pakistan time, so default the dates to that day, not the UTC day.
  const pakistanDate = date => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Karachi' }).format(date);
  const today = new Date();
  const monthAgo = new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000);
  $('to').value = pakistanDate(today);
  $('from').value = pakistanDate(monthAgo);
}

init().catch(() => show('Could not load data. Is the server running?', true));
