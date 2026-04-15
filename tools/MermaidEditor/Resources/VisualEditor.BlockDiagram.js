// ============================================================
// VisualEditor.BlockDiagram.js - Block Diagram Visual Editor
// Renders block-beta diagrams with grid layout, blocks, arrows,
// edges, groups, toolbar, context menus, property panel, undo/redo,
// minimap, copy/paste, and keyboard shortcuts.
//
// Architecture: matches other editors (Journey, Quadrant, etc.)
//   - No IIFE - shares scope with main HTML (currentDiagramType,
//     postMessage, updateToolbarForDiagramType, editorCanvasZoom, etc.)
//   - Uses CSS variables for theming (not inline colors)
//   - Uses property panel (upper-right) for editing (not centered modals)
//   - Uses WPF toolbar buttons (not custom DOM toolbar)
//   - Sets currentDiagramType + calls updateToolbarForDiagramType()
// ============================================================

// ========== Block Diagram State ==========
var bdModel = null;
var bdSelectedItem = null; // { type: 'block'|'arrow'|'space'|'group', id, groupId, index }
var bdSelectedEdge = null; // edge index
var bdClipboard = null;

// ========== Shape Rendering ==========
var bdShapeOptions = [
    { value: 'Rectangle', label: 'Rectangle [ ]' },
    { value: 'Rounded', label: 'Rounded ( )' },
    { value: 'Stadium', label: 'Stadium ([ ])' },
    { value: 'Subroutine', label: 'Subroutine [[ ]]' },
    { value: 'Cylinder', label: 'Cylinder [( )]' },
    { value: 'Circle', label: 'Circle (( ))' },
    { value: 'Rhombus', label: 'Diamond { }' },
    { value: 'Hexagon', label: 'Hexagon {{ }}' },
    { value: 'Asymmetric', label: 'Flag > ]' },
    { value: 'Parallelogram', label: 'Parallelogram [/ /]' },
    { value: 'ParallelogramAlt', label: 'Parallelogram Alt [\\ \\]' },
    { value: 'Trapezoid', label: 'Trapezoid [/ \\]' },
    { value: 'TrapezoidAlt', label: 'Trapezoid Alt [\\ /]' },
    { value: 'DoubleCircle', label: 'Double Circle ((( )))' }
];

function bdGetShapeSVGPath(shape, w, h) {
    var cx = w / 2, cy = h / 2;
    switch (shape) {
        case 'Rounded':
            return '<rect x="0" y="0" width="' + w + '" height="' + h + '" rx="12" ry="12"/>';
        case 'Stadium':
            return '<rect x="0" y="0" width="' + w + '" height="' + h + '" rx="' + (h / 2) + '" ry="' + (h / 2) + '"/>';
        case 'Subroutine': {
            var inset = 8;
            return '<rect x="0" y="0" width="' + w + '" height="' + h + '" rx="2" ry="2"/>' +
                '<line x1="' + inset + '" y1="0" x2="' + inset + '" y2="' + h + '"/>' +
                '<line x1="' + (w - inset) + '" y1="0" x2="' + (w - inset) + '" y2="' + h + '"/>';
        }
        case 'Cylinder':
            return '<path d="M0,' + (h * 0.15) + ' C0,0 ' + w + ',0 ' + w + ',' + (h * 0.15) + ' L' + w + ',' + (h * 0.85) + ' C' + w + ',' + h + ' 0,' + h + ' 0,' + (h * 0.85) + ' Z"/>' +
                '<path d="M0,' + (h * 0.15) + ' C0,' + (h * 0.3) + ' ' + w + ',' + (h * 0.3) + ' ' + w + ',' + (h * 0.15) + '" fill="none"/>';
        case 'Circle':
            return '<ellipse cx="' + cx + '" cy="' + cy + '" rx="' + cx + '" ry="' + cy + '"/>';
        case 'Rhombus':
            return '<polygon points="' + cx + ',0 ' + w + ',' + cy + ' ' + cx + ',' + h + ' 0,' + cy + '"/>';
        case 'Hexagon': {
            var ins = w * 0.15;
            return '<polygon points="' + ins + ',0 ' + (w - ins) + ',0 ' + w + ',' + cy + ' ' + (w - ins) + ',' + h + ' ' + ins + ',' + h + ' 0,' + cy + '"/>';
        }
        case 'Asymmetric':
            return '<polygon points="0,0 ' + (w - 15) + ',0 ' + w + ',' + cy + ' ' + (w - 15) + ',' + h + ' 0,' + h + '"/>';
        case 'Parallelogram': {
            var sk = w * 0.15;
            return '<polygon points="' + sk + ',0 ' + w + ',0 ' + (w - sk) + ',' + h + ' 0,' + h + '"/>';
        }
        case 'ParallelogramAlt': {
            var sk2 = w * 0.15;
            return '<polygon points="0,0 ' + (w - sk2) + ',0 ' + w + ',' + h + ' ' + sk2 + ',' + h + '"/>';
        }
        case 'Trapezoid': {
            var sk3 = w * 0.15;
            return '<polygon points="' + sk3 + ',0 ' + (w - sk3) + ',0 ' + w + ',' + h + ' 0,' + h + '"/>';
        }
        case 'TrapezoidAlt': {
            var sk4 = w * 0.15;
            return '<polygon points="0,0 ' + w + ',0 ' + (w - sk4) + ',' + h + ' ' + sk4 + ',' + h + '"/>';
        }
        case 'DoubleCircle':
            return '<ellipse cx="' + cx + '" cy="' + cy + '" rx="' + cx + '" ry="' + cy + '"/>' +
                '<ellipse cx="' + cx + '" cy="' + cy + '" rx="' + (cx - 5) + '" ry="' + (cy - 5) + '" fill="none"/>';
        default: // Rectangle
            return '<rect x="0" y="0" width="' + w + '" height="' + h + '" rx="3" ry="3"/>';
    }
}

// ========== Theme Helper (CSS Variables) ==========
function _bdCssVar(prop) {
    return getComputedStyle(document.body).getPropertyValue(prop).trim();
}

function _bdTheme() {
    var bgColor = _bdCssVar('--bg-color') || '#1E1E1E';
    var textColor = _bdCssVar('--node-text') || '#D4D4D4';
    var borderColor = _bdCssVar('--node-stroke') || '#3E3E42';
    var nodeFill = _bdCssVar('--node-fill') || '#252526';
    var selectedStroke = _bdCssVar('--node-selected-stroke') || '#007ACC';
    var toolbarBg = _bdCssVar('--toolbar-bg') || '#2D2D30';
    var edgeColor = _bdCssVar('--edge-color') || '#6A6A6A';
    var subgraphFill = _bdCssVar('--subgraph-fill') || 'rgba(62, 62, 66, 0.3)';
    var subgraphStroke = _bdCssVar('--subgraph-stroke') || '#3E3E42';
    var subgraphText = _bdCssVar('--subgraph-text') || '#858585';
    var isLight = document.body.classList.contains('theme-light');

    return {
        bg: bgColor, text: textColor, border: borderColor,
        nodeFill: nodeFill, selected: selectedStroke,
        selectedBg: isLight ? 'rgba(0,122,204,0.12)' : 'rgba(0,122,204,0.15)',
        toolbarBg: toolbarBg, edgeColor: edgeColor,
        subgraphFill: subgraphFill, subgraphStroke: subgraphStroke,
        subgraphText: subgraphText,
        spaceBg: isLight ? 'rgba(0,0,0,0.05)' : 'rgba(255,255,255,0.05)',
        mutedText: isLight ? '#6c757d' : subgraphText,
        dangerColor: isLight ? '#dc3545' : '#f38ba8',
        isLight: isLight,
        arrowBg: isLight ? '#e9ecef' : 'rgba(255,255,255,0.08)'
    };
}

// ========== HTML escaping ==========
function _bdEsc(str) {
    if (!str) return '';
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ========== Rendering ==========

function renderBlockDiagram() {
    var canvas = document.getElementById('editorCanvas');
    if (!canvas || !bdModel) return;

    // Show editorCanvas, hide diagram-svg
    var diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';

    canvas.innerHTML = '';

    var c = _bdTheme();

    canvas.style.background = c.bg;

    var wrapper = document.createElement('div');
    wrapper.id = 'bd-wrapper';
    wrapper.style.cssText = 'padding:20px;padding-top:48px;font-family:\'Segoe UI\',Tahoma,Geneva,Verdana,sans-serif;color:' + c.text + ';min-height:100%;';

    // Diagram area
    var diagramArea = document.createElement('div');
    diagramArea.id = 'bd-diagram';

    // Render top-level grid
    diagramArea.appendChild(bdRenderItemsGrid(bdModel.items || [], bdModel.columns || 1, null, c));

    // Render edges section
    if (bdModel.edges && bdModel.edges.length > 0) {
        var edgesSection = document.createElement('div');
        edgesSection.style.cssText = 'margin-top:16px;padding:12px;border:1px dashed ' + c.border + ';border-radius:6px;';
        var edgesTitle = document.createElement('div');
        edgesTitle.textContent = 'Connections';
        edgesTitle.style.cssText = 'font-size:12px;font-weight:600;color:' + c.mutedText + ';margin-bottom:8px;text-transform:uppercase;letter-spacing:0.5px;';
        edgesSection.appendChild(edgesTitle);

        bdModel.edges.forEach(function(edge, idx) {
            var edgeEl = document.createElement('div');
            var isEdgeSel = bdSelectedEdge === idx;
            edgeEl.style.cssText = 'display:inline-flex;align-items:center;gap:6px;padding:4px 10px;margin:3px;border-radius:4px;font-size:13px;cursor:pointer;border:1px solid ' + (isEdgeSel ? c.selected : c.border) + ';background:' + (isEdgeSel ? c.selectedBg : 'transparent') + ';';

            var fromSpan = document.createElement('span');
            fromSpan.style.fontWeight = '600';
            fromSpan.textContent = bdGetDisplayLabel(edge.fromId);
            edgeEl.appendChild(fromSpan);

            var styleSpan = document.createElement('span');
            styleSpan.style.color = c.selected;
            styleSpan.textContent = edge.style || '-->';
            edgeEl.appendChild(styleSpan);

            if (edge.label) {
                var labelSpan = document.createElement('span');
                labelSpan.style.cssText = 'font-style:italic;color:' + c.mutedText;
                labelSpan.textContent = '"' + edge.label + '"';
                edgeEl.appendChild(labelSpan);
            }

            var toSpan = document.createElement('span');
            toSpan.style.fontWeight = '600';
            toSpan.textContent = bdGetDisplayLabel(edge.toId);
            edgeEl.appendChild(toSpan);

            edgeEl.addEventListener('click', function(e) { e.stopPropagation(); bdSelectEdge(idx); });
            edgeEl.addEventListener('contextmenu', function(e) { e.preventDefault(); e.stopPropagation(); bdShowEdgeContextMenu(e, idx, c); });
            edgeEl.addEventListener('dblclick', function(e) { e.stopPropagation(); bdShowEdgeDialog(idx); });
            edgesSection.appendChild(edgeEl);
        });
        diagramArea.appendChild(edgesSection);
    }

    wrapper.appendChild(diagramArea);

    // Click on empty space deselects
    wrapper.addEventListener('click', function() {
        bdSelectedItem = null;
        bdSelectedEdge = null;
        var pp = document.getElementById('property-panel');
        if (pp) pp.classList.remove('visible');
        renderBlockDiagram();
    });

    canvas.appendChild(wrapper);

    if (typeof window.updateVisualEditorMinimap === 'function') {
        window.updateVisualEditorMinimap();
    }
}

function bdRenderItemsGrid(items, columns, groupId, c) {
    var grid = document.createElement('div');
    grid.style.cssText = 'display:grid;grid-template-columns:repeat(' + columns + ', 1fr);gap:8px;';

    items.forEach(function(item, idx) {
        var el = bdRenderItem(item, idx, groupId, c);
        if (el) {
            var span = item.width || 1;
            if (span > 1) el.style.gridColumn = 'span ' + span;
            grid.appendChild(el);
        }
    });

    return grid;
}

function bdRenderItem(item, index, groupId, c) {
    if (!item) return null;
    switch (item.type) {
        case 'block': return bdRenderBlock(item, index, groupId, c);
        case 'space': return bdRenderSpace(item, index, groupId, c);
        case 'arrow': return bdRenderArrowBlock(item, index, groupId, c);
        case 'group': return bdRenderGroup(item, index, groupId, c);
        default: return null;
    }
}

function bdRenderBlock(block, index, groupId, c) {
    var isSelected = bdSelectedItem && bdSelectedItem.type === 'block' && bdSelectedItem.id === block.id;
    var el = document.createElement('div');
    el.style.cssText = 'position:relative;text-align:center;cursor:pointer;';

    var shape = block.shape || 'Rectangle';
    var label = block.label || block.id || '';
    var w = 140, h = 50;

    var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', '100%');
    svg.setAttribute('viewBox', '0 0 ' + w + ' ' + h);
    svg.style.cssText = 'max-width:' + w + 'px;display:block;margin:0 auto;';

    var g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    g.innerHTML = bdGetShapeSVGPath(shape, w, h);
    var paths = g.querySelectorAll('rect, polygon, ellipse, path, circle');
    paths.forEach(function(p) {
        p.setAttribute('fill', isSelected ? c.selectedBg : c.nodeFill);
        p.setAttribute('stroke', isSelected ? c.selected : c.border);
        p.setAttribute('stroke-width', isSelected ? '2.5' : '1.5');
    });
    var lines = g.querySelectorAll('line');
    lines.forEach(function(l) { l.setAttribute('stroke', isSelected ? c.selected : c.border); l.setAttribute('stroke-width', '1.5'); });
    svg.appendChild(g);

    var text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    text.setAttribute('x', w / 2);
    text.setAttribute('y', h / 2);
    text.setAttribute('text-anchor', 'middle');
    text.setAttribute('dominant-baseline', 'central');
    text.setAttribute('fill', c.text);
    text.setAttribute('font-size', '13');
    text.setAttribute('font-family', "'Segoe UI',Tahoma,Geneva,Verdana,sans-serif");
    text.textContent = label.length > 18 ? label.substring(0, 16) + '...' : label;
    svg.appendChild(text);

    el.appendChild(svg);

    var idBadge = document.createElement('div');
    idBadge.textContent = block.id;
    idBadge.style.cssText = 'font-size:10px;color:' + c.mutedText + ';margin-top:2px;';
    el.appendChild(idBadge);

    el.addEventListener('click', function(e) {
        e.stopPropagation();
        bdSelectedItem = { type: 'block', id: block.id, groupId: groupId, index: index };
        bdSelectedEdge = null;
        renderBlockDiagram();
    });
    el.addEventListener('dblclick', function(e) { e.stopPropagation(); bdShowBlockDialog(block); });
    el.addEventListener('contextmenu', function(e) { e.preventDefault(); e.stopPropagation(); bdShowBlockContextMenu(e, block, index, groupId, c); });

    return el;
}

function bdRenderSpace(space, index, groupId, c) {
    var isSelected = bdSelectedItem && bdSelectedItem.type === 'space' && bdSelectedItem.index === index && bdSelectedItem.groupId === groupId;
    var el = document.createElement('div');
    el.style.cssText = 'min-height:40px;display:flex;align-items:center;justify-content:center;border:1px dashed ' + (isSelected ? c.selected : c.border) + ';border-radius:4px;background:' + (isSelected ? c.selectedBg : c.spaceBg) + ';cursor:pointer;font-size:11px;color:' + c.mutedText + ';';
    el.textContent = space.width > 1 ? 'space:' + space.width : 'space';
    el.addEventListener('click', function(e) {
        e.stopPropagation();
        bdSelectedItem = { type: 'space', index: index, groupId: groupId };
        bdSelectedEdge = null;
        renderBlockDiagram();
    });
    el.addEventListener('contextmenu', function(e) { e.preventDefault(); e.stopPropagation(); bdShowSpaceContextMenu(e, index, groupId, c); });
    return el;
}

function bdRenderArrowBlock(arrow, index, groupId, c) {
    var isSelected = bdSelectedItem && bdSelectedItem.type === 'arrow' && bdSelectedItem.id === arrow.id;
    var el = document.createElement('div');
    el.style.cssText = 'text-align:center;cursor:pointer;padding:6px;';

    var dirSymbols = { down: '\u25BC', up: '\u25B2', left: '\u25C4', right: '\u25BA', x: '\u2715', y: '\u2715' };
    var symbol = dirSymbols[arrow.direction] || '\u25BC';

    var arrowEl = document.createElement('div');
    arrowEl.style.cssText = 'display:inline-flex;align-items:center;gap:6px;padding:6px 14px;border-radius:6px;border:1.5px solid ' + (isSelected ? c.selected : c.border) + ';background:' + (isSelected ? c.selectedBg : c.arrowBg) + ';color:' + c.text + ';font-size:18px;';

    var symbolSpan = document.createElement('span');
    symbolSpan.style.fontSize = '22px';
    symbolSpan.textContent = symbol;
    arrowEl.appendChild(symbolSpan);

    if (arrow.label) {
        var labelSpan = document.createElement('span');
        labelSpan.style.fontSize = '12px';
        // Use textContent to prevent XSS - arrow labels are user input
        labelSpan.textContent = arrow.label;
        arrowEl.appendChild(labelSpan);
    }
    el.appendChild(arrowEl);

    var idBadge = document.createElement('div');
    idBadge.textContent = arrow.id;
    idBadge.style.cssText = 'font-size:10px;color:' + c.mutedText + ';margin-top:2px;';
    el.appendChild(idBadge);

    el.addEventListener('click', function(e) {
        e.stopPropagation();
        bdSelectedItem = { type: 'arrow', id: arrow.id, groupId: groupId, index: index };
        bdSelectedEdge = null;
        renderBlockDiagram();
    });
    el.addEventListener('dblclick', function(e) { e.stopPropagation(); bdShowArrowDialog(arrow); });
    el.addEventListener('contextmenu', function(e) { e.preventDefault(); e.stopPropagation(); bdShowArrowContextMenu(e, arrow, index, groupId, c); });
    return el;
}

function bdRenderGroup(group, index, parentGroupId, c) {
    var isSelected = bdSelectedItem && bdSelectedItem.type === 'group' && bdSelectedItem.id === group.id;
    var el = document.createElement('div');
    el.style.cssText = 'border:2px solid ' + (isSelected ? c.selected : c.subgraphStroke) + ';border-radius:8px;padding:10px;background:' + (isSelected ? c.selectedBg : c.subgraphFill) + ';cursor:default;';

    var header = document.createElement('div');
    header.style.cssText = 'display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;padding-bottom:4px;border-bottom:1px solid ' + c.border + ';';
    var title = document.createElement('span');
    title.textContent = group.label || group.id || 'Group';
    title.style.cssText = 'font-weight:600;font-size:12px;color:' + c.subgraphText + ';';
    header.appendChild(title);
    var colsBadge = document.createElement('span');
    colsBadge.textContent = (group.columns || 1) + ' col' + ((group.columns || 1) > 1 ? 's' : '');
    colsBadge.style.cssText = 'font-size:10px;color:' + c.mutedText + ';background:' + c.toolbarBg + ';padding:1px 6px;border-radius:3px;';
    header.appendChild(colsBadge);
    el.appendChild(header);

    el.appendChild(bdRenderItemsGrid(group.items || [], group.columns || 1, group.id, c));

    el.addEventListener('click', function(e) {
        e.stopPropagation();
        bdSelectedItem = { type: 'group', id: group.id, groupId: parentGroupId, index: index };
        bdSelectedEdge = null;
        renderBlockDiagram();
    });
    el.addEventListener('dblclick', function(e) { e.stopPropagation(); bdShowGroupDialog(group); });
    el.addEventListener('contextmenu', function(e) { e.preventDefault(); e.stopPropagation(); bdShowGroupContextMenu(e, group, index, parentGroupId, c); });
    return el;
}

// ========== Selection ==========

function bdSelectEdge(idx) {
    bdSelectedEdge = idx;
    bdSelectedItem = null;
    renderBlockDiagram();
}

function bdEditSelected() {
    if (bdSelectedItem) {
        if (bdSelectedItem.type === 'block') {
            var block = bdFindItemById(bdSelectedItem.id);
            if (block) bdShowBlockDialog(block);
        } else if (bdSelectedItem.type === 'arrow') {
            var arrow = bdFindItemById(bdSelectedItem.id);
            if (arrow) bdShowArrowDialog(arrow);
        } else if (bdSelectedItem.type === 'group') {
            var group = bdFindItemById(bdSelectedItem.id);
            if (group) bdShowGroupDialog(group);
        } else if (bdSelectedItem.type === 'space') {
            bdShowSpaceWidthDialog(bdSelectedItem.index, bdSelectedItem.groupId);
        }
    } else if (bdSelectedEdge != null) {
        bdShowEdgeDialog(bdSelectedEdge);
    }
}

function bdDeleteSelected() {
    if (bdSelectedItem) {
        if (bdSelectedItem.type === 'space') {
            postMessage({ type: 'bd_blockDeleted', id: '__space__', groupId: bdSelectedItem.groupId || null, index: bdSelectedItem.index });
        } else if (bdSelectedItem.id) {
            postMessage({ type: 'bd_blockDeleted', id: bdSelectedItem.id });
        }
        bdSelectedItem = null;
    } else if (bdSelectedEdge != null) {
        postMessage({ type: 'bd_edgeDeleted', index: bdSelectedEdge });
        bdSelectedEdge = null;
    }
}

// ========== Data Helpers ==========

function bdFindItemById(id) {
    if (!bdModel) return null;
    return bdFindItemByIdRecursive(id, bdModel.items || []);
}

function bdFindItemByIdRecursive(id, items) {
    for (var i = 0; i < items.length; i++) {
        if (items[i].id === id) return items[i];
        if (items[i].type === 'group' && items[i].items) {
            var found = bdFindItemByIdRecursive(id, items[i].items);
            if (found) return found;
        }
    }
    return null;
}

function bdGetItemList(groupId) {
    if (!bdModel) return [];
    if (!groupId) return bdModel.items;
    var group = bdFindItemById(groupId);
    return group && group.items ? group.items : bdModel.items;
}

function bdGetAllBlockIds() {
    if (!bdModel) return [];
    var ids = [];
    bdCollectIds(bdModel.items || [], ids);
    return ids;
}

function bdCollectIds(items, ids) {
    for (var i = 0; i < items.length; i++) {
        if (items[i].id) ids.push(items[i].id);
        if (items[i].type === 'group' && items[i].items) bdCollectIds(items[i].items, ids);
    }
}

// Returns display label for a block/group/arrow ID, e.g. "a (Frontend)"
function bdGetDisplayLabel(id) {
    var item = bdFindItemById(id);
    if (item && item.label && item.label !== id) return id + ' (' + item.label + ')';
    return id;
}

function bdGenerateUniqueId(prefix) {
    var ids = bdGetAllBlockIds();
    var counter = 1;
    while (ids.indexOf(prefix + counter) !== -1) counter++;
    return prefix + counter;
}

// ========== Add Actions ==========

function bdAddSpace() {
    var groupId = bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null;
    postMessage({ type: 'bd_spaceCreated', width: 1, groupId: groupId });
}

// ========== Property Panel Dialogs ==========

function bdShowBlockDialog(block) {
    var isNew = !block.id;
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = isNew ? 'Add Block' : 'Edit Block';
    var body = document.querySelector('.property-panel-body');

    var shapeOpts = '';
    bdShapeOptions.forEach(function(opt) {
        var sel = opt.value === (block.shape || 'Rectangle') ? 'selected' : '';
        shapeOpts += '<option value="' + _bdEsc(opt.value) + '" ' + sel + '>' + _bdEsc(opt.label) + '</option>';
    });

    body.innerHTML =
        '<div class="property-row"><div class="property-label">ID</div>' +
        '<input class="property-input" id="bd-dlg-id" value="' + _bdEsc(block.id || bdGenerateUniqueId('block')) + '" /></div>' +
        '<div class="property-row"><div class="property-label">Label</div>' +
        '<input class="property-input" id="bd-dlg-label" value="' + _bdEsc(block.label || '') + '" placeholder="Block Label" /></div>' +
        '<div class="property-row"><div class="property-label">Shape</div>' +
        '<select class="property-select" id="bd-dlg-shape">' + shapeOpts + '</select></div>' +
        '<div class="property-row"><div class="property-label">Column Span</div>' +
        '<input class="property-input" id="bd-dlg-width" type="number" min="1" max="20" value="' + (block.width || 1) + '" /></div>' +
        '<div class="property-row" style="margin-top:8px;display:flex;gap:6px">' +
        '<button id="bd-dlg-ok" style="flex:1;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">' + (isNew ? 'Add Block' : 'Save') + '</button>' +
        (!isNew ? '<button id="bd-dlg-delete" style="padding:6px 12px;cursor:pointer;background:transparent;color:var(--edge-color);border:1px solid var(--node-stroke);border-radius:4px">Delete</button>' : '') +
        '</div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var id = document.getElementById('bd-dlg-id').value.trim();
        if (!id) return;
        var label = document.getElementById('bd-dlg-label').value.trim();
        var shape = document.getElementById('bd-dlg-shape').value;
        var width = parseInt(document.getElementById('bd-dlg-width').value) || 1;
        if (isNew) {
            var targetGroupId = block.__groupId || (bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null);
            postMessage({ type: 'bd_blockCreated', id: id, label: label || null, shape: shape, width: width, groupId: targetGroupId });
        } else {
            postMessage({ type: 'bd_blockEdited', id: block.id, newId: id !== block.id ? id : undefined, label: label || null, shape: shape, width: width });
        }
        propertyPanel.classList.remove('visible');
    });

    if (!isNew) {
        var delBtn = document.getElementById('bd-dlg-delete');
        if (delBtn) delBtn.addEventListener('click', function() {
            postMessage({ type: 'bd_blockDeleted', id: block.id });
            propertyPanel.classList.remove('visible');
        });
    }

    propertyPanel.classList.add('visible');
    setTimeout(function() { var el = document.getElementById('bd-dlg-label'); if (el) el.select(); }, 50);
}

function bdShowAddBlockDialog() {
    bdShowBlockDialog({});
}

function bdShowArrowDialog(arrow) {
    var isNew = !arrow.id;
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = isNew ? 'Add Arrow Block' : 'Edit Arrow Block';
    var body = document.querySelector('.property-panel-body');

    var dirs = ['down', 'up', 'left', 'right', 'x', 'y'];
    var dirOpts = '';
    dirs.forEach(function(d) {
        var sel = d === (arrow.direction || 'down') ? 'selected' : '';
        dirOpts += '<option value="' + d + '" ' + sel + '>' + d + '</option>';
    });

    body.innerHTML =
        '<div class="property-row"><div class="property-label">ID</div>' +
        '<input class="property-input" id="bd-dlg-id" value="' + _bdEsc(arrow.id || bdGenerateUniqueId('arrow')) + '" /></div>' +
        '<div class="property-row"><div class="property-label">Label</div>' +
        '<input class="property-input" id="bd-dlg-label" value="' + _bdEsc(arrow.label || '') + '" placeholder="Arrow Label" /></div>' +
        '<div class="property-row"><div class="property-label">Direction</div>' +
        '<select class="property-select" id="bd-dlg-direction">' + dirOpts + '</select></div>' +
        '<div class="property-row"><div class="property-label">Column Span</div>' +
        '<input class="property-input" id="bd-dlg-width" type="number" min="1" max="20" value="' + (arrow.width || 1) + '" /></div>' +
        '<div class="property-row" style="margin-top:8px;display:flex;gap:6px">' +
        '<button id="bd-dlg-ok" style="flex:1;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">' + (isNew ? 'Add Arrow' : 'Save') + '</button>' +
        (!isNew ? '<button id="bd-dlg-delete" style="padding:6px 12px;cursor:pointer;background:transparent;color:var(--edge-color);border:1px solid var(--node-stroke);border-radius:4px">Delete</button>' : '') +
        '</div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var id = document.getElementById('bd-dlg-id').value.trim();
        if (!id) return;
        var label = document.getElementById('bd-dlg-label').value.trim();
        var direction = document.getElementById('bd-dlg-direction').value;
        var width = parseInt(document.getElementById('bd-dlg-width').value) || 1;
        if (isNew) {
            postMessage({ type: 'bd_arrowCreated', id: id, label: label || null, direction: direction, width: width, groupId: bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null });
        } else {
            postMessage({ type: 'bd_blockEdited', id: arrow.id, newId: id !== arrow.id ? id : undefined, label: label || null, direction: direction, width: width });
        }
        propertyPanel.classList.remove('visible');
    });

    if (!isNew) {
        var delBtn = document.getElementById('bd-dlg-delete');
        if (delBtn) delBtn.addEventListener('click', function() {
            postMessage({ type: 'bd_blockDeleted', id: arrow.id });
            propertyPanel.classList.remove('visible');
        });
    }

    propertyPanel.classList.add('visible');
    setTimeout(function() { var el = document.getElementById('bd-dlg-label'); if (el) el.select(); }, 50);
}

function bdShowAddArrowDialog() {
    bdShowArrowDialog({});
}

function bdShowGroupDialog(group) {
    var isNew = !group.id;
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = isNew ? 'Add Group' : 'Edit Group';
    var body = document.querySelector('.property-panel-body');

    body.innerHTML =
        '<div class="property-row"><div class="property-label">ID</div>' +
        '<input class="property-input" id="bd-dlg-id" value="' + _bdEsc(group.id || bdGenerateUniqueId('group')) + '" /></div>' +
        '<div class="property-row"><div class="property-label">Label (optional)</div>' +
        '<input class="property-input" id="bd-dlg-label" value="' + _bdEsc(group.label || '') + '" placeholder="Group Label" /></div>' +
        '<div class="property-row"><div class="property-label">Columns</div>' +
        '<input class="property-input" id="bd-dlg-columns" type="number" min="1" max="20" value="' + (group.columns || 1) + '" /></div>' +
        '<div class="property-row"><div class="property-label">Column Span (in parent)</div>' +
        '<input class="property-input" id="bd-dlg-width" type="number" min="1" max="20" value="' + (group.width || 1) + '" /></div>' +
        '<div class="property-row" style="margin-top:8px;display:flex;gap:6px">' +
        '<button id="bd-dlg-ok" style="flex:1;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">' + (isNew ? 'Add Group' : 'Save') + '</button>' +
        (!isNew ? '<button id="bd-dlg-delete" style="padding:6px 12px;cursor:pointer;background:transparent;color:var(--edge-color);border:1px solid var(--node-stroke);border-radius:4px">Delete</button>' : '') +
        '</div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var id = document.getElementById('bd-dlg-id').value.trim();
        if (!id) return;
        var label = document.getElementById('bd-dlg-label').value.trim();
        var columns = parseInt(document.getElementById('bd-dlg-columns').value) || 1;
        var width = parseInt(document.getElementById('bd-dlg-width').value) || 1;
        if (isNew) {
            postMessage({ type: 'bd_groupCreated', id: id, label: label || null, columns: columns, width: width, groupId: bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null });
        } else {
            postMessage({ type: 'bd_blockEdited', id: group.id, newId: id !== group.id ? id : undefined, label: label || null, columns: columns, width: width });
        }
        propertyPanel.classList.remove('visible');
    });

    if (!isNew) {
        var delBtn = document.getElementById('bd-dlg-delete');
        if (delBtn) delBtn.addEventListener('click', function() {
            postMessage({ type: 'bd_blockDeleted', id: group.id });
            propertyPanel.classList.remove('visible');
        });
    }

    propertyPanel.classList.add('visible');
    setTimeout(function() { var el = document.getElementById('bd-dlg-label'); if (el) el.select(); }, 50);
}

function bdShowAddGroupDialog() {
    bdShowGroupDialog({});
}

function bdShowEdgeDialog(edgeIndex) {
    var isNew = edgeIndex === -1;
    var edge = isNew ? { fromId: '', toId: '', label: '', style: '-->' } : (bdModel.edges || [])[edgeIndex];
    if (!edge && !isNew) return;

    var blockIds = bdGetAllBlockIds();
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = isNew ? 'Add Connection' : 'Edit Connection';
    var body = document.querySelector('.property-panel-body');

    var fromOpts = '', toOpts = '';
    blockIds.forEach(function(id) {
        var fSel = id === edge.fromId ? 'selected' : '';
        var tSel = id === edge.toId ? 'selected' : '';
        var displayLabel = _bdEsc(bdGetDisplayLabel(id));
        fromOpts += '<option value="' + _bdEsc(id) + '" ' + fSel + '>' + displayLabel + '</option>';
        toOpts += '<option value="' + _bdEsc(id) + '" ' + tSel + '>' + displayLabel + '</option>';
    });

    // Mermaid block-beta only supports labeled edges with --> style
    var effectiveStyle = edge.label ? '-->' : (edge.style || '-->');

    body.innerHTML =
        '<div class="property-row"><div class="property-label">From Block</div>' +
        '<select class="property-select" id="bd-dlg-from">' + fromOpts + '</select></div>' +
        '<div class="property-row"><div class="property-label">To Block</div>' +
        '<select class="property-select" id="bd-dlg-to">' + toOpts + '</select></div>' +
        '<div class="property-row"><div class="property-label">Label (optional)</div>' +
        '<input class="property-input" id="bd-dlg-label" value="' + _bdEsc(edge.label || '') + '" placeholder="Connection label" /></div>' +
        '<div class="property-row"><div class="property-label">Style</div>' +
        '<select class="property-select" id="bd-dlg-style">' +
        '<option value="-->"' + (effectiveStyle === '-->' ? ' selected' : '') + '>\u2192 Arrow (-->)</option>' +
        '<option value="---"' + (effectiveStyle === '---' ? ' selected' : '') + '>\u2014 Line (---)</option>' +
        '</select></div>' +
        '<div class="property-row" style="margin-top:8px;display:flex;gap:6px">' +
        '<button id="bd-dlg-ok" style="flex:1;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">' + (isNew ? 'Add' : 'Save') + '</button>' +
        (!isNew ? '<button id="bd-dlg-delete" style="padding:6px 12px;cursor:pointer;background:transparent;color:var(--edge-color);border:1px solid var(--node-stroke);border-radius:4px">Delete</button>' : '') +
        '</div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var fromId = document.getElementById('bd-dlg-from').value;
        var toId = document.getElementById('bd-dlg-to').value;
        var label = document.getElementById('bd-dlg-label').value.trim();
        var style = label ? '-->' : document.getElementById('bd-dlg-style').value;
        if (isNew) {
            postMessage({ type: 'bd_edgeCreated', fromId: fromId, toId: toId, label: label || null, style: style });
        } else {
            postMessage({ type: 'bd_edgeEdited', index: edgeIndex, fromId: fromId, toId: toId, label: label || null, style: style });
        }
        propertyPanel.classList.remove('visible');
    });

    if (!isNew) {
        var delBtn = document.getElementById('bd-dlg-delete');
        if (delBtn) delBtn.addEventListener('click', function() {
            postMessage({ type: 'bd_edgeDeleted', index: edgeIndex });
            bdSelectedEdge = null;
            propertyPanel.classList.remove('visible');
        });
    }

    propertyPanel.classList.add('visible');
}

function bdShowAddEdgeDialog() {
    bdShowEdgeDialog(-1);
}

function bdShowSettingsDialog() {
    if (!bdModel) return;
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Diagram Settings';
    var body = document.querySelector('.property-panel-body');

    body.innerHTML =
        '<div class="property-row"><div class="property-label">Columns</div>' +
        '<input class="property-input" id="bd-dlg-columns" type="number" min="1" max="20" value="' + (bdModel.columns || 1) + '" /></div>' +
        '<div class="property-row" style="margin-top:8px">' +
        '<button id="bd-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button></div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var columns = parseInt(document.getElementById('bd-dlg-columns').value) || 1;
        postMessage({ type: 'bd_settingsChanged', columns: columns });
        propertyPanel.classList.remove('visible');
    });

    propertyPanel.classList.add('visible');
}

function bdShowSpaceWidthDialog(index, groupId) {
    var items = bdGetItemList(groupId);
    var space = items && items[index];
    if (!space) return;
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Edit Space';
    var body = document.querySelector('.property-panel-body');

    body.innerHTML =
        '<div class="property-row"><div class="property-label">Column Span</div>' +
        '<input class="property-input" id="bd-dlg-width" type="number" min="1" max="20" value="' + (space.width || 1) + '" /></div>' +
        '<div class="property-row" style="margin-top:8px">' +
        '<button id="bd-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button></div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var width = parseInt(document.getElementById('bd-dlg-width').value) || 1;
        postMessage({ type: 'bd_spaceEdited', index: index, groupId: groupId, width: width });
        propertyPanel.classList.remove('visible');
    });

    propertyPanel.classList.add('visible');
}

function bdShowChangeShapeDialog(block) {
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Change Shape';
    var body = document.querySelector('.property-panel-body');

    var shapeOpts = '';
    bdShapeOptions.forEach(function(opt) {
        var sel = opt.value === (block.shape || 'Rectangle') ? 'selected' : '';
        shapeOpts += '<option value="' + _bdEsc(opt.value) + '" ' + sel + '>' + _bdEsc(opt.label) + '</option>';
    });

    body.innerHTML =
        '<div class="property-row"><div class="property-label">Shape</div>' +
        '<select class="property-select" id="bd-dlg-shape">' + shapeOpts + '</select></div>' +
        '<div class="property-row" style="margin-top:8px">' +
        '<button id="bd-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button></div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        postMessage({ type: 'bd_blockEdited', id: block.id, shape: document.getElementById('bd-dlg-shape').value });
        propertyPanel.classList.remove('visible');
    });

    propertyPanel.classList.add('visible');
}

function bdShowEdgeDialogFrom(fromId) {
    var blockIds = bdGetAllBlockIds().filter(function(id) { return id !== fromId; });
    if (blockIds.length === 0) return;
    var propertyPanel = document.getElementById('property-panel');
    var propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Add Connection';
    var body = document.querySelector('.property-panel-body');

    var toOpts = '';
    blockIds.forEach(function(id) {
        toOpts += '<option value="' + _bdEsc(id) + '">' + _bdEsc(bdGetDisplayLabel(id)) + '</option>';
    });

    body.innerHTML =
        '<div class="property-row"><div class="property-label">From: ' + _bdEsc(fromId) + '</div></div>' +
        '<div class="property-row"><div class="property-label">To Block</div>' +
        '<select class="property-select" id="bd-dlg-to">' + toOpts + '</select></div>' +
        '<div class="property-row"><div class="property-label">Label (optional)</div>' +
        '<input class="property-input" id="bd-dlg-label" value="" placeholder="Connection label" /></div>' +
        '<div class="property-row"><div class="property-label">Style</div>' +
        '<select class="property-select" id="bd-dlg-style">' +
        '<option value="-->" selected>\u2192 Arrow (-->)</option>' +
        '<option value="---">\u2014 Line (---)</option></select></div>' +
        '<div class="property-row" style="margin-top:8px">' +
        '<button id="bd-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add Connection</button></div>';

    document.getElementById('bd-dlg-ok').addEventListener('click', function() {
        var toId = document.getElementById('bd-dlg-to').value;
        var label = document.getElementById('bd-dlg-label').value.trim();
        var style = label ? '-->' : document.getElementById('bd-dlg-style').value;
        postMessage({ type: 'bd_edgeCreated', fromId: fromId, toId: toId, label: label || null, style: style });
        propertyPanel.classList.remove('visible');
    });

    propertyPanel.classList.add('visible');
}

// ========== Context Menus ==========

function bdShowContextMenu(e, items, c) {
    bdCloseAllContextMenus();
    var menu = document.createElement('div');
    menu.className = 'bd-context-menu';
    menu.style.cssText = 'position:fixed;top:' + e.clientY + 'px;left:' + e.clientX + 'px;background:var(--context-menu-bg);border:1px solid var(--context-menu-border);border-radius:6px;padding:4px 0;z-index:10001;box-shadow:0 4px 16px rgba(0,0,0,0.25);min-width:160px;';

    items.forEach(function(item) {
        if (item.sep) {
            var sep = document.createElement('div');
            sep.style.cssText = 'height:1px;background:var(--context-menu-border);margin:4px 8px;';
            menu.appendChild(sep);
            return;
        }
        var menuItem = document.createElement('div');
        menuItem.textContent = item.label;
        menuItem.style.cssText = 'padding:6px 14px;cursor:pointer;font-size:13px;color:' + (item.danger ? c.dangerColor : 'var(--context-menu-text)') + ';';
        menuItem.addEventListener('mouseenter', function() { menuItem.style.background = 'var(--context-menu-hover)'; });
        menuItem.addEventListener('mouseleave', function() { menuItem.style.background = 'transparent'; });
        menuItem.addEventListener('click', function(ev) { ev.stopPropagation(); menu.remove(); item.action(); });
        menu.appendChild(menuItem);
    });

    document.body.appendChild(menu);
    var closeHandler = function() { menu.remove(); document.removeEventListener('click', closeHandler); };
    setTimeout(function() { document.addEventListener('click', closeHandler); }, 10);
}

function bdShowBlockContextMenu(e, block, index, groupId, c) {
    bdShowContextMenu(e, [
        { label: 'Edit Block...', action: function() { bdShowBlockDialog(block); } },
        { label: 'Change Shape...', action: function() { bdShowChangeShapeDialog(block); } },
        { sep: true },
        { label: 'Add Block Before', action: function() { bdShowBlockDialog({ __insertBefore: index, __groupId: groupId }); } },
        { label: 'Add Block After', action: function() { bdShowBlockDialog({ __insertAfter: index, __groupId: groupId }); } },
        { label: 'Add Connection From...', action: function() { bdShowEdgeDialogFrom(block.id); } },
        { sep: true },
        { label: 'Copy', action: function() { bdCopyItem(block); } },
        { label: 'Delete', action: function() { postMessage({ type: 'bd_blockDeleted', id: block.id }); }, danger: true }
    ], c);
}

function bdShowArrowContextMenu(e, arrow, index, groupId, c) {
    bdShowContextMenu(e, [
        { label: 'Edit Arrow...', action: function() { bdShowArrowDialog(arrow); } },
        { sep: true },
        { label: 'Copy', action: function() { bdCopyItem(arrow); } },
        { label: 'Delete', action: function() { postMessage({ type: 'bd_blockDeleted', id: arrow.id }); }, danger: true }
    ], c);
}

function bdShowGroupContextMenu(e, group, index, parentGroupId, c) {
    bdShowContextMenu(e, [
        { label: 'Edit Group...', action: function() { bdShowGroupDialog(group); } },
        { sep: true },
        { label: 'Add Block Inside', action: function() { bdShowBlockDialog({ __groupId: group.id }); } },
        { label: 'Add Space Inside', action: function() { postMessage({ type: 'bd_spaceCreated', width: 1, groupId: group.id }); } },
        { sep: true },
        { label: 'Delete Group', action: function() { postMessage({ type: 'bd_blockDeleted', id: group.id }); }, danger: true }
    ], c);
}

function bdShowSpaceContextMenu(e, index, groupId, c) {
    bdShowContextMenu(e, [
        { label: 'Set Width...', action: function() { bdShowSpaceWidthDialog(index, groupId); } },
        { label: 'Delete', action: function() {
            postMessage({ type: 'bd_blockDeleted', id: '__space__', groupId: groupId, index: index });
        }, danger: true }
    ], c);
}

function bdShowEdgeContextMenu(e, edgeIndex, c) {
    bdShowContextMenu(e, [
        { label: 'Edit Connection...', action: function() { bdShowEdgeDialog(edgeIndex); } },
        { sep: true },
        { label: 'Delete', action: function() { postMessage({ type: 'bd_edgeDeleted', index: edgeIndex }); bdSelectedEdge = null; }, danger: true }
    ], c);
}

// ========== Copy/Paste ==========

function bdCopyItem(item) {
    if (!item) return;
    bdClipboard = JSON.parse(JSON.stringify(item));
}

function bdPasteItem() {
    if (!bdClipboard) return;
    var item = JSON.parse(JSON.stringify(bdClipboard));
    if (item.id) item.id = bdGenerateUniqueId(item.id.replace(/\d+$/, '') || 'block');
    var groupId = bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null;

    if (item.type === 'block') {
        postMessage({ type: 'bd_blockCreated', id: item.id, label: item.label, shape: item.shape, width: item.width || 1, groupId: groupId });
    } else if (item.type === 'arrow') {
        postMessage({ type: 'bd_arrowCreated', id: item.id, label: item.label, direction: item.direction, width: item.width || 1, groupId: groupId });
    } else if (item.type === 'group') {
        postMessage({ type: 'bd_groupCreated', id: item.id, label: item.label, columns: item.columns || 1, width: item.width || 1, groupId: groupId });
    }
}

// ========== Keyboard Shortcuts ==========

function handleBlockDiagramKeyDown(e) {
    // Rule 1: Guard - only handle when blockDiagram is active
    if (currentDiagramType !== 'blockDiagram') return;
    if (!bdModel) return;
    // Don't handle keys when typing in property panel inputs
    if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT' || e.target.tagName === 'TEXTAREA')) return;

    if (e.key === 'Delete' || e.key === 'Backspace') {
        if (bdSelectedItem || bdSelectedEdge != null) {
            e.preventDefault();
            bdDeleteSelected();
        }
    }
    if ((e.ctrlKey || e.metaKey) && e.key === 'c') {
        if (bdSelectedItem && bdSelectedItem.id) {
            var item = bdFindItemById(bdSelectedItem.id);
            if (item) { e.preventDefault(); bdCopyItem(item); }
        }
    }
    if ((e.ctrlKey || e.metaKey) && e.key === 'v') {
        if (bdClipboard) { e.preventDefault(); bdPasteItem(); }
    }
}

// ========== Utility ==========

function bdCloseAllContextMenus() {
    document.querySelectorAll('.bd-context-menu').forEach(function(el) { el.remove(); });
}

// ========== Entry Points ==========

window.loadBlockDiagram = function (json) {
    try {
        // Rule 19: Set currentDiagramType and call updateToolbarForDiagramType
        currentDiagramType = 'blockDiagram';
        bdModel = typeof json === 'string' ? JSON.parse(json) : json;
    } catch (ex) { bdModel = null; }
    bdSelectedItem = null;
    bdSelectedEdge = null;
    bdClipboard = null;
    editorCanvasZoom = 1;
    updateToolbarForDiagramType();
    renderBlockDiagram();
    document.removeEventListener('keydown', handleBlockDiagramKeyDown);
    document.addEventListener('keydown', handleBlockDiagramKeyDown);

    // Rule 44: Deferred re-render for WebView2 layout settling
    setTimeout(function() { renderBlockDiagram(); }, 150);
};

window.restoreBlockDiagram = function (json) {
    try {
        bdModel = typeof json === 'string' ? JSON.parse(json) : json;
    } catch (ex) { bdModel = null; }
    bdSelectedItem = null;
    bdSelectedEdge = null;
    renderBlockDiagram();
};

window.refreshBlockDiagram = function (json) {
    try {
        bdModel = typeof json === 'string' ? JSON.parse(json) : json;
    } catch (ex) { bdModel = null; }
    renderBlockDiagram();
};

window.getBlockDiagramMinimapData = function () {
    if (!bdModel) return null;
    var items = bdModel.items || [];
    var edges = bdModel.edges || [];
    return {
        type: 'block-diagram',
        itemCount: bdCountItems(items),
        edgeCount: edges.length,
        columns: bdModel.columns || 1
    };
};

function bdCountItems(items) {
    var count = 0;
    for (var i = 0; i < items.length; i++) {
        count++;
        if (items[i].type === 'group' && items[i].items) count += bdCountItems(items[i].items);
    }
    return count;
}
