// ============================================================
// VisualEditor.MindMap.js - Mind Map Visual Editor
// Tree-based layout from central root node
// ============================================================

// ========== Mind Map State ==========
let mindMapModel = null;
let mindMapSelectedPath = null; // Array of indices from root

// ========== Mind Map Load/Restore ==========

window.loadMindMap = function(jsonStr) {
    try {
        currentDiagramType = 'mindmap';
        mindMapModel = JSON.parse(jsonStr);
        mindMapSelectedPath = null;
        updateToolbarForDiagramType();
        renderMindMap();
        // Deferred re-render: on first load the container may not have its final
        // dimensions yet (WebView still sizing), causing a scrunched layout.
        setTimeout(function() { if (mindMapModel) renderMindMap(); }, 150);
    } catch (e) {
        console.error('Failed to load mind map:', e);
    }
};

window.restoreMindMap = function(jsonStr) {
    try {
        mindMapModel = JSON.parse(jsonStr);
        renderMindMap();
    } catch (e) {
        console.error('Failed to restore mind map:', e);
    }
};

window.refreshMindMap = function(jsonStr) {
    try {
        mindMapModel = JSON.parse(jsonStr);
        renderMindMap();
    } catch (e) {
        console.error('Failed to refresh mind map:', e);
    }
};

// ========== Mind Map Rendering ==========

function renderMindMap() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !mindMapModel || !mindMapModel.root) return;

    // Show editorCanvas, hide diagram-svg for standalone SVG rendering
    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';

    canvas.innerHTML = '';

    // Read theme colors from CSS variables (set by theme-light / theme-twilight / default dark)
    const cs = getComputedStyle(document.body);
    const cv = (v) => cs.getPropertyValue(v).trim();
    const textColor = cv('--node-text') || '#D4D4D4';
    const bgColor = cv('--bg-color') || '#1E1E1E';
    const isLight = document.body.classList.contains('theme-light');
    const isTwilight = document.body.classList.contains('theme-twilight');

    // Color palette for different levels - adapt to current theme
    const levelColors = isLight
        ? ['#2196f3', '#4caf50', '#ff9800', '#f44336', '#9c27b0', '#009688', '#ff5722']
        : isTwilight
        ? ['#4A90D9', '#5A9E6F', '#D19A66', '#E06C75', '#B48EAD', '#56B6C2', '#D19A66']
        : ['#89b4fa', '#a6e3a1', '#f9e2af', '#f38ba8', '#cba6f7', '#94e2d5', '#fab387'];

    // Calculate layout
    const layout = calculateMindMapLayout(mindMapModel.root, 0);

    // Determine SVG size
    const margin = 60;
    const bounds = getMindMapBounds(layout);
    const svgWidth = Math.max(600, bounds.maxX - bounds.minX + margin * 2);
    const svgHeight = Math.max(400, bounds.maxY - bounds.minY + margin * 2);

    // Create SVG
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

    // Offset to center the layout
    const offsetX = -bounds.minX + margin;
    const offsetY = -bounds.minY + margin;

    // Draw connections first (behind nodes)
    drawMindMapConnections(svg, layout, offsetX, offsetY, levelColors, isLight);

    // Draw nodes
    drawMindMapNodes(svg, layout, offsetX, offsetY, levelColors, textColor, isLight);

    // Toolbar
    renderMindMapToolbar(svg, 10, svgHeight - 50, svgWidth - 20, isLight, textColor);

    canvas.appendChild(svg);
}

function calculateMindMapLayout(node, level, path) {
    path = path || [];
    const nodeWidth = Math.max(80, node.label.length * 9 + 30);
    const nodeHeight = level === 0 ? 50 : 36;
    const horizontalGap = 60;
    const verticalGap = level === 0 ? 80 : 50;

    const result = {
        node: node,
        level: level,
        path: [...path],
        x: 0,
        y: 0,
        width: nodeWidth,
        height: nodeHeight,
        children: []
    };

    if (!node.children || node.children.length === 0) {
        return result;
    }

    // Layout children
    const childLayouts = node.children.map((child, idx) => {
        return calculateMindMapLayout(child, level + 1, [...path, idx]);
    });

    result.children = childLayouts;

    // Position children relative to parent
    // For root: spread children in a radial-ish pattern
    // For others: tree layout to the right
    if (level === 0) {
        // Split children into left and right
        const halfCount = Math.ceil(childLayouts.length / 2);
        const rightChildren = childLayouts.slice(0, halfCount);
        const leftChildren = childLayouts.slice(halfCount);

        // Position right children
        let rightY = -(rightChildren.length - 1) * (verticalGap + nodeHeight) / 2;
        rightChildren.forEach(child => {
            child.x = horizontalGap + nodeWidth;
            child.y = rightY;
            positionSubtree(child, child.x, child.y, horizontalGap, verticalGap);
            rightY += verticalGap + getSubtreeHeight(child, verticalGap);
        });

        // Position left children (mirrored)
        let leftY = -(leftChildren.length - 1) * (verticalGap + nodeHeight) / 2;
        leftChildren.forEach(child => {
            child.x = -(horizontalGap + nodeWidth);
            child.y = leftY;
            positionSubtree(child, child.x, child.y, -horizontalGap, verticalGap);
            leftY += verticalGap + getSubtreeHeight(child, verticalGap);
        });
    }

    return result;
}

function positionSubtree(layout, baseX, baseY, hGap, vGap) {
    if (layout.children.length === 0) return;

    const totalHeight = layout.children.reduce((sum, c) =>
        sum + getSubtreeHeight(c, vGap), 0) + (layout.children.length - 1) * vGap;

    let currentY = baseY - totalHeight / 2 + layout.height / 2;

    layout.children.forEach(child => {
        child.x = baseX + (hGap > 0 ? layout.width + Math.abs(hGap) : -(child.width + Math.abs(hGap)));
        child.y = currentY;
        positionSubtree(child, child.x, child.y, hGap, vGap);
        currentY += getSubtreeHeight(child, vGap) + vGap;
    });
}

function getSubtreeHeight(layout, vGap) {
    if (layout.children.length === 0) return layout.height;
    const childrenHeight = layout.children.reduce((sum, c) =>
        sum + getSubtreeHeight(c, vGap), 0) + (layout.children.length - 1) * vGap;
    return Math.max(layout.height, childrenHeight);
}

function getMindMapBounds(layout) {
    let minX = layout.x, maxX = layout.x + layout.width;
    let minY = layout.y, maxY = layout.y + layout.height;

    function traverse(node) {
        minX = Math.min(minX, node.x);
        maxX = Math.max(maxX, node.x + node.width);
        minY = Math.min(minY, node.y);
        maxY = Math.max(maxY, node.y + node.height);
        (node.children || []).forEach(traverse);
    }
    traverse(layout);

    return { minX, maxX, minY, maxY };
}

function drawMindMapConnections(svg, layout, offsetX, offsetY, colors, isLight) {
    const lineColor = isLight ? 'rgba(0,0,0,0.15)' : 'rgba(255,255,255,0.2)';

    function drawConnections(parent) {
        (parent.children || []).forEach(child => {
            const color = colors[child.level % colors.length];

            // Curved connection line
            const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            const px = parent.x + parent.width / 2 + offsetX;
            const py = parent.y + parent.height / 2 + offsetY;
            const cx = child.x + child.width / 2 + offsetX;
            const cy = child.y + child.height / 2 + offsetY;
            const midX = (px + cx) / 2;

            path.setAttribute('d', `M ${px} ${py} C ${midX} ${py}, ${midX} ${cy}, ${cx} ${cy}`);
            path.setAttribute('stroke', color);
            path.setAttribute('stroke-width', Math.max(1, 3 - child.level));
            path.setAttribute('fill', 'none');
            path.setAttribute('opacity', '0.6');
            svg.appendChild(path);

            drawConnections(child);
        });
    }

    drawConnections(layout);
}

function drawMindMapNodes(svg, layout, offsetX, offsetY, colors, textColor, isLight) {
    function drawNode(nodeLayout) {
        const x = nodeLayout.x + offsetX;
        const y = nodeLayout.y + offsetY;
        const w = nodeLayout.width;
        const h = nodeLayout.height;
        const level = nodeLayout.level;
        const color = colors[level % colors.length];
        const isSelected = mindMapSelectedPath !== null &&
            JSON.stringify(mindMapSelectedPath) === JSON.stringify(nodeLayout.path);

        // Draw shape based on node shape type
        const shape = nodeLayout.node.shape || 'Default';
        let nodeElement;

        switch (shape) {
            case 'Circle':
                nodeElement = document.createElementNS('http://www.w3.org/2000/svg', 'ellipse');
                nodeElement.setAttribute('cx', x + w / 2);
                nodeElement.setAttribute('cy', y + h / 2);
                nodeElement.setAttribute('rx', w / 2);
                nodeElement.setAttribute('ry', h / 2);
                break;
            case 'Hexagon': {
                const points = [
                    `${x + w * 0.25},${y}`,
                    `${x + w * 0.75},${y}`,
                    `${x + w},${y + h / 2}`,
                    `${x + w * 0.75},${y + h}`,
                    `${x + w * 0.25},${y + h}`,
                    `${x},${y + h / 2}`
                ].join(' ');
                nodeElement = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                nodeElement.setAttribute('points', points);
                break;
            }
            case 'Cloud': {
                nodeElement = document.createElementNS('http://www.w3.org/2000/svg', 'ellipse');
                nodeElement.setAttribute('cx', x + w / 2);
                nodeElement.setAttribute('cy', y + h / 2);
                nodeElement.setAttribute('rx', w / 2 + 5);
                nodeElement.setAttribute('ry', h / 2 + 3);
                break;
            }
            case 'Bang': {
                // Starburst shape approximated by a larger rounded rect
                nodeElement = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                nodeElement.setAttribute('x', x - 3);
                nodeElement.setAttribute('y', y - 3);
                nodeElement.setAttribute('width', w + 6);
                nodeElement.setAttribute('height', h + 6);
                nodeElement.setAttribute('rx', '2');
                break;
            }
            case 'Square':
                nodeElement = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                nodeElement.setAttribute('x', x);
                nodeElement.setAttribute('y', y);
                nodeElement.setAttribute('width', w);
                nodeElement.setAttribute('height', h);
                nodeElement.setAttribute('rx', '2');
                break;
            case 'Rounded':
            default:
                nodeElement = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                nodeElement.setAttribute('x', x);
                nodeElement.setAttribute('y', y);
                nodeElement.setAttribute('width', w);
                nodeElement.setAttribute('height', h);
                nodeElement.setAttribute('rx', level === 0 ? '25' : '12');
                break;
        }

        // Style
        if (level === 0) {
            nodeElement.setAttribute('fill', color);
            nodeElement.setAttribute('opacity', '0.9');
        } else {
            nodeElement.setAttribute('fill', isLight ? 'rgba(0,0,0,0.03)' : 'rgba(255,255,255,0.05)');
            nodeElement.setAttribute('stroke', color);
            nodeElement.setAttribute('stroke-width', '2');
        }

        if (isSelected) {
            nodeElement.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
            nodeElement.setAttribute('stroke-width', '3');
        }

        nodeElement.style.cursor = 'pointer';
        const nodePath = [...nodeLayout.path];
        nodeElement.addEventListener('click', (e) => {
            e.stopPropagation();
            selectMindMapNode(nodePath);
        });
        nodeElement.addEventListener('dblclick', (e) => {
            e.stopPropagation();
            editMindMapNode(nodePath);
        });
        svg.appendChild(nodeElement);

        // Label text
        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', x + w / 2);
        text.setAttribute('y', y + h / 2 + 5);
        text.setAttribute('text-anchor', 'middle');
        text.setAttribute('fill', level === 0 ? (isLight ? '#ffffff' : '#1e1e2e') : textColor);
        text.setAttribute('font-size', level === 0 ? '15' : '12');
        text.setAttribute('font-weight', level === 0 ? 'bold' : 'normal');
        text.textContent = nodeLayout.node.label;
        text.style.cursor = 'pointer';
        text.style.pointerEvents = 'none';
        svg.appendChild(text);

        // Icon indicator
        if (nodeLayout.node.icon) {
            const iconText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            iconText.setAttribute('x', x + w - 5);
            iconText.setAttribute('y', y + 12);
            iconText.setAttribute('text-anchor', 'end');
            iconText.setAttribute('fill', textColor);
            iconText.setAttribute('font-size', '10');
            iconText.setAttribute('opacity', '0.5');
            iconText.textContent = '*';
            svg.appendChild(iconText);
        }

        // Draw children
        (nodeLayout.children || []).forEach(drawNode);
    }

    drawNode(layout);
}

function renderMindMapToolbar(svg, x, y, width, isLight, textColor) {
    const btnBg = isLight ? '#e0e0e0' : '#313244';
    const btnHover = isLight ? '#bdbdbd' : '#45475a';
    const buttons = [
        { label: '+ Add Child', action: () => createMindMapChild() },
        { label: 'Edit Node', action: () => { if (mindMapSelectedPath) editMindMapNode(mindMapSelectedPath); } }
    ];

    if (mindMapSelectedPath !== null && mindMapSelectedPath.length > 0) {
        buttons.push({ label: 'Delete Node', action: () => deleteMindMapNode() });
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

// ========== Mind Map Interactions ==========

function selectMindMapNode(path) {
    mindMapSelectedPath = path;
    renderMindMap();
    postMessage({ type: 'mm_nodeSelected', path });
}

function createMindMapChild() {
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Child Node';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="mm-dlg-label" value="New Node" />
        </div>
        <div class="property-row">
            <div class="property-label">Shape</div>
            <select class="property-select" id="mm-dlg-shape">
                <option value="Default" selected>Default (Rounded)</option>
                <option value="Square">Square</option>
                <option value="Rounded">Rounded</option>
                <option value="Circle">Circle</option>
                <option value="Bang">Bang</option>
                <option value="Cloud">Cloud</option>
                <option value="Hexagon">Hexagon</option>
            </select>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="mm-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Node</button>
        </div>
    `;
    document.getElementById('mm-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('mm-dlg-label').value.trim();
        if (!label) return;
        const shape = document.getElementById('mm-dlg-shape').value;
        postMessage({
            type: 'mm_nodeCreated',
            label,
            shape: shape || 'Default',
            parentPath: mindMapSelectedPath || []
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('mm-dlg-label').select(), 50);
}

function editMindMapNode(path) {
    const node = findMindMapNodeByPath(path);
    if (!node) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Node';
    const body = document.querySelector('.property-panel-body');
    const currentShape = node.shape || 'Default';
    body.innerHTML = `
        <div class="property-row">
            <div class="property-label">Label</div>
            <input class="property-input" id="mm-dlg-label" value="${_escHtml(node.label)}" />
        </div>
        <div class="property-row">
            <div class="property-label">Shape</div>
            <select class="property-select" id="mm-dlg-shape">
                <option value="Default" ${currentShape === 'Default' ? 'selected' : ''}>Default (Rounded)</option>
                <option value="Square" ${currentShape === 'Square' ? 'selected' : ''}>Square</option>
                <option value="Rounded" ${currentShape === 'Rounded' ? 'selected' : ''}>Rounded</option>
                <option value="Circle" ${currentShape === 'Circle' ? 'selected' : ''}>Circle</option>
                <option value="Bang" ${currentShape === 'Bang' ? 'selected' : ''}>Bang</option>
                <option value="Cloud" ${currentShape === 'Cloud' ? 'selected' : ''}>Cloud</option>
                <option value="Hexagon" ${currentShape === 'Hexagon' ? 'selected' : ''}>Hexagon</option>
            </select>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="mm-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>
    `;
    document.getElementById('mm-dlg-ok').addEventListener('click', function() {
        const label = document.getElementById('mm-dlg-label').value.trim();
        if (!label) return;
        const shape = document.getElementById('mm-dlg-shape').value;
        postMessage({
            type: 'mm_nodeEdited',
            path,
            label,
            shape: shape || 'Default'
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('mm-dlg-label').select(), 50);
}

function deleteMindMapNode() {
    if (!mindMapSelectedPath || mindMapSelectedPath.length === 0) {
        // Show info in property panel instead of alert
        const propertyPanel = document.getElementById('property-panel');
        const propPanelTitle = document.getElementById('property-panel-title');
        propPanelTitle.textContent = 'Info';
        const body = document.querySelector('.property-panel-body');
        body.innerHTML = `
            <div class="property-row"><div class="property-label" style="width:100%;text-align:center">Cannot delete the root node.</div></div>
            <div class="property-row" style="margin-top:8px">
                <button id="mm-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">OK</button>
            </div>
        `;
        document.getElementById('mm-dlg-ok').addEventListener('click', function() {
            propertyPanel.classList.remove('visible');
        });
        propertyPanel.classList.add('visible');
        return;
    }

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Node';
    const body = document.querySelector('.property-panel-body');
    body.innerHTML = `
        <div class="property-row"><div class="property-label" style="width:100%;text-align:center">Delete this node and all its children?</div></div>
        <div class="property-row" style="display:flex;gap:8px;margin-top:8px">
            <button id="mm-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>
            <button id="mm-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">Cancel</button>
        </div>
    `;
    document.getElementById('mm-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'mm_nodeDeleted', path: mindMapSelectedPath });
        mindMapSelectedPath = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('mm-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function findMindMapNodeByPath(path) {
    if (!mindMapModel || !mindMapModel.root) return null;
    let current = mindMapModel.root;
    for (const idx of path) {
        if (!current.children || idx >= current.children.length) return null;
        current = current.children[idx];
    }
    return current;
}
