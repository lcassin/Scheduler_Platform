// ============================================================
// VisualEditor.XYChart.js - XY Chart Visual Editor
// Bar and line series visualization with interactive editing
// ============================================================

// ========== XY Chart State ==========
let xyModel = null;
let xySelectedSeries = null; // index of selected series or null
let xySelectedDataPoint = null; // { seriesIndex, dataIndex } or null - individual data point selection
let xyClipboard = null; // { type: 'series', data: {...} }

// ========== XY Chart Load/Restore ==========

window.loadXYChart = function(jsonStr) {
    try {
        currentDiagramType = 'xyChart';
        xyModel = JSON.parse(jsonStr);
        xySelectedSeries = null;
        xySelectedDataPoint = null;
        editorCanvasZoom = 1;
        updateToolbarForDiagramType();
        renderXYChart();
    } catch (e) {
        console.error('Failed to load XY chart:', e);
    }
};

window.restoreXYChart = function(jsonStr) {
    try {
        xyModel = JSON.parse(jsonStr);
        renderXYChart();
    } catch (e) {
        console.error('Failed to restore XY chart:', e);
    }
};

window.refreshXYChart = function(jsonStr) {
    try {
        xyModel = JSON.parse(jsonStr);
        renderXYChart();
    } catch (e) {
        console.error('Failed to refresh XY chart:', e);
    }
};

// ========== XY Chart Rendering ==========

function renderXYChart() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !xyModel) return;

    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';
    canvas.innerHTML = '';

    const cs = getComputedStyle(document.body);
    const cv = (v) => cs.getPropertyValue(v).trim();
    const bgColor = cv('--bg-color') || '#1E1E1E';
    const textColor = cv('--node-text') || '#D4D4D4';
    const borderColor = cv('--node-stroke') || '#3E3E42';
    const gridColor = cv('--grid-color') || '#2D2D30';
    const isLight = document.body.classList.contains('theme-light');
    const isTwilight = document.body.classList.contains('theme-twilight');

    // Color palette for series
    const seriesColors = isLight
        ? ['#2196f3', '#4caf50', '#ff9800', '#f44336', '#9c27b0', '#009688', '#ff5722', '#3f51b5']
        : isTwilight
        ? ['#4A90D9', '#5A9E6F', '#D19A66', '#E06C75', '#B48EAD', '#56B6C2', '#C678DD', '#61AFEF']
        : ['#89b4fa', '#a6e3a1', '#f9e2af', '#f38ba8', '#cba6f7', '#94e2d5', '#fab387', '#74c7ec'];

    // Layout constants
    const svgWidth = 700;
    const chartLeft = 80;
    const chartRight = svgWidth - 40;
    const chartTop = 60;
    const chartBottom = 380;
    const chartWidth = chartRight - chartLeft;
    const chartHeight = chartBottom - chartTop;

    const series = xyModel.dataSeries || [];
    const categories = xyModel.xAxisCategories || [];
    const hasCategories = categories.length > 0;

    // Determine data bounds
    let allValues = [];
    series.forEach(s => { if (s.data) allValues = allValues.concat(s.data); });
    let dataMax = allValues.length > 0 ? Math.max(...allValues) : 100;
    let dataMin = 0;

    // Use y-axis range if specified
    if (xyModel.yAxisMin !== null && xyModel.yAxisMin !== undefined) dataMin = xyModel.yAxisMin;
    if (xyModel.yAxisMax !== null && xyModel.yAxisMax !== undefined) dataMax = xyModel.yAxisMax;
    if (dataMax <= dataMin) dataMax = dataMin + 100;

    const dataRange = dataMax - dataMin;

    // Number of data points
    const numPoints = hasCategories ? categories.length : (series.length > 0 && series[0].data ? series[0].data.length : 0);

    // Legend height
    const legendItemHeight = 24;
    const legendHeight = series.length > 0 ? series.length * legendItemHeight + 20 : 0;
    const svgHeight = chartBottom + 80 + legendHeight + 50;

    // Create SVG
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', svgWidth);
    svg.setAttribute('height', svgHeight);
    svg.setAttribute('viewBox', `0 0 ${svgWidth} ${svgHeight}`);
    svg.style.display = 'block';

    // Background
    const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    bg.setAttribute('width', svgWidth);
    bg.setAttribute('height', svgHeight);
    bg.setAttribute('fill', bgColor);
    bg.setAttribute('rx', '8');
    svg.appendChild(bg);

    // Click background to deselect
    bg.addEventListener('click', (e) => {
        if (e.target === bg) {
            xySelectedSeries = null;
            xySelectedDataPoint = null;
            renderXYChart();
            _xyUpdateToolbarSelection();
        }
    });

    // Title
    if (xyModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', svgWidth / 2);
        titleText.setAttribute('y', 30);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '18');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = xyModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', () => editXYChartSettings());
        svg.appendChild(titleText);
    }

    // Chart area background
    const chartBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    chartBg.setAttribute('x', chartLeft);
    chartBg.setAttribute('y', chartTop);
    chartBg.setAttribute('width', chartWidth);
    chartBg.setAttribute('height', chartHeight);
    chartBg.setAttribute('fill', 'none');
    chartBg.setAttribute('stroke', borderColor);
    chartBg.setAttribute('stroke-width', '1');
    svg.appendChild(chartBg);

    // Y-axis grid lines and labels
    const yTicks = 5;
    for (let i = 0; i <= yTicks; i++) {
        const val = dataMin + (dataRange * i / yTicks);
        const y = chartBottom - (chartHeight * i / yTicks);

        // Grid line
        const gridLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        gridLine.setAttribute('x1', chartLeft);
        gridLine.setAttribute('y1', y);
        gridLine.setAttribute('x2', chartRight);
        gridLine.setAttribute('y2', y);
        gridLine.setAttribute('stroke', gridColor);
        gridLine.setAttribute('stroke-width', '0.5');
        gridLine.setAttribute('stroke-dasharray', '3,3');
        svg.appendChild(gridLine);

        // Label
        const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        label.setAttribute('x', chartLeft - 8);
        label.setAttribute('y', y + 4);
        label.setAttribute('text-anchor', 'end');
        label.setAttribute('fill', textColor);
        label.setAttribute('font-size', '10');
        label.textContent = _xyFormatNumber(val);
        svg.appendChild(label);
    }

    // Y-axis title
    if (xyModel.yAxisTitle) {
        const yTitle = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        yTitle.setAttribute('x', 15);
        yTitle.setAttribute('y', chartTop + chartHeight / 2);
        yTitle.setAttribute('text-anchor', 'middle');
        yTitle.setAttribute('fill', textColor);
        yTitle.setAttribute('font-size', '12');
        yTitle.setAttribute('transform', `rotate(-90, 15, ${chartTop + chartHeight / 2})`);
        yTitle.textContent = xyModel.yAxisTitle;
        svg.appendChild(yTitle);
    }

    // X-axis labels
    if (hasCategories && numPoints > 0) {
        const barGroupWidth = chartWidth / numPoints;
        categories.forEach((cat, i) => {
            const x = chartLeft + barGroupWidth * i + barGroupWidth / 2;
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', x);
            label.setAttribute('y', chartBottom + 18);
            label.setAttribute('text-anchor', 'middle');
            label.setAttribute('fill', textColor);
            label.setAttribute('font-size', '10');
            label.textContent = cat.length > 10 ? cat.substring(0, 10) + '...' : cat;
            svg.appendChild(label);
        });
    }

    // X-axis title
    if (xyModel.xAxisTitle) {
        const xTitle = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        xTitle.setAttribute('x', chartLeft + chartWidth / 2);
        xTitle.setAttribute('y', chartBottom + 45);
        xTitle.setAttribute('text-anchor', 'middle');
        xTitle.setAttribute('fill', textColor);
        xTitle.setAttribute('font-size', '12');
        xTitle.textContent = xyModel.xAxisTitle;
        svg.appendChild(xTitle);
    }

    // Count bar series for bar width calculation
    const barSeriesIndices = [];
    const lineSeriesIndices = [];
    series.forEach((s, i) => {
        if (s.type === 'bar') barSeriesIndices.push(i);
        else lineSeriesIndices.push(i);
    });

    const numBarSeries = barSeriesIndices.length;

    // Draw series
    if (numPoints > 0) {
        const groupWidth = chartWidth / numPoints;
        const barPadding = Math.max(2, groupWidth * 0.1);
        const barAreaWidth = groupWidth - barPadding * 2;
        const singleBarWidth = numBarSeries > 0 ? barAreaWidth / numBarSeries : barAreaWidth;

        series.forEach((s, si) => {
            const color = seriesColors[si % seriesColors.length];
            const isSelected = xySelectedSeries === si;
            const data = s.data || [];

            if (s.type === 'bar') {
                const barIdx = barSeriesIndices.indexOf(si);
                data.forEach((val, di) => {
                    if (di >= numPoints) return;
                    const barHeight = ((val - dataMin) / dataRange) * chartHeight;
                    const x = chartLeft + groupWidth * di + barPadding + singleBarWidth * barIdx;
                    // Ensure zero-value bars still have a minimum clickable height
                    const minBarHeight = 4;
                    const displayHeight = Math.max(minBarHeight, barHeight);
                    const y = chartBottom - displayHeight;

                    const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    rect.setAttribute('x', x);
                    rect.setAttribute('y', y);
                    rect.setAttribute('width', Math.max(1, singleBarWidth - 1));
                    rect.setAttribute('height', displayHeight);
                    rect.setAttribute('fill', color);
                    rect.setAttribute('opacity', isSelected ? '1' : '0.8');
                    if (isSelected) {
                        rect.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
                        rect.setAttribute('stroke-width', '2');
                    }
                    // Highlight individual selected data point
                    const isPointSelected = xySelectedDataPoint && xySelectedDataPoint.seriesIndex === si && xySelectedDataPoint.dataIndex === di;
                    if (isPointSelected) {
                        rect.setAttribute('stroke', isLight ? '#e91e63' : '#f38ba8');
                        rect.setAttribute('stroke-width', '3');
                        rect.setAttribute('opacity', '1');
                    }
                    rect.style.cursor = 'pointer';
                    rect.addEventListener('click', (e) => { e.stopPropagation(); selectXYDataPoint(si, di); });
                    rect.addEventListener('dblclick', (e) => { e.stopPropagation(); editXYDataPoint(si, di); });
                    rect.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showXYChartContextMenu(e, si, di); });
                    svg.appendChild(rect);
                });
            } else {
                // Line series
                if (data.length < 1) return;
                const noValueFlags = s.noValue || [];
                let pathD = '';
                const points = [];
                data.forEach((val, di) => {
                    if (di >= numPoints) return;
                    const x = chartLeft + groupWidth * di + groupWidth / 2;
                    // For NoValue points, compute interpolated value for display
                    let displayVal = val;
                    if (noValueFlags[di] && di > 0 && di < data.length - 1) {
                        displayVal = _xyInterpolateValue(data, noValueFlags, di);
                    }
                    const y = chartBottom - ((displayVal - dataMin) / dataRange) * chartHeight;
                    points.push({ x, y, isNoValue: !!noValueFlags[di] });
                    pathD += (di === 0 ? 'M' : 'L') + ` ${x} ${y} `;
                });

                const linePath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                linePath.setAttribute('d', pathD);
                linePath.setAttribute('fill', 'none');
                linePath.setAttribute('stroke', color);
                linePath.setAttribute('stroke-width', isSelected ? '3' : '2');
                if (isSelected) {
                    linePath.setAttribute('filter', 'drop-shadow(0 0 3px ' + color + ')');
                }
                linePath.style.cursor = 'pointer';
                linePath.addEventListener('click', (e) => { e.stopPropagation(); selectXYSeries(si); });
                linePath.addEventListener('dblclick', (e) => { e.stopPropagation(); editXYChartSeries(si); });
                linePath.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showXYChartContextMenu(e, si); });
                svg.appendChild(linePath);

                // Draw dots on line points
                points.forEach((pt, di) => {
                    const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    dot.setAttribute('cx', pt.x);
                    dot.setAttribute('cy', pt.y);
                    dot.setAttribute('r', isSelected ? '5' : '3');
                    // NoValue points rendered as hollow (outline only)
                    if (pt.isNoValue) {
                        dot.setAttribute('fill', bgColor);
                        dot.setAttribute('stroke', color);
                        dot.setAttribute('stroke-width', '2');
                        dot.setAttribute('stroke-dasharray', '3,2');
                    } else {
                        dot.setAttribute('fill', color);
                    }
                    if (isSelected && !pt.isNoValue) {
                        dot.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
                        dot.setAttribute('stroke-width', '2');
                    }
                    // Highlight individual selected data point
                    const isPointSelected = xySelectedDataPoint && xySelectedDataPoint.seriesIndex === si && xySelectedDataPoint.dataIndex === di;
                    if (isPointSelected) {
                        dot.setAttribute('stroke', isLight ? '#e91e63' : '#f38ba8');
                        dot.setAttribute('stroke-width', '3');
                        dot.setAttribute('r', '7');
                    }
                    dot.style.cursor = 'pointer';
                    dot.addEventListener('click', (e) => { e.stopPropagation(); selectXYDataPoint(si, di); });
                    dot.addEventListener('dblclick', (e) => { e.stopPropagation(); editXYDataPoint(si, di); });
                    dot.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showXYChartContextMenu(e, si, di); });
                    svg.appendChild(dot);
                });
            }
        });
    }

    // Empty state
    if (series.length === 0) {
        const emptyText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        emptyText.setAttribute('x', chartLeft + chartWidth / 2);
        emptyText.setAttribute('y', chartTop + chartHeight / 2);
        emptyText.setAttribute('text-anchor', 'middle');
        emptyText.setAttribute('fill', textColor);
        emptyText.setAttribute('font-size', '14');
        emptyText.setAttribute('opacity', '0.5');
        emptyText.textContent = 'No data series. Click "+ Add Series" to get started.';
        svg.appendChild(emptyText);
    }

    // Legend
    if (series.length > 0) {
        const legendStartY = chartBottom + 60;
        const legendX = chartLeft;

        series.forEach((s, i) => {
            const y = legendStartY + i * legendItemHeight;
            const color = seriesColors[i % seriesColors.length];
            const isSelected = xySelectedSeries === i;

            // Color indicator
            if (s.type === 'bar') {
                const colorBox = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                colorBox.setAttribute('x', legendX);
                colorBox.setAttribute('y', y);
                colorBox.setAttribute('width', 16);
                colorBox.setAttribute('height', 16);
                colorBox.setAttribute('fill', color);
                colorBox.setAttribute('rx', '2');
                if (isSelected) {
                    colorBox.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
                    colorBox.setAttribute('stroke-width', '2');
                }
                svg.appendChild(colorBox);
            } else {
                const lineInd = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                lineInd.setAttribute('x1', legendX);
                lineInd.setAttribute('y1', y + 8);
                lineInd.setAttribute('x2', legendX + 16);
                lineInd.setAttribute('y2', y + 8);
                lineInd.setAttribute('stroke', color);
                lineInd.setAttribute('stroke-width', isSelected ? '3' : '2');
                svg.appendChild(lineInd);
                const dotInd = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                dotInd.setAttribute('cx', legendX + 8);
                dotInd.setAttribute('cy', y + 8);
                dotInd.setAttribute('r', '3');
                dotInd.setAttribute('fill', color);
                svg.appendChild(dotInd);
            }

            // Label
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', legendX + 24);
            label.setAttribute('y', y + 12);
            label.setAttribute('fill', textColor);
            label.setAttribute('font-size', '12');
            label.setAttribute('font-weight', isSelected ? 'bold' : 'normal');
            if (s.label) {
                label.textContent = `${s.label} (${s.type})`;
            } else {
                const dataStr = s.data ? s.data.map(v => _xyFormatNumber(v)).join(', ') : '';
                const truncData = dataStr.length > 50 ? dataStr.substring(0, 50) + '...' : dataStr;
                label.textContent = `${s.type} [${truncData}]`;
            }
            label.style.cursor = 'pointer';
            label.addEventListener('click', () => selectXYSeries(i));
            label.addEventListener('dblclick', () => editXYChartSeries(i));
            label.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showXYChartContextMenu(e, i); });
            svg.appendChild(label);
        });
    }

    // Toolbar at bottom of SVG
    const toolbarY = svgHeight - 45;
    _xyRenderToolbar(svg, 10, toolbarY, svgWidth - 20, isLight, textColor);

    canvas.appendChild(svg);

    // Apply current zoom level and update minimap
    if (typeof editorCanvasZoom !== 'undefined' && editorCanvasZoom !== 1) {
        svg.style.transformOrigin = 'top left';
        svg.style.transform = 'scale(' + editorCanvasZoom + ')';
        svg.style.maxWidth = 'none';
    }
    if (typeof updateMinimap === 'function') updateMinimap();
}

// Compute interpolated value for a NoValue point based on nearest defined neighbors
function _xyInterpolateValue(data, noValueFlags, index) {
    // Find previous defined point
    let prev = index - 1;
    while (prev >= 0 && noValueFlags[prev]) prev--;
    // Find next defined point
    let next = index + 1;
    while (next < data.length && noValueFlags[next]) next++;
    if (prev < 0 || next >= data.length) return data[index];
    // Linear interpolation
    var span = next - prev;
    var t = (index - prev) / span;
    return data[prev] + (data[next] - data[prev]) * t;
}

function _xyFormatNumber(val) {
    if (val >= 1000000) return (val / 1000000).toFixed(1) + 'M';
    if (val >= 1000) return (val / 1000).toFixed(1) + 'K';
    if (Number.isInteger(val)) return val.toString();
    return val.toFixed(1);
}

function _xyRenderToolbar(svg, x, y, width, isLight, textColor) {
    const btnBg = isLight ? '#e0e0e0' : '#313244';
    const btnHover = isLight ? '#bdbdbd' : '#45475a';
    const buttons = [
        { label: '+ Add Series', action: () => createXYChartSeries() },
        { label: 'Settings', action: () => editXYChartSettings() }
    ];

    if (xySelectedSeries !== null) {
        buttons.push({ label: 'Edit Series', action: () => editXYChartSeries(xySelectedSeries) });
        buttons.push({ label: 'Delete Series', action: () => deleteXYChartSeries() });
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

// ========== XY Chart Interactions ==========

function selectXYSeries(index) {
    xySelectedSeries = index;
    xySelectedDataPoint = null;
    renderXYChart();
    _xyUpdateToolbarSelection();
    postMessage({ type: 'xy_seriesSelected', index });
}

function selectXYDataPoint(seriesIndex, dataIndex) {
    xySelectedSeries = seriesIndex;
    xySelectedDataPoint = { seriesIndex, dataIndex };
    renderXYChart();
    _xyUpdateToolbarSelection();
    postMessage({ type: 'xy_dataPointSelected', seriesIndex, dataIndex });
}

function editXYDataPoint(seriesIndex, dataIndex) {
    if (!xyModel || !xyModel.dataSeries || seriesIndex >= xyModel.dataSeries.length) return;
    var s = xyModel.dataSeries[seriesIndex];
    if (!s.data || dataIndex >= s.data.length) return;
    var currentVal = s.data[dataIndex];
    var catLabel = (xyModel.xAxisCategories && xyModel.xAxisCategories[dataIndex]) ? xyModel.xAxisCategories[dataIndex] : ('Point ' + (dataIndex + 1));
    var isLine = s.type === 'line';
    var noValueFlags = s.noValue || [];
    var isNoValue = !!noValueFlags[dataIndex];
    var isFirstOrLast = dataIndex === 0 || dataIndex === s.data.length - 1;

    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Data Point';
    var body = document.querySelector('.property-panel-body');

    var noValueHtml = '';
    if (isLine) {
        noValueHtml = `
        <div class="property-row">
            <label style="display:flex;align-items:center;gap:8px;cursor:${isFirstOrLast ? 'not-allowed' : 'pointer'};opacity:${isFirstOrLast ? '0.4' : '1'}">
                <input type="checkbox" id="xy-dp-novalue" ${isNoValue ? 'checked' : ''} ${isFirstOrLast ? 'disabled' : ''} style="accent-color:var(--node-selected-stroke);width:16px;height:16px" />
                <span>No Value (interpolate)</span>
            </label>
            ${isFirstOrLast ? '<div style="font-size:11px;opacity:0.5;margin-top:2px">First and last points must have values</div>' : ''}
        </div>`;
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Series</div>
            <div class="property-label" style="font-weight:normal;opacity:0.7">${_escHtml(s.type)} #${seriesIndex + 1}</div>
        </div>
        <div class="property-row">
            <div class="property-label">Category</div>
            <div class="property-label" style="font-weight:normal;opacity:0.7">${_escHtml(catLabel)}</div>
        </div>
        ${noValueHtml}
        <div class="property-row" id="xy-dp-value-row" style="${isNoValue ? 'opacity:0.4;pointer-events:none' : ''}">
            <div class="property-label">Value</div>
            <input class="property-input" id="xy-dp-value" type="number" step="any" value="${currentVal}" ${isNoValue ? 'disabled' : ''} />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="xy-dp-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;

    // Wire up custom No Value checkbox toggle
    var noValueCheckbox = document.getElementById('xy-dp-novalue');
    if (noValueCheckbox && !isFirstOrLast) {
        document.getElementById('xy-dp-novalue-label').addEventListener('click', function(e) {
            e.preventDefault();
            var cb = document.getElementById('xy-dp-novalue');
            var isChecked = cb.getAttribute('data-checked') === 'true';
            var nowChecked = !isChecked;
            cb.setAttribute('data-checked', nowChecked);
            cb.style.background = nowChecked ? 'var(--node-selected-stroke)' : 'transparent';
            cb.innerHTML = nowChecked ? '<svg width="10" height="10" viewBox="0 0 10 10"><path d="M2 5l2.5 2.5L8 3" stroke="#fff" stroke-width="2" fill="none"/></svg>' : '';
            var valueInput = document.getElementById('xy-dp-value');
            var valueRow = document.getElementById('xy-dp-value-row');
            if (nowChecked) {
                valueInput.disabled = true;
                valueRow.style.opacity = '0.4';
                valueRow.style.pointerEvents = 'none';
            } else {
                valueInput.disabled = false;
                valueRow.style.opacity = '1';
                valueRow.style.pointerEvents = '';
            }
        });
    }

    document.getElementById('xy-dp-ok').addEventListener('click', function() {
        var noValueChecked = noValueCheckbox ? noValueCheckbox.getAttribute('data-checked') === 'true' : false;
        if (noValueChecked) {
            // Toggle to NoValue
            postMessage({ type: 'xy_dataPointNoValueToggled', seriesIndex: seriesIndex, dataIndex: dataIndex, noValue: true });
        } else {
            var val = parseFloat(document.getElementById('xy-dp-value').value);
            if (isNaN(val)) return;
            // If was NoValue, first clear the flag, then set value
            if (isNoValue) {
                postMessage({ type: 'xy_dataPointNoValueToggled', seriesIndex: seriesIndex, dataIndex: dataIndex, noValue: false });
            }
            postMessage({ type: 'xy_dataPointEdited', seriesIndex: seriesIndex, dataIndex: dataIndex, value: val });
        }
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    if (!isNoValue) {
        setTimeout(function() { document.getElementById('xy-dp-value').select(); }, 50);
    }
}

function deleteXYDataPoint(seriesIndex, dataIndex) {
    if (!xyModel || !xyModel.dataSeries || seriesIndex >= xyModel.dataSeries.length) return;
    var s = xyModel.dataSeries[seriesIndex];
    if (!s.data || dataIndex >= s.data.length) return;
    var currentVal = s.data[dataIndex];
    var catLabel = (xyModel.xAxisCategories && xyModel.xAxisCategories[dataIndex]) ? xyModel.xAxisCategories[dataIndex] : ('Point ' + (dataIndex + 1));
    var hasCategories = xyModel.xAxisCategories && xyModel.xAxisCategories.length > 0;

    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Data Point';
    var body = document.querySelector('.property-panel-body');
    var warningHtml = hasCategories ? `<div class="property-row"><div class="property-label" style="width:100%;text-align:center;font-size:11px;opacity:0.6">This will remove the "${_escHtml(catLabel)}" column from all series</div></div>` : '';
    body.innerHTML = `
        <div class="property-row"><div class="property-label" style="width:100%;text-align:center">Delete value ${_escHtml(String(currentVal))} at ${_escHtml(catLabel)}?</div></div>
        ${warningHtml}
        <div class="property-row" style="display:flex;gap:8px;margin-top:8px">
            <button id="xy-dp-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="xy-dp-no" style="flex:1;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('xy-dp-yes').addEventListener('click', function() {
        postMessage({ type: 'xy_dataPointDeleted', seriesIndex: seriesIndex, dataIndex: dataIndex, syncAll: hasCategories });
        xySelectedDataPoint = null;
        _xyUpdateToolbarSelection();
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('xy-dp-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function insertXYDataPoint(seriesIndex, dataIndex) {
    if (!xyModel || !xyModel.dataSeries || seriesIndex >= xyModel.dataSeries.length) return;
    var hasCategories = xyModel.xAxisCategories && xyModel.xAxisCategories.length > 0;

    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Insert Data Point';
    var body = document.querySelector('.property-panel-body');

    var categoryHtml = '';
    if (hasCategories) {
        categoryHtml = `
        <div class="property-row">
            <div class="property-label">Category Name</div>
            <input class="property-input" id="xy-dp-category" type="text" value="" placeholder="e.g. New Month" />
        </div>
        <div class="property-row"><div class="property-label" style="font-size:11px;opacity:0.5;width:100%">A new column will be added to all series</div></div>`;
    }

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Series</div>
            <div class="property-label" style="font-weight:normal;opacity:0.7">${_escHtml(xyModel.dataSeries[seriesIndex].type)} #${seriesIndex + 1}</div>
        </div>
        <div class="property-row">
            <div class="property-label">Position</div>
            <div class="property-label" style="font-weight:normal;opacity:0.7">${dataIndex !== undefined ? 'At index ' + dataIndex : 'At end'}</div>
        </div>
        ${categoryHtml}
        <div class="property-row">
            <div class="property-label">Value</div>
            <input class="property-input" id="xy-dp-value" type="number" step="any" value="0" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="xy-dp-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Insert</button>
        </div>
    `;
    document.getElementById('xy-dp-ok').addEventListener('click', function() {
        var val = parseFloat(document.getElementById('xy-dp-value').value);
        if (isNaN(val)) return;
        var catInput = document.getElementById('xy-dp-category');
        if (catInput && !catInput.value.trim()) {
            catInput.style.borderColor = '#f44336';
            catInput.focus();
            return;
        }
        var msg = { type: 'xy_dataPointCreated', seriesIndex: seriesIndex, value: val, syncAll: hasCategories };
        if (dataIndex !== undefined) msg.dataIndex = dataIndex;
        if (catInput) msg.categoryName = catInput.value.trim();
        postMessage(msg);
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    if (hasCategories) {
        setTimeout(function() { document.getElementById('xy-dp-category').focus(); }, 50);
    } else {
        setTimeout(function() { document.getElementById('xy-dp-value').select(); }, 50);
    }
}

function _xyUpdateToolbarSelection() {
    var editBtn = document.getElementById('tb-xy-edit');
    var delBtn = document.getElementById('tb-xy-delete');
    var copyBtn = document.getElementById('tb-xy-copy');
    if (editBtn) editBtn.style.display = (xySelectedSeries !== null) ? '' : 'none';
    if (delBtn) delBtn.style.display = (xySelectedSeries !== null) ? '' : 'none';
    if (copyBtn) copyBtn.style.display = (xySelectedSeries !== null) ? '' : 'none';
}

// ========== XY Chart CRUD ==========

function createXYChartSeries(insertIndex) {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Data Series';
    const body = document.querySelector('.property-panel-body');

    const numPoints = (xyModel.xAxisCategories && xyModel.xAxisCategories.length > 0)
        ? xyModel.xAxisCategories.length
        : ((xyModel.dataSeries && xyModel.dataSeries.length > 0 && xyModel.dataSeries[0].data)
            ? xyModel.dataSeries[0].data.length : 4);

    const defaultValues = Array(numPoints).fill(0).join(', ');
    const posHtml = _xyBuildPositionHtml(insertIndex);

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Type</div>
            <select class="property-select" id="xy-dlg-type">
                <option value="bar">Bar</option>
                <option value="line">Line</option>
            </select>
        </div>
        <div class="property-row">
            <div class="property-label">Label (optional)</div>
            <input class="property-input" id="xy-dlg-label" placeholder="e.g. Revenue, Expenses..." value="" />
        </div>
        <div class="property-row">
            <div class="property-label">Values (comma-separated)</div>
            <input class="property-input" id="xy-dlg-values" value="${defaultValues}" />
        </div>
        ${posHtml}
        <div class="property-row" style="margin-top:8px">
            <button id="xy-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Series</button>
        </div>
    `;
    document.getElementById('xy-dlg-ok').addEventListener('click', function() {
        const type = document.getElementById('xy-dlg-type').value;
        const label = document.getElementById('xy-dlg-label').value.trim() || null;
        const valStr = document.getElementById('xy-dlg-values').value;
        const data = valStr.split(',').map(v => parseFloat(v.trim())).filter(v => !isNaN(v));
        if (data.length === 0) return;

        const msg = { type: 'xy_seriesCreated', seriesType: type, data, label };
        const posEl = document.getElementById('xy-dlg-position');
        if (posEl) {
            const idx = parseInt(posEl.value);
            if (!isNaN(idx)) msg.index = idx;
        }
        postMessage(msg);
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('xy-dlg-values').select(), 50);
}

function editXYChartSeries(index) {
    if (!xyModel || !xyModel.dataSeries || index >= xyModel.dataSeries.length) return;
    const s = xyModel.dataSeries[index];

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Data Series';
    const body = document.querySelector('.property-panel-body');

    const valStr = (s.data || []).join(', ');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Type</div>
            <select class="property-select" id="xy-dlg-type">
                <option value="bar" ${s.type === 'bar' ? 'selected' : ''}>Bar</option>
                <option value="line" ${s.type === 'line' ? 'selected' : ''}>Line</option>
            </select>
        </div>
        <div class="property-row">
            <div class="property-label">Label (optional)</div>
            <input class="property-input" id="xy-dlg-label" placeholder="e.g. Revenue, Expenses..." value="${_escHtml(s.label || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Values (comma-separated)</div>
            <input class="property-input" id="xy-dlg-values" value="${_escHtml(valStr)}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="xy-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('xy-dlg-ok').addEventListener('click', function() {
        const type = document.getElementById('xy-dlg-type').value;
        const label = document.getElementById('xy-dlg-label').value.trim() || null;
        const valInput = document.getElementById('xy-dlg-values').value;
        const data = valInput.split(',').map(v => parseFloat(v.trim())).filter(v => !isNaN(v));
        if (data.length === 0) return;
        const msg = { type: 'xy_seriesEdited', index, seriesType: type, data, label };
        // Preserve existing NoValue flags so they aren't silently cleared
        if (s.noValue && s.noValue.length > 0) msg.noValue = s.noValue;
        postMessage(msg);
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('xy-dlg-label').focus(), 50);
}

function deleteXYChartSeries(index) {
    const idx = (index !== undefined) ? index : xySelectedSeries;
    if (idx === null || idx === undefined) return;
    if (!xyModel || !xyModel.dataSeries || idx >= xyModel.dataSeries.length) return;

    const s = xyModel.dataSeries[idx];
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Series';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row"><div class="property-label" style="width:100%;text-align:center">Delete this ${_escHtml(s.type)} series?</div></div>
        <div class="property-row" style="display:flex;gap:8px;margin-top:8px">
            <button id="xy-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="xy-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('xy-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'xy_seriesDeleted', index: idx });
        xySelectedSeries = null;
        xySelectedDataPoint = null;
        _xyUpdateToolbarSelection();
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('xy-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function editXYChartSettings() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'XY Chart Settings';
    const body = document.querySelector('.property-panel-body');

    const cats = (xyModel.xAxisCategories || []).join(', ');

    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Chart Title</div>
            <input class="property-input" id="xy-dlg-title" value="${_escHtml(xyModel.title || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Orientation</div>
            <select class="property-select" id="xy-dlg-horizontal">
                <option value="false" ${!xyModel.horizontal ? 'selected' : ''}>Vertical</option>
                <option value="true" ${xyModel.horizontal ? 'selected' : ''}>Horizontal</option>
            </select>
        </div>
        <div class="property-row">
            <div class="property-label">X-Axis Title</div>
            <input class="property-input" id="xy-dlg-xtitle" value="${_escHtml(xyModel.xAxisTitle || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">X-Axis Categories (comma-separated)</div>
            <input class="property-input" id="xy-dlg-xcats" value="${_escHtml(cats)}" />
        </div>
        <div class="property-row">
            <div class="property-label">Y-Axis Title</div>
            <input class="property-input" id="xy-dlg-ytitle" value="${_escHtml(xyModel.yAxisTitle || '')}" />
        </div>
        <div class="property-row">
            <div class="property-label">Y-Axis Min</div>
            <input class="property-input" id="xy-dlg-ymin" type="number" step="any" value="${xyModel.yAxisMin !== null && xyModel.yAxisMin !== undefined ? xyModel.yAxisMin : ''}" />
        </div>
        <div class="property-row">
            <div class="property-label">Y-Axis Max</div>
            <input class="property-input" id="xy-dlg-ymax" type="number" step="any" value="${xyModel.yAxisMax !== null && xyModel.yAxisMax !== undefined ? xyModel.yAxisMax : ''}" />
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="xy-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('xy-dlg-ok').addEventListener('click', function() {
        const title = document.getElementById('xy-dlg-title').value.trim() || null;
        const horizontal = document.getElementById('xy-dlg-horizontal').value === 'true';
        const xAxisTitle = document.getElementById('xy-dlg-xtitle').value.trim() || null;
        const yAxisTitle = document.getElementById('xy-dlg-ytitle').value.trim() || null;
        const yMinStr = document.getElementById('xy-dlg-ymin').value;
        const yMaxStr = document.getElementById('xy-dlg-ymax').value;
        const yAxisMin = yMinStr !== '' ? parseFloat(yMinStr) : null;
        const yAxisMax = yMaxStr !== '' ? parseFloat(yMaxStr) : null;

        const catStr = document.getElementById('xy-dlg-xcats').value.trim();
        const xAxisCategories = catStr ? catStr.split(',').map(c => c.trim()).filter(c => c.length > 0) : null;

        postMessage({
            type: 'xy_settingsChanged',
            title, horizontal, xAxisTitle, xAxisCategories, yAxisTitle, yAxisMin, yAxisMax
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('xy-dlg-title').focus(), 50);
}

// ========== XY Chart Position Helper ==========

function _xyBuildPositionHtml(insertIndex) {
    if (!xyModel || !xyModel.dataSeries || xyModel.dataSeries.length === 0) return '';
    const options = xyModel.dataSeries.map((s, i) =>
        `<option value="${i}" ${i === insertIndex ? 'selected' : ''}>Before ${s.type} #${i + 1}</option>`
    );
    options.push(`<option value="${xyModel.dataSeries.length}" ${insertIndex === undefined || insertIndex === xyModel.dataSeries.length ? 'selected' : ''}>At end</option>`);
    return `
        <div class="property-row">
            <div class="property-label">Position</div>
            <select class="property-select" id="xy-dlg-position">${options.join('')}</select>
        </div>
    `;
}

// ========== XY Chart Copy/Paste ==========

function _xyCopySeries(index) {
    if (!xyModel || !xyModel.dataSeries || index >= xyModel.dataSeries.length) return;
    const s = xyModel.dataSeries[index];
    xyClipboard = { type: 'series', data: { seriesType: s.type, data: [...(s.data || [])], label: s.label || null, noValue: s.noValue ? [...s.noValue] : [] } };
}

function _xyPasteSeries(index) {
    if (!xyClipboard || xyClipboard.type !== 'series') return;
    const msg = { type: 'xy_seriesCreated', seriesType: xyClipboard.data.seriesType, data: [...xyClipboard.data.data] };
    if (xyClipboard.data.label) msg.label = xyClipboard.data.label;
    if (xyClipboard.data.noValue && xyClipboard.data.noValue.length > 0) msg.noValue = [...xyClipboard.data.noValue];
    if (index !== undefined) msg.index = index;
    postMessage(msg);
}

// ========== XY Chart Context Menu ==========

function _xyAddCtxItem(menu, label, action, disabled) {
    const item = document.createElement('div');
    item.textContent = label;
    item.className = 'ctx-item';
    if (disabled) {
        item.style.cssText = 'padding:6px 16px;white-space:nowrap;color:var(--context-menu-text);opacity:0.35;cursor:default';
    } else {
        item.style.cssText = 'padding:6px 16px;cursor:pointer;white-space:nowrap;color:var(--context-menu-text)';
        item.addEventListener('mouseenter', () => item.style.background = 'var(--context-menu-hover)');
        item.addEventListener('mouseleave', () => item.style.background = 'transparent');
        item.addEventListener('click', () => { menu.remove(); action(); });
    }
    menu.appendChild(item);
}

function _xyAddCtxSeparator(menu) {
    const sep = document.createElement('div');
    sep.style.cssText = 'height:1px;background:var(--context-menu-border);margin:4px 0';
    menu.appendChild(sep);
}

function showXYChartContextMenu(e, seriesIndex, dataIndex) {
    // Remove existing menus
    document.querySelectorAll('.xy-ctx-menu').forEach(m => m.remove());

    const menu = document.createElement('div');
    menu.className = 'xy-ctx-menu';
    menu.style.cssText = `position:fixed;left:${e.clientX}px;top:${e.clientY}px;background:var(--context-menu-bg);border:1px solid var(--context-menu-border);border-radius:6px;padding:4px 0;z-index:9999;min-width:160px;box-shadow:0 4px 12px rgba(0,0,0,0.3)`;

    if (seriesIndex !== undefined && seriesIndex !== null) {
        // Data point-level context menu items
        if (dataIndex !== undefined && dataIndex !== null) {
            selectXYDataPoint(seriesIndex, dataIndex);
            var s = xyModel.dataSeries[seriesIndex];
            var maxDataLen = Math.max(...xyModel.dataSeries.map(ds => (ds.data || []).length));
            var catLabel = (xyModel.xAxisCategories && xyModel.xAxisCategories[dataIndex]) ? xyModel.xAxisCategories[dataIndex] : ('Point ' + (dataIndex + 1));
            _xyAddCtxItem(menu, 'Edit Value (' + catLabel + ')', () => editXYDataPoint(seriesIndex, dataIndex));
            _xyAddCtxItem(menu, 'Delete Value', () => deleteXYDataPoint(seriesIndex, dataIndex), maxDataLen <= 1);
            // Toggle No Value option for line series (not first/last)
            if (s.type === 'line' && dataIndex > 0 && s.data && dataIndex < s.data.length - 1) {
                var nvFlags = s.noValue || [];
                var isNV = !!nvFlags[dataIndex];
                _xyAddCtxItem(menu, isNV ? 'Set Explicit Value' : 'Set No Value (Interpolate)', () => {
                    postMessage({ type: 'xy_dataPointNoValueToggled', seriesIndex: seriesIndex, dataIndex: dataIndex, noValue: !isNV });
                });
            }
            _xyAddCtxSeparator(menu);
            _xyAddCtxItem(menu, 'Insert Value Before', () => insertXYDataPoint(seriesIndex, dataIndex));
            _xyAddCtxItem(menu, 'Insert Value After', () => insertXYDataPoint(seriesIndex, dataIndex + 1));
            _xyAddCtxItem(menu, 'Append Value', () => insertXYDataPoint(seriesIndex, undefined));
            _xyAddCtxSeparator(menu);
            // Move Left/Right (swap data point with neighbor across all series)
            _xyAddCtxItem(menu, 'Move Left', () => {
                postMessage({ type: 'xy_columnMoved', fromIndex: dataIndex, toIndex: dataIndex - 1 });
            }, dataIndex <= 0);
            _xyAddCtxItem(menu, 'Move Right', () => {
                postMessage({ type: 'xy_columnMoved', fromIndex: dataIndex, toIndex: dataIndex + 1 });
            }, dataIndex >= maxDataLen - 1);
            _xyAddCtxSeparator(menu);
        } else {
            selectXYSeries(seriesIndex);
        }

        _xyAddCtxItem(menu, 'Edit Series', () => editXYChartSeries(seriesIndex));
        _xyAddCtxItem(menu, 'Delete Series', () => deleteXYChartSeries(seriesIndex), xyModel.dataSeries && xyModel.dataSeries.length <= 1);
        _xyAddCtxSeparator(menu);
        _xyAddCtxItem(menu, 'Copy Series', () => _xyCopySeries(seriesIndex));
        if (xyClipboard && xyClipboard.type === 'series') {
            _xyAddCtxItem(menu, 'Paste Series Above', () => _xyPasteSeries(seriesIndex));
            _xyAddCtxItem(menu, 'Paste Series Below', () => _xyPasteSeries(seriesIndex + 1));
        }
        _xyAddCtxSeparator(menu);

        // Move Series Up/Down
        _xyAddCtxItem(menu, 'Move Series Up', () => {
            postMessage({ type: 'xy_seriesMoved', fromIndex: seriesIndex, toIndex: seriesIndex - 1 });
        }, seriesIndex <= 0);
        _xyAddCtxItem(menu, 'Move Series Down', () => {
            postMessage({ type: 'xy_seriesMoved', fromIndex: seriesIndex, toIndex: seriesIndex + 1 });
        }, !xyModel.dataSeries || seriesIndex >= xyModel.dataSeries.length - 1);

        _xyAddCtxSeparator(menu);
        _xyAddCtxItem(menu, 'Insert Series Above', () => createXYChartSeries(seriesIndex));
        _xyAddCtxItem(menu, 'Insert Series Below', () => createXYChartSeries(seriesIndex + 1));
    } else {
        _xyAddCtxItem(menu, 'Add Series', () => createXYChartSeries());
        if (xyClipboard && xyClipboard.type === 'series') {
            _xyAddCtxItem(menu, 'Paste Series', () => _xyPasteSeries());
        }
    }

    _xyAddCtxSeparator(menu);
    _xyAddCtxItem(menu, 'Settings', () => editXYChartSettings());

    document.body.appendChild(menu);

    // Adjust position if off-screen
    const rect = menu.getBoundingClientRect();
    if (rect.right > window.innerWidth) menu.style.left = (window.innerWidth - rect.width - 5) + 'px';
    if (rect.bottom > window.innerHeight) menu.style.top = (window.innerHeight - rect.height - 5) + 'px';

    // Close on click outside
    const closeHandler = (ev) => {
        if (!menu.contains(ev.target)) {
            menu.remove();
            document.removeEventListener('click', closeHandler);
        }
    };
    setTimeout(() => document.addEventListener('click', closeHandler), 0);
}
