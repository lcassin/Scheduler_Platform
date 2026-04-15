// ============================================================
// VisualEditor.BlockDiagram.js — Block Diagram Visual Editor
// Renders block-beta diagrams with grid layout, blocks, arrows,
// edges, groups, toolbar, context menus, dialogs, undo/redo,
// minimap, copy/paste, and keyboard shortcuts.
// ============================================================

(function () {
    'use strict';

    let bdModel = null;
    let bdSelectedItem = null; // { type: 'block'|'arrow'|'space'|'group', id, groupId, index }
    let bdSelectedEdge = null; // edge index
    let bdClipboard = null;

    // ========== Theme Colors ==========
    function bdColors() {
        const t = window.currentTheme || 'light';
        if (t === 'dark') return {
            bg: '#1e1e2e', surface: '#2a2a3e', text: '#cdd6f4', textMuted: '#a6adc8',
            border: '#45475a', accent: '#89b4fa', accentHover: '#74c7ec',
            blockBg: '#313244', blockBorder: '#585b70', groupBg: '#1e1e2e',
            groupBorder: '#585b70', arrowBg: '#45475a', arrowText: '#cdd6f4',
            edgeLine: '#a6adc8', selected: '#89b4fa', selectedBg: 'rgba(137,180,250,0.15)',
            spaceBg: 'rgba(88,91,112,0.3)', btnBg: '#313244', btnText: '#cdd6f4',
            btnHover: '#45475a', dialogBg: '#2a2a3e', dialogBorder: '#45475a',
            inputBg: '#313244', inputBorder: '#585b70', inputText: '#cdd6f4',
            dangerBg: '#f38ba8', dangerText: '#1e1e2e'
        };
        if (t === 'twilight') return {
            bg: '#1a1b2e', surface: '#232440', text: '#c8cad8', textMuted: '#8b8da0',
            border: '#3d3e56', accent: '#7c8af5', accentHover: '#6b7bf0',
            blockBg: '#2a2b44', blockBorder: '#4a4b66', groupBg: '#1a1b2e',
            groupBorder: '#4a4b66', arrowBg: '#3d3e56', arrowText: '#c8cad8',
            edgeLine: '#8b8da0', selected: '#7c8af5', selectedBg: 'rgba(124,138,245,0.15)',
            spaceBg: 'rgba(74,75,102,0.3)', btnBg: '#2a2b44', btnText: '#c8cad8',
            btnHover: '#3d3e56', dialogBg: '#232440', dialogBorder: '#3d3e56',
            inputBg: '#2a2b44', inputBorder: '#4a4b66', inputText: '#c8cad8',
            dangerBg: '#e06080', dangerText: '#1a1b2e'
        };
        return {
            bg: '#ffffff', surface: '#f8f9fa', text: '#1a1a2e', textMuted: '#6c757d',
            border: '#dee2e6', accent: '#4a6cf7', accentHover: '#3b5de7',
            blockBg: '#ffffff', blockBorder: '#dee2e6', groupBg: '#f8f9fa',
            groupBorder: '#dee2e6', arrowBg: '#e9ecef', arrowText: '#495057',
            edgeLine: '#6c757d', selected: '#4a6cf7', selectedBg: 'rgba(74,108,247,0.1)',
            spaceBg: 'rgba(108,117,125,0.1)', btnBg: '#ffffff', btnText: '#1a1a2e',
            btnHover: '#f0f0f0', dialogBg: '#ffffff', dialogBorder: '#dee2e6',
            inputBg: '#ffffff', inputBorder: '#ced4da', inputText: '#495057',
            dangerBg: '#dc3545', dangerText: '#ffffff'
        };
    }

    // ========== Shape Rendering ==========
    const shapeOptions = [
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

    function getShapeSVGPath(shape, w, h) {
        const cx = w / 2, cy = h / 2;
        switch (shape) {
            case 'Rounded':
                return `<rect x="0" y="0" width="${w}" height="${h}" rx="12" ry="12"/>`;
            case 'Stadium':
                return `<rect x="0" y="0" width="${w}" height="${h}" rx="${h / 2}" ry="${h / 2}"/>`;
            case 'Subroutine': {
                const inset = 8;
                return `<rect x="0" y="0" width="${w}" height="${h}" rx="2" ry="2"/>` +
                    `<line x1="${inset}" y1="0" x2="${inset}" y2="${h}"/>` +
                    `<line x1="${w - inset}" y1="0" x2="${w - inset}" y2="${h}"/>`;
            }
            case 'Cylinder':
                return `<path d="M0,${h * 0.15} C0,0 ${w},0 ${w},${h * 0.15} L${w},${h * 0.85} C${w},${h} 0,${h} 0,${h * 0.85} Z"/>` +
                    `<path d="M0,${h * 0.15} C0,${h * 0.3} ${w},${h * 0.3} ${w},${h * 0.15}" fill="none"/>`;
            case 'Circle':
                return `<ellipse cx="${cx}" cy="${cy}" rx="${cx}" ry="${cy}"/>`;
            case 'Rhombus':
                return `<polygon points="${cx},0 ${w},${cy} ${cx},${h} 0,${cy}"/>`;
            case 'Hexagon': {
                const inset = w * 0.15;
                return `<polygon points="${inset},0 ${w - inset},0 ${w},${cy} ${w - inset},${h} ${inset},${h} 0,${cy}"/>`;
            }
            case 'Asymmetric':
                return `<polygon points="0,0 ${w - 15},0 ${w},${cy} ${w - 15},${h} 0,${h}"/>`;
            case 'Parallelogram': {
                const sk = w * 0.15;
                return `<polygon points="${sk},0 ${w},0 ${w - sk},${h} 0,${h}"/>`;
            }
            case 'ParallelogramAlt': {
                const sk = w * 0.15;
                return `<polygon points="0,0 ${w - sk},0 ${w},${h} ${sk},${h}"/>`;
            }
            case 'Trapezoid': {
                const sk = w * 0.15;
                return `<polygon points="${sk},0 ${w - sk},0 ${w},${h} 0,${h}"/>`;
            }
            case 'TrapezoidAlt': {
                const sk = w * 0.15;
                return `<polygon points="0,0 ${w},0 ${w - sk},${h} ${sk},${h}"/>`;
            }
            case 'DoubleCircle':
                return `<ellipse cx="${cx}" cy="${cy}" rx="${cx}" ry="${cy}"/>` +
                    `<ellipse cx="${cx}" cy="${cy}" rx="${cx - 5}" ry="${cy - 5}" fill="none"/>`;
            default: // Rectangle
                return `<rect x="0" y="0" width="${w}" height="${h}" rx="3" ry="3"/>`;
        }
    }

    // ========== Rendering ==========

    function renderBlockDiagram() {
        const container = document.getElementById('visual-editor-container');
        if (!container || !bdModel) return;
        container.innerHTML = '';

        const c = bdColors();
        const wrapper = document.createElement('div');
        wrapper.id = 'bd-wrapper';
        wrapper.style.cssText = `padding:20px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:${c.text};min-height:100%;`;

        // Toolbar
        wrapper.appendChild(renderToolbar(c));

        // Diagram area
        const diagramArea = document.createElement('div');
        diagramArea.id = 'bd-diagram';
        diagramArea.style.cssText = `margin-top:12px;`;

        // Render top-level grid
        diagramArea.appendChild(renderItemsGrid(bdModel.items || [], bdModel.columns || 1, null, c));

        // Render edges section
        if (bdModel.edges && bdModel.edges.length > 0) {
            const edgesSection = document.createElement('div');
            edgesSection.style.cssText = `margin-top:16px;padding:12px;border:1px dashed ${c.border};border-radius:6px;`;
            const edgesTitle = document.createElement('div');
            edgesTitle.textContent = 'Connections';
            edgesTitle.style.cssText = `font-size:12px;font-weight:600;color:${c.textMuted};margin-bottom:8px;text-transform:uppercase;letter-spacing:0.5px;`;
            edgesSection.appendChild(edgesTitle);

            bdModel.edges.forEach((edge, idx) => {
                const edgeEl = document.createElement('div');
                const isSelected = bdSelectedEdge === idx;
                edgeEl.style.cssText = `display:inline-flex;align-items:center;gap:6px;padding:4px 10px;margin:3px;border-radius:4px;font-size:13px;cursor:pointer;border:1px solid ${isSelected ? c.selected : c.border};background:${isSelected ? c.selectedBg : 'transparent'};`;
                edgeEl.innerHTML = `<span style="font-weight:600">${esc(edge.fromId)}</span>` +
                    `<span style="color:${c.accent}">${esc(edge.style || '-->')}</span>` +
                    (edge.label ? `<span style="font-style:italic;color:${c.textMuted}">"${esc(edge.label)}"</span>` : '') +
                    `<span style="font-weight:600">${esc(edge.toId)}</span>`;
                edgeEl.addEventListener('click', (e) => { e.stopPropagation(); selectEdge(idx); });
                edgeEl.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showEdgeContextMenu(e, idx, c); });
                edgeEl.addEventListener('dblclick', (e) => { e.stopPropagation(); showEdgeDialog(idx, c); });
                edgesSection.appendChild(edgeEl);
            });
            diagramArea.appendChild(edgesSection);
        }

        wrapper.appendChild(diagramArea);

        // Click on empty space deselects
        wrapper.addEventListener('click', () => { bdSelectedItem = null; bdSelectedEdge = null; renderBlockDiagram(); });

        container.appendChild(wrapper);
        updateMinimap();
    }

    function renderItemsGrid(items, columns, groupId, c) {
        const grid = document.createElement('div');
        grid.style.cssText = `display:grid;grid-template-columns:repeat(${columns}, 1fr);gap:8px;`;

        items.forEach((item, idx) => {
            const el = renderItem(item, idx, groupId, c);
            if (el) {
                // Handle column spanning
                const span = item.width || 1;
                if (span > 1) el.style.gridColumn = `span ${span}`;
                grid.appendChild(el);
            }
        });

        return grid;
    }

    function renderItem(item, index, groupId, c) {
        if (!item) return null;
        switch (item.type) {
            case 'block': return renderBlock(item, index, groupId, c);
            case 'space': return renderSpace(item, index, groupId, c);
            case 'arrow': return renderArrowBlock(item, index, groupId, c);
            case 'group': return renderGroup(item, index, groupId, c);
            default: return null;
        }
    }

    function renderBlock(block, index, groupId, c) {
        const isSelected = bdSelectedItem && bdSelectedItem.type === 'block' && bdSelectedItem.id === block.id;
        const el = document.createElement('div');
        el.style.cssText = `position:relative;text-align:center;cursor:pointer;`;

        const shape = block.shape || 'Rectangle';
        const label = block.label || block.id || '';
        const w = 140, h = 50;

        // SVG shape
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('width', '100%');
        svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
        svg.style.cssText = `max-width:${w}px;display:block;margin:0 auto;`;

        const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        g.innerHTML = getShapeSVGPath(shape, w, h);
        const paths = g.querySelectorAll('rect, polygon, ellipse, path, circle');
        paths.forEach(p => {
            p.setAttribute('fill', isSelected ? c.selectedBg : c.blockBg);
            p.setAttribute('stroke', isSelected ? c.selected : c.blockBorder);
            p.setAttribute('stroke-width', isSelected ? '2.5' : '1.5');
        });
        // For subroutine lines and cylinder overlay
        const lines = g.querySelectorAll('line');
        lines.forEach(l => { l.setAttribute('stroke', isSelected ? c.selected : c.blockBorder); l.setAttribute('stroke-width', '1.5'); });
        svg.appendChild(g);

        // Label text
        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', w / 2);
        text.setAttribute('y', h / 2);
        text.setAttribute('text-anchor', 'middle');
        text.setAttribute('dominant-baseline', 'central');
        text.setAttribute('fill', c.text);
        text.setAttribute('font-size', '13');
        text.setAttribute('font-family', '-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif');
        text.textContent = label.length > 18 ? label.substring(0, 16) + '...' : label;
        svg.appendChild(text);

        el.appendChild(svg);

        // ID badge
        const idBadge = document.createElement('div');
        idBadge.textContent = block.id;
        idBadge.style.cssText = `font-size:10px;color:${c.textMuted};margin-top:2px;`;
        el.appendChild(idBadge);

        el.addEventListener('click', (e) => {
            e.stopPropagation();
            bdSelectedItem = { type: 'block', id: block.id, groupId, index };
            bdSelectedEdge = null;
            renderBlockDiagram();
        });
        el.addEventListener('dblclick', (e) => { e.stopPropagation(); showBlockDialog(block, c); });
        el.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showBlockContextMenu(e, block, index, groupId, c); });

        return el;
    }

    function renderSpace(space, index, groupId, c) {
        const isSelected = bdSelectedItem && bdSelectedItem.type === 'space' && bdSelectedItem.index === index && bdSelectedItem.groupId === groupId;
        const el = document.createElement('div');
        el.style.cssText = `min-height:40px;display:flex;align-items:center;justify-content:center;border:1px dashed ${isSelected ? c.selected : c.border};border-radius:4px;background:${isSelected ? c.selectedBg : c.spaceBg};cursor:pointer;font-size:11px;color:${c.textMuted};`;
        el.textContent = space.width > 1 ? `space:${space.width}` : 'space';
        el.addEventListener('click', (e) => {
            e.stopPropagation();
            bdSelectedItem = { type: 'space', index, groupId };
            bdSelectedEdge = null;
            renderBlockDiagram();
        });
        el.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showSpaceContextMenu(e, index, groupId, c); });
        return el;
    }

    function renderArrowBlock(arrow, index, groupId, c) {
        const isSelected = bdSelectedItem && bdSelectedItem.type === 'arrow' && bdSelectedItem.id === arrow.id;
        const el = document.createElement('div');
        el.style.cssText = `text-align:center;cursor:pointer;padding:6px;`;

        const dirSymbols = { down: '▼', up: '▲', left: '◄', right: '►', x: '✕', y: '✕' };
        const symbol = dirSymbols[arrow.direction] || '▼';

        const arrowEl = document.createElement('div');
        arrowEl.style.cssText = `display:inline-flex;align-items:center;gap:6px;padding:6px 14px;border-radius:6px;border:1.5px solid ${isSelected ? c.selected : c.arrowBg};background:${isSelected ? c.selectedBg : c.arrowBg};color:${c.arrowText};font-size:18px;`;
        arrowEl.innerHTML = `<span style="font-size:22px">${symbol}</span>`;
        if (arrow.label) arrowEl.innerHTML += `<span style="font-size:12px">${esc(arrow.label)}</span>`;
        el.appendChild(arrowEl);

        const idBadge = document.createElement('div');
        idBadge.textContent = arrow.id;
        idBadge.style.cssText = `font-size:10px;color:${c.textMuted};margin-top:2px;`;
        el.appendChild(idBadge);

        el.addEventListener('click', (e) => {
            e.stopPropagation();
            bdSelectedItem = { type: 'arrow', id: arrow.id, groupId, index };
            bdSelectedEdge = null;
            renderBlockDiagram();
        });
        el.addEventListener('dblclick', (e) => { e.stopPropagation(); showArrowDialog(arrow, c); });
        el.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showArrowContextMenu(e, arrow, index, groupId, c); });
        return el;
    }

    function renderGroup(group, index, parentGroupId, c) {
        const isSelected = bdSelectedItem && bdSelectedItem.type === 'group' && bdSelectedItem.id === group.id;
        const el = document.createElement('div');
        el.style.cssText = `border:2px solid ${isSelected ? c.selected : c.groupBorder};border-radius:8px;padding:10px;background:${isSelected ? c.selectedBg : c.groupBg};cursor:default;`;

        // Group header
        const header = document.createElement('div');
        header.style.cssText = `display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;padding-bottom:4px;border-bottom:1px solid ${c.border};`;
        const title = document.createElement('span');
        title.textContent = group.label || group.id || 'Group';
        title.style.cssText = `font-weight:600;font-size:12px;color:${c.textMuted};`;
        header.appendChild(title);
        const colsBadge = document.createElement('span');
        colsBadge.textContent = `${group.columns || 1} col${(group.columns || 1) > 1 ? 's' : ''}`;
        colsBadge.style.cssText = `font-size:10px;color:${c.textMuted};background:${c.surface};padding:1px 6px;border-radius:3px;`;
        header.appendChild(colsBadge);
        el.appendChild(header);

        // Group items grid
        el.appendChild(renderItemsGrid(group.items || [], group.columns || 1, group.id, c));

        el.addEventListener('click', (e) => {
            e.stopPropagation();
            bdSelectedItem = { type: 'group', id: group.id, groupId: parentGroupId, index };
            bdSelectedEdge = null;
            renderBlockDiagram();
        });
        el.addEventListener('dblclick', (e) => { e.stopPropagation(); showGroupDialog(group, c); });
        el.addEventListener('contextmenu', (e) => { e.preventDefault(); e.stopPropagation(); showGroupContextMenu(e, group, index, parentGroupId, c); });
        return el;
    }

    // ========== Toolbar ==========

    function renderToolbar(c) {
        const bar = document.createElement('div');
        bar.style.cssText = `display:flex;flex-wrap:wrap;gap:4px;padding:8px;border:1px solid ${c.border};border-radius:6px;background:${c.surface};`;

        const buttons = [
            { label: '+ Block', action: () => showAddBlockDialog(c) },
            { label: '+ Arrow', action: () => showAddArrowDialog(c) },
            { label: '+ Group', action: () => showAddGroupDialog(c) },
            { label: '+ Space', action: () => addSpace() },
            { label: '+ Edge', action: () => showAddEdgeDialog(c) },
            { sep: true },
            { label: 'Edit', action: () => editSelected(c), disabled: !bdSelectedItem && bdSelectedEdge == null },
            { label: 'Delete', action: () => deleteSelected(), disabled: !bdSelectedItem && bdSelectedEdge == null, danger: true },
            { sep: true },
            { label: 'Settings', action: () => showSettingsDialog(c) },
        ];

        buttons.forEach(btn => {
            if (btn.sep) {
                const sep = document.createElement('div');
                sep.style.cssText = `width:1px;background:${c.border};margin:0 4px;align-self:stretch;`;
                bar.appendChild(sep);
                return;
            }
            const b = document.createElement('button');
            b.textContent = btn.label;
            b.disabled = btn.disabled || false;
            b.style.cssText = `padding:5px 12px;border:1px solid ${btn.danger ? c.dangerBg : c.border};border-radius:4px;background:${btn.danger ? c.dangerBg : c.btnBg};color:${btn.danger ? c.dangerText : c.btnText};font-size:12px;cursor:${btn.disabled ? 'not-allowed' : 'pointer'};opacity:${btn.disabled ? '0.5' : '1'};white-space:nowrap;`;
            if (!btn.disabled) {
                b.addEventListener('mouseenter', () => { b.style.background = btn.danger ? c.accentHover : c.btnHover; });
                b.addEventListener('mouseleave', () => { b.style.background = btn.danger ? c.dangerBg : c.btnBg; });
            }
            b.addEventListener('click', (e) => { e.stopPropagation(); if (!btn.disabled) btn.action(); });
            bar.appendChild(b);
        });

        return bar;
    }

    // ========== Selection ==========

    function selectEdge(idx) {
        bdSelectedEdge = idx;
        bdSelectedItem = null;
        renderBlockDiagram();
    }

    function editSelected(c) {
        if (bdSelectedItem) {
            if (bdSelectedItem.type === 'block') {
                const block = findItemById(bdSelectedItem.id);
                if (block) showBlockDialog(block, c);
            } else if (bdSelectedItem.type === 'arrow') {
                const arrow = findItemById(bdSelectedItem.id);
                if (arrow) showArrowDialog(arrow, c);
            } else if (bdSelectedItem.type === 'group') {
                const group = findItemById(bdSelectedItem.id);
                if (group) showGroupDialog(group, c);
            }
        } else if (bdSelectedEdge != null) {
            showEdgeDialog(bdSelectedEdge, c);
        }
    }

    function deleteSelected() {
        if (bdSelectedItem) {
            if (bdSelectedItem.type === 'space') {
                // Delete space by index
                const items = getItemList(bdSelectedItem.groupId);
                if (items && bdSelectedItem.index >= 0 && bdSelectedItem.index < items.length) {
                    postMessage('bd_blockDeleted', { id: '__space__', groupId: bdSelectedItem.groupId, index: bdSelectedItem.index });
                    items.splice(bdSelectedItem.index, 1);
                    bdSelectedItem = null;
                    renderBlockDiagram();
                    return;
                }
            }
            if (bdSelectedItem.id) {
                postMessage('bd_blockDeleted', { id: bdSelectedItem.id });
            }
            bdSelectedItem = null;
        } else if (bdSelectedEdge != null) {
            postMessage('bd_edgeDeleted', { index: bdSelectedEdge });
            bdSelectedEdge = null;
        }
    }

    function findItemById(id) {
        if (!bdModel || !id) return null;
        return findItemByIdRecursive(id, bdModel.items || []);
    }

    function findItemByIdRecursive(id, items) {
        for (const item of items) {
            if (item.id === id) return item;
            if (item.type === 'group' && item.items) {
                const found = findItemByIdRecursive(id, item.items);
                if (found) return found;
            }
        }
        return null;
    }

    function getItemList(groupId) {
        if (!bdModel) return null;
        if (!groupId) return bdModel.items;
        const group = findItemById(groupId);
        return group && group.items ? group.items : bdModel.items;
    }

    function getAllBlockIds() {
        if (!bdModel) return [];
        const ids = [];
        collectIds(bdModel.items || [], ids);
        return ids;
    }

    function collectIds(items, ids) {
        for (const item of items) {
            if (item.id) ids.push(item.id);
            if (item.type === 'group' && item.items) collectIds(item.items, ids);
        }
    }

    function generateUniqueId(prefix) {
        const ids = getAllBlockIds();
        let counter = 1;
        while (ids.includes(prefix + counter)) counter++;
        return prefix + counter;
    }

    // ========== Add Actions ==========

    function addSpace() {
        const groupId = bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null;
        postMessage('bd_spaceCreated', { width: 1, groupId });
    }

    // ========== Dialogs ==========

    function showDialog(title, fields, onSave, c, onDelete) {
        closeAllDialogs();
        const overlay = document.createElement('div');
        overlay.className = 'bd-dialog-overlay';
        overlay.style.cssText = `position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.4);z-index:10000;display:flex;align-items:center;justify-content:center;`;

        const dialog = document.createElement('div');
        dialog.style.cssText = `background:${c.dialogBg};border:1px solid ${c.dialogBorder};border-radius:10px;padding:20px;min-width:340px;max-width:480px;box-shadow:0 8px 32px rgba(0,0,0,0.3);`;

        const titleEl = document.createElement('div');
        titleEl.textContent = title;
        titleEl.style.cssText = `font-size:16px;font-weight:700;color:${c.text};margin-bottom:16px;`;
        dialog.appendChild(titleEl);

        const inputs = {};
        fields.forEach(field => {
            const row = document.createElement('div');
            row.style.cssText = 'margin-bottom:12px;';
            const lbl = document.createElement('label');
            lbl.textContent = field.label;
            lbl.style.cssText = `display:block;font-size:12px;font-weight:600;color:${c.textMuted};margin-bottom:4px;`;
            row.appendChild(lbl);

            if (field.type === 'select') {
                const sel = document.createElement('select');
                sel.style.cssText = `width:100%;padding:8px;border:1px solid ${c.inputBorder};border-radius:4px;background:${c.inputBg};color:${c.inputText};font-size:13px;`;
                (field.options || []).forEach(opt => {
                    const o = document.createElement('option');
                    o.value = opt.value;
                    o.textContent = opt.label;
                    if (opt.value === field.value) o.selected = true;
                    sel.appendChild(o);
                });
                row.appendChild(sel);
                inputs[field.key] = sel;
            } else if (field.type === 'number') {
                const inp = document.createElement('input');
                inp.type = 'number';
                inp.min = field.min || 1;
                inp.max = field.max || 99;
                inp.value = field.value || 1;
                inp.style.cssText = `width:100%;padding:8px;border:1px solid ${c.inputBorder};border-radius:4px;background:${c.inputBg};color:${c.inputText};font-size:13px;box-sizing:border-box;`;
                row.appendChild(inp);
                inputs[field.key] = inp;
            } else {
                const inp = document.createElement('input');
                inp.type = 'text';
                inp.value = field.value || '';
                inp.placeholder = field.placeholder || '';
                inp.style.cssText = `width:100%;padding:8px;border:1px solid ${c.inputBorder};border-radius:4px;background:${c.inputBg};color:${c.inputText};font-size:13px;box-sizing:border-box;`;
                row.appendChild(inp);
                inputs[field.key] = inp;
            }
            dialog.appendChild(row);
        });

        // Buttons
        const btns = document.createElement('div');
        btns.style.cssText = 'display:flex;justify-content:flex-end;gap:8px;margin-top:16px;';

        if (onDelete) {
            const delBtn = document.createElement('button');
            delBtn.textContent = 'Delete';
            delBtn.style.cssText = `padding:8px 16px;border:none;border-radius:4px;background:${c.dangerBg};color:${c.dangerText};cursor:pointer;font-size:13px;margin-right:auto;`;
            delBtn.addEventListener('click', () => { overlay.remove(); onDelete(); });
            btns.appendChild(delBtn);
        }

        const cancelBtn = document.createElement('button');
        cancelBtn.textContent = 'Cancel';
        cancelBtn.style.cssText = `padding:8px 16px;border:1px solid ${c.border};border-radius:4px;background:transparent;color:${c.text};cursor:pointer;font-size:13px;`;
        cancelBtn.addEventListener('click', () => overlay.remove());
        btns.appendChild(cancelBtn);

        const saveBtn = document.createElement('button');
        saveBtn.textContent = 'Save';
        saveBtn.style.cssText = `padding:8px 16px;border:none;border-radius:4px;background:${c.accent};color:#fff;cursor:pointer;font-size:13px;font-weight:600;`;
        saveBtn.addEventListener('click', () => {
            const values = {};
            Object.keys(inputs).forEach(k => {
                const el = inputs[k];
                values[k] = el.tagName === 'SELECT' ? el.value : el.value;
            });
            overlay.remove();
            onSave(values);
        });
        btns.appendChild(saveBtn);
        dialog.appendChild(btns);

        overlay.appendChild(dialog);
        overlay.addEventListener('click', (e) => { if (e.target === overlay) overlay.remove(); });
        document.body.appendChild(overlay);

        // Focus first input
        const firstInput = dialog.querySelector('input, select');
        if (firstInput) setTimeout(() => firstInput.focus(), 50);

        // Enter key saves
        dialog.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') { saveBtn.click(); }
            if (e.key === 'Escape') { overlay.remove(); }
        });
    }

    function showBlockDialog(block, c) {
        const isNew = !block.id;
        showDialog(isNew ? 'Add Block' : 'Edit Block', [
            { key: 'id', label: 'ID', value: block.id || generateUniqueId('block'), placeholder: 'blockId' },
            { key: 'label', label: 'Label', value: block.label || '', placeholder: 'Block Label' },
            { key: 'shape', label: 'Shape', type: 'select', value: block.shape || 'Rectangle', options: shapeOptions },
            { key: 'width', label: 'Column Span', type: 'number', value: block.width || 1, min: 1, max: 20 }
        ], (values) => {
            if (isNew) {
                postMessage('bd_blockCreated', {
                    id: values.id || generateUniqueId('block'),
                    label: values.label || null,
                    shape: values.shape,
                    width: parseInt(values.width) || 1,
                    groupId: bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null
                });
            } else {
                postMessage('bd_blockEdited', {
                    id: block.id,
                    newId: values.id !== block.id ? values.id : undefined,
                    label: values.label || null,
                    shape: values.shape,
                    width: parseInt(values.width) || 1
                });
            }
        }, c, isNew ? null : () => {
            postMessage('bd_blockDeleted', { id: block.id });
        });
    }

    function showAddBlockDialog(c) {
        showBlockDialog({}, c);
    }

    function showArrowDialog(arrow, c) {
        const isNew = !arrow.id;
        const dirOptions = ['down', 'up', 'left', 'right', 'x', 'y'].map(d => ({ value: d, label: d }));
        showDialog(isNew ? 'Add Arrow Block' : 'Edit Arrow Block', [
            { key: 'id', label: 'ID', value: arrow.id || generateUniqueId('arrow'), placeholder: 'arrowId' },
            { key: 'label', label: 'Label', value: arrow.label || '', placeholder: 'Arrow Label' },
            { key: 'direction', label: 'Direction', type: 'select', value: arrow.direction || 'down', options: dirOptions },
            { key: 'width', label: 'Column Span', type: 'number', value: arrow.width || 1, min: 1, max: 20 }
        ], (values) => {
            if (isNew) {
                postMessage('bd_arrowCreated', {
                    id: values.id || generateUniqueId('arrow'),
                    label: values.label || null,
                    direction: values.direction,
                    width: parseInt(values.width) || 1,
                    groupId: bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null
                });
            } else {
                postMessage('bd_blockEdited', {
                    id: arrow.id,
                    newId: values.id !== arrow.id ? values.id : undefined,
                    label: values.label || null,
                    direction: values.direction,
                    width: parseInt(values.width) || 1
                });
            }
        }, c, isNew ? null : () => {
            postMessage('bd_blockDeleted', { id: arrow.id });
        });
    }

    function showAddArrowDialog(c) {
        showArrowDialog({}, c);
    }

    function showGroupDialog(group, c) {
        const isNew = !group.id;
        showDialog(isNew ? 'Add Group' : 'Edit Group', [
            { key: 'id', label: 'ID', value: group.id || generateUniqueId('group'), placeholder: 'groupId' },
            { key: 'label', label: 'Label (optional)', value: group.label || '', placeholder: 'Group Label' },
            { key: 'columns', label: 'Columns', type: 'number', value: group.columns || 1, min: 1, max: 20 },
            { key: 'width', label: 'Column Span (in parent)', type: 'number', value: group.width || 1, min: 1, max: 20 }
        ], (values) => {
            if (isNew) {
                postMessage('bd_groupCreated', {
                    id: values.id || generateUniqueId('group'),
                    label: values.label || null,
                    columns: parseInt(values.columns) || 1,
                    width: parseInt(values.width) || 1,
                    groupId: bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null
                });
            } else {
                postMessage('bd_blockEdited', {
                    id: group.id,
                    newId: values.id !== group.id ? values.id : undefined,
                    label: values.label || null,
                    columns: parseInt(values.columns) || 1,
                    width: parseInt(values.width) || 1
                });
            }
        }, c, isNew ? null : () => {
            postMessage('bd_blockDeleted', { id: group.id });
        });
    }

    function showAddGroupDialog(c) {
        showGroupDialog({}, c);
    }

    function showEdgeDialog(edgeIndex, c) {
        const isNew = edgeIndex === -1;
        const edge = isNew ? { fromId: '', toId: '', label: '', style: '-->' } : (bdModel.edges || [])[edgeIndex];
        if (!edge && !isNew) return;

        const blockIds = getAllBlockIds();
        const idOptions = blockIds.map(id => ({ value: id, label: id }));
        const styleOptions = [{ value: '-->', label: '--> (arrow)' }, { value: '---', label: '--- (line)' }];

        showDialog(isNew ? 'Add Connection' : 'Edit Connection', [
            { key: 'fromId', label: 'From Block', type: 'select', value: edge.fromId || '', options: idOptions },
            { key: 'toId', label: 'To Block', type: 'select', value: edge.toId || '', options: idOptions },
            { key: 'label', label: 'Label (optional)', value: edge.label || '', placeholder: 'Connection label' },
            { key: 'style', label: 'Style', type: 'select', value: edge.style || '-->', options: styleOptions }
        ], (values) => {
            if (isNew) {
                postMessage('bd_edgeCreated', {
                    fromId: values.fromId,
                    toId: values.toId,
                    label: values.label || null,
                    style: values.style
                });
            } else {
                postMessage('bd_edgeEdited', {
                    index: edgeIndex,
                    fromId: values.fromId,
                    toId: values.toId,
                    label: values.label || null,
                    style: values.style
                });
            }
        }, c, isNew ? null : () => {
            postMessage('bd_edgeDeleted', { index: edgeIndex });
            bdSelectedEdge = null;
        });
    }

    function showAddEdgeDialog(c) {
        showEdgeDialog(-1, c);
    }

    function showSettingsDialog(c) {
        if (!bdModel) return;
        showDialog('Diagram Settings', [
            { key: 'columns', label: 'Columns', type: 'number', value: bdModel.columns || 1, min: 1, max: 20 }
        ], (values) => {
            postMessage('bd_settingsChanged', {
                columns: parseInt(values.columns) || 1
            });
        }, c);
    }

    // ========== Context Menus ==========

    function showContextMenu(e, items, c) {
        closeAllContextMenus();
        const menu = document.createElement('div');
        menu.className = 'bd-context-menu';
        menu.style.cssText = `position:fixed;top:${e.clientY}px;left:${e.clientX}px;background:${c.dialogBg};border:1px solid ${c.dialogBorder};border-radius:6px;padding:4px 0;z-index:10001;box-shadow:0 4px 16px rgba(0,0,0,0.25);min-width:160px;`;

        items.forEach(item => {
            if (item.sep) {
                const sep = document.createElement('div');
                sep.style.cssText = `height:1px;background:${c.border};margin:4px 8px;`;
                menu.appendChild(sep);
                return;
            }
            const menuItem = document.createElement('div');
            menuItem.textContent = item.label;
            menuItem.style.cssText = `padding:6px 14px;cursor:pointer;font-size:13px;color:${item.danger ? c.dangerBg : c.text};`;
            menuItem.addEventListener('mouseenter', () => { menuItem.style.background = c.btnHover; });
            menuItem.addEventListener('mouseleave', () => { menuItem.style.background = 'transparent'; });
            menuItem.addEventListener('click', (ev) => { ev.stopPropagation(); menu.remove(); item.action(); });
            menu.appendChild(menuItem);
        });

        document.body.appendChild(menu);
        const closeHandler = () => { menu.remove(); document.removeEventListener('click', closeHandler); };
        setTimeout(() => document.addEventListener('click', closeHandler), 10);
    }

    function showBlockContextMenu(e, block, index, groupId, c) {
        showContextMenu(e, [
            { label: 'Edit Block...', action: () => showBlockDialog(block, c) },
            { label: 'Change Shape...', action: () => showChangeShapeMenu(block, c) },
            { sep: true },
            { label: 'Add Block Before', action: () => showBlockDialog({ __insertBefore: index, __groupId: groupId }, c) },
            { label: 'Add Block After', action: () => showBlockDialog({ __insertAfter: index, __groupId: groupId }, c) },
            { label: 'Add Connection From...', action: () => showEdgeDialogFrom(block.id, c) },
            { sep: true },
            { label: 'Copy', action: () => copyItem(block) },
            { label: 'Delete', action: () => postMessage('bd_blockDeleted', { id: block.id }), danger: true }
        ], c);
    }

    function showArrowContextMenu(e, arrow, index, groupId, c) {
        showContextMenu(e, [
            { label: 'Edit Arrow...', action: () => showArrowDialog(arrow, c) },
            { sep: true },
            { label: 'Copy', action: () => copyItem(arrow) },
            { label: 'Delete', action: () => postMessage('bd_blockDeleted', { id: arrow.id }), danger: true }
        ], c);
    }

    function showGroupContextMenu(e, group, index, parentGroupId, c) {
        showContextMenu(e, [
            { label: 'Edit Group...', action: () => showGroupDialog(group, c) },
            { sep: true },
            { label: 'Add Block Inside', action: () => { showBlockDialog({ __groupId: group.id }, c); } },
            { label: 'Add Space Inside', action: () => postMessage('bd_spaceCreated', { width: 1, groupId: group.id }) },
            { sep: true },
            { label: 'Delete Group', action: () => postMessage('bd_blockDeleted', { id: group.id }), danger: true }
        ], c);
    }

    function showSpaceContextMenu(e, index, groupId, c) {
        showContextMenu(e, [
            { label: 'Set Width...', action: () => showSpaceWidthDialog(index, groupId, c) },
            { label: 'Delete', action: () => {
                const items = getItemList(groupId);
                if (items && index >= 0 && index < items.length) {
                    // Remove locally and signal
                    postMessage('bd_blockDeleted', { id: '__space__', groupId, index });
                }
            }, danger: true }
        ], c);
    }

    function showEdgeContextMenu(e, edgeIndex, c) {
        showContextMenu(e, [
            { label: 'Edit Connection...', action: () => showEdgeDialog(edgeIndex, c) },
            { sep: true },
            { label: 'Delete', action: () => { postMessage('bd_edgeDeleted', { index: edgeIndex }); bdSelectedEdge = null; }, danger: true }
        ], c);
    }

    function showSpaceWidthDialog(index, groupId, c) {
        const items = getItemList(groupId);
        const space = items && items[index];
        if (!space) return;
        showDialog('Edit Space', [
            { key: 'width', label: 'Column Span', type: 'number', value: space.width || 1, min: 1, max: 20 }
        ], (values) => {
            postMessage('bd_spaceEdited', { index, groupId, width: parseInt(values.width) || 1 });
        }, c);
    }

    function showChangeShapeMenu(block, c) {
        // Quick shape change dialog
        showDialog('Change Shape', [
            { key: 'shape', label: 'Shape', type: 'select', value: block.shape || 'Rectangle', options: shapeOptions }
        ], (values) => {
            postMessage('bd_blockEdited', { id: block.id, shape: values.shape });
        }, c);
    }

    function showEdgeDialogFrom(fromId, c) {
        const blockIds = getAllBlockIds().filter(id => id !== fromId);
        if (blockIds.length === 0) return;
        const idOptions = blockIds.map(id => ({ value: id, label: id }));
        showDialog('Add Connection', [
            { key: 'toId', label: 'To Block', type: 'select', value: blockIds[0], options: idOptions },
            { key: 'label', label: 'Label (optional)', value: '', placeholder: 'Connection label' },
            { key: 'style', label: 'Style', type: 'select', value: '-->', options: [{ value: '-->', label: '--> (arrow)' }, { value: '---', label: '--- (line)' }] }
        ], (values) => {
            postMessage('bd_edgeCreated', {
                fromId: fromId,
                toId: values.toId,
                label: values.label || null,
                style: values.style
            });
        }, c);
    }

    // ========== Copy/Paste ==========

    function copyItem(item) {
        if (!item) return;
        bdClipboard = JSON.parse(JSON.stringify(item));
    }

    function pasteItem() {
        if (!bdClipboard) return;
        const item = JSON.parse(JSON.stringify(bdClipboard));
        // Generate a new unique ID
        if (item.id) item.id = generateUniqueId(item.id.replace(/\d+$/, '') || 'block');
        const groupId = bdSelectedItem && bdSelectedItem.groupId ? bdSelectedItem.groupId : null;

        if (item.type === 'block') {
            postMessage('bd_blockCreated', { id: item.id, label: item.label, shape: item.shape, width: item.width || 1, groupId });
        } else if (item.type === 'arrow') {
            postMessage('bd_arrowCreated', { id: item.id, label: item.label, direction: item.direction, width: item.width || 1, groupId });
        } else if (item.type === 'group') {
            postMessage('bd_groupCreated', { id: item.id, label: item.label, columns: item.columns || 1, width: item.width || 1, groupId });
        }
    }

    // ========== Keyboard Shortcuts ==========

    function handleBlockDiagramKeyDown(e) {
        if (!bdModel) return;
        if (document.querySelector('.bd-dialog-overlay')) return; // dialog is open

        if (e.key === 'Delete' || e.key === 'Backspace') {
            if (bdSelectedItem || bdSelectedEdge != null) {
                e.preventDefault();
                deleteSelected();
            }
        }
        if ((e.ctrlKey || e.metaKey) && e.key === 'c') {
            if (bdSelectedItem && bdSelectedItem.id) {
                const item = findItemById(bdSelectedItem.id);
                if (item) { e.preventDefault(); copyItem(item); }
            }
        }
        if ((e.ctrlKey || e.metaKey) && e.key === 'v') {
            if (bdClipboard) { e.preventDefault(); pasteItem(); }
        }
    }

    // ========== Minimap ==========

    function updateMinimap() {
        if (typeof window.updateVisualEditorMinimap === 'function') {
            window.updateVisualEditorMinimap();
        }
    }

    // ========== Utility ==========

    function esc(text) {
        if (!text) return '';
        return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function postMessage(type, data) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify({ type, ...data }));
        }
    }

    function closeAllDialogs() {
        document.querySelectorAll('.bd-dialog-overlay').forEach(el => el.remove());
    }

    function closeAllContextMenus() {
        document.querySelectorAll('.bd-context-menu').forEach(el => el.remove());
    }

    // ========== Entry Points ==========

    window.loadBlockDiagram = function (json) {
        try {
            bdModel = typeof json === 'string' ? JSON.parse(json) : json;
        } catch { bdModel = null; }
        bdSelectedItem = null;
        bdSelectedEdge = null;
        bdClipboard = null;
        renderBlockDiagram();
        document.removeEventListener('keydown', handleBlockDiagramKeyDown);
        document.addEventListener('keydown', handleBlockDiagramKeyDown);
    };

    window.restoreBlockDiagram = function (json) {
        try {
            bdModel = typeof json === 'string' ? JSON.parse(json) : json;
        } catch { bdModel = null; }
        bdSelectedItem = null;
        bdSelectedEdge = null;
        renderBlockDiagram();
    };

    window.refreshBlockDiagram = function (json) {
        try {
            bdModel = typeof json === 'string' ? JSON.parse(json) : json;
        } catch { bdModel = null; }
        renderBlockDiagram();
    };

    window.getBlockDiagramMinimapData = function () {
        if (!bdModel) return null;
        const items = bdModel.items || [];
        const edges = bdModel.edges || [];
        return {
            type: 'block-diagram',
            itemCount: countItems(items),
            edgeCount: edges.length,
            columns: bdModel.columns || 1
        };
    };

    function countItems(items) {
        let count = 0;
        for (const item of items) {
            count++;
            if (item.type === 'group' && item.items) count += countItems(item.items);
        }
        return count;
    }

})();
