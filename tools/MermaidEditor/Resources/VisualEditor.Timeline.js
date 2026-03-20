// ============================================================
// VisualEditor.Timeline.js - Timeline Diagram Visual Editor
// Vertical timeline with sections, time periods, and events
// ============================================================

// ========== Timeline State ==========
let timelineModel = null;
let timelineSelectedEvent = null; // { index, section } or null
let timelineSelectedSection = null; // section name or null

// ========== Timeline Load/Restore ==========

window.loadTimelineDiagram = function(jsonStr) {
    try {
        currentDiagramType = 'timeline';
        timelineModel = JSON.parse(jsonStr);
        timelineSelectedEvent = null;
        timelineSelectedSection = null;
        editorCanvasZoom = 1;
        updateToolbarForDiagramType();
        renderTimelineDiagram();
    } catch (e) {
        console.error('Failed to load timeline diagram:', e);
    }
};

window.restoreTimelineDiagram = function(jsonStr) {
    try {
        timelineModel = JSON.parse(jsonStr);
        renderTimelineDiagram();
    } catch (e) {
        console.error('Failed to restore timeline diagram:', e);
    }
};

window.refreshTimelineDiagram = function(jsonStr) {
    try {
        timelineModel = JSON.parse(jsonStr);
        renderTimelineDiagram();
    } catch (e) {
        console.error('Failed to refresh timeline diagram:', e);
    }
};

// ========== Timeline Rendering ==========

function renderTimelineDiagram() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !timelineModel) return;

    // Show editorCanvas, hide diagram-svg
    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';

    canvas.innerHTML = '';

    // Read theme colors
    const cs = getComputedStyle(document.body);
    const cv = (v) => cs.getPropertyValue(v).trim();
    const bgColor = cv('--bg-color') || '#1E1E1E';
    const textColor = cv('--node-text') || '#D4D4D4';
    const headerBg = cv('--toolbar-bg') || '#2D2D30';
    const borderColor = cv('--node-stroke') || '#3E3E42';
    const isLight = document.body.classList.contains('theme-light');
    const isTwilight = document.body.classList.contains('theme-twilight');

    // Color palette for time period nodes - adapt to current theme
    const periodColors = isLight
        ? ['#2196f3', '#4caf50', '#ff9800', '#9c27b0', '#f44336', '#009688', '#ff5722', '#3f51b5', '#e91e63', '#00bcd4']
        : isTwilight
        ? ['#4A90D9', '#5A9E6F', '#D19A66', '#B48EAD', '#E06C75', '#56B6C2', '#C678DD', '#61AFEF', '#E5C07B', '#98C379']
        : ['#89b4fa', '#a6e3a1', '#f9e2af', '#cba6f7', '#f38ba8', '#94e2d5', '#fab387', '#74c7ec', '#f5c2e7', '#b4befe'];

    const sectionBg = isLight ? 'rgba(0,0,0,0.03)' : (isTwilight ? 'rgba(255,255,255,0.03)' : 'rgba(255,255,255,0.03)');
    const sectionHeaderBg = isLight ? 'rgba(0,0,0,0.06)' : (isTwilight ? 'rgba(255,255,255,0.06)' : 'rgba(255,255,255,0.06)');

    // Layout constants
    const padding = 20;
    const titleHeight = timelineModel.title ? 40 : 0;
    const sectionHeaderHeight = 32;
    const eventRowHeight = 70;
    const eventBubbleWidth = 120;
    const eventBubbleHeight = 28;
    const timePeriodWidth = 100;
    const eventGap = 8;
    const timelineLineX = padding + timePeriodWidth + 30;

    // Collect all events in order for rendering
    const allRows = []; // { type: 'section-header'|'event', sectionName?, event?, sectionIdx? }

    // Top-level events (before any section)
    if (timelineModel.events) {
        timelineModel.events.forEach((evt, i) => {
            allRows.push({ type: 'event', event: evt, index: i, section: null });
        });
    }

    // Sections
    if (timelineModel.sections) {
        timelineModel.sections.forEach((section, si) => {
            allRows.push({ type: 'section-header', sectionName: section.name, sectionIdx: si });
            if (section.events && section.events.length > 0) {
                section.events.forEach((evt, ei) => {
                    allRows.push({ type: 'event', event: evt, index: ei, section: section.name });
                });
            } else {
                allRows.push({ type: 'empty-section', sectionName: section.name });
            }
        });
    }

    // Calculate SVG dimensions
    let totalHeight = padding + titleHeight;
    allRows.forEach(row => {
        if (row.type === 'section-header') {
            totalHeight += sectionHeaderHeight;
        } else if (row.type === 'empty-section') {
            totalHeight += 40;
        } else {
            // Event row: height depends on number of events in the time period
            const numEvents = row.event ? row.event.events.length : 0;
            const rows = Math.ceil(Math.max(numEvents, 1) / 3);
            totalHeight += Math.max(eventRowHeight, rows * (eventBubbleHeight + eventGap) + 20);
        }
    });
    totalHeight += 80; // toolbar space

    const totalWidth = Math.max(600, 700);

    // Create SVG
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', totalWidth);
    svg.setAttribute('height', totalHeight);
    svg.setAttribute('viewBox', `0 0 ${totalWidth} ${totalHeight}`);
    svg.style.display = 'block';

    // Background
    const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    bg.setAttribute('width', totalWidth);
    bg.setAttribute('height', totalHeight);
    bg.setAttribute('fill', bgColor);
    bg.setAttribute('rx', '8');
    svg.appendChild(bg);

    // Click on background to deselect
    bg.addEventListener('click', () => {
        timelineSelectedEvent = null;
        timelineSelectedSection = null;
        renderTimelineDiagram();
    });

    // Title
    let currentY = padding;
    if (timelineModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', totalWidth / 2);
        titleText.setAttribute('y', currentY + 20);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '18');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = timelineModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', (e) => { e.stopPropagation(); editTimelineSettings(); });
        svg.appendChild(titleText);
        currentY += titleHeight;
    }

    // Draw vertical timeline line
    const lineStartY = currentY;

    let colorIdx = 0;

    // Render rows
    allRows.forEach((row) => {
        if (row.type === 'section-header') {
            // Section header background
            const secBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            secBg.setAttribute('x', padding);
            secBg.setAttribute('y', currentY);
            secBg.setAttribute('width', totalWidth - padding * 2);
            secBg.setAttribute('height', sectionHeaderHeight);
            secBg.setAttribute('fill', sectionHeaderBg);
            secBg.setAttribute('rx', '4');
            secBg.style.cursor = 'pointer';
            const secName = row.sectionName;
            secBg.addEventListener('click', (e) => { e.stopPropagation(); selectTimelineSection(secName); });
            secBg.addEventListener('dblclick', (e) => { e.stopPropagation(); editTimelineSection(secName); });
            svg.appendChild(secBg);

            // Section label
            const secLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            secLabel.setAttribute('x', padding + 10);
            secLabel.setAttribute('y', currentY + sectionHeaderHeight / 2 + 5);
            secLabel.setAttribute('fill', textColor);
            secLabel.setAttribute('font-size', '13');
            secLabel.setAttribute('font-weight', 'bold');
            secLabel.textContent = row.sectionName;
            secLabel.style.cursor = 'pointer';
            secLabel.addEventListener('click', (e) => { e.stopPropagation(); selectTimelineSection(secName); });
            secLabel.addEventListener('dblclick', (e) => { e.stopPropagation(); editTimelineSection(secName); });
            svg.appendChild(secLabel);

            // Highlight selected section
            if (timelineSelectedSection === secName) {
                const selBorder = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                selBorder.setAttribute('x', padding);
                selBorder.setAttribute('y', currentY);
                selBorder.setAttribute('width', totalWidth - padding * 2);
                selBorder.setAttribute('height', sectionHeaderHeight);
                selBorder.setAttribute('fill', 'none');
                selBorder.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
                selBorder.setAttribute('stroke-width', '2');
                selBorder.setAttribute('rx', '4');
                svg.appendChild(selBorder);
            }

            currentY += sectionHeaderHeight;
            return;
        }

        if (row.type === 'empty-section') {
            const emptyHint = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            emptyHint.setAttribute('x', timelineLineX + 20);
            emptyHint.setAttribute('y', currentY + 24);
            emptyHint.setAttribute('fill', textColor);
            emptyHint.setAttribute('font-size', '11');
            emptyHint.setAttribute('opacity', '0.35');
            emptyHint.setAttribute('font-style', 'italic');
            emptyHint.textContent = '(no events \u2013 add one to this section)';
            svg.appendChild(emptyHint);
            currentY += 40;
            return;
        }

        // Event row
        const evt = row.event;
        const numEvents = evt.events ? evt.events.length : 0;
        const rows = Math.ceil(Math.max(numEvents, 1) / 3);
        const rowHeight = Math.max(eventRowHeight, rows * (eventBubbleHeight + eventGap) + 20);
        const rowCenterY = currentY + rowHeight / 2;
        const color = periodColors[colorIdx % periodColors.length];
        colorIdx++;

        const isSelected = timelineSelectedEvent !== null &&
            timelineSelectedEvent.index === row.index &&
            timelineSelectedEvent.section === row.section;

        // Timeline dot
        const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        dot.setAttribute('cx', timelineLineX);
        dot.setAttribute('cy', rowCenterY);
        dot.setAttribute('r', isSelected ? 8 : 6);
        dot.setAttribute('fill', color);
        if (isSelected) {
            dot.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
            dot.setAttribute('stroke-width', '2');
        }
        svg.appendChild(dot);

        // Time period label (left of timeline)
        const periodText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        periodText.setAttribute('x', timelineLineX - 20);
        periodText.setAttribute('y', rowCenterY + 5);
        periodText.setAttribute('text-anchor', 'end');
        periodText.setAttribute('fill', color);
        periodText.setAttribute('font-size', '14');
        periodText.setAttribute('font-weight', 'bold');
        periodText.textContent = evt.timePeriod;
        periodText.style.cursor = 'pointer';
        const evtIdx = row.index;
        const evtSection = row.section;
        periodText.addEventListener('click', (e) => { e.stopPropagation(); selectTimelineEvent(evtIdx, evtSection); });
        periodText.addEventListener('dblclick', (e) => { e.stopPropagation(); editTimelineEvent(evtIdx, evtSection); });
        svg.appendChild(periodText);

        // Event bubbles (right of timeline)
        if (numEvents > 0) {
            const bubbleStartX = timelineLineX + 20;
            const maxBubblesPerRow = 3;

            evt.events.forEach((eventText, ei) => {
                const bRow = Math.floor(ei / maxBubblesPerRow);
                const bCol = ei % maxBubblesPerRow;
                const bx = bubbleStartX + bCol * (eventBubbleWidth + eventGap);
                const by = rowCenterY - (rows * (eventBubbleHeight + eventGap)) / 2 + bRow * (eventBubbleHeight + eventGap);

                // Bubble background
                const bubble = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                bubble.setAttribute('x', bx);
                bubble.setAttribute('y', by);
                bubble.setAttribute('width', eventBubbleWidth);
                bubble.setAttribute('height', eventBubbleHeight);
                bubble.setAttribute('fill', color);
                bubble.setAttribute('opacity', isSelected ? '1' : '0.75');
                bubble.setAttribute('rx', '14');
                bubble.style.cursor = 'pointer';
                bubble.addEventListener('click', (e) => { e.stopPropagation(); selectTimelineEvent(evtIdx, evtSection); });
                bubble.addEventListener('dblclick', (e) => { e.stopPropagation(); editTimelineEvent(evtIdx, evtSection); });
                bubble.addEventListener('mouseenter', () => bubble.setAttribute('opacity', '1'));
                bubble.addEventListener('mouseleave', () => bubble.setAttribute('opacity', isSelected ? '1' : '0.75'));
                svg.appendChild(bubble);

                // Bubble text
                const bubbleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                bubbleText.setAttribute('x', bx + eventBubbleWidth / 2);
                bubbleText.setAttribute('y', by + eventBubbleHeight / 2 + 4);
                bubbleText.setAttribute('text-anchor', 'middle');
                bubbleText.setAttribute('fill', '#ffffff');
                bubbleText.setAttribute('font-size', '11');
                bubbleText.style.pointerEvents = 'none';
                // Truncate long text
                const maxChars = 14;
                bubbleText.textContent = eventText.length > maxChars ? eventText.substring(0, maxChars - 1) + '\u2026' : eventText;
                svg.appendChild(bubbleText);

                // Connection line from dot to first bubble
                if (ei === 0) {
                    const connLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    connLine.setAttribute('x1', timelineLineX + 6);
                    connLine.setAttribute('y1', rowCenterY);
                    connLine.setAttribute('x2', bx);
                    connLine.setAttribute('y2', by + eventBubbleHeight / 2);
                    connLine.setAttribute('stroke', color);
                    connLine.setAttribute('stroke-width', '1.5');
                    connLine.setAttribute('stroke-opacity', '0.5');
                    svg.appendChild(connLine);
                }
            });
        }

        // Selected highlight
        if (isSelected) {
            const selRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            selRect.setAttribute('x', padding);
            selRect.setAttribute('y', currentY);
            selRect.setAttribute('width', totalWidth - padding * 2);
            selRect.setAttribute('height', rowHeight);
            selRect.setAttribute('fill', 'none');
            selRect.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
            selRect.setAttribute('stroke-width', '2');
            selRect.setAttribute('rx', '4');
            selRect.setAttribute('stroke-dasharray', '4,2');
            svg.appendChild(selRect);
        }

        // Row separator line
        const sepLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        sepLine.setAttribute('x1', padding);
        sepLine.setAttribute('y1', currentY + rowHeight);
        sepLine.setAttribute('x2', totalWidth - padding);
        sepLine.setAttribute('y2', currentY + rowHeight);
        sepLine.setAttribute('stroke', borderColor);
        sepLine.setAttribute('stroke-opacity', '0.2');
        sepLine.setAttribute('stroke-width', '1');
        svg.appendChild(sepLine);

        currentY += rowHeight;
    });

    // Draw vertical timeline line (behind everything, but we draw it now with known extent)
    const lineEndY = currentY;
    const timelineLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    timelineLine.setAttribute('x1', timelineLineX);
    timelineLine.setAttribute('y1', lineStartY);
    timelineLine.setAttribute('x2', timelineLineX);
    timelineLine.setAttribute('y2', lineEndY);
    timelineLine.setAttribute('stroke', borderColor);
    timelineLine.setAttribute('stroke-width', '2');
    // Insert after background but before other elements
    svg.insertBefore(timelineLine, svg.children[1]);

    // Toolbar
    const toolbarY = currentY + 10;
    renderTimelineToolbar(svg, 10, toolbarY, totalWidth - 20, isLight, textColor);

    // Adjust SVG height if needed
    const finalHeight = toolbarY + 50;
    if (finalHeight > totalHeight) {
        svg.setAttribute('height', finalHeight);
        svg.setAttribute('viewBox', `0 0 ${totalWidth} ${finalHeight}`);
        bg.setAttribute('height', finalHeight);
    }

    canvas.appendChild(svg);

    // Apply current zoom level and update minimap
    if (typeof editorCanvasZoom !== 'undefined' && editorCanvasZoom !== 1) {
        svg.style.transformOrigin = 'top left';
        svg.style.transform = 'scale(' + editorCanvasZoom + ')';
        svg.style.maxWidth = 'none';
    }
    if (typeof updateMinimap === 'function') updateMinimap();
}

function renderTimelineToolbar(svg, x, y, width, isLight, textColor) {
    const btnBg = isLight ? '#e0e0e0' : '#313244';
    const btnHover = isLight ? '#bdbdbd' : '#45475a';
    const buttons = [
        { label: '+ Add Event', action: () => createTimelineEvent() },
        { label: '+ Add Section', action: () => createTimelineSection() },
        { label: 'Settings', action: () => editTimelineSettings() }
    ];

    if (timelineSelectedEvent !== null) {
        buttons.push({ label: 'Edit Event', action: () => editTimelineEvent(timelineSelectedEvent.index, timelineSelectedEvent.section) });
        buttons.push({ label: 'Delete Event', action: () => deleteTimelineEvent() });
    }

    if (timelineSelectedSection !== null) {
        buttons.push({ label: 'Edit Section', action: () => editTimelineSection(timelineSelectedSection) });
        buttons.push({ label: 'Delete Section', action: () => deleteTimelineSection() });
    }

    let btnX = x + 10;
    buttons.forEach(btn => {
        const btnWidth = btn.label.length * 8 + 20;
        const btnRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        btnRect.setAttribute('x', btnX);
        btnRect.setAttribute('y', y);
        btnRect.setAttribute('width', btnWidth);
        btnRect.setAttribute('height', 28);
        btnRect.setAttribute('fill', btnBg);
        btnRect.setAttribute('rx', '4');
        btnRect.style.cursor = 'pointer';
        btnRect.addEventListener('mouseenter', () => btnRect.setAttribute('fill', btnHover));
        btnRect.addEventListener('mouseleave', () => btnRect.setAttribute('fill', btnBg));
        btnRect.addEventListener('click', (e) => { e.stopPropagation(); btn.action(); });
        svg.appendChild(btnRect);

        const btnText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        btnText.setAttribute('x', btnX + btnWidth / 2);
        btnText.setAttribute('y', y + 18);
        btnText.setAttribute('text-anchor', 'middle');
        btnText.setAttribute('fill', textColor);
        btnText.setAttribute('font-size', '12');
        btnText.textContent = btn.label;
        btnText.style.cursor = 'pointer';
        btnText.style.pointerEvents = 'none';
        svg.appendChild(btnText);

        btnX += btnWidth + 10;
    });
}

// ========== Timeline Interactions ==========

function selectTimelineEvent(index, section) {
    timelineSelectedEvent = { index, section };
    timelineSelectedSection = null;
    renderTimelineDiagram();
    postMessage({ type: 'tl_eventSelected', index, section });
}

function selectTimelineSection(sectionName) {
    timelineSelectedSection = sectionName;
    timelineSelectedEvent = null;
    renderTimelineDiagram();
    postMessage({ type: 'tl_sectionSelected', section: sectionName });
}

function createTimelineEvent() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Event';
    const body = document.querySelector('.property-panel-body');

    // Build section options
    let sectionOptions = '<option value="">(Top Level)</option>';
    if (timelineModel.sections) {
        timelineModel.sections.forEach(s => {
            sectionOptions += `<option value="${_escHtml(s.name)}">${_escHtml(s.name)}</option>`;
        });
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Time Period</div>
            <input class="property-input" id="tl-dlg-period" value="2024" />
        </div>
        <div class="property-row">
            <div class="property-label">Events (one per line)</div>
            <textarea class="property-input" id="tl-dlg-events" rows="3" style="resize:vertical">Event A
Event B</textarea>
        </div>
        <div class="property-row">
            <div class="property-label">Section</div>
            <select class="property-input" id="tl-dlg-section">${sectionOptions}</select>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="tl-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Event</button>
        </div>
    `;
    document.getElementById('tl-dlg-ok').addEventListener('click', function() {
        const period = document.getElementById('tl-dlg-period').value.trim();
        if (!period) return;
        const eventsText = document.getElementById('tl-dlg-events').value.trim();
        const events = eventsText.split('\n').map(e => e.trim()).filter(e => e.length > 0);
        const section = document.getElementById('tl-dlg-section').value || null;
        postMessage({ type: 'tl_eventCreated', timePeriod: period, events, section });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('tl-dlg-period').select(), 50);
}

function editTimelineEvent(index, section) {
    if (!timelineModel) return;

    // Find the event
    let evt;
    if (section) {
        const sec = timelineModel.sections ? timelineModel.sections.find(s => s.name === section) : null;
        if (!sec || !sec.events || index >= sec.events.length) return;
        evt = sec.events[index];
    } else {
        if (!timelineModel.events || index >= timelineModel.events.length) return;
        evt = timelineModel.events[index];
    }

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Event';
    const body = document.querySelector('.property-panel-body');

    // Build section options
    let sectionOptions = `<option value="" ${!section ? 'selected' : ''}>(Top Level)</option>`;
    if (timelineModel.sections) {
        timelineModel.sections.forEach(s => {
            const sel = s.name === section ? 'selected' : '';
            sectionOptions += `<option value="${_escHtml(s.name)}" ${sel}>${_escHtml(s.name)}</option>`;
        });
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Time Period</div>
            <input class="property-input" id="tl-dlg-period" value="${_escHtml(evt.timePeriod)}" />
        </div>
        <div class="property-row">
            <div class="property-label">Events (one per line)</div>
            <textarea class="property-input" id="tl-dlg-events" rows="4" style="resize:vertical">${_escHtml(evt.events.join('\n'))}</textarea>
        </div>
        <div class="property-row">
            <div class="property-label">Section</div>
            <select class="property-input" id="tl-dlg-section">${sectionOptions}</select>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="tl-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('tl-dlg-ok').addEventListener('click', function() {
        const period = document.getElementById('tl-dlg-period').value.trim();
        if (!period) return;
        const eventsText = document.getElementById('tl-dlg-events').value.trim();
        const events = eventsText.split('\n').map(e => e.trim()).filter(e => e.length > 0);
        const newSection = document.getElementById('tl-dlg-section').value || null;
        postMessage({ type: 'tl_eventEdited', index, section, timePeriod: period, events, newSection });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('tl-dlg-period').select(), 50);
}

function deleteTimelineEvent() {
    if (!timelineSelectedEvent) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Event';
    const body = document.querySelector('.property-panel-body');

    // Find event for display
    let evtLabel = 'this event';
    const sel = timelineSelectedEvent;
    if (sel.section) {
        const sec = timelineModel.sections ? timelineModel.sections.find(s => s.name === sel.section) : null;
        if (sec && sec.events && sel.index < sec.events.length) evtLabel = sec.events[sel.index].timePeriod;
    } else if (timelineModel.events && sel.index < timelineModel.events.length) {
        evtLabel = timelineModel.events[sel.index].timePeriod;
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Are you sure you want to delete "${_escHtml(evtLabel)}"?</div>
        </div>
        <div class="property-row" style="margin-top:8px;display:flex;gap:8px">
            <button id="tl-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="tl-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--toolbar-bg);color:var(--node-text);border:1px solid var(--node-stroke);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('tl-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'tl_eventDeleted', index: sel.index, section: sel.section });
        timelineSelectedEvent = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('tl-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function createTimelineSection() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Section Name</div>
            <input class="property-input" id="tl-dlg-secname" value="New Section" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="tl-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Section</button>
        </div>
    `;
    document.getElementById('tl-dlg-ok').addEventListener('click', function() {
        const name = document.getElementById('tl-dlg-secname').value.trim();
        if (!name) return;
        postMessage({ type: 'tl_sectionCreated', name });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('tl-dlg-secname').select(), 50);
}

function editTimelineSection(sectionName) {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Section Name</div>
            <input class="property-input" id="tl-dlg-secname" value="${_escHtml(sectionName)}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="tl-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('tl-dlg-ok').addEventListener('click', function() {
        const newName = document.getElementById('tl-dlg-secname').value.trim();
        if (!newName) return;
        postMessage({ type: 'tl_sectionEdited', oldName: sectionName, newName });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('tl-dlg-secname').select(), 50);
}

function deleteTimelineSection() {
    if (!timelineSelectedSection) return;
    const sectionName = timelineSelectedSection;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Delete section "${_escHtml(sectionName)}" and all its events?</div>
        </div>
        <div class="property-row" style="margin-top:8px;display:flex;gap:8px">
            <button id="tl-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="tl-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--toolbar-bg);color:var(--node-text);border:1px solid var(--node-stroke);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('tl-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'tl_sectionDeleted', name: sectionName });
        timelineSelectedSection = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('tl-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function editTimelineSettings() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Timeline Settings';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Title</div>
            <input class="property-input" id="tl-dlg-title" value="${_escHtml(timelineModel.title || '')}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="tl-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('tl-dlg-ok').addEventListener('click', function() {
        const title = document.getElementById('tl-dlg-title').value.trim();
        postMessage({ type: 'tl_settingsChanged', title: title || null });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('tl-dlg-title').select(), 50);
}
