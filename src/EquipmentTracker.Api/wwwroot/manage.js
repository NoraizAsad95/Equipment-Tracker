const $ = id => document.getElementById(id);
let people = [];
let equipment = [];

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
async function api(method, url, body) {
  const options = { method, headers: { 'Content-Type': 'application/json' } };
  if ($('actor').value) options.headers['X-Person-Id'] = $('actor').value;
  if (body) options.body = JSON.stringify({ ...body, pin: $('pin').value });
  const response = await fetch(url, options);
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new Error(problem.detail || problem.title || 'Something went wrong.');
  }
  return response.json();
}

function fillSelect(select, items, valueOf, textOf, keepValue) {
  select.replaceChildren(...items.map(i => {
    const option = el('option', '', textOf(i));
    option.value = valueOf(i);
    return option;
  }));
  if (keepValue) select.value = keepValue;
}

async function load() {
    people = await api('GET', '/api/people');
    equipment = await api('GET', '/api/equipment');

    fillSelect(
        $('actor'),
        people.filter(p => p.role === 'Owner'),
        p => p.id,
        p => p.name,
        $('actor').value
    );

    fillSelect(
        $('eq-approver'),
        people.filter(p => p.hasPin),
        p => p.id,
        p => p.name,
        $('eq-approver').value
    );

    $('equipment-list').replaceChildren(...equipment.map(e => {
        const li = el('li', 'management-row');
        const info = el('div', 'management-row-info');
        const approver = people.find(p => p.id === e.approverId);

        const name = el('div', 'management-name', e.name);
        const typeText = e.trackingMode === 'Quantity'
            ? `Quantity tracked · ${e.totalQuantity} total`
            : 'Individually tracked';
        const details = el('div', 'management-details', typeText);

        info.append(name, details);

        if (e.requiresApproval) {
            const approval = el(
                'div',
                'management-details',
                `Approval required · ${approver ? approver.name : 'Approver not set'}`
            );
            info.append(approval);
        }

        const edit = el('button', 'secondary management-edit', 'Edit');
        edit.type = 'button';
        edit.onclick = () => editEquipment(e);

        li.append(info, edit);
        return li;
    }));

    $('people-list').replaceChildren(...people.map(p => {
        const li = el('li', 'management-row');
        const info = el('div', 'management-row-info');

        const name = el('div', 'management-name', p.name);
        const details = el(
            'div',
            'management-details',
            `${p.role} · ${p.hasPin ? 'PIN configured' : 'No PIN configured'}`
        );

        const role = el('span', `role-badge role-${p.role.toLowerCase()}`, p.role);
        info.append(name, details);

        const right = el('div', 'management-row-actions');
        right.append(role);

        const edit = el('button', 'secondary management-edit', 'Edit');
        edit.type = 'button';
        edit.onclick = () => editPerson(p);
        right.append(edit);

        li.append(info, right);
        return li;
    }));
}

// ---- Equipment form ----

function updateEquipmentForm() {
  const isQuantity = $('eq-mode').value === 'Quantity';
  $('eq-total-row').hidden = !isQuantity;
  $('eq-approval-row').hidden = isQuantity;
  $('eq-approver-row').hidden = isQuantity || !$('eq-approval').checked;
}

function resetEquipmentForm() {
    $('equipment-form').reset();
    $('eq-id').value = '';
    $('eq-mode').disabled = false;
    $('equipment-title').textContent = 'Add equipment';
    $('equipment-form').hidden = true;
    updateEquipmentForm();
}

function editEquipment(e) {
    $('eq-id').value = e.id;
    $('eq-name').value = e.name;
    $('eq-mode').value = e.trackingMode;
    $('eq-mode').disabled = true;
    $('eq-total').value = e.totalQuantity;
    $('eq-approval').checked = e.requiresApproval;

    if (e.approverId) {
        $('eq-approver').value = e.approverId;
    }

    updateEquipmentForm();
    $('equipment-title').textContent = `Edit ${e.name}`;
    $('equipment-form').hidden = false;
    $('equipment-form').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

$('eq-mode').onchange = updateEquipmentForm;
$('eq-approval').onchange = updateEquipmentForm;
$('add-equipment-btn').onclick = () => {
    resetEquipmentForm();
    $('equipment-form').hidden = false;
    $('equipment-form').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    $('eq-name').focus();
};

$('eq-cancel').onclick = resetEquipmentForm;

$('equipment-form').onsubmit = async event => {
  event.preventDefault();
  const isQuantity = $('eq-mode').value === 'Quantity';
  const body = {
    name: $('eq-name').value,
    trackingMode: $('eq-mode').value,
    totalQuantity: isQuantity ? Number($('eq-total').value) : 1,
    requiresApproval: !isQuantity && $('eq-approval').checked,
    approverId: !isQuantity && $('eq-approval').checked ? Number($('eq-approver').value) : null
  };
  const id = $('eq-id').value;
  try {
    await api(id ? 'PUT' : 'POST', id ? `/api/equipment/${id}` : '/api/equipment', body);
    show('Equipment saved.');
    resetEquipmentForm();
    await load();
  } catch (error) {
    show(error.message, true);
  }
};

// ---- People form ----

function resetPersonForm() {
    $('person-form').reset();
    $('p-id').value = '';
    $('person-title').textContent = 'Add person';
    $('person-form').hidden = true;
}

function editPerson(p) {
    $('p-id').value = p.id;
    $('p-name').value = p.name;
    $('p-role').value = p.role;
    $('p-pin').value = '';
    $('person-title').textContent = `Edit ${p.name}`;
    $('person-form').hidden = false;
    $('person-form').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

$('add-person-btn').onclick = () => {
    resetPersonForm();
    $('person-form').hidden = false;
    $('person-form').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    $('p-name').focus();
};

$('p-cancel').onclick = resetPersonForm;
$('person-form').onsubmit = async event => {
  event.preventDefault();
  const body = { name: $('p-name').value, role: $('p-role').value, personPin: $('p-pin').value || null };
  const id = $('p-id').value;
  try {
    await api(id ? 'PUT' : 'POST', id ? `/api/people/${id}` : '/api/people', body);
    show('Person saved.');
    resetPersonForm();
    await load();
  } catch (error) {
    show(error.message, true);
  }
};

updateEquipmentForm();
load().catch(() => show('Could not load data. Is the server running?', true));
