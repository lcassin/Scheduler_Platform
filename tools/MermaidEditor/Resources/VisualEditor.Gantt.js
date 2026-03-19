// ============================================================
// VisualEditor.Gantt.js - Gantt Chart Visual Editor
// Timeline-based editor with task bars, sections, and dependencies
// ============================================================

// ========== Gantt State ==========
let ganttModel = null;
let ganttSelectedTask = null;
let ganttSelectedSection = null;

// ========== Gantt Load/Restore ==========

window.loadGanttDiagram = function(jsonStr) {
    try {
        currentDiagramType = 'gantt';
        ganttModel = JSON.parse(jsonStr);
        ganttSelectedTask = null;
        ganttSelectedSection = null;
        updateToolbarForDiagramType();
        renderGanttDiagram();
    } catch (e) {
        console.error('Failed to load gantt diagram:', e);
    }
};

window.restoreGanttDiagram = function(jsonStr) {
    try {
        ganttModel = JSON.parse(jsonStr);
        renderGanttDiagram();
    } catch (e) {
        console.error('Failed to restore gantt diagram:', e);
    }
};

window.refreshGanttDiagram = function(jsonStr) {
    try {
        ganttModel = JSON.parse(jsonStr);
        renderGanttDiagram();
    } catch (e) {
        console.error('Failed to refresh gantt diagram:', e);
    }
};

// ========== Gantt Rendering ==========

function renderGanttDiagram() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !ganttModel) return;

    // Show editorCanvas, hide diagram-svg for standalone SVG rendering
    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';

    canvas.innerHTML = '';

    // Read theme colors from CSS variables (set by theme-light / theme-twilight / default dark)
    const cs = getComputedStyle(document.documentElement);
    const cv = (v) => cs.getPropertyValue(v).trim();
    const bgColor = cv('--bg-color') || '#1E1E1E';
    const textColor = cv('--node-text') || '#D4D4D4';
    const headerBg = cv('--toolbar-bg') || '#2D2D30';
    const borderColor = cv('--node-stroke') || '#3E3E42';
    const sectionBg1 = cv('--bg-color') || '#1E1E1E';
    const isLight = document.body.classList.contains('theme-light');
    const isTwilight = document.body.classList.contains('theme-twilight');
    const sectionBg2 = isLight ? '#f8f8f8' : (cv('--subgraph-fill') || '#252536');
    const taskDoneColor = isLight ? '#4caf50' : (isTwilight ? '#74c7ec' : '#a6e3a1');
    const taskActiveColor = cv('--node-selected-stroke') || (isLight ? '#0078D4' : (isTwilight ? '#4A90D9' : '#007ACC'));
    const taskCritColor = isLight ? '#f44336' : (isTwilight ? '#e06c75' : '#f38ba8');
    const taskNormalColor = cv('--edge-color') || (isLight ? '#999999' : (isTwilight ? '#5A6A8A' : '#6A6A6A'));
    const milestoneColor = isLight ? '#ff9800' : (isTwilight ? '#d19a66' : '#f9e2af');

    // Layout constants
    const headerHeight = 50;
    const rowHeight = 40;
    const labelWidth = 220;
    const padding = 16;
    const taskBarHeight = 24;

    // Collect all tasks in order
    const allTasks = [];
    const sectionStarts = [];

    // Top-level tasks
    if (ganttModel.tasks) {
        ganttModel.tasks.forEach(t => {
            allTasks.push({ ...t, section: null });
        });
    }

    // Section tasks
    if (ganttModel.sections) {
        ganttModel.sections.forEach(sectionName => {
            sectionStarts.push({ name: sectionName, startIndex: allTasks.length });
            const sectionTasks = (ganttModel.tasks || []).filter(t => t.section === sectionName);
            sectionTasks.forEach(t => {
                allTasks.push({ ...t });
            });
        });
    }

    // If tasks have section info directly from the DTO
    if (ganttModel.tasks) {
        // Re-collect properly: tasks without section first, then by section
        allTasks.length = 0;
        sectionStarts.length = 0;

        const unsectioned = ganttModel.tasks.filter(t => !t.section);
        unsectioned.forEach(t => allTasks.push(t));

        if (ganttModel.sections) {
            ganttModel.sections.forEach(sectionName => {
                sectionStarts.push({ name: sectionName, startIndex: allTasks.length });
                const sectionTasks = ganttModel.tasks.filter(t => t.section === sectionName);
                sectionTasks.forEach(t => allTasks.push(t));
            });
        }
    }

    const totalRows = Math.max(allTasks.length, 1);
    // Make width responsive to container; use window.innerWidth as fallback when canvas
    // hasn't been laid out yet (e.g. first load from cache before the container is visible)
    const containerWidth = (canvas.clientWidth > 50 ? canvas.clientWidth : null) || (canvas.parentElement && canvas.parentElement.clientWidth > 50 ? canvas.parentElement.clientWidth : null) || window.innerWidth;
    const timelineWidth = Math.max(200, containerWidth - labelWidth - padding * 2 - 40);
    const totalWidth = labelWidth + timelineWidth + padding * 2;
    const totalHeight = headerHeight + totalRows * rowHeight + padding * 2 + 60; // extra for toolbar

    // Create SVG
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', totalWidth);
    svg.setAttribute('height', totalHeight);
    svg.setAttribute('viewBox', `0 0 ${totalWidth} ${totalHeight}`);
    svg.style.display = 'block';
    svg.style.maxWidth = '100%';
    svg.style.height = 'auto';

    // Background
    const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    bg.setAttribute('width', totalWidth);
    bg.setAttribute('height', totalHeight);
    bg.setAttribute('fill', bgColor);
    bg.setAttribute('rx', '8');
    svg.appendChild(bg);

    // Title
    if (ganttModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', totalWidth / 2);
        titleText.setAttribute('y', padding + 14);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '16');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = ganttModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', () => editGanttTitle());
        svg.appendChild(titleText);
    }

    const startY = padding + (ganttModel.title ? 30 : 0);

    // Header row
    const headerRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    headerRect.setAttribute('x', padding);
    headerRect.setAttribute('y', startY);
    headerRect.setAttribute('width', totalWidth - padding * 2);
    headerRect.setAttribute('height', headerHeight);
    headerRect.setAttribute('fill', headerBg);
    headerRect.setAttribute('rx', '4');
    svg.appendChild(headerRect);

    // Header labels
    const taskHeader = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    taskHeader.setAttribute('x', padding + 10);
    taskHeader.setAttribute('y', startY + headerHeight / 2 + 5);
    taskHeader.setAttribute('fill', textColor);
    taskHeader.setAttribute('font-size', '13');
    taskHeader.setAttribute('font-weight', 'bold');
    taskHeader.textContent = 'Task';
    svg.appendChild(taskHeader);

    const timelineHeader = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    timelineHeader.setAttribute('x', padding + labelWidth + timelineWidth / 2);
    timelineHeader.setAttribute('y', startY + headerHeight / 2 + 5);
    timelineHeader.setAttribute('text-anchor', 'middle');
    timelineHeader.setAttribute('fill', textColor);
    timelineHeader.setAttribute('font-size', '13');
    timelineHeader.setAttribute('font-weight', 'bold');
    timelineHeader.textContent = 'Timeline';
    svg.appendChild(timelineHeader);

    // Separator line
    const sepLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    sepLine.setAttribute('x1', padding + labelWidth);
    sepLine.setAttribute('y1', startY);
    sepLine.setAttribute('x2', padding + labelWidth);
    sepLine.setAttribute('y2', startY + headerHeight + totalRows * rowHeight);
    sepLine.setAttribute('stroke', borderColor);
    sepLine.setAttribute('stroke-width', '1');
    svg.appendChild(sepLine);

    // Render section headers and tasks
    let currentSectionIdx = 0;
    const rowStartY = startY + headerHeight;

    for (let i = 0; i < allTasks.length; i++) {
        const task = allTasks[i];
        const y = rowStartY + i * rowHeight;

        // Check if this is the start of a section
        const sectionInfo = sectionStarts.find(s => s.startIndex === i);
        if (sectionInfo) {
            // Draw section background
            const sectionEndIdx = sectionStarts.findIndex(s => s.startIndex > i);
            const sectionTaskCount = sectionEndIdx >= 0
                ? sectionStarts[sectionEndIdx].startIndex - i
                : allTasks.length - i;

            const secBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            secBg.setAttribute('x', padding);
            secBg.setAttribute('y', y);
            secBg.setAttribute('width', labelWidth - 2);
            secBg.setAttribute('height', sectionTaskCount * rowHeight);
            secBg.setAttribute('fill', currentSectionIdx % 2 === 0 ? sectionBg1 : sectionBg2);
            secBg.setAttribute('opacity', '0.5');
            svg.appendChild(secBg);

            // Section label
            const secLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            secLabel.setAttribute('x', padding + 6);
            secLabel.setAttribute('y', y + 14);
            secLabel.setAttribute('fill', textColor);
            secLabel.setAttribute('font-size', '11');
            secLabel.setAttribute('font-weight', 'bold');
            secLabel.setAttribute('opacity', '0.7');
            secLabel.textContent = sectionInfo.name;
            secLabel.style.cursor = 'pointer';
            const secName = sectionInfo.name;
            secLabel.addEventListener('dblclick', () => editGanttSection(secName));
            svg.appendChild(secLabel);

            currentSectionIdx++;
        }

        // Row background (alternating)
        const rowBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rowBg.setAttribute('x', padding + labelWidth);
        rowBg.setAttribute('y', y);
        rowBg.setAttribute('width', timelineWidth);
        rowBg.setAttribute('height', rowHeight);
        rowBg.setAttribute('fill', i % 2 === 0 ? 'transparent' : (isLight ? 'rgba(0,0,0,0.02)' : 'rgba(255,255,255,0.02)'));
        svg.appendChild(rowBg);

        // Row border
        const rowLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        rowLine.setAttribute('x1', padding);
        rowLine.setAttribute('y1', y + rowHeight);
        rowLine.setAttribute('x2', totalWidth - padding);
        rowLine.setAttribute('y2', y + rowHeight);
        rowLine.setAttribute('stroke', borderColor);
        rowLine.setAttribute('stroke-opacity', '0.3');
        rowLine.setAttribute('stroke-width', '1');
        svg.appendChild(rowLine);

        // Task label
        const labelText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        labelText.setAttribute('x', padding + 10);
        labelText.setAttribute('y', y + rowHeight / 2 + 5 + (sectionInfo ? 10 : 0));
        labelText.setAttribute('fill', textColor);
        labelText.setAttribute('font-size', '12');
        labelText.textContent = task.label || 'Task';
        labelText.style.cursor = 'pointer';
        // Calculate section-local index for C# bridge compatibility
        const taskSection = task.section;
        let taskIdx;
        if (taskSection) {
            // Count how many tasks with same section appear before this one
            let localIdx = 0;
            for (let j = 0; j < i; j++) {
                if (allTasks[j].section === taskSection) localIdx++;
            }
            taskIdx = localIdx;
        } else {
            // Count how many unsectioned tasks appear before this one
            let localIdx = 0;
            for (let j = 0; j < i; j++) {
                if (!allTasks[j].section) localIdx++;
            }
            taskIdx = localIdx;
        }
        labelText.addEventListener('click', () => selectGanttTask(taskIdx, taskSection));
        labelText.addEventListener('dblclick', () => editGanttTask(taskIdx, taskSection));
        svg.appendChild(labelText);

        // Task bar in timeline area
        const tags = task.tags || [];
        let barColor = taskNormalColor;
        if (tags.includes('done')) barColor = taskDoneColor;
        else if (tags.includes('active')) barColor = taskActiveColor;
        else if (tags.includes('crit')) barColor = taskCritColor;
        if (task.isMilestone) barColor = milestoneColor;

        // Simple proportional placement (without actual date parsing)
        const barX = padding + labelWidth + 20 + (i * 40) % (timelineWidth - 150);
        const barWidth = task.isMilestone ? 16 : Math.max(60, 120 - i * 5);
        const barY = y + (rowHeight - taskBarHeight) / 2 + (sectionInfo ? 5 : 0);

        if (task.isMilestone) {
            // Draw diamond for milestone
            const diamond = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
            const cx = barX + 8;
            const cy = barY + taskBarHeight / 2;
            const r = 8;
            diamond.setAttribute('points', `${cx},${cy - r} ${cx + r},${cy} ${cx},${cy + r} ${cx - r},${cy}`);
            diamond.setAttribute('fill', barColor);
            diamond.style.cursor = 'pointer';
            diamond.addEventListener('click', () => selectGanttTask(taskIdx, taskSection));
            diamond.addEventListener('dblclick', () => editGanttTask(taskIdx, taskSection));
            svg.appendChild(diamond);
        } else {
            // Draw task bar
            const bar = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            bar.setAttribute('x', barX);
            bar.setAttribute('y', barY);
            bar.setAttribute('width', barWidth);
            bar.setAttribute('height', taskBarHeight);
            bar.setAttribute('fill', barColor);
            bar.setAttribute('rx', '4');
            bar.setAttribute('ry', '4');
            bar.style.cursor = 'pointer';
            bar.addEventListener('click', () => selectGanttTask(taskIdx, taskSection));
            bar.addEventListener('dblclick', () => editGanttTask(taskIdx, taskSection));

            // Highlight selected
            if (ganttSelectedTask !== null && ganttSelectedTask.index === taskIdx) {
                bar.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
                bar.setAttribute('stroke-width', '2');
            }
            svg.appendChild(bar);

            // Status indicators on bar
            if (tags.includes('done')) {
                const check = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                check.setAttribute('x', barX + barWidth / 2);
                check.setAttribute('y', barY + taskBarHeight / 2 + 4);
                check.setAttribute('text-anchor', 'middle');
                check.setAttribute('fill', isLight ? '#ffffff' : '#1e1e2e');
                check.setAttribute('font-size', '12');
                check.textContent = 'Done';
                svg.appendChild(check);
            } else if (tags.includes('active')) {
                const activeLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                activeLabel.setAttribute('x', barX + barWidth / 2);
                activeLabel.setAttribute('y', barY + taskBarHeight / 2 + 4);
                activeLabel.setAttribute('text-anchor', 'middle');
                activeLabel.setAttribute('fill', isLight ? '#ffffff' : '#1e1e2e');
                activeLabel.setAttribute('font-size', '11');
                activeLabel.textContent = 'Active';
                svg.appendChild(activeLabel);
            }
        }

        // Date info text
        const dateInfo = [];
        if (task.startDate) dateInfo.push(task.startDate);
        if (task.endDate) dateInfo.push(task.endDate);
        if (dateInfo.length > 0) {
            const dateText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            dateText.setAttribute('x', barX + (task.isMilestone ? 20 : barWidth + 8));
            dateText.setAttribute('y', barY + taskBarHeight / 2 + 4);
            dateText.setAttribute('fill', textColor);
            dateText.setAttribute('font-size', '10');
            dateText.setAttribute('opacity', '0.6');
            dateText.textContent = dateInfo.join(' - ');
            svg.appendChild(dateText);
        }
    }

    // Empty state
    if (allTasks.length === 0) {
        const emptyText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        emptyText.setAttribute('x', totalWidth / 2);
        emptyText.setAttribute('y', rowStartY + 40);
        emptyText.setAttribute('text-anchor', 'middle');
        emptyText.setAttribute('fill', textColor);
        emptyText.setAttribute('font-size', '14');
        emptyText.setAttribute('opacity', '0.5');
        emptyText.textContent = 'No tasks. Click "Add Task" to get started.';
        svg.appendChild(emptyText);
    }

    // Toolbar area at bottom
    const toolbarY = startY + headerHeight + totalRows * rowHeight + 20;
    renderGanttToolbar(svg, padding, toolbarY, totalWidth - padding * 2, isLight, textColor);

    canvas.appendChild(svg);
}

function renderGanttToolbar(svg, x, y, width, isLight, textColor) {
    const btnBg = isLight ? '#e0e0e0' : '#313244';
    const btnHover = isLight ? '#bdbdbd' : '#45475a';
    const buttons = [
        { label: '+ Add Task', action: () => createGanttTask() },
        { label: '+ Add Section', action: () => createGanttSection() },
        { label: 'Settings', action: () => editGanttSettings() }
    ];

    if (ganttSelectedTask !== null) {
        buttons.push({ label: 'Delete Task', action: () => deleteGanttTask() });
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
        btnRect.addEventListener('click', btn.action);
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

// ========== Gantt Interactions ==========

function selectGanttTask(index, section) {
    ganttSelectedTask = { index, section };
    ganttSelectedSection = null;
    renderGanttDiagram();
    postMessage({ type: 'gantt_taskSelected', index, section });
}

function createGanttTask() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Task';
    const body = document.querySelector('.property-panel-body');

    const sectionOptions = (ganttModel.sections || []).map(s =>
        `<option value="${_escHtml(s)}">${_escHtml(s)}</option>`).join('');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="gantt-dlg-label" value="New Task" />
        </div>
        <div class="property-row">
            <div class="property-label">Start Date</div>
            <input class="property-input" id="gantt-dlg-start" placeholder="YYYY-MM-DD or after taskId" />
        </div>
        <div class="property-row">
            <div class="property-label">End / Duration</div>
            <input class="property-input" id="gantt-dlg-end" value="5d" placeholder="e.g. 2024-01-15 or 5d" />
        </div>
        <div class="property-row">
            <div class="property-label">Status</div>
            <select class="property-select" id="gantt-dlg-status">
                <option value="active" selected>Active</option>
                <option value="done">Done</option>
                <option value="crit">Critical</option>
                <option value="milestone">Milestone</option>
                <option value="none">None</option>
            </select>
        </div>
        ${ganttModel.sections && ganttModel.sections.length > 0 ? `
        <div class="property-row">
            <div class="property-label">Section</div>
            <select class="property-select" id="gantt-dlg-section">
                <option value="">(none)</option>
                ${sectionOptions}
            </select>
        </div>` : ''}
        <div class="property-row" style="margin-top:8px">
            <button class="property-btn" id="gantt-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Task</button>
        </div>
    `;

    document.getElementById('gantt-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('gantt-dlg-label').value.trim();
        if (!label) return;
        const startDate = document.getElementById('gantt-dlg-start').value.trim();
        const endDate = document.getElementById('gantt-dlg-end').value.trim();
        const status = document.getElementById('gantt-dlg-status').value;
        const secEl = document.getElementById('gantt-dlg-section');
        const sectionName = secEl ? secEl.value : null;

        const tags = [];
        if (status && status !== 'none') tags.push(status);

        postMessage({
            type: 'gantt_taskCreated',
            label,
            startDate: startDate || null,
            endDate: endDate || '5d',
            tags,
            section: sectionName || null
        });
        propertyPanel.classList.remove('visible');
    });

    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('gantt-dlg-label').select(), 50);
}

function editGanttTask(index, section) {
    if (!ganttModel || !ganttModel.tasks) return;

    // index is section-local: find matching task within its group
    const sectionTasks = section
        ? ganttModel.tasks.filter(t => t.section === section)
        : ganttModel.tasks.filter(t => !t.section);

    const task = (index >= 0 && index < sectionTasks.length) ? sectionTasks[index] : null;
    if (!task) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Task';
    const body = document.querySelector('.property-panel-body');

    const currentTags = (task.tags || []).join(',');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="gantt-dlg-label" value="${_escHtml(task.label || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Start Date</div>
            <input class="property-input" id="gantt-dlg-start" value="${_escHtml(task.startDate || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">End / Duration</div>
            <input class="property-input" id="gantt-dlg-end" value="${_escHtml(task.endDate || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Status Tags</div>
            <input class="property-input" id="gantt-dlg-tags" value="${_escHtml(currentTags)}" placeholder="done,active,crit,milestone" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button class="property-btn" id="gantt-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;

    document.getElementById('gantt-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('gantt-dlg-label').value.trim();
        if (!label) return;
        const startDate = document.getElementById('gantt-dlg-start').value.trim();
        const endDate = document.getElementById('gantt-dlg-end').value.trim();
        const statusStr = document.getElementById('gantt-dlg-tags').value.trim();
        const tags = statusStr ? statusStr.split(',').map(s => s.trim()).filter(s => s) : [];

        postMessage({
            type: 'gantt_taskEdited',
            index,
            section: section || null,
            label,
            startDate: startDate || null,
            endDate: endDate || null,
            tags
        });
        propertyPanel.classList.remove('visible');
    });

    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('gantt-dlg-label').select(), 50);
}

function deleteGanttTask() {
    if (!ganttSelectedTask) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Task';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row"><div class="property-label" style="width:100%;text-align:center">Delete this task?</div></div>
        <div class="property-row" style="display:flex;gap:8px;margin-top:8px">
            <button id="gantt-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="gantt-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('gantt-dlg-yes').addEventListener('click', function() {
        postMessage({
            type: 'gantt_taskDeleted',
            index: ganttSelectedTask.index,
            section: ganttSelectedTask.section || null
        });
        ganttSelectedTask = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('gantt-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function createGanttSection() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Section Name</div>
            <input class="property-input" id="gantt-dlg-name" value="New Section" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="gantt-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Section</button>
        </div>
    `;
    document.getElementById('gantt-dlg-ok').addEventListener('click', function() {
        const name = document.getElementById('gantt-dlg-name').value.trim();
        if (!name) return;
        postMessage({ type: 'gantt_sectionCreated', name });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('gantt-dlg-name').select(), 50);
}

function editGanttSection(name) {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Section';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Section Name</div>
            <input class="property-input" id="gantt-dlg-name" value="${_escHtml(name)}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="gantt-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('gantt-dlg-ok').addEventListener('click', function() {
        const newName = document.getElementById('gantt-dlg-name').value.trim();
        if (!newName || newName === name) { propertyPanel.classList.remove('visible'); return; }
        postMessage({ type: 'gantt_sectionEdited', oldName: name, name: newName });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('gantt-dlg-name').select(), 50);
}

function editGanttTitle() {
    editGanttSettings();
}

function editGanttSettings() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Gantt Settings';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Chart Title</div>
            <input class="property-input" id="gantt-dlg-title" value="${_escHtml(ganttModel.title || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Date Format</div>
            <input class="property-input" id="gantt-dlg-datefmt" value="${_escHtml(ganttModel.dateFormat || 'YYYY-MM-DD')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Axis Format</div>
            <input class="property-input" id="gantt-dlg-axisfmt" value="${_escHtml(ganttModel.axisFormat || '%Y-%m-%d')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Excludes</div>
            <input class="property-input" id="gantt-dlg-excludes" value="${_escHtml(ganttModel.excludes || '')}" placeholder="e.g. weekends" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="gantt-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('gantt-dlg-ok').addEventListener('click', function() {
        const title = document.getElementById('gantt-dlg-title').value.trim();
        const dateFormat = document.getElementById('gantt-dlg-datefmt').value.trim();
        const axisFormat = document.getElementById('gantt-dlg-axisfmt').value.trim();
        const excludes = document.getElementById('gantt-dlg-excludes').value.trim();
        postMessage({
            type: 'gantt_settingsChanged',
            title,
            dateFormat: dateFormat || 'YYYY-MM-DD',
            axisFormat: axisFormat || null,
            excludes: excludes || null
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('gantt-dlg-title').focus(), 50);
}

// HTML escape helper shared across Gantt/MindMap/Pie dialog functions
function _escHtml(str) {
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
