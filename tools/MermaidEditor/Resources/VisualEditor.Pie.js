// ============================================================
// VisualEditor.Pie.js - Pie Chart Visual Editor
// Circular segment visualization with interactive slices
// ============================================================

// ========== Pie Chart State ==========
let pieModel = null;
let pieSelectedSlice = null; // index

// ========== Pie Chart Load/Restore ==========

window.loadPieChart = function(jsonStr) {
    try {
        currentDiagramType = 'pie';
        pieModel = JSON.parse(jsonStr);
        pieSelectedSlice = null;
        updateToolbarForDiagramType();
        renderPieChart();
    } catch (e) {
        console.error('Failed to load pie chart:', e);
    }
};

window.restorePieChart = function(jsonStr) {
    try {
        pieModel = JSON.parse(jsonStr);
        renderPieChart();
    } catch (e) {
        console.error('Failed to restore pie chart:', e);
    }
};

window.refreshPieChart = function(jsonStr) {
    try {
        pieModel = JSON.parse(jsonStr);
        renderPieChart();
    } catch (e) {
        console.error('Failed to refresh pie chart:', e);
    }
};

// ========== Pie Chart Rendering ==========

function renderPieChart() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !pieModel) return;

    // Show editorCanvas, hide diagram-svg for standalone SVG rendering
    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';

    canvas.innerHTML = '';

    const isDark = document.body.classList.contains('dark-theme');
    const textColor = isDark ? '#cdd6f4' : '#333333';
    const bgColor = isDark ? '#1e1e2e' : '#ffffff';

    // Color palette for slices
    const sliceColors = isDark
        ? ['#89b4fa', '#a6e3a1', '#f9e2af', '#f38ba8', '#cba6f7', '#94e2d5', '#fab387', '#74c7ec', '#f5c2e7', '#b4befe']
        : ['#2196f3', '#4caf50', '#ff9800', '#f44336', '#9c27b0', '#009688', '#ff5722', '#3f51b5', '#e91e63', '#00bcd4'];

    const svgWidth = 600;
    const svgHeight = 500;
    const centerX = svgWidth / 2;
    const centerY = 220;
    const radius = 150;

    // Create SVG - responsive with viewBox
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', svgWidth);
    svg.setAttribute('height', svgHeight);
    svg.setAttribute('viewBox', `0 0 ${svgWidth} ${svgHeight}`);
    svg.style.display = 'block';
    svg.style.maxWidth = '100%';
    svg.style.height = 'auto';

    // Background
    const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    bg.setAttribute('width', svgWidth);
    bg.setAttribute('height', svgHeight);
    bg.setAttribute('fill', bgColor);
    bg.setAttribute('rx', '8');
    svg.appendChild(bg);

    // Title
    if (pieModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', centerX);
        titleText.setAttribute('y', 30);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '18');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = pieModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', () => editPieSettings());
        svg.appendChild(titleText);
    }

    const slices = pieModel.slices || [];
    const total = slices.reduce((sum, s) => sum + (s.value || 0), 0);

    if (slices.length === 0 || total === 0) {
        // Empty state - draw circle outline
        const emptyCircle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        emptyCircle.setAttribute('cx', centerX);
        emptyCircle.setAttribute('cy', centerY);
        emptyCircle.setAttribute('r', radius);
        emptyCircle.setAttribute('fill', 'none');
        emptyCircle.setAttribute('stroke', isDark ? '#45475a' : '#cccccc');
        emptyCircle.setAttribute('stroke-width', '2');
        emptyCircle.setAttribute('stroke-dasharray', '5,5');
        svg.appendChild(emptyCircle);

        const emptyText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        emptyText.setAttribute('x', centerX);
        emptyText.setAttribute('y', centerY);
        emptyText.setAttribute('text-anchor', 'middle');
        emptyText.setAttribute('fill', textColor);
        emptyText.setAttribute('font-size', '14');
        emptyText.setAttribute('opacity', '0.5');
        emptyText.textContent = 'No slices. Click "Add Slice" to get started.';
        svg.appendChild(emptyText);
    } else {
        // Draw pie slices
        let currentAngle = -Math.PI / 2; // Start from top

        slices.forEach((slice, i) => {
            const sliceAngle = (slice.value / total) * Math.PI * 2;
            const color = sliceColors[i % sliceColors.length];
            const isSelected = pieSelectedSlice === i;

            // Calculate slice path
            const startAngle = currentAngle;
            const endAngle = currentAngle + sliceAngle;

            const x1 = centerX + radius * Math.cos(startAngle);
            const y1 = centerY + radius * Math.sin(startAngle);
            const x2 = centerX + radius * Math.cos(endAngle);
            const y2 = centerY + radius * Math.sin(endAngle);

            const largeArcFlag = sliceAngle > Math.PI ? 1 : 0;

            // Offset selected slice slightly
            let sliceCx = centerX;
            let sliceCy = centerY;
            if (isSelected) {
                const midAngle = startAngle + sliceAngle / 2;
                sliceCx += 8 * Math.cos(midAngle);
                sliceCy += 8 * Math.sin(midAngle);
            }

            const sx1 = sliceCx + radius * Math.cos(startAngle);
            const sy1 = sliceCy + radius * Math.sin(startAngle);
            const sx2 = sliceCx + radius * Math.cos(endAngle);
            const sy2 = sliceCy + radius * Math.sin(endAngle);

            let pathD;
            if (slices.length === 1) {
                // Full circle
                const r = radius;
                pathD = `M ${sliceCx + r} ${sliceCy} A ${r} ${r} 0 1 1 ${sliceCx - r} ${sliceCy} A ${r} ${r} 0 1 1 ${sliceCx + r} ${sliceCy} Z`;
            } else {
                pathD = `M ${sliceCx} ${sliceCy} L ${sx1} ${sy1} A ${radius} ${radius} 0 ${largeArcFlag} 1 ${sx2} ${sy2} Z`;
            }

            const slicePath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            slicePath.setAttribute('d', pathD);
            slicePath.setAttribute('fill', color);
            slicePath.setAttribute('stroke', bgColor);
            slicePath.setAttribute('stroke-width', '2');

            if (isSelected) {
                slicePath.setAttribute('stroke', isDark ? '#f9e2af' : '#ff9800');
                slicePath.setAttribute('stroke-width', '3');
            }

            slicePath.style.cursor = 'pointer';
            slicePath.setAttribute('opacity', isSelected ? '1' : '0.85');

            const idx = i;
            slicePath.addEventListener('click', (e) => {
                e.stopPropagation();
                selectPieSlice(idx);
            });
            slicePath.addEventListener('dblclick', (e) => {
                e.stopPropagation();
                editPieSlice(idx);
            });
            slicePath.addEventListener('mouseenter', () => {
                slicePath.setAttribute('opacity', '1');
            });
            slicePath.addEventListener('mouseleave', () => {
                slicePath.setAttribute('opacity', isSelected ? '1' : '0.85');
            });

            svg.appendChild(slicePath);

            // Label on slice
            const midAngle = startAngle + sliceAngle / 2;
            const labelRadius = radius * 0.65;
            const labelX = (isSelected ? sliceCx : centerX) + labelRadius * Math.cos(midAngle);
            const labelY = (isSelected ? sliceCy : centerY) + labelRadius * Math.sin(midAngle);

            if (sliceAngle > 0.2) { // Only show label if slice is big enough
                const labelText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                labelText.setAttribute('x', labelX);
                labelText.setAttribute('y', labelY);
                labelText.setAttribute('text-anchor', 'middle');
                labelText.setAttribute('dominant-baseline', 'middle');
                labelText.setAttribute('fill', '#ffffff');
                labelText.setAttribute('font-size', '11');
                labelText.setAttribute('font-weight', 'bold');
                labelText.style.pointerEvents = 'none';

                const percentage = ((slice.value / total) * 100).toFixed(1);
                labelText.textContent = `${percentage}%`;
                svg.appendChild(labelText);
            }

            currentAngle = endAngle;
        });

        // Legend
        const legendStartY = centerY + radius + 30;
        const legendX = 40;
        const legendItemHeight = 24;

        slices.forEach((slice, i) => {
            const y = legendStartY + i * legendItemHeight;
            const color = sliceColors[i % sliceColors.length];

            // Color box
            const colorBox = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            colorBox.setAttribute('x', legendX);
            colorBox.setAttribute('y', y);
            colorBox.setAttribute('width', 16);
            colorBox.setAttribute('height', 16);
            colorBox.setAttribute('fill', color);
            colorBox.setAttribute('rx', '2');
            svg.appendChild(colorBox);

            // Label
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', legendX + 24);
            label.setAttribute('y', y + 12);
            label.setAttribute('fill', textColor);
            label.setAttribute('font-size', '12');

            const percentage = total > 0 ? ((slice.value / total) * 100).toFixed(1) : '0';
            const valueStr = pieModel.showData ? ` (${slice.value})` : '';
            label.textContent = `${slice.label}: ${percentage}%${valueStr}`;
            label.style.cursor = 'pointer';

            const idx = i;
            label.addEventListener('click', () => selectPieSlice(idx));
            label.addEventListener('dblclick', () => editPieSlice(idx));
            svg.appendChild(label);
        });

        // Adjust SVG height for legend
        const neededHeight = legendStartY + slices.length * legendItemHeight + 60;
        if (neededHeight > svgHeight) {
            svg.setAttribute('height', neededHeight);
            bg.setAttribute('height', neededHeight);
        }
    }

    // Toolbar
    const toolbarY = parseFloat(svg.getAttribute('height')) - 45;
    renderPieToolbar(svg, 10, toolbarY, svgWidth - 20, isDark, textColor);

    canvas.appendChild(svg);
}

function renderPieToolbar(svg, x, y, width, isDark, textColor) {
    const btnBg = isDark ? '#313244' : '#e0e0e0';
    const btnHover = isDark ? '#45475a' : '#bdbdbd';
    const buttons = [
        { label: '+ Add Slice', action: () => createPieSlice() },
        { label: 'Settings', action: () => editPieSettings() }
    ];

    if (pieSelectedSlice !== null) {
        buttons.push({ label: 'Edit Slice', action: () => editPieSlice(pieSelectedSlice) });
        buttons.push({ label: 'Delete Slice', action: () => deletePieSlice() });
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

// ========== Pie Chart Interactions ==========

function selectPieSlice(index) {
    pieSelectedSlice = index;
    renderPieChart();
    postMessage({ type: 'pie_sliceSelected', index });
}

function createPieSlice() {
    const label = prompt('Slice label:', 'New Slice');
    if (!label) return;

    const valueStr = prompt('Value:', '10');
    const value = parseFloat(valueStr);
    if (isNaN(value) || value <= 0) {
        alert('Please enter a positive number.');
        return;
    }

    postMessage({ type: 'pie_sliceCreated', label, value });
}

function editPieSlice(index) {
    if (!pieModel || !pieModel.slices || index >= pieModel.slices.length) return;
    const slice = pieModel.slices[index];

    const label = prompt('Slice label:', slice.label);
    if (label === null) return;

    const valueStr = prompt('Value:', String(slice.value));
    const value = parseFloat(valueStr);
    if (isNaN(value) || value <= 0) {
        alert('Please enter a positive number.');
        return;
    }

    postMessage({ type: 'pie_sliceEdited', index, label, value });
}

function deletePieSlice() {
    if (pieSelectedSlice === null) return;
    if (!confirm('Delete this slice?')) return;

    postMessage({ type: 'pie_sliceDeleted', index: pieSelectedSlice });
    pieSelectedSlice = null;
}

function editPieSettings() {
    const title = prompt('Chart title:', pieModel.title || '');
    if (title === null) return;

    const showData = confirm('Show data values in legend?');

    postMessage({ type: 'pie_settingsChanged', title, showData });
}
