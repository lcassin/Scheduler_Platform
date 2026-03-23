// ============================================================
// VisualEditor.Journey.js - Journey Diagram Visual Editor
// User journey map with sections, scored tasks, and actors
// ============================================================

// ========== Journey State ==========
let journeyModel = null;
let journeySelectedTask = null; // { index, section } or null
let journeySelectedSection = null; // section name or null
let journeyClipboard = null; // { type: 'task'|'section', data: ... }

// ========== Journey Load/Restore ==========

window.loadJourneyDiagram = function(jsonStr) {
    try {
        currentDiagramType = 'journey';
        journeyModel = JSON.parse(jsonStr);
        journeySelectedTask = null;
        journeySelectedSection = null;
        editorCanvasZoom = 1;
        updateToolbarForDiagramType();
        renderJourneyDiagram();
    } catch (e) {
        console.error('Failed to load journey diagram:', e);
    }
};

window.restoreJourneyDiagram = function(jsonStr) {
    try {
        journeyModel = JSON.parse(jsonStr);
        renderJourneyDiagram();
    } catch (e) {
        console.error('Failed to restore journey diagram:', e);
    }
};

window.refreshJourneyDiagram = function(jsonStr) {
    try {
        journeyModel = JSON.parse(jsonStr);
        renderJourneyDiagram();
    } catch (e) {
        console.error('Failed to refresh journey diagram:', e);
    }
};

// ========== Journey Rendering ==========

function renderJourneyDiagram() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !journeyModel) return;

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

    // Score colors (0=grey/none, 1=red/frustrated to 5=green/happy)
    const scoreColors = isLight
        ? ['#9e9e9e', '#f44336', '#ff9800', '#ffeb3b', '#8bc34a', '#4caf50']
        : isTwilight
        ? ['#6B6B6B', '#E06C75', '#D19A66', '#E5C07B', '#98C379', '#5A9E6F']
        : ['#585b70', '#f38ba8', '#fab387', '#f9e2af', '#a6e3a1', '#89b4fa'];

    // Section colors (rotating palette for section headers)
    const sectionColors = isLight
        ? ['#2196f3', '#4caf50', '#ff9800', '#9c27b0', '#f44336', '#009688', '#ff5722', '#3f51b5']
        : isTwilight
        ? ['#4A90D9', '#5A9E6F', '#D19A66', '#B48EAD', '#E06C75', '#56B6C2', '#C678DD', '#61AFEF']
        : ['#89b4fa', '#a6e3a1', '#f9e2af', '#cba6f7', '#f38ba8', '#94e2d5', '#fab387', '#74c7ec'];

    const sectionBg = isLight ? 'rgba(0,0,0,0.03)' : 'rgba(255,255,255,0.03)';
    const sectionHeaderBg = isLight ? 'rgba(0,0,0,0.06)' : 'rgba(255,255,255,0.06)';

    // Layout constants
    const padding = 20;
    const titleHeight = journeyModel.title ? 40 : 0;
    const sectionHeaderHeight = 32;
    const taskRowHeight = 50;
    const scoreBarWidth = 200;
    const scoreBarHeight = 16;
    const labelWidth = 200;
    const actorWidth = 150;

    // Collect all rows in order for rendering
    const allRows = []; // { type: 'section-header'|'task'|'empty-section', ... }

    // Top-level tasks (before any section)
    if (journeyModel.tasks) {
        journeyModel.tasks.forEach((task, i) => {
            allRows.push({ type: 'task', task: task, index: i, section: null });
        });
    }

    // Sections
    let sectionColorIdx = 0;
    if (journeyModel.sections) {
        journeyModel.sections.forEach((section, si) => {
            allRows.push({ type: 'section-header', sectionName: section.name, sectionIdx: si, colorIdx: sectionColorIdx });
            sectionColorIdx++;
            if (section.tasks && section.tasks.length > 0) {
                section.tasks.forEach((task, ti) => {
                    allRows.push({ type: 'task', task: task, index: ti, section: section.name });
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
            totalHeight += taskRowHeight;
        }
    });
    totalHeight += 80; // toolbar space

    const totalWidth = Math.max(700, padding + labelWidth + scoreBarWidth + actorWidth + 100);

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
        journeySelectedTask = null;
        journeySelectedSection = null;
        renderJourneyDiagram();
    });

    // Title
    let currentY = padding;
    if (journeyModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', totalWidth / 2);
        titleText.setAttribute('y', currentY + 20);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '18');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = journeyModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', (e) => { e.stopPropagation(); editJourneySettings(); });
        svg.appendChild(titleText);
        currentY += titleHeight;
    }

    // Column headers
    const colHeaderY = currentY;
    const colHeaders = [
        { label: 'Task', x: padding + 10 },
        { label: 'Score', x: padding + labelWidth + 10 },
        { label: 'Actors', x: padding + labelWidth + scoreBarWidth + 40 }
    ];
    colHeaders.forEach(col => {
        const hText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        hText.setAttribute('x', col.x);
        hText.setAttribute('y', colHeaderY + 14);
        hText.setAttribute('fill', textColor);
        hText.setAttribute('font-size', '11');
        hText.setAttribute('font-weight', 'bold');
        hText.setAttribute('opacity', '0.5');
        hText.textContent = col.label;
        svg.appendChild(hText);
    });
    currentY += 22;

    // Header separator
    const headerSep = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    headerSep.setAttribute('x1', padding);
    headerSep.setAttribute('y1', currentY);
    headerSep.setAttribute('x2', totalWidth - padding);
    headerSep.setAttribute('y2', currentY);
    headerSep.setAttribute('stroke', borderColor);
    headerSep.setAttribute('stroke-opacity', '0.3');
    headerSep.setAttribute('stroke-width', '1');
    svg.appendChild(headerSep);
    currentY += 4;

    // Render rows
    let currentSectionColorIdx = -1;
    let currentSectionColor = null;
    allRows.forEach((row) => {
        if (row.type === 'section-header') {
            currentSectionColorIdx = row.colorIdx;
            currentSectionColor = sectionColors[currentSectionColorIdx % sectionColors.length];
            const secColor = currentSectionColor;

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
            secBg.setAttribute('data-jn-type', 'section');
            secBg.setAttribute('data-jn-section', secName);
            secBg.setAttribute('data-jn-section-index', String(row.sectionIdx));
            secBg.addEventListener('click', (e) => { e.stopPropagation(); selectJourneySection(secName); });
            secBg.addEventListener('dblclick', (e) => { e.stopPropagation(); editJourneySection(secName); });
            svg.appendChild(secBg);

            // Section color indicator bar
            const secBar = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            secBar.setAttribute('x', padding);
            secBar.setAttribute('y', currentY);
            secBar.setAttribute('width', 4);
            secBar.setAttribute('height', sectionHeaderHeight);
            secBar.setAttribute('fill', secColor);
            secBar.setAttribute('rx', '2');
            secBar.style.pointerEvents = 'none';
            svg.appendChild(secBar);

            // Section label
            const secLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            secLabel.setAttribute('x', padding + 14);
            secLabel.setAttribute('y', currentY + sectionHeaderHeight / 2 + 5);
            secLabel.setAttribute('fill', textColor);
            secLabel.setAttribute('font-size', '13');
            secLabel.setAttribute('font-weight', 'bold');
            secLabel.textContent = row.sectionName;
            secLabel.style.cursor = 'pointer';
            secLabel.style.pointerEvents = 'none';
            svg.appendChild(secLabel);

            // Highlight selected section
            if (journeySelectedSection === secName) {
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
            emptyHint.setAttribute('x', padding + 14);
            emptyHint.setAttribute('y', currentY + 24);
            emptyHint.setAttribute('fill', textColor);
            emptyHint.setAttribute('font-size', '11');
            emptyHint.setAttribute('opacity', '0.35');
            emptyHint.setAttribute('font-style', 'italic');
            emptyHint.textContent = '(no tasks \u2013 add one to this section)';
            svg.appendChild(emptyHint);
            currentY += 40;
            return;
        }

        // Task row
        const task = row.task;
        const rowCenterY = currentY + taskRowHeight / 2;
        const score = Math.max(0, Math.min(5, task.score != null ? task.score : 3));
        const scoreColor = scoreColors[score] || scoreColors[0];

        const isSelected = journeySelectedTask !== null &&
            journeySelectedTask.index === row.index &&
            journeySelectedTask.section === row.section;

        // Section color tint for task row (matching Mermaid's per-section coloring)
        const taskSectionColor = currentSectionColor;
        const _hexToRgba = (hex, alpha) => {
            const r = parseInt(hex.slice(1,3), 16);
            const g = parseInt(hex.slice(3,5), 16);
            const b = parseInt(hex.slice(5,7), 16);
            return `rgba(${r},${g},${b},${alpha})`;
        };
        const sectionTint = taskSectionColor ? _hexToRgba(taskSectionColor, isLight ? 0.08 : 0.06) : 'transparent';
        const sectionTintHover = taskSectionColor ? _hexToRgba(taskSectionColor, isLight ? 0.14 : 0.12) : (isLight ? 'rgba(0,0,0,0.03)' : 'rgba(255,255,255,0.03)');

        // Row background with section color tint
        const rowBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rowBg.setAttribute('x', padding);
        rowBg.setAttribute('y', currentY);
        rowBg.setAttribute('width', totalWidth - padding * 2);
        rowBg.setAttribute('height', taskRowHeight);
        rowBg.setAttribute('fill', isSelected ? (isLight ? 'rgba(33,150,243,0.12)' : 'rgba(137,180,250,0.12)') : sectionTint);
        rowBg.setAttribute('rx', '4');
        rowBg.style.cursor = 'pointer';
        rowBg.setAttribute('data-jn-type', 'task');
        rowBg.setAttribute('data-jn-index', String(row.index));
        rowBg.setAttribute('data-jn-section', row.section || '');
        rowBg.addEventListener('click', (e) => { e.stopPropagation(); selectJourneyTask(row.index, row.section); });
        rowBg.addEventListener('dblclick', (e) => { e.stopPropagation(); editJourneyTask(row.index, row.section); });
        rowBg.addEventListener('mouseenter', () => {
            if (!isSelected) rowBg.setAttribute('fill', sectionTintHover);
        });
        rowBg.addEventListener('mouseleave', () => {
            if (!isSelected) rowBg.setAttribute('fill', sectionTint);
        });
        svg.appendChild(rowBg);

        // Task label
        const taskLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        taskLabel.setAttribute('x', padding + 10);
        taskLabel.setAttribute('y', rowCenterY + 4);
        taskLabel.setAttribute('fill', textColor);
        taskLabel.setAttribute('font-size', '13');
        const maxLabelChars = 28;
        const labelText = task.label || '';
        taskLabel.textContent = labelText.length > maxLabelChars ? labelText.substring(0, maxLabelChars - 1) + '\u2026' : labelText;
        taskLabel.style.pointerEvents = 'none';
        svg.appendChild(taskLabel);

        // Score bar background (track)
        const barX = padding + labelWidth + 10;
        const barY = rowCenterY - scoreBarHeight / 2;
        const trackBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        trackBg.setAttribute('x', barX);
        trackBg.setAttribute('y', barY);
        trackBg.setAttribute('width', scoreBarWidth);
        trackBg.setAttribute('height', scoreBarHeight);
        trackBg.setAttribute('fill', isLight ? 'rgba(0,0,0,0.08)' : 'rgba(255,255,255,0.08)');
        trackBg.setAttribute('rx', '8');
        trackBg.style.pointerEvents = 'none';
        svg.appendChild(trackBg);

        // Score bar fill
        const fillWidth = (score / 5) * scoreBarWidth;
        const scoreFill = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        scoreFill.setAttribute('x', barX);
        scoreFill.setAttribute('y', barY);
        scoreFill.setAttribute('width', fillWidth);
        scoreFill.setAttribute('height', scoreBarHeight);
        scoreFill.setAttribute('fill', scoreColor);
        scoreFill.setAttribute('opacity', '0.8');
        scoreFill.setAttribute('rx', '8');
        scoreFill.style.pointerEvents = 'none';
        svg.appendChild(scoreFill);

        // Score emoji inside bar at fill endpoint (moves right as score increases)
        const scoreEmojis = ['\u{1F480}', '\u{1F621}', '\u{1F61F}', '\u{1F610}', '\u{1F642}', '\u{1F600}']; // 💀😡😟😐🙂😀
        const scoreEmoji = scoreEmojis[score] || '';
        const emojiSize = 24; // ~8px larger than scoreBarHeight (16) so it pops
        // Position emoji centered on the fill endpoint
        const emojiX = barX + fillWidth - emojiSize / 2;

        const emojiText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        emojiText.setAttribute('x', Math.max(barX, emojiX));
        emojiText.setAttribute('y', rowCenterY + 8);
        emojiText.setAttribute('font-size', String(emojiSize));
        emojiText.textContent = scoreEmoji;
        emojiText.style.pointerEvents = 'none';
        svg.appendChild(emojiText);

        // Score number (right of bar, extra spacing so emoji at score=5 doesn't overlap)
        const scoreText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        scoreText.setAttribute('x', barX + scoreBarWidth + 18);
        scoreText.setAttribute('y', rowCenterY + 4);
        scoreText.setAttribute('fill', scoreColor);
        scoreText.setAttribute('font-size', '12');
        scoreText.setAttribute('font-weight', 'bold');
        scoreText.textContent = String(score);
        scoreText.style.pointerEvents = 'none';
        svg.appendChild(scoreText);

        // Actors
        const actorsX = padding + labelWidth + scoreBarWidth + 40;
        const actorsText = (task.actors || []).join(', ');
        if (actorsText) {
            const actorLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            actorLabel.setAttribute('x', actorsX);
            actorLabel.setAttribute('y', rowCenterY + 4);
            actorLabel.setAttribute('fill', textColor);
            actorLabel.setAttribute('font-size', '12');
            actorLabel.setAttribute('opacity', '0.7');
            const maxActorChars = 20;
            actorLabel.textContent = actorsText.length > maxActorChars ? actorsText.substring(0, maxActorChars - 1) + '\u2026' : actorsText;
            actorLabel.style.pointerEvents = 'none';
            svg.appendChild(actorLabel);
        }

        // Selected highlight
        if (isSelected) {
            const selRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            selRect.setAttribute('x', padding);
            selRect.setAttribute('y', currentY);
            selRect.setAttribute('width', totalWidth - padding * 2);
            selRect.setAttribute('height', taskRowHeight);
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
        sepLine.setAttribute('y1', currentY + taskRowHeight);
        sepLine.setAttribute('x2', totalWidth - padding);
        sepLine.setAttribute('y2', currentY + taskRowHeight);
        sepLine.setAttribute('stroke', borderColor);
        sepLine.setAttribute('stroke-opacity', '0.15');
        sepLine.setAttribute('stroke-width', '1');
        svg.appendChild(sepLine);

        currentY += taskRowHeight;
    });

    // Toolbar
    const toolbarY = currentY + 10;
    renderJourneyToolbar(svg, 10, toolbarY, totalWidth - 20, isLight, textColor);

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

function renderJourneyToolbar(svg, x, y, width, isLight, textColor) {
    const btnBg = isLight ? '#e0e0e0' : '#313244';
    const btnHover = isLight ? '#bdbdbd' : '#45475a';
    const buttons = [
        { label: '+ Add Task', action: () => createJourneyTask() },
        { label: '+ Add Section', action: () => createJourneySection() },
        { label: 'Settings', action: () => editJourneySettings() }
    ];

    if (journeySelectedTask !== null) {
        buttons.push({ label: 'Edit Task', action: () => editJourneyTask(journeySelectedTask.index, journeySelectedTask.section) });
        buttons.push({ label: 'Delete Task', action: () => deleteJourneyTask() });
    }

    if (journeySelectedSection !== null) {
        buttons.push({ label: 'Edit Section', action: () => editJourneySection(journeySelectedSection) });
        buttons.push({ label: 'Delete Section', action: () => deleteJourneySection() });
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

// ========== Journey Interactions ==========

function selectJourneyTask(index, section) {
    journeySelectedTask = { index, section };
    journeySelectedSection = null;
    renderJourneyDiagram();
    postMessage({ type: 'jn_taskSelected', index, section });
}

function selectJourneySection(sectionName) {
    journeySelectedSection = sectionName;
    journeySelectedTask = null;
    renderJourneyDiagram();
    postMessage({ type: 'jn_sectionSelected', section: sectionName });
}

// ========== Journey Position Helpers ==========

function _jnBuildTaskPositionHtml(sectionName) {
    let tasks;
    if (sectionName) {
        const sec = journeyModel.sections ? journeyModel.sections.find(s => s.name === sectionName) : null;
        tasks = sec ? (sec.tasks || []) : [];
    } else {
        tasks = journeyModel.tasks || [];
    }
    if (tasks.length === 0) return '';
    const options = tasks.map((t, idx) =>
        `<option value="${idx}">After: ${_escHtml(t.label)}</option>`).join('');
    return `
        <div class="property-row" id="jn-dlg-position-row">
            <div class="property-label">Position</div>
            <select class="property-select" id="jn-dlg-position">
                <option value="end" selected>At End</option>
                <option value="start">At Start (Before All)</option>
                ${options}
            </select>
        </div>`;
}

function _jnBuildSectionPositionHtml() {
    const sections = journeyModel.sections || [];
    if (sections.length === 0) return '';
    const options = sections.map((s, idx) =>
        `<option value="${idx}">After: ${_escHtml(s.name)}</option>`).join('');
    return `
        <div class="property-row">
            <div class="property-label">Position</div>
            <select class="property-select" id="jn-dlg-sec-position">
                <option value="end" selected>At End</option>
                <option value="start">At Start (Before All)</option>
                ${options}
            </select>
        </div>`;
}

function _jnReadTaskInsertIndex() {
    const posEl = document.getElementById('jn-dlg-position');
    if (!posEl) return null;
    const val = posEl.value;
    if (val === 'start') return 0;
    if (val !== 'end') return parseInt(val) + 1;
    return null; // null = append at end
}

function _jnReadSectionInsertIndex() {
    const posEl = document.getElementById('jn-dlg-sec-position');
    if (!posEl) return null;
    const val = posEl.value;
    if (val === 'start') return 0;
    if (val !== 'end') return parseInt(val) + 1;
    return null;
}

// ========== Journey Dialogs ==========

function createJourneyTask() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Task';
    const body = document.querySelector('.property-panel-body');

    // Build section options
    let sectionOptions = '<option value="">(Top Level)</option>';
    if (journeyModel.sections) {
        journeyModel.sections.forEach(s => {
            sectionOptions += `<option value="${_escHtml(s.name)}">${_escHtml(s.name)}</option>`;
        });
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Task Name</div>
            <input class="property-input" id="jn-dlg-label" value="New Task" />
        </div>
        <div class="property-row">
            <div class="property-label">Score (0-5)</div>
            <select class="property-select" id="jn-dlg-score">
                <option value="0">0 - None</option>
                <option value="1">1 - Frustrated</option>
                <option value="2">2 - Unhappy</option>
                <option value="3" selected>3 - Neutral</option>
                <option value="4">4 - Happy</option>
                <option value="5">5 - Very Happy</option>
            </select>
        </div>
        <div class="property-row">
            <div class="property-label">Actors (comma separated)</div>
            <input class="property-input" id="jn-dlg-actors" value="" placeholder="e.g. User, Admin" />
        </div>
        <div class="property-row">
            <div class="property-label">Section</div>
            <select class="property-select" id="jn-dlg-section">${sectionOptions}</select>
        </div>
        <div id="jn-dlg-position-container">${_jnBuildTaskPositionHtml(null)}</div>
        <div class="property-row" style="margin-top:8px">
            <button id="jn-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Task</button>
        </div>
    `;

    // Update position dropdown when section changes
    const secEl = document.getElementById('jn-dlg-section');
    if (secEl) {
        secEl.addEventListener('change', function() {
            const container = document.getElementById('jn-dlg-position-container');
            if (container) container.innerHTML = _jnBuildTaskPositionHtml(this.value || null);
        });
    }

    document.getElementById('jn-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('jn-dlg-label').value.trim();
        if (!label) return;
        const score = parseInt(document.getElementById('jn-dlg-score').value, 10);
        const actorsStr = document.getElementById('jn-dlg-actors').value.trim();
        const actors = actorsStr ? actorsStr.split(',').map(a => a.trim()).filter(a => a.length > 0) : [];
        const section = document.getElementById('jn-dlg-section').value || null;
        const insertAtIndex = _jnReadTaskInsertIndex();
        postMessage({ type: 'jn_taskCreated', label, score, actors, section, insertAtIndex });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('jn-dlg-label').select(), 50);
}

function editJourneyTask(index, section) {
    if (!journeyModel) return;

    // Find the task
    let task;
    if (section) {
        const sec = journeyModel.sections ? journeyModel.sections.find(s => s.name === section) : null;
        if (!sec || !sec.tasks || index >= sec.tasks.length) return;
        task = sec.tasks[index];
    } else {
        if (!journeyModel.tasks || index >= journeyModel.tasks.length) return;
        task = journeyModel.tasks[index];
    }

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Task';
    const body = document.querySelector('.property-panel-body');

    // Build section options
    let sectionOptions = `<option value="" ${!section ? 'selected' : ''}>(Top Level)</option>`;
    if (journeyModel.sections) {
        journeyModel.sections.forEach(s => {
            const sel = s.name === section ? 'selected' : '';
            sectionOptions += `<option value="${_escHtml(s.name)}" ${sel}>${_escHtml(s.name)}</option>`;
        });
    }

    // Score options
    let scoreOptions = '';
    const scoreLabels = ['0 - None', '1 - Frustrated', '2 - Unhappy', '3 - Neutral', '4 - Happy', '5 - Very Happy'];
    for (let i = 0; i <= 5; i++) {
        const sel = task.score === i ? 'selected' : '';
        scoreOptions += `<option value="${i}" ${sel}>${scoreLabels[i]}</option>`;
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Task Name</div>
            <input class="property-input" id="jn-dlg-label" value="${_escHtml(task.label)}" />
        </div>
        <div class="property-row">
            <div class="property-label">Score (0-5)</div>
            <select class="property-select" id="jn-dlg-score">${scoreOptions}</select>
        </div>
        <div class="property-row">
            <div class="property-label">Actors (comma separated)</div>
            <input class="property-input" id="jn-dlg-actors" value="${_escHtml((task.actors || []).join(', '))}" />
        </div>
        <div class="property-row">
            <div class="property-label">Section</div>
            <select class="property-select" id="jn-dlg-section">${sectionOptions}</select>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="jn-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('jn-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('jn-dlg-label').value.trim();
        if (!label) return;
        const score = parseInt(document.getElementById('jn-dlg-score').value, 10);
        const actorsStr = document.getElementById('jn-dlg-actors').value.trim();
        const actors = actorsStr ? actorsStr.split(',').map(a => a.trim()).filter(a => a.length > 0) : [];
        const newSection = document.getElementById('jn-dlg-section').value || null;
        postMessage({ type: 'jn_taskEdited', index, section, label, score, actors, newSection });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('jn-dlg-label').select(), 50);
}

function deleteJourneyTask() {
    if (!journeySelectedTask) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Task';
    const body = document.querySelector('.property-panel-body');

    // Find task for display
    let taskLabel = 'this task';
    const sel = journeySelectedTask;
    if (sel.section) {
        const sec = journeyModel.sections ? journeyModel.sections.find(s => s.name === sel.section) : null;
        if (sec && sec.tasks && sel.index < sec.tasks.length) taskLabel = sec.tasks[sel.index].label;
    } else if (journeyModel.tasks && sel.index < journeyModel.tasks.length) {
        taskLabel = journeyModel.tasks[sel.index].label;
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Are you sure you want to delete "${_escHtml(taskLabel)}"?</div>
        </div>
        <div class="property-row" style="margin-top:8px;display:flex;gap:8px">
            <button id="jn-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="jn-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--toolbar-bg);color:var(--node-text);border:1px solid var(--node-stroke);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('jn-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'jn_taskDeleted', index: sel.index, section: sel.section });
        journeySelectedTask = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('jn-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function createJourneySection() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Section Name</div>
            <input class="property-input" id="jn-dlg-secname" value="New Section" />
        </div>
        ${_jnBuildSectionPositionHtml()}
        <div class="property-row" style="margin-top:8px">
            <button id="jn-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Section</button>
        </div>
    `;
    document.getElementById('jn-dlg-ok').addEventListener('click', function() {
        const name = document.getElementById('jn-dlg-secname').value.trim();
        if (!name) return;
        const insertAtIndex = _jnReadSectionInsertIndex();
        postMessage({ type: 'jn_sectionCreated', name, insertAtIndex });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('jn-dlg-secname').select(), 50);
}

function editJourneySection(sectionName) {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Section Name</div>
            <input class="property-input" id="jn-dlg-secname" value="${_escHtml(sectionName)}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="jn-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('jn-dlg-ok').addEventListener('click', function() {
        const newName = document.getElementById('jn-dlg-secname').value.trim();
        if (!newName) return;
        postMessage({ type: 'jn_sectionEdited', oldName: sectionName, newName });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('jn-dlg-secname').select(), 50);
}

function deleteJourneySection() {
    if (!journeySelectedSection) return;
    const sectionName = journeySelectedSection;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Delete section "${_escHtml(sectionName)}" and all its tasks?</div>
        </div>
        <div class="property-row" style="margin-top:8px;display:flex;gap:8px">
            <button id="jn-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="jn-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--toolbar-bg);color:var(--node-text);border:1px solid var(--node-stroke);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('jn-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'jn_sectionDeleted', name: sectionName });
        journeySelectedSection = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('jn-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function editJourneySettings() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Journey Settings';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Title</div>
            <input class="property-input" id="jn-dlg-title" value="${_escHtml(journeyModel.title || '')}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="jn-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('jn-dlg-ok').addEventListener('click', function() {
        const title = document.getElementById('jn-dlg-title').value.trim();
        postMessage({ type: 'jn_settingsChanged', title: title || null });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('jn-dlg-title').select(), 50);
}

// ========== Journey Context Menu ==========

function _jnAddCtxItem(label, onClick) {
    const contextMenu = document.getElementById('context-menu');
    const item = document.createElement('div');
    item.classList.add('context-menu-item');
    item.textContent = label;
    item.addEventListener('click', function(e) {
        e.stopPropagation();
        contextMenu.classList.remove('visible');
        onClick();
    });
    contextMenu.appendChild(item);
}

function _jnAddCtxSeparator() {
    const contextMenu = document.getElementById('context-menu');
    const sep = document.createElement('div');
    sep.classList.add('context-menu-separator');
    contextMenu.appendChild(sep);
}

function _jnGetTaskList(sectionName) {
    if (sectionName) {
        const sec = journeyModel.sections ? journeyModel.sections.find(s => s.name === sectionName) : null;
        return sec ? (sec.tasks || []) : [];
    }
    return journeyModel.tasks || [];
}

function _jnGetSectionIndex(sectionName) {
    if (!journeyModel.sections) return -1;
    return journeyModel.sections.findIndex(s => s.name === sectionName);
}

function _jnCopyTask(index, sectionName) {
    const tasks = _jnGetTaskList(sectionName);
    if (index >= 0 && index < tasks.length) {
        const task = tasks[index];
        journeyClipboard = {
            type: 'task',
            data: { label: task.label, score: task.score, actors: (task.actors || []).slice(), section: sectionName }
        };
    }
}

function _jnCopySection(sectionName) {
    const secIdx = _jnGetSectionIndex(sectionName);
    if (secIdx < 0) return;
    const sec = journeyModel.sections[secIdx];
    journeyClipboard = {
        type: 'section',
        data: {
            name: sec.name + ' (Copy)',
            tasks: sec.tasks ? sec.tasks.map(t => ({ label: t.label, score: t.score, actors: (t.actors || []).slice() })) : []
        }
    };
}

function _jnPasteTask(insertAtIndex, sectionName) {
    if (!journeyClipboard || journeyClipboard.type !== 'task') return;
    const d = journeyClipboard.data;
    postMessage({
        type: 'jn_taskCreated',
        label: d.label,
        score: d.score,
        actors: (d.actors || []).slice(),
        section: sectionName || null,
        insertAtIndex: insertAtIndex
    });
}

function _jnPasteSection(insertAtIndex) {
    if (!journeyClipboard || journeyClipboard.type !== 'section') return;
    const d = journeyClipboard.data;
    postMessage({
        type: 'jn_sectionCreated',
        name: d.name,
        insertAtIndex: insertAtIndex
    });
}

function showJourneyContextMenu(e) {
    if (currentDiagramType !== 'journey') return;
    if (!journeyModel) return;

    e.preventDefault();
    e.stopPropagation();

    const contextMenu = document.getElementById('context-menu');
    contextMenu.innerHTML = '';

    const target = e.target;

    // Check data attributes on target and parent
    const jnType = target.getAttribute('data-jn-type') ||
        (target.parentElement ? target.parentElement.getAttribute('data-jn-type') : null);
    const jnIndex = target.getAttribute('data-jn-index') ||
        (target.parentElement ? target.parentElement.getAttribute('data-jn-index') : null);
    const jnSection = target.getAttribute('data-jn-section') ||
        (target.parentElement ? target.parentElement.getAttribute('data-jn-section') : null);
    const jnSectionIndex = target.getAttribute('data-jn-section-index') ||
        (target.parentElement ? target.parentElement.getAttribute('data-jn-section-index') : null);

    if (jnType === 'task' && jnIndex !== null) {
        const idx = parseInt(jnIndex, 10);
        const section = jnSection || null;

        // Select the task
        selectJourneyTask(idx, section);

        _jnAddCtxItem('\u270E Edit Task', () => { editJourneyTask(idx, section); });
        _jnAddCtxItem('\u{1F5D1} Delete Task', () => {
            journeySelectedTask = { index: idx, section: section };
            deleteJourneyTask();
        });
        _jnAddCtxSeparator();
        _jnAddCtxItem('\u{1F4CB} Copy Task', () => { _jnCopyTask(idx, section); });
        _jnAddCtxSeparator();
        _jnAddCtxItem('\u2191 Insert Task Above', () => {
            postMessage({
                type: 'jn_taskCreated',
                label: 'New Task',
                score: 3,
                actors: [],
                section: section,
                insertAtIndex: idx
            });
        });
        _jnAddCtxItem('\u2193 Insert Task Below', () => {
            postMessage({
                type: 'jn_taskCreated',
                label: 'New Task',
                score: 3,
                actors: [],
                section: section,
                insertAtIndex: idx + 1
            });
        });
        _jnAddCtxItem('\u2191 Insert Section Above', () => {
            if (section) {
                const secIdx = _jnGetSectionIndex(section);
                if (secIdx >= 0) {
                    postMessage({ type: 'jn_sectionCreated', name: 'New Section', insertAtIndex: secIdx });
                }
            } else {
                postMessage({ type: 'jn_sectionCreated', name: 'New Section', insertAtIndex: 0 });
            }
        });

        // Paste options
        if (journeyClipboard) {
            _jnAddCtxSeparator();
            if (journeyClipboard.type === 'task') {
                _jnAddCtxItem('\u{1F4CB} Paste Task Above', () => { _jnPasteTask(idx, section); });
                _jnAddCtxItem('\u{1F4CB} Paste Task Below', () => { _jnPasteTask(idx + 1, section); });
            } else if (journeyClipboard.type === 'section') {
                if (section) {
                    const secIdx = _jnGetSectionIndex(section);
                    _jnAddCtxItem('\u{1F4CB} Paste Section Above', () => { _jnPasteSection(secIdx); });
                    _jnAddCtxItem('\u{1F4CB} Paste Section Below', () => { _jnPasteSection(secIdx + 1); });
                }
            }
        }

    } else if (jnType === 'section' && jnSection) {
        const sectionName = jnSection;
        const secIdx = jnSectionIndex !== null ? parseInt(jnSectionIndex, 10) : _jnGetSectionIndex(sectionName);

        // Select the section
        selectJourneySection(sectionName);

        _jnAddCtxItem('\u270E Edit Section', () => { editJourneySection(sectionName); });
        _jnAddCtxItem('\u{1F5D1} Delete Section', () => {
            journeySelectedSection = sectionName;
            deleteJourneySection();
        });
        _jnAddCtxSeparator();
        _jnAddCtxItem('\u{1F4CB} Copy Section', () => { _jnCopySection(sectionName); });
        _jnAddCtxSeparator();
        _jnAddCtxItem('\u2191 Insert Section Above', () => {
            postMessage({ type: 'jn_sectionCreated', name: 'New Section', insertAtIndex: secIdx });
        });
        _jnAddCtxItem('\u2193 Insert Section Below', () => {
            postMessage({ type: 'jn_sectionCreated', name: 'New Section', insertAtIndex: secIdx + 1 });
        });
        _jnAddCtxSeparator();
        _jnAddCtxItem('\u2191 Insert Task at Top of Section', () => {
            postMessage({
                type: 'jn_taskCreated',
                label: 'New Task',
                score: 3,
                actors: [],
                section: sectionName,
                insertAtIndex: 0
            });
        });
        _jnAddCtxItem('\u2193 Insert Task at Bottom of Section', () => {
            postMessage({
                type: 'jn_taskCreated',
                label: 'New Task',
                score: 3,
                actors: [],
                section: sectionName,
                insertAtIndex: null // append at end
            });
        });

        // Paste options
        if (journeyClipboard) {
            _jnAddCtxSeparator();
            if (journeyClipboard.type === 'task') {
                _jnAddCtxItem('\u{1F4CB} Paste Task at Top', () => { _jnPasteTask(0, sectionName); });
                _jnAddCtxItem('\u{1F4CB} Paste Task at Bottom', () => { _jnPasteTask(null, sectionName); });
            } else if (journeyClipboard.type === 'section') {
                _jnAddCtxItem('\u{1F4CB} Paste Section Above', () => { _jnPasteSection(secIdx); });
                _jnAddCtxItem('\u{1F4CB} Paste Section Below', () => { _jnPasteSection(secIdx + 1); });
            }
        }

    } else {
        // Empty space - show general context menu
        _jnAddCtxItem('+ Add Task', () => { createJourneyTask(); });
        _jnAddCtxItem('+ Add Section', () => { createJourneySection(); });
        _jnAddCtxItem('\u2699 Settings', () => { editJourneySettings(); });

        if (journeyClipboard) {
            _jnAddCtxSeparator();
            if (journeyClipboard.type === 'task') {
                _jnAddCtxItem('\u{1F4CB} Paste Task at Top', () => {
                    _jnPasteTask(0, null);
                });
                _jnAddCtxItem('\u{1F4CB} Paste Task at Bottom', () => {
                    _jnPasteTask(null, null);
                });
            } else if (journeyClipboard.type === 'section') {
                _jnAddCtxItem('\u{1F4CB} Paste Section at Top', () => {
                    _jnPasteSection(0);
                });
                _jnAddCtxItem('\u{1F4CB} Paste Section at Bottom', () => {
                    _jnPasteSection(null);
                });
            }
        }
    }

    positionContextMenu(contextMenu, e.clientX, e.clientY);
}
