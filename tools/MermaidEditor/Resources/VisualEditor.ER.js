        // ========== ER Diagram Visual Editor ==========

        let erDiagram = null; // When non-null, we're in ER diagram mode
        let erSelectedEntityName = null;
        let erSelectedAttrIndex = -1; // index of selected attribute within erSelectedEntityName
        let erSelectedRelIndex = -1;
        let erDraggingEntityName = null;
        let erDragOffsetX = 0;
        let erDragOffsetY = 0;
        let erDrawingRelFrom = null; // entityName when drawing a relationship
        let erEntityPositions = {}; // { entityName: { x, y, width, height } }

        const ER_BOX_MIN_WIDTH = 160;
        const ER_BOX_PADDING = 10;
        const ER_HEADER_HEIGHT = 28;
        const ER_ATTR_HEIGHT = 22;
        const ER_TOP_MARGIN = 40;
        const ER_LEFT_MARGIN = 60;
        const ER_GAP_X = 240;
        const ER_GAP_Y = 60;

        window.loadERDiagram = function(jsonData) {
            try {
                currentDiagramType = 'er';
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                erDiagram = {
                    entities: (data.entities || []).map(e => ({
                        name: e.name || '',
                        isExplicit: e.isExplicit !== false,
                        attributes: (e.attributes || []).map(a => ({
                            type: a.type || 'string',
                            name: a.name || '',
                            key: a.key || null,
                            comment: a.comment || null
                        })),
                        x: e.x || 0,
                        y: e.y || 0,
                        hasManualPosition: e.hasManualPosition || false
                    })),
                    relationships: (data.relationships || []).map(r => ({
                        fromEntity: r.fromEntity || '',
                        toEntity: r.toEntity || '',
                        leftCardinality: r.leftCardinality || 'ExactlyOne',
                        rightCardinality: r.rightCardinality || 'ExactlyOne',
                        isIdentifying: r.isIdentifying || false,
                        label: r.label || null
                    })),
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Clear other diagram states
                diagram.nodes = [];
                diagram.edges = [];
                diagram.subgraphs = [];
                seqDiagram = null;
                clsDiagram = null;
                stDiagram = null;

                // Hide property panel from previous diagram
                propertyPanel.classList.remove('visible');

                // Hydrate saved positions from C# model (if HasManualPosition)
                if (erDiagram.entities) {
                    erDiagram.entities.forEach(entity => {
                        if (entity.hasManualPosition && (entity.x || entity.y)) {
                            erEntityPositions[entity.name] = {
                                x: entity.x,
                                y: entity.y
                            };
                        }
                    });
                }

                erAutoLayoutEntities();
                updateToolbarForDiagramType();
                renderERDiagram();
                centerView();
            } catch (err) {
                console.error('Failed to load ER diagram:', err);
            }
        };

        // Refresh ER diagram data without resetting view (called after C# model changes)
        window.refreshERDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                erDiagram = {
                    entities: (data.entities || []).map(e => ({
                        name: e.name || '',
                        isExplicit: e.isExplicit !== false,
                        attributes: (e.attributes || []).map(a => ({
                            type: a.type || 'string',
                            name: a.name || '',
                            key: a.key || null,
                            comment: a.comment || null
                        })),
                        x: e.x || 0,
                        y: e.y || 0,
                        hasManualPosition: e.hasManualPosition || false
                    })),
                    relationships: (data.relationships || []).map(r => ({
                        fromEntity: r.fromEntity || '',
                        toEntity: r.toEntity || '',
                        leftCardinality: r.leftCardinality || 'ExactlyOne',
                        rightCardinality: r.rightCardinality || 'ExactlyOne',
                        isIdentifying: r.isIdentifying || false,
                        label: r.label || null
                    })),
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };
                // Auto-layout only new entities that don't have positions yet
                erAutoLayoutEntities();
                renderERDiagram();
            } catch (err) {
                console.error('Failed to refresh ER diagram:', err);
            }
        };

        // Restore ER diagram for undo/redo — forces ALL positions from model data
        window.restoreERDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                erDiagram = {
                    entities: (data.entities || []).map(e => ({
                        name: e.name || '',
                        isExplicit: e.isExplicit !== false,
                        attributes: (e.attributes || []).map(a => ({
                            type: a.type || 'string',
                            name: a.name || '',
                            key: a.key || null,
                            comment: a.comment || null
                        })),
                        x: e.x || 0,
                        y: e.y || 0,
                        hasManualPosition: e.hasManualPosition || false
                    })),
                    relationships: (data.relationships || []).map(r => ({
                        fromEntity: r.fromEntity || '',
                        toEntity: r.toEntity || '',
                        leftCardinality: r.leftCardinality || 'ExactlyOne',
                        rightCardinality: r.rightCardinality || 'ExactlyOne',
                        isIdentifying: r.isIdentifying || false,
                        label: r.label || null
                    })),
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Force-hydrate ALL positions from model data (not just hasManualPosition)
                erEntityPositions = {};
                if (erDiagram.entities) {
                    erDiagram.entities.forEach(entity => {
                        if (entity.x || entity.y) {
                            erEntityPositions[entity.name] = { x: entity.x, y: entity.y };
                        }
                    });
                }

                // Auto-layout only entities that still don't have positions
                erAutoLayoutEntities();
                renderERDiagram();
            } catch (err) {
                console.error('Failed to restore ER diagram:', err);
            }
        };

        // updateToolbarForERDiagram removed - using unified updateToolbarForDiagramType()

        function erAutoLayoutEntities() {
            if (!erDiagram) return;
            // Simple grid fallback for initial placement of new entities only
            const cols = Math.max(1, Math.ceil(Math.sqrt(erDiagram.entities.length)));
            erDiagram.entities.forEach((entity, idx) => {
                if (!erEntityPositions[entity.name]) {
                    const col = idx % cols;
                    const row = Math.floor(idx / cols);
                    erEntityPositions[entity.name] = {
                        x: ER_LEFT_MARGIN + col * ER_GAP_X,
                        y: ER_TOP_MARGIN + row * (200 + ER_GAP_Y)
                    };
                }
            });
        }

        // Dagre-based auto layout for ER diagrams (uses relationships as edges)
        function erDagreAutoLayout() {
            if (!erDiagram || typeof dagre === 'undefined') return;

            const g = new dagre.graphlib.Graph();
            g.setGraph({
                rankdir: 'TB',
                nodesep: 80,
                ranksep: 100,
                edgesep: 30,
                marginx: ER_LEFT_MARGIN,
                marginy: ER_TOP_MARGIN
            });
            g.setDefaultEdgeLabel(function() { return {}; });

            // Register all entity nodes with their measured dimensions
            erDiagram.entities.forEach(entity => {
                const dim = erGetEntityBoxDimensions(entity);
                g.setNode(entity.name, { width: dim.width, height: dim.height });
            });

            // Register relationships as edges
            erDiagram.relationships.forEach(rel => {
                if (g.hasNode(rel.fromEntity) && g.hasNode(rel.toEntity)) {
                    g.setEdge(rel.fromEntity, rel.toEntity);
                }
            });

            try {
                dagre.layout(g);
            } catch (e) {
                console.error('ER diagram dagre layout failed:', e);
                return;
            }

            // Apply computed positions
            erDiagram.entities.forEach(entity => {
                const layoutNode = g.node(entity.name);
                if (layoutNode) {
                    erEntityPositions[entity.name] = {
                        x: layoutNode.x - layoutNode.width / 2,
                        y: layoutNode.y - layoutNode.height / 2,
                        width: layoutNode.width,
                        height: layoutNode.height
                    };
                }
            });
        }

        function erGetEntityBoxDimensions(entity) {
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            ctx.font = 'bold 13px monospace';
            let maxTextWidth = ER_BOX_MIN_WIDTH;
            const nameWidth = ctx.measureText(entity.name).width + ER_BOX_PADDING * 2 + 20;
            maxTextWidth = Math.max(maxTextWidth, nameWidth);

            ctx.font = '12px monospace';
            const keyColumnWidth = 26; // Always reserved for PK/FK column
            entity.attributes.forEach(attr => {
                ctx.font = '11px monospace';
                const typeWidth = ctx.measureText(attr.type || '').width;
                ctx.font = '12px monospace';
                const nameWidth2 = ctx.measureText(attr.name || '').width;
                const w = ER_BOX_PADDING + keyColumnWidth + typeWidth + 14 + nameWidth2 + ER_BOX_PADDING + 10;
                maxTextWidth = Math.max(maxTextWidth, w);
            });

            const width = Math.ceil(maxTextWidth);
            const height = ER_HEADER_HEIGHT + 1 + Math.max(1, entity.attributes.length) * ER_ATTR_HEIGHT + ER_BOX_PADDING;
            return { width, height };
        }

        function erFormatAttribute(attr) {
            let text = attr.type + ' ' + attr.name;
            if (attr.key) text += ' ' + attr.key;
            if (attr.comment) text += ' "' + attr.comment + '"';
            return text;
        }

        function erCardinalitySymbol(cardinality) {
            switch (cardinality) {
                case 'ExactlyOne': return '||';
                case 'ZeroOrOne': return '|o';
                case 'ZeroOrMore': return '}o';
                case 'OneOrMore': return '}|';
                default: return '||';
            }
        }

        function erCardinalityLabel(cardinality) {
            switch (cardinality) {
                case 'ExactlyOne': return '1';
                case 'ZeroOrOne': return '0..1';
                case 'ZeroOrMore': return '0..*';
                case 'OneOrMore': return '1..*';
                default: return '1';
            }
        }

        function renderERDiagram() {
            if (!erDiagram) return;

            // Show diagram-svg, hide editorCanvas when rendering standard diagram types
            var dSvg = document.getElementById('diagram-svg');
            var eCanvas = document.getElementById('editorCanvas');
            if (dSvg) dSvg.style.display = '';
            if (eCanvas) { eCanvas.style.display = 'none'; eCanvas.innerHTML = ''; }

            nodesLayer.innerHTML = '';
            edgesLayer.innerHTML = '';
            subgraphsLayer.innerHTML = '';

            // Render entities
            erDiagram.entities.forEach(entity => {
                erRenderEntity(entity);
            });

            // Render relationships
            erDiagram.relationships.forEach((rel, idx) => {
                erRenderRelationship(rel, idx);
            });

            updateMinimap();
            updateCanvasAndZoomLimits();
        }

        function erRenderEntity(entity) {
            const pos = erEntityPositions[entity.name];
            if (!pos) return;
            const dim = erGetEntityBoxDimensions(entity);
            pos.width = dim.width;
            pos.height = dim.height;

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('node-group', 'er-entity');
            g.setAttribute('data-entity-name', entity.name);
            g.setAttribute('transform', `translate(${pos.x}, ${pos.y})`);

            // Main box
            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', '0');
            rect.setAttribute('y', '0');
            rect.setAttribute('width', dim.width);
            rect.setAttribute('height', dim.height);
            rect.setAttribute('rx', '3');
            rect.classList.add('node-shape');
            if (erSelectedEntityName === entity.name) rect.classList.add('selected');
            g.appendChild(rect);

            // Entity name header
            const headerBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            headerBg.setAttribute('x', '0');
            headerBg.setAttribute('y', '0');
            headerBg.setAttribute('width', dim.width);
            headerBg.setAttribute('height', ER_HEADER_HEIGHT);
            headerBg.setAttribute('rx', '3');
            headerBg.setAttribute('fill', 'var(--node-header-fill, rgba(100,100,200,0.15))');
            g.appendChild(headerBg);

            const nameText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            nameText.setAttribute('x', dim.width / 2);
            nameText.setAttribute('y', ER_HEADER_HEIGHT / 2 + 5);
            nameText.setAttribute('text-anchor', 'middle');
            nameText.setAttribute('fill', 'var(--node-text)');
            nameText.setAttribute('font-size', '13');
            nameText.setAttribute('font-weight', 'bold');
            nameText.textContent = entity.name;
            g.appendChild(nameText);

            // Divider
            const divider = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            divider.setAttribute('x1', '0');
            divider.setAttribute('y1', ER_HEADER_HEIGHT);
            divider.setAttribute('x2', dim.width);
            divider.setAttribute('y2', ER_HEADER_HEIGHT);
            divider.setAttribute('stroke', 'var(--node-stroke)');
            divider.setAttribute('stroke-width', '1');
            g.appendChild(divider);

            // Attributes
            let yOffset = ER_HEADER_HEIGHT + 1;
            if (entity.attributes.length === 0) {
                const emptyText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                emptyText.setAttribute('x', ER_BOX_PADDING);
                emptyText.setAttribute('y', yOffset + ER_ATTR_HEIGHT / 2 + 4);
                emptyText.setAttribute('fill', 'var(--text-muted, #888)');
                emptyText.setAttribute('font-size', '11');
                emptyText.setAttribute('font-style', 'italic');
                emptyText.textContent = '(no attributes)';
                g.appendChild(emptyText);
            } else {
                entity.attributes.forEach((attr, attrIdx) => {
                    const attrG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                    attrG.classList.add('er-attribute');
                    attrG.setAttribute('data-entity-name', entity.name);
                    attrG.setAttribute('data-attr-index', attrIdx);

                    // Attribute row background for hover
                    const attrBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    attrBg.setAttribute('x', '1');
                    attrBg.setAttribute('y', yOffset);
                    attrBg.setAttribute('width', dim.width - 2);
                    attrBg.setAttribute('height', ER_ATTR_HEIGHT);
                    const isSelectedAttr = (erSelectedEntityName === entity.name && erSelectedAttrIndex === attrIdx);
                    attrBg.setAttribute('fill', isSelectedAttr ? 'var(--node-selected-stroke, rgba(0,122,204,0.25))' : 'transparent');
                    if (isSelectedAttr) attrBg.setAttribute('fill-opacity', '0.25');
                    attrBg.style.cursor = 'pointer';
                    attrG.appendChild(attrBg);

                    // Key indicator
                    if (attr.key === 'PK') {
                        const keyText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        keyText.setAttribute('x', ER_BOX_PADDING);
                        keyText.setAttribute('y', yOffset + ER_ATTR_HEIGHT / 2 + 4);
                        keyText.setAttribute('fill', 'var(--accent-color, #4a9eff)');
                        keyText.setAttribute('font-size', '10');
                        keyText.setAttribute('font-weight', 'bold');
                        keyText.textContent = 'PK';
                        attrG.appendChild(keyText);
                    } else if (attr.key === 'FK') {
                        const keyText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        keyText.setAttribute('x', ER_BOX_PADDING);
                        keyText.setAttribute('y', yOffset + ER_ATTR_HEIGHT / 2 + 4);
                        keyText.setAttribute('fill', 'var(--accent-color-secondary, #ff9f43)');
                        keyText.setAttribute('font-size', '10');
                        keyText.setAttribute('font-weight', 'bold');
                        keyText.textContent = 'FK';
                        attrG.appendChild(keyText);
                    }

                    // Always reserve key column width so type/name columns stay aligned
                    const keyColumnWidth = 26;
                    const typeText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    typeText.setAttribute('x', ER_BOX_PADDING + keyColumnWidth);
                    typeText.setAttribute('y', yOffset + ER_ATTR_HEIGHT / 2 + 4);
                    typeText.setAttribute('fill', 'var(--text-muted, #888)');
                    typeText.setAttribute('font-size', '11');
                    typeText.textContent = attr.type;
                    attrG.appendChild(typeText);

                    const nameTextEl = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    nameTextEl.setAttribute('x', ER_BOX_PADDING + keyColumnWidth + 60);
                    nameTextEl.setAttribute('y', yOffset + ER_ATTR_HEIGHT / 2 + 4);
                    nameTextEl.setAttribute('fill', 'var(--node-text)');
                    nameTextEl.setAttribute('font-size', '12');
                    nameTextEl.textContent = attr.name;
                    attrG.appendChild(nameTextEl);

                    if (attr.comment) {
                        const commentText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        commentText.setAttribute('x', dim.width - ER_BOX_PADDING);
                        commentText.setAttribute('y', yOffset + ER_ATTR_HEIGHT / 2 + 4);
                        commentText.setAttribute('text-anchor', 'end');
                        commentText.setAttribute('fill', 'var(--text-muted, #888)');
                        commentText.setAttribute('font-size', '10');
                        commentText.setAttribute('font-style', 'italic');
                        commentText.textContent = '"' + attr.comment + '"';
                        attrG.appendChild(commentText);
                    }

                    g.appendChild(attrG);
                    yOffset += ER_ATTR_HEIGHT;
                });
            }

            // Connection grip points (N, S, E, W)
            const grips = [
                { cx: dim.width / 2, cy: 0 },
                { cx: dim.width / 2, cy: dim.height },
                { cx: dim.width, cy: dim.height / 2 },
                { cx: 0, cy: dim.height / 2 }
            ];
            grips.forEach(gp => {
                const grip = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                grip.setAttribute('cx', gp.cx);
                grip.setAttribute('cy', gp.cy);
                grip.setAttribute('r', '3');
                grip.classList.add('grip-point');
                grip.setAttribute('data-entity-name', entity.name);
                g.appendChild(grip);
            });

            nodesLayer.appendChild(g);
        }

        function erRenderRelationship(rel, idx) {
            const fromEntity = erDiagram.entities.find(e => e.name === rel.fromEntity);
            const toEntity = erDiagram.entities.find(e => e.name === rel.toEntity);
            if (!fromEntity || !toEntity) return;

            const fromPos = erEntityPositions[rel.fromEntity];
            const toPos = erEntityPositions[rel.toEntity];
            if (!fromPos || !toPos) return;

            const fromDim = erGetEntityBoxDimensions(fromEntity);
            const toDim = erGetEntityBoxDimensions(toEntity);

            const fromBox = { x: fromPos.x, y: fromPos.y, width: fromDim.width, height: fromDim.height };
            const toBox = { x: toPos.x, y: toPos.y, width: toDim.width, height: toDim.height };

            const fromCx = fromBox.x + fromBox.width / 2;
            const fromCy = fromBox.y + fromBox.height / 2;
            const toCx = toBox.x + toBox.width / 2;
            const toCy = toBox.y + toBox.height / 2;

            const ports = stGetEdgePorts(fromBox, toBox, fromCx, fromCy, toCx, toCy);

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('er-relationship');
            g.setAttribute('data-rel-index', idx);

            // Main line
            const erEdgePathD = buildEdgePath(ports);
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            line.setAttribute('d', erEdgePathD);
            line.setAttribute('fill', 'none');
            line.setAttribute('stroke', erSelectedRelIndex === idx ? 'var(--edge-selected)' : 'var(--edge-color)');
            line.setAttribute('stroke-width', erSelectedRelIndex === idx ? '3' : '2');
            if (!rel.isIdentifying) {
                line.setAttribute('stroke-dasharray', '6,3');
            }
            g.appendChild(line);

            // Clickable hitbox
            const hitbox = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            hitbox.setAttribute('d', erEdgePathD);
            hitbox.setAttribute('fill', 'none');
            hitbox.setAttribute('stroke', 'transparent');
            hitbox.setAttribute('stroke-width', '12');
            hitbox.style.cursor = 'pointer';
            g.appendChild(hitbox);

            // Cardinality labels near endpoints
            const dx = ports.x2 - ports.x1;
            const dy = ports.y2 - ports.y1;
            const len = Math.hypot(dx, dy);
            if (len > 0) {
                const nx = dx / len;
                const ny = dy / len;
                const offset = 20;

                const leftLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                leftLabel.setAttribute('x', ports.x1 + nx * offset + ny * 10);
                leftLabel.setAttribute('y', ports.y1 + ny * offset - nx * 10);
                leftLabel.setAttribute('text-anchor', 'middle');
                leftLabel.setAttribute('fill', 'var(--node-text)');
                leftLabel.setAttribute('font-size', '10');
                leftLabel.setAttribute('font-weight', 'bold');
                leftLabel.textContent = erCardinalityLabel(rel.leftCardinality);
                g.appendChild(leftLabel);

                const rightLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                rightLabel.setAttribute('x', ports.x2 - nx * offset + ny * 10);
                rightLabel.setAttribute('y', ports.y2 - ny * offset - nx * 10);
                rightLabel.setAttribute('text-anchor', 'middle');
                rightLabel.setAttribute('fill', 'var(--node-text)');
                rightLabel.setAttribute('font-size', '10');
                rightLabel.setAttribute('font-weight', 'bold');
                rightLabel.textContent = erCardinalityLabel(rel.rightCardinality);
                g.appendChild(rightLabel);
            }

            // Relationship label (diamond shape with text)
            if (rel.label) {
                const erMid = edgeMidpoint(ports);
                const midX = erMid.x;
                const midY = erMid.y;

                // Diamond background
                const diamondSize = 8;
                const diamond = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                diamond.setAttribute('points',
                    `${midX},${midY - diamondSize} ${midX + diamondSize},${midY} ${midX},${midY + diamondSize} ${midX - diamondSize},${midY}`
                );
                diamond.setAttribute('fill', 'var(--bg-color, white)');
                diamond.setAttribute('stroke', erSelectedRelIndex === idx ? 'var(--edge-selected)' : 'var(--edge-color)');
                diamond.setAttribute('stroke-width', '1');
                g.appendChild(diamond);

                const labelText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                labelText.setAttribute('x', midX);
                labelText.setAttribute('y', midY - diamondSize - 4);
                labelText.setAttribute('text-anchor', 'middle');
                labelText.setAttribute('fill', 'var(--node-text)');
                labelText.setAttribute('font-size', '11');
                labelText.setAttribute('font-style', 'italic');
                setTextWithLineBreaks(labelText, rel.label);
                g.appendChild(labelText);
            }

            edgesLayer.appendChild(g);
        }

        // ===== ER Diagram Mouse Interaction =====

        svg.addEventListener('mousedown', function(e) {
            if (currentDiagramType !== 'er') return;
            if (!erDiagram) return;

            const entityGroup = e.target.closest('.er-entity');
            const relGroup = e.target.closest('.er-relationship');
            const gripPoint = e.target.closest('.grip-point');

            if (gripPoint && gripPoint.getAttribute('data-entity-name')) {
                // Start drawing a relationship
                const entityName = gripPoint.getAttribute('data-entity-name');
                erDrawingRelFrom = entityName;
                const pos = erEntityPositions[entityName];
                if (pos) {
                    const dim = erGetEntityBoxDimensions(erDiagram.entities.find(e => e.name === entityName));
                    const cx = pos.x + (dim ? dim.width : ER_BOX_MIN_WIDTH) / 2;
                    const cy = pos.y + (dim ? dim.height : 60) / 2;
                    tempEdge.setAttribute('x1', cx);
                    tempEdge.setAttribute('y1', cy);
                    tempEdge.setAttribute('x2', cx);
                    tempEdge.setAttribute('y2', cy);
                    tempEdge.style.display = '';
                }
                e.preventDefault();
                return;
            }

            // Handle toolbar "Add Relationship" two-click mode
            if (erPickingRelSource) {
                if (entityGroup) {
                    const entityName = entityGroup.getAttribute('data-entity-name');
                    if (!erPickingRelFrom) {
                        // First click: pick source — highlight it, wait for second click
                        erPickingRelFrom = entityName;
                        erSelectedEntityName = entityName;
                        renderERDiagram();
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    } else if (entityName !== erPickingRelFrom) {
                        // Second click: pick target — create relationship
                        postMessage({
                            type: 'er_relationshipCreated',
                            fromEntity: erPickingRelFrom,
                            toEntity: entityName,
                            leftCardinality: 'ExactlyOne',
                            rightCardinality: 'ZeroOrMore',
                            isIdentifying: false,
                            label: 'relates'
                        });
                        erPickingRelSource = false;
                        erPickingRelFrom = null;
                        document.body.style.cursor = '';
                        document.getElementById('tb-er-add-relationship').classList.remove('active');
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    } else {
                        // Clicked source again: deselect source, wait for new source
                        erPickingRelFrom = null;
                        erSelectedEntityName = null;
                        renderERDiagram();
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    }
                } else {
                    // Clicked empty space: cancel pick mode
                    erPickingRelSource = false;
                    erPickingRelFrom = null;
                    document.body.style.cursor = '';
                    document.getElementById('tb-er-add-relationship').classList.remove('active');
                    tempEdge.style.display = 'none';
                }
            }

            // Check for attribute click BEFORE entity click (attribute rows are inside entity groups)
            const attrGroup = e.target.closest('.er-attribute');
            if (attrGroup && entityGroup) {
                const entityName = attrGroup.getAttribute('data-entity-name');
                const attrIdx = parseInt(attrGroup.getAttribute('data-attr-index'), 10);
                erSelectedEntityName = entityName;
                erSelectedAttrIndex = attrIdx;
                erSelectedRelIndex = -1;
                erShowAttributeProperties(entityName, attrIdx);
                renderERDiagram();
                e.preventDefault();
                return;
            }

            if (entityGroup) {
                const entityName = entityGroup.getAttribute('data-entity-name');
                erSelectedEntityName = entityName;
                erSelectedAttrIndex = -1;
                erSelectedRelIndex = -1;

                // Start dragging
                const svgPt = getSVGPoint(e);
                const pos = erEntityPositions[entityName];
                if (pos) {
                    erDraggingEntityName = entityName;
                    erDragOffsetX = svgPt.x - pos.x;
                    erDragOffsetY = svgPt.y - pos.y;
                }

                erShowEntityProperties(entityName);
                renderERDiagram();
                e.preventDefault();
                return;
            }

            if (relGroup) {
                const idx = parseInt(relGroup.getAttribute('data-rel-index'), 10);
                erSelectedRelIndex = idx;
                erSelectedEntityName = null;
                erSelectedAttrIndex = -1;
                erShowRelationshipProperties(idx);
                renderERDiagram();
                e.preventDefault();
                return;
            }

            // Click on empty space
            erSelectedEntityName = null;
            erSelectedAttrIndex = -1;
            erSelectedRelIndex = -1;
            propertyPanel.classList.remove('visible');
            renderERDiagram();
        });

        svg.addEventListener('mousemove', function(e) {
            if (currentDiagramType !== 'er') return;
            if (!erDiagram) return;
            const svgPt = getSVGPoint(e);

            if (erDrawingRelFrom) {
                tempEdge.setAttribute('x2', svgPt.x);
                tempEdge.setAttribute('y2', svgPt.y);
                return;
            }

            if (erDraggingEntityName) {
                const newX = Math.round((svgPt.x - erDragOffsetX) / SNAP_GRID) * SNAP_GRID;
                const newY = Math.round((svgPt.y - erDragOffsetY) / SNAP_GRID) * SNAP_GRID;
                erEntityPositions[erDraggingEntityName] = {
                    ...erEntityPositions[erDraggingEntityName],
                    x: newX,
                    y: newY
                };
                renderERDiagram();
            }
        });

        svg.addEventListener('mouseup', function(e) {
            if (currentDiagramType !== 'er') return;
            if (!erDiagram) return;

            if (erDrawingRelFrom) {
                const targetGroup = e.target.closest('.er-entity');
                if (targetGroup) {
                    const toEntityName = targetGroup.getAttribute('data-entity-name');
                    if (toEntityName && toEntityName !== erDrawingRelFrom) {
                        postMessage({
                            type: 'er_relationshipCreated',
                            fromEntity: erDrawingRelFrom,
                            toEntity: toEntityName,
                            leftCardinality: 'ExactlyOne',
                            rightCardinality: 'ZeroOrMore',
                            isIdentifying: false,
                            label: 'relates'
                        });
                    }
                }
                tempEdge.style.display = 'none';
                erDrawingRelFrom = null;
                // Reset pick mode after relationship created
                erPickingRelSource = false;
                erPickingRelFrom = null;
                document.body.style.cursor = '';
                return;
            }

            if (erDraggingEntityName) {
                const pos = erEntityPositions[erDraggingEntityName];
                if (pos) {
                    postMessage({
                        type: 'er_entityMoved',
                        entityName: erDraggingEntityName,
                        x: pos.x,
                        y: pos.y
                    });
                }
                erDraggingEntityName = null;
            }
        });

        // ===== ER Diagram Double-Click to Edit =====

        svg.addEventListener('dblclick', function(e) {
            if (currentDiagramType !== 'er') return;
            if (!erDiagram) return;

            const entityGroup = e.target.closest('.er-entity');
            const attrGroup = e.target.closest('.er-attribute');
            const relGroup = e.target.closest('.er-relationship');

            if (attrGroup) {
                const entityName = attrGroup.getAttribute('data-entity-name');
                const attrIdx = parseInt(attrGroup.getAttribute('data-attr-index'), 10);
                erSelectedEntityName = entityName;
                erSelectedAttrIndex = attrIdx;
                erSelectedRelIndex = -1;
                erShowAttributeProperties(entityName, attrIdx);
                renderERDiagram();
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            if (entityGroup) {
                const entityName = entityGroup.getAttribute('data-entity-name');
                erSelectedEntityName = entityName;
                erSelectedAttrIndex = -1;
                erSelectedRelIndex = -1;
                erShowEntityProperties(entityName);
                e.preventDefault();
                return;
            }

            if (relGroup) {
                const idx = parseInt(relGroup.getAttribute('data-rel-index'), 10);
                if (idx >= 0 && idx < erDiagram.relationships.length) {
                    erSelectedRelIndex = idx;
                    erSelectedEntityName = null;
                    erShowRelationshipProperties(idx);
                }
                e.preventDefault();
                return;
            }
        });

        // ===== ER Diagram Property Panel =====

        function erShowEntityProperties(entityName) {
            const entity = erDiagram.entities.find(e => e.name === entityName);
            if (!entity) return;

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Name</div>
                    <input class="property-input" id="er-entity-name" value="${escapeHtml(entity.name)}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Attributes</div>
                    <div style="font-size: 11px; color: var(--text-muted, #888);">${entity.attributes.length} attributes</div>
                </div>
                <div class="property-row" style="margin-top:8px">
                    <button class="property-btn" id="er-add-attribute" style="flex:1;padding:4px 8px;cursor:pointer;">+ Attribute</button>
                </div>
            `;

            document.getElementById('er-entity-name').addEventListener('change', function() {
                if (this.value.trim()) {
                    postMessage({ type: 'er_entityEdited', oldName: entityName, name: this.value.trim() });
                }
            });
            document.getElementById('er-add-attribute').addEventListener('click', function() {
                postMessage({ type: 'er_attributeAdded', entityName: entityName, attrType: 'string', name: 'newField' });
            });

            propertyPanel.classList.add('visible');
        }

        function erShowAttributeProperties(entityName, attrIdx) {
            const entity = erDiagram.entities.find(e => e.name === entityName);
            if (!entity || attrIdx < 0 || attrIdx >= entity.attributes.length) return;
            const attr = entity.attributes[attrIdx];

            const keyOptions = ['', 'PK', 'FK'];
            const keyOpts = keyOptions.map(k => `<option value="${k}" ${(attr.key || '') === k ? 'selected' : ''}>${k || '(none)'}</option>`).join('');

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Entity</div>
                    <input class="property-input" value="${escapeHtml(entityName)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Name</div>
                    <input class="property-input" id="er-attr-name" value="${escapeHtml(attr.name || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <input class="property-input" id="er-attr-type" value="${escapeHtml(attr.type || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Key</div>
                    <select class="property-input" id="er-attr-key">${keyOpts}</select>
                </div>
                <div class="property-row">
                    <div class="property-label">Comment</div>
                    <input class="property-input" id="er-attr-comment" value="${escapeHtml(attr.comment || '')}" placeholder="Optional comment" />
                </div>
            `;

            document.getElementById('er-attr-name').addEventListener('change', function() {
                postMessage({ type: 'er_attributeEdited', entityName: entityName, index: attrIdx, name: this.value });
            });
            document.getElementById('er-attr-type').addEventListener('change', function() {
                postMessage({ type: 'er_attributeEdited', entityName: entityName, index: attrIdx, attrType: this.value });
            });
            document.getElementById('er-attr-key').addEventListener('change', function() {
                postMessage({ type: 'er_attributeEdited', entityName: entityName, index: attrIdx, key: this.value });
            });
            document.getElementById('er-attr-comment').addEventListener('change', function() {
                postMessage({ type: 'er_attributeEdited', entityName: entityName, index: attrIdx, comment: this.value });
            });

            propertyPanel.classList.add('visible');
        }

        function erShowRelationshipProperties(idx) {
            if (!erDiagram || idx < 0 || idx >= erDiagram.relationships.length) return;
            const rel = erDiagram.relationships[idx];

            const cardOptions = ['ExactlyOne', 'ZeroOrOne', 'ZeroOrMore', 'OneOrMore'];
            const cardLabels = { 'ExactlyOne': '1 (Exactly One)', 'ZeroOrOne': '0..1 (Zero or One)', 'ZeroOrMore': '0..* (Zero or More)', 'OneOrMore': '1..* (One or More)' };

            const leftOpts = cardOptions.map(c => `<option value="${c}" ${rel.leftCardinality === c ? 'selected' : ''}>${cardLabels[c]}</option>`).join('');
            const rightOpts = cardOptions.map(c => `<option value="${c}" ${rel.rightCardinality === c ? 'selected' : ''}>${cardLabels[c]}</option>`).join('');

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">From</div>
                    <input class="property-input" value="${escapeHtml(rel.fromEntity)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <input class="property-input" value="${escapeHtml(rel.toEntity)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Label</div>
                    <input class="property-input" id="er-rel-label" value="${escapeHtml(rel.label || '')}" placeholder="Relationship label" />
                </div>
                <div class="property-row">
                    <div class="property-label">Left Card.</div>
                    <select class="property-input" id="er-rel-left-card">${leftOpts}</select>
                </div>
                <div class="property-row">
                    <div class="property-label">Right Card.</div>
                    <select class="property-input" id="er-rel-right-card">${rightOpts}</select>
                </div>
                <div class="property-row">
                    <div class="property-label">Identifying</div>
                    <select class="property-input" id="er-rel-identifying">
                        <option value="true" ${rel.isIdentifying ? 'selected' : ''}>Yes (solid line)</option>
                        <option value="false" ${!rel.isIdentifying ? 'selected' : ''}>No (dashed line)</option>
                    </select>
                </div>
            `;

            document.getElementById('er-rel-label').addEventListener('change', function() {
                postMessage({ type: 'er_relationshipEdited', index: idx, label: this.value });
            });
            document.getElementById('er-rel-left-card').addEventListener('change', function() {
                postMessage({ type: 'er_relationshipEdited', index: idx, leftCardinality: this.value });
            });
            document.getElementById('er-rel-right-card').addEventListener('change', function() {
                postMessage({ type: 'er_relationshipEdited', index: idx, rightCardinality: this.value });
            });
            document.getElementById('er-rel-identifying').addEventListener('change', function() {
                postMessage({ type: 'er_relationshipEdited', index: idx, isIdentifying: this.value === 'true' });
            });

            propertyPanel.classList.add('visible');
        }

        // ===== ER Diagram Context Menu =====

        svg.addEventListener('contextmenu', function(e) {
            if (currentDiagramType !== 'er') return;
            if (!erDiagram) return;
            e.preventDefault();
            e.stopPropagation();

            const entityGroup = e.target.closest('.er-entity');
            const attrGroup = e.target.closest('.er-attribute');
            const relGroup = e.target.closest('.er-relationship');

            contextMenu.innerHTML = '';

            if (attrGroup) {
                const entityName = attrGroup.getAttribute('data-entity-name');
                const attrIdx = parseInt(attrGroup.getAttribute('data-attr-index'), 10);
                erSelectedEntityName = entityName;
                erSelectedAttrIndex = attrIdx;
                erSelectedRelIndex = -1;

                addErContextMenuItem('Edit Attribute', () => {
                    erShowAttributeProperties(entityName, attrIdx);
                });
                addErContextMenuItem('Delete Attribute', () => {
                    postMessage({ type: 'er_attributeDeleted', entityName: entityName, index: attrIdx });
                });
                // Attribute-level copy/paste
                addErContextMenuSeparator();
                addErContextMenuItem('\u{1F4CB} Copy Attribute', () => {
                    erSelectedEntityName = entityName;
                    erSelectedAttrIndex = attrIdx;
                    copySelected();
                });
                if (clipboard && clipboard.diagramType === 'er' && clipboard.type === 'er_attribute') {
                    addErContextMenuItem('\u{1F4CB} Paste Attribute', () => {
                        erSelectedEntityName = entityName;
                        pasteERAttribute();
                    });
                }
            } else if (entityGroup) {
                const entityName = entityGroup.getAttribute('data-entity-name');
                erSelectedEntityName = entityName;
                erSelectedAttrIndex = -1;

                addErContextMenuItem('Edit Entity', () => {
                    erShowEntityProperties(entityName);
                });
                addErContextMenuItem('Add Attribute', () => {
                    postMessage({ type: 'er_attributeAdded', entityName: entityName, attrType: 'string', name: 'newField' });
                });
                addErContextMenuSeparator();
                addErContextMenuItem('Delete Entity', () => {
                    postMessage({ type: 'er_entityDeleted', name: entityName });
                });
            } else if (relGroup) {
                const idx = parseInt(relGroup.getAttribute('data-rel-index'), 10);
                erSelectedRelIndex = idx;

                addErContextMenuItem('Edit Relationship', () => {
                    erShowRelationshipProperties(idx);
                });
                addErContextMenuSeparator();
                addErContextMenuItem('Delete Relationship', () => {
                    postMessage({ type: 'er_relationshipDeleted', index: idx });
                });
            } else {
                // Empty space
                addErContextMenuItem('Add Entity', () => {
                    const svgPt = getSVGPoint(e);
                    const newName = 'Entity' + (erDiagram.entities.length + 1);
                    erEntityPositions[newName] = {
                        x: Math.round(svgPt.x / SNAP_GRID) * SNAP_GRID,
                        y: Math.round(svgPt.y / SNAP_GRID) * SNAP_GRID
                    };
                    postMessage({ type: 'er_entityCreated', name: newName });
                });
            }

            // Copy/Paste for ER diagrams (entity-level)
            if (erSelectedEntityName && erSelectedAttrIndex < 0) {
                addErContextMenuSeparator();
                addErContextMenuItem('\u{1F4CB} Copy Entity', () => { copySelected(); });
            }
            if (clipboard && clipboard.diagramType === 'er' && clipboard.type === 'er_entity') {
                addErContextMenuItem('\u{1F4CB} Paste Entity', () => { pasteClipboard(0, 0); });
            }
            // Show "Paste Attribute" on entity right-click when clipboard has an attribute
            if (erSelectedEntityName && clipboard && clipboard.diagramType === 'er' && clipboard.type === 'er_attribute') {
                if (erSelectedAttrIndex < 0) addErContextMenuSeparator();
                addErContextMenuItem('\u{1F4CB} Paste Attribute', () => { pasteERAttribute(); });
            }

            positionContextMenu(contextMenu, e.clientX, e.clientY);
            renderERDiagram();
        });

        function addErContextMenuItem(label, onClick) {
            const item = document.createElement('div');
            item.classList.add('context-menu-item');
            item.textContent = label;
            item.addEventListener('click', function(e) {
                e.stopPropagation();
                e.preventDefault();
                contextMenu.classList.remove('visible');
                // Defer so the click event finishes propagating before the panel opens
                setTimeout(onClick, 0);
            });
            contextMenu.appendChild(item);
        }

        function addErContextMenuSeparator() {
            const sep = document.createElement('div');
            sep.classList.add('context-menu-separator');
            contextMenu.appendChild(sep);
        }

        // Keyboard shortcuts for ER diagram elements
        document.addEventListener('keydown', function(e) {
            if (currentDiagramType !== 'er') return;
            if (!erDiagram) return;
            if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'SELECT')) return;

            if (e.key === 'Delete' || e.key === 'Backspace') {
                if (erSelectedEntityName) {
                    postMessage({ type: 'er_entityDeleted', name: erSelectedEntityName });
                    erSelectedEntityName = null;
                    e.preventDefault();
                } else if (erSelectedRelIndex >= 0) {
                    postMessage({ type: 'er_relationshipDeleted', index: erSelectedRelIndex });
                    erSelectedRelIndex = -1;
                    e.preventDefault();
                }
            } else if (e.key === 'c' && (e.ctrlKey || e.metaKey)) {
                copySelected();
                e.preventDefault();
            } else if (e.key === 'v' && (e.ctrlKey || e.metaKey)) {
                pasteClipboard(0, 0);
                e.preventDefault();
            }
        });


        // ===== Toolbar Button Handlers =====

        // ===== ER diagram toolbar buttons =====
        document.getElementById('tb-er-add-entity').addEventListener('click', function() {
            if (!erDiagram) return;
            postMessage({ type: 'er_entityCreated', name: 'ENTITY' + (erDiagram.entities.length + 1) });
        });

        let erPickingRelSource = false; // true when toolbar Add Relationship is waiting for source click
        let erPickingRelFrom = null; // source entity name when picking target
        document.getElementById('tb-er-add-relationship').addEventListener('click', function() {
            if (!erDiagram || erDiagram.entities.length < 2) return;
            erPickingRelSource = true;
            erPickingRelFrom = null;
            document.body.style.cursor = 'crosshair';
            this.classList.add('active');
        });

        document.getElementById('tb-er-auto-layout').addEventListener('click', function() {
            erDagreAutoLayout();
            renderERDiagram();
            const positions = [];
            erDiagram.entities.forEach(entity => {
                const pos = erEntityPositions[entity.name];
                if (pos) {
                    positions.push({ entityName: entity.name, x: pos.x, y: pos.y, width: pos.width || 0, height: pos.height || 0 });
                }
            });
            postMessage({ type: 'er_autoLayoutComplete', positions: positions });
        });

