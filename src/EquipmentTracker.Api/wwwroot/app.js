const searchBox = document.getElementById('search');
const list = document.getElementById('list');
const message = document.getElementById('message');

const statusText = {
  Available: 'Available',
  CheckedOut: 'Checked out',
  Held: 'Held (awaiting approval)',
  Damaged: 'Damaged',
  Unavailable: 'None available'
};

// Shows a banner at the bottom of the screen. Success clears itself; errors stay until the next action.
let messageTimer;
function notify(text, isError) {
  message.textContent = text;
  message.className = isError ? 'error' : 'ok';
  clearTimeout(messageTimer);
  if (text && !isError) messageTimer = setTimeout(() => notify(''), 6000);
}

// Builds an element; text is set with textContent so names can never inject HTML.
function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function holderText(item, h) {
  const qty = item.trackingMode === 'Quantity' ? ` ×${h.quantity}` : '';
  const what = h.pending ? 'requested' : 'since';
  return `${h.person}${qty} · ${what} ${h.since} · due ${h.dueDate}` + (h.overdue ? ' · OVERDUE' : '');
}

// A "Return" button that opens a small form: how many, how many damaged, and a note.
function returnButton(item, holder) {
  const isQuantity = item.trackingMode === 'Quantity';
  const button = el('button', 'secondary', 'Return');

  button.onclick = () => {
    const quantity = el('input');
    quantity.type = 'number'; quantity.min = 1; quantity.max = holder.quantity; quantity.value = holder.quantity;
    const damaged = el('input');
    damaged.type = isQuantity ? 'number' : 'checkbox'; damaged.min = 0; damaged.value = 0;
    const note = el('input');
    note.placeholder = 'Note (optional)';
    const confirm = el('button', '', 'Confirm return');

    confirm.onclick = async () => {
      const response = await fetch(`/api/checkouts/lines/${holder.lineId}/return`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          quantity: isQuantity ? Number(quantity.value) : 1,
          damagedQuantity: isQuantity ? Number(damaged.value) : (damaged.checked ? 1 : 0),
          note: note.value || null
        })
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        notify(problem.detail || problem.title || 'Something went wrong.', true);
        return;
      }
      await Promise.all([load(), loadPickList()]);
      notify('Returned.');
    };

    quantity.setAttribute('aria-label', 'Quantity returning');
    damaged.setAttribute('aria-label', isQuantity ? 'Quantity damaged' : 'Damaged');
    note.setAttribute('aria-label', 'Note');
    const form = el('div', 'detail action-form');
    if (isQuantity) form.append('Returning ', quantity, ' of which damaged ', damaged);
    else form.append(damaged, ' Damaged');
    form.append(' ', note, ' ', confirm);
    button.replaceWith(form);
  };
  return button;
}

// Approve or reject a pending request: pick who you are, type your PIN, optionally give a reason.
function decideButton(holder) {
  const button = el('button', 'secondary', 'Approve / Reject');

  button.onclick = () => {
    const who = el('select');
    people.filter(p => p.hasPin).forEach(p => {
      const option = el('option', '', p.name);
      option.value = p.id;
      who.append(option);
    });
    const pin = el('input');
    pin.type = 'password'; pin.placeholder = 'PIN'; pin.inputMode = 'numeric'; pin.size = 6;
    const reason = el('input');
    reason.placeholder = 'Reason (optional, for reject)';

    const decide = async action => {
      const response = await fetch(`/api/checkouts/lines/${holder.lineId}/${action}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Person-Id': who.value },
        body: JSON.stringify({ pin: pin.value, reason: reason.value || null })
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        notify(problem.detail || problem.title || 'Something went wrong.', true);
        return;
      }
      await Promise.all([load(), loadPickList()]);
      notify(action === 'approve' ? 'Approved.' : 'Rejected.');
    };

    const approve = el('button', '', 'Approve');
    approve.onclick = () => decide('approve');
    const reject = el('button', 'secondary', 'Reject');
    reject.onclick = () => decide('reject');

    who.setAttribute('aria-label', 'Who are you');
    pin.setAttribute('aria-label', 'PIN');
    reason.setAttribute('aria-label', 'Reason');
    const form = el('div', 'detail action-form');
    form.append(who, ' ', pin, ' ', reason, ' ', approve, ' ', reject);
    button.replaceWith(form);
  };
  return button;
}

// Restore damaged equipment (Office or Owner): pick who you are, type your PIN, say how many, add a note.
function restoreButton(item) {
  const isQuantity = item.trackingMode === 'Quantity';
  const button = el('button', 'secondary', 'Restore');

  button.onclick = () => {
    const who = el('select');
    people.filter(p => p.hasPin).forEach(p => {
      const option = el('option', '', p.name);
      option.value = p.id;
      who.append(option);
    });
    const pin = el('input');
    pin.type = 'password'; pin.placeholder = 'PIN'; pin.inputMode = 'numeric'; pin.size = 6;
    const quantity = el('input');
    quantity.type = 'number'; quantity.min = 1; quantity.max = item.damaged; quantity.value = item.damaged;
    const note = el('input');
    note.placeholder = 'Note (optional)';
    const confirm = el('button', '', 'Confirm restore');

    confirm.onclick = async () => {
      const response = await fetch(`/api/equipment/${item.id}/restore`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Person-Id': who.value },
        body: JSON.stringify({ pin: pin.value, quantity: isQuantity ? Number(quantity.value) : 1, note: note.value || null })
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        notify(problem.detail || problem.title || 'Something went wrong.', true);
        return;
      }
      await Promise.all([load(), loadPickList()]);
      notify('Restored.');
    };

    who.setAttribute('aria-label', 'Who are you');
    pin.setAttribute('aria-label', 'PIN');
    quantity.setAttribute('aria-label', 'Quantity to restore');
    note.setAttribute('aria-label', 'Note');
    const form = el('div', 'detail action-form');
    form.append(who, ' ', pin, ' ');
    if (isQuantity) form.append('Restore ', quantity, ' ');
    form.append(note, ' ', confirm);
    button.replaceWith(form);
  };
  return button;
}

function renderItem(item) {
  const li = el('li', 'item');

  const top = el('div', 'top');
  top.append(el('span', 'name', item.name), el('span', `badge ${item.status}`, statusText[item.status] || item.status));
  li.append(top);

  if (item.trackingMode === 'Quantity') {
    li.append(el('div', 'detail',
      `Total ${item.total} · Usable ${item.usable} · Available ${item.available}` +
      (item.damaged ? ` · Damaged ${item.damaged}` : '')));
  }
  item.holders.forEach(h => {
    const row = el('div', h.overdue ? 'detail overdue' : 'detail', holderText(item, h));
    row.append(' ', h.pending ? decideButton(h) : returnButton(item, h));
    li.append(row);
  });
  if (item.conditionNote && (item.status === 'Damaged' || item.damaged)) {
    li.append(el('div', 'detail', `Note: ${item.conditionNote}`));
  }
  // Single items are damaged as a whole; counted items when at least one unit is damaged.
  if (item.status === 'Damaged' || item.damaged) {
    const row = el('div', 'detail');
    row.append(restoreButton(item));
    li.append(row);
  }
  return li;
}

async function load() {
  const text = searchBox.value.trim();
  try {
    const response = await fetch('/api/status' + (text ? '?search=' + encodeURIComponent(text) : ''));
    if (!response.ok) throw new Error();
    const items = await response.json();
    if (items.length) {
      list.replaceChildren(...items.map(renderItem));
    } else {
      // Plain text on the page, not a banner.
      list.replaceChildren(el('li', 'empty-state', text ? `No equipment matches "${text}".` : 'No equipment found.'));
    }
    if (message.className === 'error') notify('');
  } catch {
    notify('Could not load equipment. Is the server running?', true);
  }
}

// ---- Check out ----

// Lists the items that can be taken right now: a tick box for single items, a number for counted ones.
async function loadPickList() {
  const items = await (await fetch('/api/status')).json();
  const rows = items
    .filter(i => i.status === 'Available')
    .map(i => {
      const label = el('label', 'check');
      const input = el('input');
      input.dataset.id = i.id;
      if (i.trackingMode === 'Quantity') {
        input.type = 'number'; input.min = 0; input.max = i.available; input.value = 0;
        label.append(input, ` ${i.name} (max ${i.available})`);
      } else {
        input.type = 'checkbox';
        label.append(input, ` ${i.name}`);
      }
      return label;
    });
  $pick.replaceChildren(...rows);
}

let people = [];

async function loadPeople() {
  people = await (await fetch('/api/people')).json();
  borrower.replaceChildren(...people.map(p => {
    const option = el('option', '', p.name);
    option.value = p.id;
    return option;
  }));
}

document.getElementById('checkout-form').addEventListener('submit', async event => {
  event.preventDefault();
  const items = [...$pick.querySelectorAll('input')]
    .map(i => ({ equipmentId: Number(i.dataset.id), quantity: i.type === 'checkbox' ? (i.checked ? 1 : 0) : Number(i.value) }))
    .filter(i => i.quantity > 0);

  const response = await fetch('/api/checkouts', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ borrowerId: Number(borrower.value), dueDate: due.value, items })
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    notify(problem.detail || problem.title || 'Something went wrong.', true);
    return;
  }
  await Promise.all([load(), loadPickList()]);
  notify('Done. Items that need approval are held until the approver decides.');
});

const $pick = document.getElementById('pick');
const borrower = document.getElementById('borrower');
const due = document.getElementById('due');

searchBox.addEventListener('input', load);
load();
loadPeople();
loadPickList();
