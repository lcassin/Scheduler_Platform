// ============================================================
// VisualEditor.MindMap.js - Mind Map Visual Editor
// Uses the flowchart infrastructure (panzoom, drag, minimap, toolbar)
// C# bridge converts the tree model to flowchart-compatible nodes+edges
// ============================================================

// ========== Mind Map Branch Colors ==========
// Mermaid-like color palette for each root-child branch
var _mmBranchColors = [
    '#E6194B', // red
    '#3CB44B', // green
    '#FFE119', // yellow
    '#4363D8', // blue
    '#F58231', // orange
    '#911EB4', // purple
    '#42D4F4', // cyan
    '#F032E6', // magenta
    '#BFEF45', // lime
    '#FABEBE', // pink
    '#469990', // teal
    '#E6BEFF', // lavender
    '#9A6324', // brown
    '#800000', // maroon
    '#AAFFC3', // mint
    '#808000', // olive
    '#FFD8B1', // apricot
    '#000075', // navy
];

function _assignMindMapBranchColors() {
    // Build a map from node ID to node for quick lookup
    var nodeMap = {};
    diagram.nodes.forEach(function(n) { nodeMap[n.id] = n; });
    
    // Build parent→children map from edges
    var childrenOf = {};
    diagram.edges.forEach(function(e) {
        if (!childrenOf[e.from]) childrenOf[e.from] = [];
        childrenOf[e.from].push(e.to);
    });
    
    // Find root node (mm_root)
    var root = nodeMap['mm_root'];
    if (!root) return;
    
    // Root gets no branch color (uses default theme)
    root._branchColor = null;
    root._branchIndex = -1;
    
    // Each root-child gets a unique branch color
    var rootChildren = childrenOf['mm_root'] || [];
    rootChildren.forEach(function(childId, branchIdx) {
        var color = _mmBranchColors[branchIdx % _mmBranchColors.length];
        // Assign color to this child and all its descendants
        _assignBranchColorRecursive(childId, branchIdx, color, nodeMap, childrenOf);
    });
}

function _assignBranchColorRecursive(nodeId, branchIdx, color, nodeMap, childrenOf) {
    var node = nodeMap[nodeId];
    if (!node) return;
    node._branchColor = color;
    node._branchIndex = branchIdx;
    var children = childrenOf[nodeId] || [];
    children.forEach(function(cid) {
        _assignBranchColorRecursive(cid, branchIdx, color, nodeMap, childrenOf);
    });
}

// ========== Mind Map Radial Layout ==========
// Places root at center, children radiate outward in a circle.
// Subtrees are laid out recursively with angular sectors.

function mindMapRadialLayout() {
    if (diagram.nodes.length === 0) return;

    // Build adjacency from edges
    var nodeMap = {};
    diagram.nodes.forEach(function(n) { nodeMap[n.id] = n; });
    var childrenOf = {};
    diagram.edges.forEach(function(e) {
        if (!childrenOf[e.from]) childrenOf[e.from] = [];
        childrenOf[e.from].push(e.to);
    });

    var root = nodeMap['mm_root'];
    if (!root) return;

    // Compute subtree sizes (total leaf count) for proportional angle allocation
    var subtreeSize = {};
    function computeSubtreeSize(id) {
        var children = childrenOf[id] || [];
        if (children.length === 0) {
            subtreeSize[id] = 1;
            return 1;
        }
        var total = 0;
        children.forEach(function(cid) { total += computeSubtreeSize(cid); });
        subtreeSize[id] = total;
        return total;
    }
    computeSubtreeSize('mm_root');

    // Place root at origin
    root.x = 0;
    root.y = 0;

    // Radial spacing parameters
    var baseRadius = 200;  // distance from root to first level
    var levelSpacing = 180; // additional distance per level

    // Layout a subtree within an angular sector [startAngle, endAngle]
    function layoutSubtree(nodeId, level, cx, cy, startAngle, endAngle) {
        var children = childrenOf[nodeId] || [];
        if (children.length === 0) return;

        var radius = baseRadius + (level - 1) * levelSpacing;
        var parentSize = subtreeSize[nodeId] || 1;
        var angleRange = endAngle - startAngle;
        var currentAngle = startAngle;

        children.forEach(function(childId) {
            var childNode = nodeMap[childId];
            if (!childNode) return;

            var childSize = subtreeSize[childId] || 1;
            var childAngleRange = angleRange * (childSize / parentSize);
            var midAngle = currentAngle + childAngleRange / 2;

            childNode.x = cx + radius * Math.cos(midAngle);
            childNode.y = cy + radius * Math.sin(midAngle);

            // Recursively layout grandchildren in a narrower sector from child's position
            layoutSubtree(childId, level + 1, childNode.x, childNode.y,
                midAngle - childAngleRange / 2.5, midAngle + childAngleRange / 2.5);

            currentAngle += childAngleRange;
        });
    }

    // Layout root's children across full 360 degrees
    layoutSubtree('mm_root', 1, 0, 0, 0, 2 * Math.PI);
}

// ========== Mind Map Load/Restore ==========
// These functions receive flowchart-compatible JSON from C# and load it
// using the existing flowchart diagram object and render() function.

window.loadMindMap = function(jsonStr) {
    try {
        currentDiagramType = 'mindmap';
        const data = typeof jsonStr === 'string' ? JSON.parse(jsonStr) : jsonStr;

        // Load into the flowchart diagram object
        diagram.direction = data.direction || 'LR';
        diagram.nodes = (data.nodes || []).map(function(n) {
            return {
                id: n.id,
                label: n.label || n.id,
                shape: n.shape || 'Rounded',
                x: n.x || 0,
                y: n.y || 0,
                width: n.width || 0,
                height: n.height || 0,
                cssClass: n.cssClass || null,
                mindMapLevel: n.mindMapLevel || 0,
                mindMapShape: n.mindMapShape || 'Default'
            };
        });
        diagram.edges = (data.edges || []).map(function(e) {
            return {
                from: e.from,
                to: e.to,
                label: e.label || '',
                style: e.style || 'Solid',
                arrowType: e.arrowType || 'None'
            };
        });
        diagram.subgraphs = [];
        diagram.styles = [];

        // Auto-size nodes
        diagram.nodes.forEach(function(node) {
            var minSize = estimateNodeSize(node);
            if (!node.width || node.width < minSize.width) node.width = minSize.width;
            if (!node.height || node.height < minSize.height) node.height = minSize.height;
        });

        // Radial layout for mind map (root centered, children radiate outward)
        var needsLayout = diagram.nodes.some(function(n) { return n.x === 0 && n.y === 0; });
        if (needsLayout && diagram.nodes.length > 0) {
            mindMapRadialLayout();
        }

        // Assign branch colors for visual distinction
        _assignMindMapBranchColors();

        selectedNodeId = null;
        selectedEdgeIndex = -1;
        updateToolbarForDiagramType();
        render();
        centerView();
    } catch (e) {
        console.error('Failed to load mind map:', e);
    }
};

window.restoreMindMap = function(jsonStr) {
    try {
        var data = typeof jsonStr === 'string' ? JSON.parse(jsonStr) : jsonStr;

        diagram.direction = data.direction || 'LR';
        diagram.nodes = (data.nodes || []).map(function(n) {
            return {
                id: n.id,
                label: n.label || n.id,
                shape: n.shape || 'Rounded',
                x: n.x || 0,
                y: n.y || 0,
                width: n.width || 0,
                height: n.height || 0,
                cssClass: n.cssClass || null,
                mindMapLevel: n.mindMapLevel || 0,
                mindMapShape: n.mindMapShape || 'Default'
            };
        });
        diagram.edges = (data.edges || []).map(function(e) {
            return {
                from: e.from,
                to: e.to,
                label: e.label || '',
                style: e.style || 'Solid',
                arrowType: e.arrowType || 'None'
            };
        });
        diagram.subgraphs = [];
        diagram.styles = [];

        diagram.nodes.forEach(function(node) {
            var minSize = estimateNodeSize(node);
            if (!node.width || node.width < minSize.width) node.width = minSize.width;
            if (!node.height || node.height < minSize.height) node.height = minSize.height;
        });

        // Radial layout for restore (all positions are 0 from C#)
        var allZero = diagram.nodes.every(function(n) { return n.x === 0 && n.y === 0; });
        if (allZero && diagram.nodes.length > 0) {
            mindMapRadialLayout();
        }

        _assignMindMapBranchColors();
        render();
    } catch (e) {
        console.error('Failed to restore mind map:', e);
    }
};

window.refreshMindMap = function(jsonStr) {
    try {
        var data = typeof jsonStr === 'string' ? JSON.parse(jsonStr) : jsonStr;

        diagram.direction = data.direction || 'LR';
        diagram.nodes = (data.nodes || []).map(function(n) {
            return {
                id: n.id,
                label: n.label || n.id,
                shape: n.shape || 'Rounded',
                x: n.x || 0,
                y: n.y || 0,
                width: n.width || 0,
                height: n.height || 0,
                cssClass: n.cssClass || null,
                mindMapLevel: n.mindMapLevel || 0,
                mindMapShape: n.mindMapShape || 'Default'
            };
        });
        diagram.edges = (data.edges || []).map(function(e) {
            return {
                from: e.from,
                to: e.to,
                label: e.label || '',
                style: e.style || 'Solid',
                arrowType: e.arrowType || 'None'
            };
        });
        diagram.subgraphs = [];
        diagram.styles = [];

        diagram.nodes.forEach(function(node) {
            var minSize = estimateNodeSize(node);
            if (!node.width || node.width < minSize.width) node.width = minSize.width;
            if (!node.height || node.height < minSize.height) node.height = minSize.height;
        });

        // Re-layout on refresh only if nodes don't have saved positions
        var allZero = diagram.nodes.every(function(n) { return n.x === 0 && n.y === 0; });
        if (allZero && diagram.nodes.length > 0) {
            mindMapRadialLayout();
        }
        _assignMindMapBranchColors();
        render();
    } catch (e) {
        console.error('Failed to refresh mind map:', e);
    }
};

// ========== Mind Map Interactions ==========
// These use the flowchart's selectedNodeId and property panel system.

function createMindMapChild() {
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Child Node';
    var body = document.querySelector('.property-panel-body');
    body.innerHTML = '<div class="property-row">' +
        '<div class="property-label">Label</div>' +
        '<input class="property-input" id="mm-dlg-label" value="New Node" />' +
        '</div>' +
        '<div class="property-row">' +
        '<div class="property-label">Shape</div>' +
        '<select class="property-select" id="mm-dlg-shape">' +
        '<option value="Default" selected>Default (Rounded)</option>' +
        '<option value="Square">Square</option>' +
        '<option value="Rounded">Rounded</option>' +
        '<option value="Circle">Circle</option>' +
        '<option value="Bang">Bang</option>' +
        '<option value="Cloud">Cloud</option>' +
        '<option value="Hexagon">Hexagon</option>' +
        '</select>' +
        '</div>' +
        '<div class="property-row" style="margin-top:8px">' +
        '<button id="mm-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Node</button>' +
        '</div>';
    document.getElementById('mm-dlg-ok').addEventListener('click', function() {
        var label = document.getElementById('mm-dlg-label').value.trim();
        if (!label) return;
        var shape = document.getElementById('mm-dlg-shape').value;
        // Use selectedNodeId (flowchart node ID) as parentId
        postMessage({
            type: 'mm_nodeCreated',
            label: label,
            shape: shape || 'Default',
            parentId: selectedNodeId || 'mm_root'
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(function() { document.getElementById('mm-dlg-label').select(); }, 50);
}

function editMindMapNode(nodeId) {
    // Find the node in the flowchart diagram object
    var node = diagram.nodes.find(function(n) { return n.id === nodeId; });
    if (!node) return;

    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Node';
    var body = document.querySelector('.property-panel-body');
    var currentShape = node.mindMapShape || 'Default';
    body.innerHTML = '<div class="property-row">' +
        '<div class="property-label">Label</div>' +
        '<input class="property-input" id="mm-dlg-label" value="' + _escHtml(node.label) + '" />' +
        '</div>' +
        '<div class="property-row">' +
        '<div class="property-label">Shape</div>' +
        '<select class="property-select" id="mm-dlg-shape">' +
        '<option value="Default"' + (currentShape === 'Default' ? ' selected' : '') + '>Default (Rounded)</option>' +
        '<option value="Square"' + (currentShape === 'Square' ? ' selected' : '') + '>Square</option>' +
        '<option value="Rounded"' + (currentShape === 'Rounded' ? ' selected' : '') + '>Rounded</option>' +
        '<option value="Circle"' + (currentShape === 'Circle' ? ' selected' : '') + '>Circle</option>' +
        '<option value="Bang"' + (currentShape === 'Bang' ? ' selected' : '') + '>Bang</option>' +
        '<option value="Cloud"' + (currentShape === 'Cloud' ? ' selected' : '') + '>Cloud</option>' +
        '<option value="Hexagon"' + (currentShape === 'Hexagon' ? ' selected' : '') + '>Hexagon</option>' +
        '</select>' +
        '</div>' +
        '<div class="property-row" style="margin-top:8px">' +
        '<button id="mm-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>' +
        '</div>';
    document.getElementById('mm-dlg-ok').addEventListener('click', function() {
        var label = document.getElementById('mm-dlg-label').value.trim();
        if (!label) return;
        var shape = document.getElementById('mm-dlg-shape').value;
        postMessage({
            type: 'mm_nodeEdited',
            nodeId: nodeId,
            label: label,
            shape: shape || 'Default'
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(function() { document.getElementById('mm-dlg-label').select(); }, 50);
}

function deleteMindMapNode() {
    if (!selectedNodeId || selectedNodeId === 'mm_root') {
        // Show info in property panel - cannot delete root
        var propertyPanel = document.getElementById('property-panel');
        var propPanelTitle = document.getElementById('property-panel-title');
        propPanelTitle.textContent = 'Info';
        var body = document.querySelector('.property-panel-body');
        body.innerHTML = '<div class="property-row"><div class="property-label" style="width:100%;text-align:center">Cannot delete the root node.</div></div>' +
            '<div class="property-row" style="margin-top:8px">' +
            '<button id="mm-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">OK</button>' +
            '</div>';
        document.getElementById('mm-dlg-ok').addEventListener('click', function() {
            propertyPanel.classList.remove('visible');
        });
        propertyPanel.classList.add('visible');
        return;
    }

    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Node';
    var body = document.querySelector('.property-panel-body');
    body.innerHTML = '<div class="property-row"><div class="property-label" style="width:100%;text-align:center">Delete this node and all its children?</div></div>' +
        '<div class="property-row" style="display:flex;gap:8px;margin-top:8px">' +
        '<button id="mm-dlg-yes" style="flex:1;padding:6px;cursor:pointer;background:#f44336;color:#fff;border:none;border-radius:4px">Delete</button>' +
        '<button id="mm-dlg-no" style="flex:1;padding:6px;cursor:pointer;background:var(--input-bg);color:var(--input-text);border:1px solid var(--input-border);border-radius:4px">Cancel</button>' +
        '</div>';
    document.getElementById('mm-dlg-yes').addEventListener('click', function() {
        postMessage({ type: 'mm_nodeDeleted', nodeId: selectedNodeId });
        selectedNodeId = null;
        propertyPanel.classList.remove('visible');
    });
    document.getElementById('mm-dlg-no').addEventListener('click', function() {
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}
