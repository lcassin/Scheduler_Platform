// ============================================================
// VisualEditor.MindMap.js - Mind Map Visual Editor
// Uses the flowchart infrastructure (panzoom, drag, minimap, toolbar)
// C# bridge converts the tree model to flowchart-compatible nodes+edges
// ============================================================

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

        // Auto-layout (dagre handles the tree layout with LR direction)
        var needsLayout = diagram.nodes.some(function(n) { return n.x === 0 && n.y === 0; });
        if (needsLayout && diagram.nodes.length > 0) {
            autoLayout();
        }

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

        // Auto-layout for restore (all positions are 0 from C#)
        var allZero = diagram.nodes.every(function(n) { return n.x === 0 && n.y === 0; });
        if (allZero && diagram.nodes.length > 0) {
            autoLayout();
        }

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

        // Always re-layout on refresh since tree structure may have changed
        autoLayout();
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
