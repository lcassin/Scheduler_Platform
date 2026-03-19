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
        ganttModel = JSON.parse(jsonStr);
        ganttSelectedTask = null;
        ganttSelectedSection = null;
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

    const isDark = document.body.classList.contains('dark-theme');
    const bgColor = isDark ? '#1e1e2e' : '#ffffff';
    const textColor = isDark ? '#cdd6f4' : '#333333';
    const headerBg = isDark ? '#313244' : '#f0f0f0';
    const borderColor = isDark ? '#45475a' : '#cccccc';
    const sectionBg1 = isDark ? '#1e1e2e' : '#ffffff';
    const sectionBg2 = isDark ? '#252536' : '#f8f8f8';
    const taskDoneColor = isDark ? '#a6e3a1' : '#4caf50';
    const taskActiveColor = isDark ? '#89b4fa' : '#2196f3';
    const taskCritColor = isDark ? '#f38ba8' : '#f44336';
    const taskNormalColor = isDark ? '#74c7ec' : '#42a5f5';
    const milestoneColor = isDark ? '#f9e2af' : '#ff9800';

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
    const timelineWidth = 600;
    const totalWidth = labelWidth + timelineWidth + padding * 2;
    const totalHeight = headerHeight + totalRows * rowHeight + padding * 2 + 60; // extra for toolbar

    // Create SVG
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', totalWidth);
    svg.setAttribute('height', totalHeight);
    svg.style.display = 'block';
    svg.style.margin = '20px auto';

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
        rowBg.setAttribute('fill', i % 2 === 0 ? 'transparent' : (isDark ? 'rgba(255,255,255,0.02)' : 'rgba(0,0,0,0.02)'));
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
                bar.setAttribute('stroke', isDark ? '#f9e2af' : '#ff9800');
                bar.setAttribute('stroke-width', '2');
            }
            svg.appendChild(bar);

            // Status indicators on bar
            if (tags.includes('done')) {
                const check = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                check.setAttribute('x', barX + barWidth / 2);
                check.setAttribute('y', barY + taskBarHeight / 2 + 4);
                check.setAttribute('text-anchor', 'middle');
                check.setAttribute('fill', isDark ? '#1e1e2e' : '#ffffff');
                check.setAttribute('font-size', '12');
                check.textContent = 'Done';
                svg.appendChild(check);
            } else if (tags.includes('active')) {
                const activeLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                activeLabel.setAttribute('x', barX + barWidth / 2);
                activeLabel.setAttribute('y', barY + taskBarHeight / 2 + 4);
                activeLabel.setAttribute('text-anchor', 'middle');
                activeLabel.setAttribute('fill', isDark ? '#1e1e2e' : '#ffffff');
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
    renderGanttToolbar(svg, padding, toolbarY, totalWidth - padding * 2, isDark, textColor);

    canvas.appendChild(svg);
}

function renderGanttToolbar(svg, x, y, width, isDark, textColor) {
    const btnBg = isDark ? '#313244' : '#e0e0e0';
    const btnHover = isDark ? '#45475a' : '#bdbdbd';
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
    const label = prompt('Task label:', 'New Task');
    if (!label) return;

    const startDate = prompt('Start date (YYYY-MM-DD or "after taskId"):', '');
    const endDate = prompt('End date or duration (e.g., 2024-01-15 or 5d):', '5d');
    const status = prompt('Status (none/done/active/crit/milestone):', 'active');
    const sectionName = ganttModel.sections && ganttModel.sections.length > 0
        ? prompt('Section name (or leave empty for none):', ganttModel.sections[0])
        : null;

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
}

function editGanttTask(index, section) {
    if (!ganttModel || !ganttModel.tasks) return;

    // index is section-local: find matching task within its group
    const sectionTasks = section
        ? ganttModel.tasks.filter(t => t.section === section)
        : ganttModel.tasks.filter(t => !t.section);

    const task = (index >= 0 && index < sectionTasks.length) ? sectionTasks[index] : null;
    if (!task) return;

    const label = prompt('Task label:', task.label);
    if (label === null) return;

    const startDate = prompt('Start date:', task.startDate || '');
    const endDate = prompt('End date/duration:', task.endDate || '');
    const statusStr = prompt('Status tags (comma-separated: done,active,crit,milestone):', (task.tags || []).join(','));
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
}

function deleteGanttTask() {
    if (!ganttSelectedTask) return;
    if (!confirm('Delete this task?')) return;

    postMessage({
        type: 'gantt_taskDeleted',
        index: ganttSelectedTask.index,
        section: ganttSelectedTask.section || null
    });
    ganttSelectedTask = null;
}

function createGanttSection() {
    const name = prompt('Section name:', 'New Section');
    if (!name) return;

    postMessage({ type: 'gantt_sectionCreated', name });
}

function editGanttSection(name) {
    const newName = prompt('Edit section name:', name);
    if (!newName || newName === name) return;

    postMessage({ type: 'gantt_sectionEdited', oldName: name, name: newName });
}

function editGanttTitle() {
    const newTitle = prompt('Chart title:', ganttModel.title || '');
    if (newTitle === null) return;

    postMessage({ type: 'gantt_settingsChanged', title: newTitle });
}

function editGanttSettings() {
    const title = prompt('Chart title:', ganttModel.title || '');
    if (title === null) return;

    const dateFormat = prompt('Date format:', ganttModel.dateFormat || 'YYYY-MM-DD');
    const axisFormat = prompt('Axis format:', ganttModel.axisFormat || '%Y-%m-%d');
    const excludes = prompt('Excludes (e.g., weekends):', ganttModel.excludes || '');

    postMessage({
        type: 'gantt_settingsChanged',
        title,
        dateFormat: dateFormat || 'YYYY-MM-DD',
        axisFormat: axisFormat || null,
        excludes: excludes || null
    });
}
