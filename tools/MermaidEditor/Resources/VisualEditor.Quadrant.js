// ============================================================
// VisualEditor.Quadrant.js - Quadrant Chart Visual Editor
// Scatter-plot in 4 labeled quadrants with draggable points
// ============================================================

// ========== Quadrant State ==========
let quadrantModel = null;
let quadrantSelectedPoint = null; // index or null
let quadrantClipboard = null; // { type: 'point', data: ... }
let quadrantDragState = null; // { index, startX, startY, origX, origY } or null

// ========== Quadrant Load/Restore ==========

window.loadQuadrantDiagram = function(jsonStr) {
    try {
        currentDiagramType = 'quadrant';
        quadrantModel = JSON.parse(jsonStr);
        quadrantSelectedPoint = null;
        editorCanvasZoom = 1;
        updateToolbarForDiagramType();
        renderQuadrantDiagram();
        // Deferred re-render (rule #44)
        setTimeout(() => renderQuadrantDiagram(), 150);
    } catch (e) {
        console.error('Failed to load quadrant diagram:', e);
    }
};

window.restoreQuadrantDiagram = function(jsonStr) {
    try {
        quadrantModel = JSON.parse(jsonStr);
        renderQuadrantDiagram();
    } catch (e) {
        console.error('Failed to restore quadrant diagram:', e);
    }
};

window.refreshQuadrantDiagram = function(jsonStr) {
    try {
        // Don't refresh during an active drag — it would destroy the SVG and
        // lose the mouseup listener that sends the final position to C#.
        if (quadrantDragState) return;
        quadrantModel = JSON.parse(jsonStr);
        renderQuadrantDiagram();
    } catch (e) {
        console.error('Failed to refresh quadrant diagram:', e);
    }
};

// ========== Quadrant Rendering ==========

function renderQuadrantDiagram() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !quadrantModel) return;

    // Show editorCanvas, hide diagram-svg
    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';

    canvas.innerHTML = '';

    // Read theme colors (rule #8, #9)
    const cs = getComputedStyle(document.body);
    const cv = (v) => cs.getPropertyValue(v).trim();
    const bgColor = cv('--bg-color') || '#1E1E1E';
    const textColor = cv('--node-text') || '#D4D4D4';
    const borderColor = cv('--node-stroke') || '#3E3E42';
    const isLight = document.body.classList.contains('theme-light');
    const isTwilight = document.body.classList.contains('theme-twilight');

    // Quadrant fill colors (matching Mermaid theme)
    const quadrantFills = isLight
        ? ['rgba(76, 175, 80, 0.15)', 'rgba(33, 150, 243, 0.15)', 'rgba(255, 152, 0, 0.15)', 'rgba(244, 67, 54, 0.15)']
        : isTwilight
        ? ['rgba(90, 158, 111, 0.2)', 'rgba(74, 144, 217, 0.2)', 'rgba(209, 154, 102, 0.2)', 'rgba(224, 108, 117, 0.2)']
        : ['rgba(166, 227, 161, 0.15)', 'rgba(137, 180, 250, 0.15)', 'rgba(249, 226, 175, 0.15)', 'rgba(243, 139, 168, 0.15)'];

    // Quadrant text colors
    const quadrantTextColors = isLight
        ? ['#2e7d32', '#1565c0', '#e65100', '#c62828']
        : isTwilight
        ? ['#5A9E6F', '#4A90D9', '#D19A66', '#E06C75']
        : ['#a6e3a1', '#89b4fa', '#f9e2af', '#f38ba8'];

    // Point colors
    const pointFill = isLight ? '#1976d2' : isTwilight ? '#61AFEF' : '#89b4fa';
    const pointStroke = isLight ? '#0d47a1' : isTwilight ? '#4A90D9' : '#74c7ec';
    const selectedStroke = isLight ? '#ff9800' : isTwilight ? '#E5C07B' : '#f9e2af';

    // Layout constants
    const padding = 40;
    const titleHeight = quadrantModel.title ? 50 : 10;
    const axisLabelSpace = 60;
    const chartSize = 500;
    const totalWidth = padding * 2 + axisLabelSpace + chartSize + 40;
    const totalHeight = padding + titleHeight + chartSize + axisLabelSpace + 40;

    // Chart area coordinates
    const chartLeft = padding + axisLabelSpace;
    const chartTop = padding + titleHeight;
    const chartRight = chartLeft + chartSize;
    const chartBottom = chartTop + chartSize;

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
        quadrantSelectedPoint = null;
        renderQuadrantDiagram();
    });

    // Title (double-click to edit settings)
    if (quadrantModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', chartLeft + chartSize / 2);
        titleText.setAttribute('y', padding + 24);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '18');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = quadrantModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', (e) => { e.stopPropagation(); editQuadrantSettings(); });
        svg.appendChild(titleText);
    }

    // Draw quadrant backgrounds (Q1=top-right, Q2=top-left, Q3=bottom-left, Q4=bottom-right)
    const halfW = chartSize / 2;
    const halfH = chartSize / 2;

    // Q2 - top-left
    const q2Rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    q2Rect.setAttribute('x', chartLeft);
    q2Rect.setAttribute('y', chartTop);
    q2Rect.setAttribute('width', halfW);
    q2Rect.setAttribute('height', halfH);
    q2Rect.setAttribute('fill', quadrantFills[1]);
    q2Rect.setAttribute('stroke', borderColor);
    q2Rect.setAttribute('stroke-width', '0.5');
    svg.appendChild(q2Rect);

    // Q1 - top-right
    const q1Rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    q1Rect.setAttribute('x', chartLeft + halfW);
    q1Rect.setAttribute('y', chartTop);
    q1Rect.setAttribute('width', halfW);
    q1Rect.setAttribute('height', halfH);
    q1Rect.setAttribute('fill', quadrantFills[0]);
    q1Rect.setAttribute('stroke', borderColor);
    q1Rect.setAttribute('stroke-width', '0.5');
    svg.appendChild(q1Rect);

    // Q3 - bottom-left
    const q3Rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    q3Rect.setAttribute('x', chartLeft);
    q3Rect.setAttribute('y', chartTop + halfH);
    q3Rect.setAttribute('width', halfW);
    q3Rect.setAttribute('height', halfH);
    q3Rect.setAttribute('fill', quadrantFills[2]);
    q3Rect.setAttribute('stroke', borderColor);
    q3Rect.setAttribute('stroke-width', '0.5');
    svg.appendChild(q3Rect);

    // Q4 - bottom-right
    const q4Rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    q4Rect.setAttribute('x', chartLeft + halfW);
    q4Rect.setAttribute('y', chartTop + halfH);
    q4Rect.setAttribute('width', halfW);
    q4Rect.setAttribute('height', halfH);
    q4Rect.setAttribute('fill', quadrantFills[3]);
    q4Rect.setAttribute('stroke', borderColor);
    q4Rect.setAttribute('stroke-width', '0.5');
    svg.appendChild(q4Rect);

    // Outer border
    const outerBorder = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    outerBorder.setAttribute('x', chartLeft);
    outerBorder.setAttribute('y', chartTop);
    outerBorder.setAttribute('width', chartSize);
    outerBorder.setAttribute('height', chartSize);
    outerBorder.setAttribute('fill', 'none');
    outerBorder.setAttribute('stroke', borderColor);
    outerBorder.setAttribute('stroke-width', '2');
    svg.appendChild(outerBorder);

    // Center cross lines
    const centerLineV = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    centerLineV.setAttribute('x1', chartLeft + halfW);
    centerLineV.setAttribute('y1', chartTop);
    centerLineV.setAttribute('x2', chartLeft + halfW);
    centerLineV.setAttribute('y2', chartBottom);
    centerLineV.setAttribute('stroke', borderColor);
    centerLineV.setAttribute('stroke-width', '1');
    svg.appendChild(centerLineV);

    const centerLineH = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    centerLineH.setAttribute('x1', chartLeft);
    centerLineH.setAttribute('y1', chartTop + halfH);
    centerLineH.setAttribute('x2', chartRight);
    centerLineH.setAttribute('y2', chartTop + halfH);
    centerLineH.setAttribute('stroke', borderColor);
    centerLineH.setAttribute('stroke-width', '1');
    svg.appendChild(centerLineH);

    // Quadrant labels
    const qLabels = [
        { text: quadrantModel.quadrant1 || '', cx: chartLeft + halfW + halfW/2, cy: chartTop + halfH/2, color: quadrantTextColors[0] },
        { text: quadrantModel.quadrant2 || '', cx: chartLeft + halfW/2, cy: chartTop + halfH/2, color: quadrantTextColors[1] },
        { text: quadrantModel.quadrant3 || '', cx: chartLeft + halfW/2, cy: chartTop + halfH + halfH/2, color: quadrantTextColors[2] },
        { text: quadrantModel.quadrant4 || '', cx: chartLeft + halfW + halfW/2, cy: chartTop + halfH + halfH/2, color: quadrantTextColors[3] }
    ];

    qLabels.forEach(ql => {
        if (ql.text) {
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', ql.cx);
            label.setAttribute('y', ql.cy);
            label.setAttribute('text-anchor', 'middle');
            label.setAttribute('dominant-baseline', 'middle');
            label.setAttribute('fill', ql.color);
            label.setAttribute('font-size', '14');
            label.setAttribute('font-weight', 'bold');
            label.setAttribute('opacity', '0.7');
            label.style.pointerEvents = 'none';
            label.textContent = ql.text;
            svg.appendChild(label);
        }
    });

    // X-axis labels
    if (quadrantModel.xAxisLeft) {
        const xLeft = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        xLeft.setAttribute('x', chartLeft);
        xLeft.setAttribute('y', chartBottom + 30);
        xLeft.setAttribute('text-anchor', 'start');
        xLeft.setAttribute('fill', textColor);
        xLeft.setAttribute('font-size', '12');
        xLeft.textContent = quadrantModel.xAxisLeft;
        svg.appendChild(xLeft);
    }

    if (quadrantModel.xAxisRight) {
        const xRight = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        xRight.setAttribute('x', chartRight);
        xRight.setAttribute('y', chartBottom + 30);
        xRight.setAttribute('text-anchor', 'end');
        xRight.setAttribute('fill', textColor);
        xRight.setAttribute('font-size', '12');
        xRight.textContent = quadrantModel.xAxisRight;
        svg.appendChild(xRight);
    }

    // X-axis arrow
    if (quadrantModel.xAxisLeft && quadrantModel.xAxisRight) {
        const xArrow = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        xArrow.setAttribute('x1', chartLeft + 80);
        xArrow.setAttribute('y1', chartBottom + 26);
        xArrow.setAttribute('x2', chartRight - 80);
        xArrow.setAttribute('y2', chartBottom + 26);
        xArrow.setAttribute('stroke', textColor);
        xArrow.setAttribute('stroke-width', '1');
        xArrow.setAttribute('opacity', '0.4');
        xArrow.setAttribute('marker-end', 'url(#qc-arrow)');
        svg.appendChild(xArrow);
    }

    // Y-axis labels
    if (quadrantModel.yAxisBottom) {
        const yBottom = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        yBottom.setAttribute('x', chartLeft - 10);
        yBottom.setAttribute('y', chartBottom);
        yBottom.setAttribute('text-anchor', 'end');
        yBottom.setAttribute('fill', textColor);
        yBottom.setAttribute('font-size', '12');
        yBottom.textContent = quadrantModel.yAxisBottom;
        svg.appendChild(yBottom);
    }

    if (quadrantModel.yAxisTop) {
        const yTop = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        yTop.setAttribute('x', chartLeft - 10);
        yTop.setAttribute('y', chartTop + 14);
        yTop.setAttribute('text-anchor', 'end');
        yTop.setAttribute('fill', textColor);
        yTop.setAttribute('font-size', '12');
        yTop.textContent = quadrantModel.yAxisTop;
        svg.appendChild(yTop);
    }

    // Y-axis arrow
    if (quadrantModel.yAxisBottom && quadrantModel.yAxisTop) {
        const yArrow = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        yArrow.setAttribute('x1', chartLeft - 14);
        yArrow.setAttribute('y1', chartBottom - 20);
        yArrow.setAttribute('x2', chartLeft - 14);
        yArrow.setAttribute('y2', chartTop + 30);
        yArrow.setAttribute('stroke', textColor);
        yArrow.setAttribute('stroke-width', '1');
        yArrow.setAttribute('opacity', '0.4');
        yArrow.setAttribute('marker-end', 'url(#qc-arrow)');
        svg.appendChild(yArrow);
    }

    // Arrow marker definition
    const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
    const marker = document.createElementNS('http://www.w3.org/2000/svg', 'marker');
    marker.setAttribute('id', 'qc-arrow');
    marker.setAttribute('markerWidth', '8');
    marker.setAttribute('markerHeight', '8');
    marker.setAttribute('refX', '6');
    marker.setAttribute('refY', '4');
    marker.setAttribute('orient', 'auto');
    const arrowPath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    arrowPath.setAttribute('d', 'M0,1 L6,4 L0,7');
    arrowPath.setAttribute('fill', 'none');
    arrowPath.setAttribute('stroke', textColor);
    arrowPath.setAttribute('stroke-width', '1');
    arrowPath.setAttribute('opacity', '0.4');
    marker.appendChild(arrowPath);
    defs.appendChild(marker);
    svg.insertBefore(defs, svg.firstChild);

    // Render data points
    if (quadrantModel.points) {
        quadrantModel.points.forEach((point, idx) => {
            // Convert 0-1 coordinates to chart pixels
            // x: 0=left, 1=right; y: 0=bottom, 1=top (Mermaid convention)
            const px = chartLeft + point.x * chartSize;
            const py = chartBottom - point.y * chartSize;
            const isSelected = quadrantSelectedPoint === idx;

            // Point group for drag handling
            const pointGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            pointGroup.setAttribute('data-qc-type', 'point');
            pointGroup.setAttribute('data-qc-index', String(idx));
            pointGroup.style.cursor = 'pointer';

            // Selection glow (if selected)
            if (isSelected) {
                const glow = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                glow.setAttribute('cx', px);
                glow.setAttribute('cy', py);
                glow.setAttribute('r', '12');
                glow.setAttribute('fill', 'none');
                glow.setAttribute('stroke', selectedStroke);
                glow.setAttribute('stroke-width', '3');
                glow.setAttribute('opacity', '0.6');
                pointGroup.appendChild(glow);
            }

            // Point circle
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', px);
            circle.setAttribute('cy', py);
            circle.setAttribute('r', isSelected ? '8' : '6');
            circle.setAttribute('fill', pointFill);
            circle.setAttribute('stroke', isSelected ? selectedStroke : pointStroke);
            circle.setAttribute('stroke-width', isSelected ? '2.5' : '1.5');
            pointGroup.appendChild(circle);

            // Point label
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', px);
            label.setAttribute('y', py + 20);
            label.setAttribute('text-anchor', 'middle');
            label.setAttribute('fill', textColor);
            label.setAttribute('font-size', '11');
            label.textContent = point.label || '';
            label.style.pointerEvents = 'none';
            pointGroup.appendChild(label);

            // Click to select
            pointGroup.addEventListener('click', (e) => {
                if (currentDiagramType !== 'quadrant') return;
                e.stopPropagation();
                quadrantSelectedPoint = idx;
                renderQuadrantDiagram();
            });

            // Double-click to edit
            pointGroup.addEventListener('dblclick', (e) => {
                if (currentDiagramType !== 'quadrant') return;
                e.stopPropagation();
                editQuadrantPoint(idx);
            });

            // Drag support
            pointGroup.addEventListener('mousedown', (e) => {
                if (currentDiagramType !== 'quadrant') return;
                if (e.button !== 0) return; // left click only
                e.stopPropagation();
                e.preventDefault();
                // Close property panel if open so stale values don't overwrite drag
                const pp = document.getElementById('property-panel');
                if (pp) pp.classList.remove('visible');
                quadrantSelectedPoint = idx;
                quadrantDragState = {
                    index: idx,
                    startX: e.clientX,
                    startY: e.clientY,
                    origX: point.x,
                    origY: point.y
                };
            });

            svg.appendChild(pointGroup);
        });
    }

    // Double-click on chart area to add a new point
    const chartOverlay = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    chartOverlay.setAttribute('x', chartLeft);
    chartOverlay.setAttribute('y', chartTop);
    chartOverlay.setAttribute('width', chartSize);
    chartOverlay.setAttribute('height', chartSize);
    chartOverlay.setAttribute('fill', 'transparent');
    chartOverlay.style.pointerEvents = 'all';
    // Insert behind points but in front of quadrant rects
    svg.insertBefore(chartOverlay, svg.querySelector('[data-qc-type="point"]') || null);

    chartOverlay.addEventListener('dblclick', (e) => {
        if (currentDiagramType !== 'quadrant') return;
        e.stopPropagation();
        // Calculate coordinates relative to chart area
        const rect = svg.getBoundingClientRect();
        const scale = chartSize / (rect.width * chartSize / totalWidth);
        const mouseX = (e.clientX - rect.left) * (totalWidth / rect.width);
        const mouseY = (e.clientY - rect.top) * (totalHeight / rect.height);
        const normX = Math.max(0, Math.min(1, (mouseX - chartLeft) / chartSize));
        const normY = Math.max(0, Math.min(1, 1 - (mouseY - chartTop) / chartSize));
        createQuadrantPoint(normX, normY);
    });

    chartOverlay.addEventListener('click', (e) => {
        if (currentDiagramType !== 'quadrant') return;
        e.stopPropagation();
        quadrantSelectedPoint = null;
        renderQuadrantDiagram();
    });

    // Drag handling on SVG
    svg.addEventListener('mousemove', (e) => {
        if (currentDiagramType !== 'quadrant') return;
        if (!quadrantDragState) return;
        const rect = svg.getBoundingClientRect();
        const mouseX = (e.clientX - rect.left) * (totalWidth / rect.width);
        const mouseY = (e.clientY - rect.top) * (totalHeight / rect.height);
        const normX = Math.max(0, Math.min(1, (mouseX - chartLeft) / chartSize));
        const normY = Math.max(0, Math.min(1, 1 - (mouseY - chartTop) / chartSize));

        // Update the point visually (live drag)
        if (quadrantModel.points && quadrantDragState.index < quadrantModel.points.length) {
            quadrantModel.points[quadrantDragState.index].x = normX;
            quadrantModel.points[quadrantDragState.index].y = normY;
            renderQuadrantDiagram();
        }
    });

    svg.addEventListener('mouseup', (e) => {
        if (currentDiagramType !== 'quadrant') return;
        if (!quadrantDragState) return;
        const ds = quadrantDragState;
        quadrantDragState = null;

        // Check if actually moved
        if (quadrantModel.points && ds.index < quadrantModel.points.length) {
            const pt = quadrantModel.points[ds.index];
            if (Math.abs(pt.x - ds.origX) > 0.005 || Math.abs(pt.y - ds.origY) > 0.005) {
                // Send move message to C#
                postMessage({ type: 'qc_pointMoved', index: ds.index, x: pt.x, y: pt.y });
            }
        }
    });

    svg.addEventListener('mouseleave', () => {
        if (quadrantDragState) {
            const ds = quadrantDragState;
            quadrantDragState = null;
            if (quadrantModel.points && ds.index < quadrantModel.points.length) {
                const pt = quadrantModel.points[ds.index];
                if (Math.abs(pt.x - ds.origX) > 0.005 || Math.abs(pt.y - ds.origY) > 0.005) {
                    postMessage({ type: 'qc_pointMoved', index: ds.index, x: pt.x, y: pt.y });
                }
            }
        }
    });

    // Store chart dimensions for minimap
    svg.setAttribute('data-qc-chart-left', chartLeft);
    svg.setAttribute('data-qc-chart-top', chartTop);
    svg.setAttribute('data-qc-chart-size', chartSize);

    canvas.appendChild(svg);

    // Render toolbar
    renderQuadrantToolbar();

    // Update minimap
    if (typeof updateMinimap === 'function') updateMinimap();
}

// ========== Quadrant Toolbar ==========

function renderQuadrantToolbar() {
    const buttons = [
        { id: 'qcAddPoint', label: 'Add Point', icon: '📍' },
        { id: 'qcEdit', label: 'Edit', icon: '✏️' },
        { id: 'qcDelete', label: 'Delete', icon: '🗑️' },
        { id: 'qcSettings', label: 'Settings', icon: '⚙️' }
    ];

    // Show/hide toolbar buttons
    buttons.forEach(btn => {
        const el = document.getElementById(btn.id);
        if (el) {
            el.style.display = 'inline-flex';
            el.title = btn.label;
        }
    });
}

// ========== Quadrant Selection ==========

function selectQuadrantPoint(index) {
    quadrantSelectedPoint = index;
    renderQuadrantDiagram();
}

// ========== Quadrant Point CRUD ==========

function createQuadrantPoint(defaultX, defaultY) {
    if (currentDiagramType !== 'quadrant') return;
    const x = typeof defaultX === 'number' ? defaultX : 0.5;
    const y = typeof defaultY === 'number' ? defaultY : 0.5;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Point';
    const body = document.querySelector('.property-panel-body');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="qcPointLabel" value="New Point" />
        </div>
        <div class="property-row">
            <div class="property-label">X (0–1)</div>
            <input class="property-input" type="number" id="qcPointX" value="${x.toFixed(2)}" min="0" max="1" step="0.01" />
        </div>
        <div class="property-row">
            <div class="property-label">Y (0–1)</div>
            <input class="property-input" type="number" id="qcPointY" value="${y.toFixed(2)}" min="0" max="1" step="0.01" />
        </div>
        <div id="qc-dlg-position-container">${_qcBuildPointPositionHtml()}</div>
        <div class="property-row" style="margin-top:8px">
            <button id="qc-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Point</button>
        </div>
    `;

    document.getElementById('qc-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('qcPointLabel').value.trim() || 'New Point';
        const px = Math.max(0, Math.min(1, parseFloat(document.getElementById('qcPointX').value) || 0.5));
        const py = Math.max(0, Math.min(1, parseFloat(document.getElementById('qcPointY').value) || 0.5));
        const insertIdx = _qcReadPointInsertIndex();
        postMessage({ type: 'qc_pointCreated', label: label, x: px, y: py, insertAtIndex: insertIdx });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('qcPointLabel').select(), 50);
}

function editQuadrantPoint(index) {
    if (currentDiagramType !== 'quadrant') return;
    if (!quadrantModel || !quadrantModel.points || index < 0 || index >= quadrantModel.points.length) return;

    const point = quadrantModel.points[index];

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Point';
    const body = document.querySelector('.property-panel-body');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="qcPointLabel" value="${(point.label || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">X (0–1)</div>
            <input class="property-input" type="number" id="qcPointX" value="${point.x.toFixed(2)}" min="0" max="1" step="0.01" />
        </div>
        <div class="property-row">
            <div class="property-label">Y (0–1)</div>
            <input class="property-input" type="number" id="qcPointY" value="${point.y.toFixed(2)}" min="0" max="1" step="0.01" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="qc-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;

    document.getElementById('qc-dlg-ok').addEventListener('click', function() {
        if (!quadrantModel || !quadrantModel.points || index >= quadrantModel.points.length) return;
        const label = document.getElementById('qcPointLabel').value.trim() || point.label;
        const px = Math.max(0, Math.min(1, parseFloat(document.getElementById('qcPointX').value) || point.x));
        const py = Math.max(0, Math.min(1, parseFloat(document.getElementById('qcPointY').value) || point.y));
        postMessage({ type: 'qc_pointEdited', index: index, label: label, x: px, y: py });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('qcPointLabel').select(), 50);
}

function deleteQuadrantPoint(index) {
    if (currentDiagramType !== 'quadrant') return;
    if (!quadrantModel || !quadrantModel.points) return;
    if (typeof index !== 'number') index = quadrantSelectedPoint;
    if (index === null || index < 0 || index >= quadrantModel.points.length) return;

    const point = quadrantModel.points[index];

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Point';
    const body = document.querySelector('.property-panel-body');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Delete point "${(point.label || '').replace(/</g, '&lt;')}"?</div>
            <div style="opacity:0.6;font-size:12px">Position: [${point.x.toFixed(2)}, ${point.y.toFixed(2)}]</div>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="qc-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:#d9534f;color:#fff;border:none;border-radius:4px">Delete</button>
        </div>
    `;

    document.getElementById('qc-dlg-ok').addEventListener('click', function() {
        if (!quadrantModel || !quadrantModel.points || index >= quadrantModel.points.length) return;
        postMessage({ type: 'qc_pointDeleted', index: index });
        quadrantSelectedPoint = null;
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

// ========== Quadrant Settings ==========

function editQuadrantSettings() {
    if (currentDiagramType !== 'quadrant') return;
    if (!quadrantModel) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Quadrant Chart Settings';
    const body = document.querySelector('.property-panel-body');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Title</div>
            <input class="property-input" id="qcTitle" value="${(quadrantModel.title || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">X-Axis Left</div>
            <input class="property-input" id="qcXAxisLeft" value="${(quadrantModel.xAxisLeft || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">X-Axis Right</div>
            <input class="property-input" id="qcXAxisRight" value="${(quadrantModel.xAxisRight || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Y-Axis Bottom</div>
            <input class="property-input" id="qcYAxisBottom" value="${(quadrantModel.yAxisBottom || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Y-Axis Top</div>
            <input class="property-input" id="qcYAxisTop" value="${(quadrantModel.yAxisTop || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Quadrant 1 (top-right)</div>
            <input class="property-input" id="qcQ1" value="${(quadrantModel.quadrant1 || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Quadrant 2 (top-left)</div>
            <input class="property-input" id="qcQ2" value="${(quadrantModel.quadrant2 || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Quadrant 3 (bottom-left)</div>
            <input class="property-input" id="qcQ3" value="${(quadrantModel.quadrant3 || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Quadrant 4 (bottom-right)</div>
            <input class="property-input" id="qcQ4" value="${(quadrantModel.quadrant4 || '').replace(/"/g, '&quot;')}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="qc-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save Settings</button>
        </div>
    `;

    document.getElementById('qc-dlg-ok').addEventListener('click', function() {
        postMessage({
            type: 'qc_settingsChanged',
            title: document.getElementById('qcTitle').value.trim() || null,
            xAxisLeft: document.getElementById('qcXAxisLeft').value.trim() || null,
            xAxisRight: document.getElementById('qcXAxisRight').value.trim() || null,
            yAxisBottom: document.getElementById('qcYAxisBottom').value.trim() || null,
            yAxisTop: document.getElementById('qcYAxisTop').value.trim() || null,
            quadrant1: document.getElementById('qcQ1').value.trim() || null,
            quadrant2: document.getElementById('qcQ2').value.trim() || null,
            quadrant3: document.getElementById('qcQ3').value.trim() || null,
            quadrant4: document.getElementById('qcQ4').value.trim() || null
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('qcTitle').select(), 50);
}

// ========== Quadrant Position Helpers ==========

function _qcBuildPointPositionHtml() {
    if (!quadrantModel || !quadrantModel.points || quadrantModel.points.length === 0) {
        return '<input type="hidden" id="qcInsertPosition" value="-1"/>';
    }
    let options = '<option value="-1">At end</option>';
    options += '<option value="0">At beginning</option>';
    quadrantModel.points.forEach((p, i) => {
        options += `<option value="${i + 1}">After "${(p.label || 'Point ' + (i+1)).replace(/"/g, '&quot;')}"</option>`;
    });
    return `<label>Position:</label><select id="qcInsertPosition" style="width:100%">${options}</select>`;
}

function _qcReadPointInsertIndex() {
    const sel = document.getElementById('qcInsertPosition');
    if (!sel) return -1;
    const val = parseInt(sel.value, 10);
    return isNaN(val) ? -1 : val;
}

// ========== Quadrant Copy/Paste ==========

function _qcCopyPoint(index) {
    if (!quadrantModel || !quadrantModel.points || index < 0 || index >= quadrantModel.points.length) return;
    const pt = quadrantModel.points[index];
    quadrantClipboard = {
        type: 'point',
        data: { label: pt.label + ' - copy', x: pt.x, y: pt.y }
    };
}

function _qcPastePoint(insertAtIndex) {
    if (!quadrantClipboard || quadrantClipboard.type !== 'point') return;
    const data = quadrantClipboard.data;
    postMessage({
        type: 'qc_pointCreated',
        label: data.label,
        x: Math.max(0, Math.min(1, data.x + 0.05)),
        y: Math.max(0, Math.min(1, data.y - 0.05)),
        insertAtIndex: insertAtIndex !== undefined ? insertAtIndex : -1
    });
}

// ========== Quadrant Context Menu Helpers ==========

function _qcAddCtxItem(label, onClick) {
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

function _qcAddCtxDisabledItem(label) {
    const contextMenu = document.getElementById('context-menu');
    const item = document.createElement('div');
    item.classList.add('context-menu-item', 'disabled');
    item.textContent = label;
    contextMenu.appendChild(item);
}

function _qcAddCtxSeparator() {
    const contextMenu = document.getElementById('context-menu');
    const sep = document.createElement('div');
    sep.classList.add('context-menu-separator');
    contextMenu.appendChild(sep);
}

// ========== Quadrant Context Menu ==========

function showQuadrantContextMenu(e) {
    if (currentDiagramType !== 'quadrant') return;
    if (!quadrantModel) return;

    e.preventDefault();
    e.stopPropagation();

    const contextMenu = document.getElementById('context-menu');
    contextMenu.innerHTML = '';

    // Find what was right-clicked
    const target = e.target.closest ? e.target.closest('[data-qc-type]') : null;
    const qcType = target ? target.getAttribute('data-qc-type') : null;
    const qcIndex = target ? parseInt(target.getAttribute('data-qc-index'), 10) : -1;

    if (qcType === 'point' && qcIndex >= 0) {
        // Point context menu
        quadrantSelectedPoint = qcIndex;
        renderQuadrantDiagram();

        _qcAddCtxItem('\u270E Edit Point', () => { editQuadrantPoint(qcIndex); });
        _qcAddCtxItem('\u{1F5D1} Delete Point', () => { deleteQuadrantPoint(qcIndex); });
        _qcAddCtxSeparator();
        _qcAddCtxItem('\u{1F4CB} Copy Point', () => { _qcCopyPoint(qcIndex); });
        _qcAddCtxSeparator();
        if (quadrantClipboard) {
            _qcAddCtxItem('\u{1F4CB} Paste Above', () => { _qcPastePoint(qcIndex); });
            _qcAddCtxItem('\u{1F4CB} Paste Below', () => { _qcPastePoint(qcIndex + 1); });
        } else {
            _qcAddCtxDisabledItem('\u{1F4CB} Paste Above');
            _qcAddCtxDisabledItem('\u{1F4CB} Paste Below');
        }
        _qcAddCtxSeparator();
        _qcAddCtxItem('\u2191 Insert Point Above', () => { createQuadrantPointAtIndex(qcIndex); });
        _qcAddCtxItem('\u2193 Insert Point Below', () => { createQuadrantPointAtIndex(qcIndex + 1); });
    } else {
        // Empty area context menu
        _qcAddCtxItem('+ Add Point', () => { createQuadrantPoint(); });
        _qcAddCtxItem('\u2699 Edit Settings', () => { editQuadrantSettings(); });
        _qcAddCtxSeparator();
        if (quadrantClipboard) {
            _qcAddCtxItem('\u{1F4CB} Paste', () => { _qcPastePoint(-1); });
        } else {
            _qcAddCtxDisabledItem('\u{1F4CB} Paste');
        }
    }

    positionContextMenu(contextMenu, e.clientX, e.clientY);
}

function createQuadrantPointAtIndex(insertIdx) {
    if (currentDiagramType !== 'quadrant') return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Point';
    const body = document.querySelector('.property-panel-body');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="qcPointLabel" value="New Point" />
        </div>
        <div class="property-row">
            <div class="property-label">X (0–1)</div>
            <input class="property-input" type="number" id="qcPointX" value="0.50" min="0" max="1" step="0.01" />
        </div>
        <div class="property-row">
            <div class="property-label">Y (0–1)</div>
            <input class="property-input" type="number" id="qcPointY" value="0.50" min="0" max="1" step="0.01" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="qc-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Point</button>
        </div>
    `;

    document.getElementById('qc-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('qcPointLabel').value.trim() || 'New Point';
        const px = Math.max(0, Math.min(1, parseFloat(document.getElementById('qcPointX').value) || 0.5));
        const py = Math.max(0, Math.min(1, parseFloat(document.getElementById('qcPointY').value) || 0.5));
        postMessage({ type: 'qc_pointCreated', label: label, x: px, y: py, insertAtIndex: insertIdx });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('qcPointLabel').select(), 50);
}

// ========== Quadrant Minimap Support ==========

function getQuadrantBoundingBox() {
    if (!quadrantModel) return null;
    const canvas = document.getElementById('editorCanvas');
    if (!canvas) return null;
    const svg = canvas.querySelector('svg');
    if (!svg) return null;

    const chartLeft = parseFloat(svg.getAttribute('data-qc-chart-left') || '0');
    const chartTop = parseFloat(svg.getAttribute('data-qc-chart-top') || '0');
    const chartSize = parseFloat(svg.getAttribute('data-qc-chart-size') || '500');

    // Bounding box includes the full chart area plus margins for labels
    return {
        minX: chartLeft - 60,
        minY: chartTop - 50,
        maxX: chartLeft + chartSize + 40,
        maxY: chartTop + chartSize + 60
    };
}
