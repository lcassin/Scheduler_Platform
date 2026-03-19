        // ========== Class Diagram Visual Editor ==========

        let clsDiagram = null; // When non-null, we're in class diagram mode
        let clsSelectedClassId = null;
        let clsSelectedMemberIndex = -1; // index into cls.members[] when a member row is selected
        let clsSelectedRelIndex = -1;
        let clsDraggingClassId = null;
        let clsDragOffsetX = 0;
        let clsDragOffsetY = 0;
        let clsDrawingRelFrom = null; // classId when drawing a relationship
        let clsClassPositions = {}; // { classId: { x, y, width, height } }

        const CLS_BOX_MIN_WIDTH = 140;
        const CLS_BOX_PADDING = 10;
        const CLS_SECTION_HEIGHT = 24;
        const CLS_MEMBER_HEIGHT = 20;
        const CLS_TOP_MARGIN = 40;
        const CLS_LEFT_MARGIN = 60;
        const CLS_CLASS_GAP_X = 220;
        const CLS_CLASS_GAP_Y = 40;

        // Normalize a class relationship so the directional marker is always on rightEnd (the TO side).
        // When Mermaid syntax like "Pet <|.. Dog" is parsed, leftEnd gets the Inheritance marker
        // and fromId=Pet, toId=Dog. We swap fromId/toId and move the marker to rightEnd so that
        // visually "From" always means the child/source and "To" always means the parent/target.
        function clsNormalizeRelationship(rel) {
            if (rel.leftEnd !== 'None' && rel.rightEnd === 'None') {
                // Swap: move leftEnd marker to rightEnd, swap fromId/toId and cardinalities
                const tmp = rel.fromId;
                rel.fromId = rel.toId;
                rel.toId = tmp;
                rel.rightEnd = rel.leftEnd;
                rel.leftEnd = 'None';
                const tmpCard = rel.fromCardinality;
                rel.fromCardinality = rel.toCardinality;
                rel.toCardinality = tmpCard;
            }
            return rel;
        }

        window.loadClassDiagram = function(jsonData) {
            try {
                currentDiagramType = 'class';
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                clsDiagram = {
                    direction: data.direction || null,
                    classes: (data.classes || []).map(c => ({
                        id: c.id,
                        label: c.label || null,
                        annotation: c.annotation || null,
                        genericType: c.genericType || null,
                        members: (c.members || []).map(m => ({
                            rawText: m.rawText || '',
                            visibility: m.visibility || 'None',
                            isMethod: m.isMethod || false,
                            name: m.name || '',
                            type: m.type || null,
                            parameters: m.parameters || null,
                            classifier: m.classifier || 'None'
                        })),
                        cssClass: c.cssClass || null,
                        isExplicit: c.isExplicit !== false,
                        x: c.x || 0,
                        y: c.y || 0,
                        hasManualPosition: c.hasManualPosition || false
                    })),
                    relationships: (data.relationships || []).map(r => clsNormalizeRelationship({
                        fromId: r.fromId,
                        toId: r.toId,
                        leftEnd: r.leftEnd || 'None',
                        rightEnd: r.rightEnd || 'None',
                        linkStyle: r.linkStyle || 'Solid',
                        label: r.label || null,
                        fromCardinality: r.fromCardinality || null,
                        toCardinality: r.toCardinality || null
                    })),
                    namespaces: data.namespaces || [],
                    notes: data.notes || [],
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Clear other diagram states
                diagram.nodes = [];
                diagram.edges = [];
                diagram.subgraphs = [];
                seqDiagram = null;

                // Hide property panel from previous diagram
                propertyPanel.classList.remove('visible');

                // Reset pick mode and button state when loading a new diagram
                clsPickingRelSource = false;
                clsPickingRelFrom = null;
                document.body.style.cursor = '';
                const addRelBtn = document.getElementById('tb-cls-add-relation');
                if (addRelBtn) addRelBtn.classList.remove('active');
                tempEdge.style.display = 'none';

                // Hydrate saved positions from C# model (if HasManualPosition)
                if (clsDiagram.classes) {
                    clsDiagram.classes.forEach(cls => {
                        if (cls.hasManualPosition && (cls.x || cls.y)) {
                            clsClassPositions[cls.id] = {
                                x: cls.x,
                                y: cls.y
                            };
                        }
                    });
                }

                // Auto-layout class positions if not already set
                clsAutoLayoutClasses();

                // Update toolbar buttons for class diagram
                updateToolbarForDiagramType();

                renderClassDiagram();
                centerView();
                // Deferred re-render: on first load the container may not have its final
                // dimensions yet (WebView still sizing), causing a scrunched layout.
                setTimeout(function() { if (clsDiagram) { renderClassDiagram(); centerView(); } }, 150);
            } catch (err) {
                console.error('Failed to load class diagram:', err);
            }
        };

        // Refresh class diagram data without resetting view (called after C# model changes)
        window.refreshClassDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                clsDiagram = {
                    direction: data.direction || null,
                    classes: (data.classes || []).map(c => ({
                        id: c.id,
                        label: c.label || null,
                        annotation: c.annotation || null,
                        genericType: c.genericType || null,
                        members: (c.members || []).map(m => ({
                            rawText: m.rawText || '',
                            visibility: m.visibility || 'None',
                            isMethod: m.isMethod || false,
                            name: m.name || '',
                            type: m.type || null,
                            parameters: m.parameters || null,
                            classifier: m.classifier || 'None'
                        })),
                        cssClass: c.cssClass || null,
                        isExplicit: c.isExplicit !== false,
                        x: c.x || 0,
                        y: c.y || 0,
                        hasManualPosition: c.hasManualPosition || false
                    })),
                    relationships: (data.relationships || []).map(r => clsNormalizeRelationship({
                        fromId: r.fromId,
                        toId: r.toId,
                        leftEnd: r.leftEnd || 'None',
                        rightEnd: r.rightEnd || 'None',
                        linkStyle: r.linkStyle || 'Solid',
                        label: r.label || null,
                        fromCardinality: r.fromCardinality || null,
                        toCardinality: r.toCardinality || null
                    })),
                    namespaces: data.namespaces || [],
                    notes: data.notes || [],
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };
                // Auto-layout only new classes that don't have positions yet
                clsAutoLayoutClasses();
                renderClassDiagram();
            } catch (err) {
                console.error('Failed to refresh class diagram:', err);
            }
        };

        // Restore class diagram for undo/redo — forces ALL positions from model data
        window.restoreClassDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                clsDiagram = {
                    direction: data.direction || null,
                    classes: (data.classes || []).map(c => ({
                        id: c.id,
                        label: c.label || null,
                        annotation: c.annotation || null,
                        genericType: c.genericType || null,
                        members: (c.members || []).map(m => ({
                            rawText: m.rawText || '',
                            visibility: m.visibility || 'None',
                            isMethod: m.isMethod || false,
                            name: m.name || '',
                            type: m.type || null,
                            parameters: m.parameters || null,
                            classifier: m.classifier || 'None'
                        })),
                        cssClass: c.cssClass || null,
                        isExplicit: c.isExplicit !== false,
                        x: c.x || 0,
                        y: c.y || 0,
                        hasManualPosition: c.hasManualPosition || false
                    })),
                    relationships: (data.relationships || []).map(r => clsNormalizeRelationship({
                        fromId: r.fromId,
                        toId: r.toId,
                        leftEnd: r.leftEnd || 'None',
                        rightEnd: r.rightEnd || 'None',
                        linkStyle: r.linkStyle || 'Solid',
                        label: r.label || null,
                        fromCardinality: r.fromCardinality || null,
                        toCardinality: r.toCardinality || null
                    })),
                    namespaces: data.namespaces || [],
                    notes: data.notes || [],
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Force-hydrate ALL positions from model data (not just hasManualPosition)
                clsClassPositions = {};
                if (clsDiagram.classes) {
                    clsDiagram.classes.forEach(cls => {
                        if (cls.x || cls.y) {
                            clsClassPositions[cls.id] = { x: cls.x, y: cls.y };
                        }
                    });
                }

                // Auto-layout only classes that still don't have positions
                clsAutoLayoutClasses();
                renderClassDiagram();
            } catch (err) {
                console.error('Failed to restore class diagram:', err);
            }
        };

        // updateToolbarForClassDiagram removed - using unified updateToolbarForDiagramType()

        function clsAutoLayoutClasses() {
            if (!clsDiagram) return;
            // Simple grid fallback for initial placement of new classes only
            const cols = Math.max(1, Math.ceil(Math.sqrt(clsDiagram.classes.length)));
            clsDiagram.classes.forEach((cls, idx) => {
                if (!clsClassPositions[cls.id]) {
                    const col = idx % cols;
                    const row = Math.floor(idx / cols);
                    clsClassPositions[cls.id] = {
                        x: CLS_LEFT_MARGIN + col * CLS_CLASS_GAP_X,
                        y: CLS_TOP_MARGIN + row * (200 + CLS_CLASS_GAP_Y)
                    };
                }
            });
        }

        // Dagre-based auto layout for class diagrams (uses relationships as edges)
        function clsDagreAutoLayout() {
            if (!clsDiagram || typeof dagre === 'undefined') return;

            const g = new dagre.graphlib.Graph();
            g.setGraph({
                rankdir: 'TB',
                nodesep: 80,
                ranksep: 100,
                edgesep: 30,
                marginx: CLS_LEFT_MARGIN,
                marginy: CLS_TOP_MARGIN
            });
            g.setDefaultEdgeLabel(function() { return {}; });

            // Register all class nodes with their measured dimensions
            clsDiagram.classes.forEach(cls => {
                const dim = clsGetClassBoxDimensions(cls);
                g.setNode(cls.id, { width: dim.width, height: dim.height });
            });

            // Register relationships as edges
            clsDiagram.relationships.forEach(rel => {
                if (g.hasNode(rel.fromId) && g.hasNode(rel.toId)) {
                    g.setEdge(rel.fromId, rel.toId);
                }
            });

            try {
                dagre.layout(g);
            } catch (e) {
                console.error('Class diagram dagre layout failed:', e);
                return;
            }

            // Apply computed positions
            clsDiagram.classes.forEach(cls => {
                const layoutNode = g.node(cls.id);
                if (layoutNode) {
                    clsClassPositions[cls.id] = {
                        x: layoutNode.x - layoutNode.width / 2,
                        y: layoutNode.y - layoutNode.height / 2,
                        width: layoutNode.width,
                        height: layoutNode.height
                    };
                }
            });
        }

        function clsGetClassBoxDimensions(cls) {
            const fields = cls.members.filter(m => !m.isMethod);
            const methods = cls.members.filter(m => m.isMethod);

            // Measure text widths
            let maxTextWidth = CLS_BOX_MIN_WIDTH;
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            ctx.font = 'bold 13px monospace';
            const nameWidth = ctx.measureText(cls.label || cls.id).width + CLS_BOX_PADDING * 2 + 20;
            maxTextWidth = Math.max(maxTextWidth, nameWidth);

            if (cls.annotation) {
                ctx.font = 'italic 11px monospace';
                const annWidth = ctx.measureText('<<' + cls.annotation + '>>').width + CLS_BOX_PADDING * 2;
                maxTextWidth = Math.max(maxTextWidth, annWidth);
            }

            // For monospace 12px, use both canvas measurement AND character-count fallback
            // to handle WebView2 font metric differences
            ctx.font = '12px monospace';
            const MONO_CHAR_WIDTH = 7.25; // conservative monospace 12px char width
            fields.forEach(f => {
                const text = clsFormatMember(f);
                const measured = ctx.measureText(text).width + CLS_BOX_PADDING * 2 + 20;
                const charBased = text.length * MONO_CHAR_WIDTH + CLS_BOX_PADDING * 2 + 20;
                maxTextWidth = Math.max(maxTextWidth, measured, charBased);
            });
            methods.forEach(m => {
                const text = clsFormatMember(m);
                const measured = ctx.measureText(text).width + CLS_BOX_PADDING * 2 + 20;
                const charBased = text.length * MONO_CHAR_WIDTH + CLS_BOX_PADDING * 2 + 20;
                maxTextWidth = Math.max(maxTextWidth, measured, charBased);
            });

            const width = Math.ceil(maxTextWidth);

            // Height: header + annotation? + divider + fields + divider + methods + bottom padding
            // Dividers use 6px in rendering, so must match here
            let height = CLS_SECTION_HEIGHT; // Class name
            if (cls.annotation) height += CLS_MEMBER_HEIGHT;
            height += 6; // divider gap (matches rendering yOffset += 6)
            height += Math.max(1, fields.length) * CLS_MEMBER_HEIGHT;
            height += 6; // divider gap (matches rendering yOffset += 6)
            height += Math.max(1, methods.length) * CLS_MEMBER_HEIGHT;
            height += CLS_BOX_PADDING;

            return { width, height };
        }

        function clsFormatMember(member) {
            let prefix = '';
            switch (member.visibility) {
                case 'Public': prefix = '+'; break;
                case 'Private': prefix = '-'; break;
                case 'Protected': prefix = '#'; break;
                case 'Package': prefix = '~'; break;
            }
            // Build the full member signature matching Mermaid rendering:
            //   Fields:  +Type name          (e.g. +String name)
            //   Methods: +name(params) : Type (e.g. +makeSound() : void)
            let text = prefix;
            if (member.isMethod) {
                text += (member.name || '');
                text += '(' + (member.parameters || '') + ')';
                if (member.type) text += ' : ' + member.type;
            } else {
                if (member.type) text += member.type + ' ';
                text += (member.name || '');
            }
            // Classifier suffix (* abstract, $ static)
            if (member.classifier === 'Abstract') text += '*';
            else if (member.classifier === 'Static') text += '$';
            return text;
        }

        function clsGetVisibilitySymbol(visibility) {
            switch (visibility) {
                case 'Public': return '+';
                case 'Private': return '-';
                case 'Protected': return '#';
                case 'Package': return '~';
                default: return '';
            }
        }

        function renderClassDiagram() {
            if (!clsDiagram) return;

            // Show diagram-svg, hide editorCanvas when rendering standard diagram types
            var dSvg = document.getElementById('diagram-svg');
            var eCanvas = document.getElementById('editorCanvas');
            if (dSvg) dSvg.style.display = '';
            if (eCanvas) { eCanvas.style.display = 'none'; eCanvas.innerHTML = ''; }

            nodesLayer.innerHTML = '';
            edgesLayer.innerHTML = '';
            subgraphsLayer.innerHTML = '';

            // ===== Render Namespaces as background groups =====
            clsDiagram.namespaces.forEach(ns => {
                const nsClasses = ns.classIds.map(id => clsDiagram.classes.find(c => c.id === id)).filter(Boolean);
                if (nsClasses.length === 0) return;

                let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
                nsClasses.forEach(cls => {
                    const pos = clsClassPositions[cls.id];
                    if (!pos) return;
                    const dim = clsGetClassBoxDimensions(cls);
                    minX = Math.min(minX, pos.x);
                    minY = Math.min(minY, pos.y);
                    maxX = Math.max(maxX, pos.x + dim.width);
                    maxY = Math.max(maxY, pos.y + dim.height);
                });

                const padding = 20;
                const nsRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                nsRect.setAttribute('x', minX - padding);
                nsRect.setAttribute('y', minY - padding - 20);
                nsRect.setAttribute('width', maxX - minX + padding * 2);
                nsRect.setAttribute('height', maxY - minY + padding * 2 + 20);
                nsRect.setAttribute('rx', '6');
                nsRect.setAttribute('fill', 'var(--subgraph-fill)');
                nsRect.setAttribute('stroke', 'var(--subgraph-stroke)');
                nsRect.setAttribute('stroke-width', '1');
                nsRect.setAttribute('stroke-dasharray', '4,4');
                subgraphsLayer.appendChild(nsRect);

                const nsLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                nsLabel.setAttribute('x', minX - padding + 8);
                nsLabel.setAttribute('y', minY - padding - 6);
                nsLabel.setAttribute('fill', 'var(--node-text)');
                nsLabel.setAttribute('font-size', '12');
                nsLabel.setAttribute('font-style', 'italic');
                nsLabel.textContent = ns.name;
                subgraphsLayer.appendChild(nsLabel);
            });

            // ===== Render Classes =====
            clsDiagram.classes.forEach(cls => {
                const pos = clsClassPositions[cls.id];
                if (!pos) return;
                const dim = clsGetClassBoxDimensions(cls);
                pos.width = dim.width;
                pos.height = dim.height;

                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.classList.add('node-group', 'cls-class');
                g.setAttribute('data-class-id', cls.id);
                g.setAttribute('transform', `translate(${pos.x}, ${pos.y})`);

                // Main box
                const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('x', '0');
                rect.setAttribute('y', '0');
                rect.setAttribute('width', dim.width);
                rect.setAttribute('height', dim.height);
                rect.setAttribute('rx', '3');
                rect.classList.add('node-shape');
                if (clsSelectedClassId === cls.id) rect.classList.add('selected');
                g.appendChild(rect);

                let yOffset = 0;

                // Annotation (e.g., <<interface>>)
                if (cls.annotation) {
                    yOffset += CLS_MEMBER_HEIGHT;
                    const annText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    annText.setAttribute('x', dim.width / 2);
                    annText.setAttribute('y', yOffset);
                    annText.setAttribute('text-anchor', 'middle');
                    annText.setAttribute('fill', 'var(--node-text)');
                    annText.setAttribute('font-size', '11');
                    annText.setAttribute('font-style', 'italic');
                    annText.textContent = '<<' + cls.annotation + '>>';
                    g.appendChild(annText);
                }

                // Class name header
                yOffset += CLS_SECTION_HEIGHT;
                const nameText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                nameText.setAttribute('x', dim.width / 2);
                nameText.setAttribute('y', yOffset);
                nameText.setAttribute('text-anchor', 'middle');
                nameText.setAttribute('fill', 'var(--node-text)');
                nameText.setAttribute('font-size', '13');
                nameText.setAttribute('font-weight', 'bold');
                nameText.setAttribute('font-family', 'monospace');
                setTextWithLineBreaks(nameText, cls.label || cls.id);
                g.appendChild(nameText);

                // Divider line
                yOffset += 6;
                const div1 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                div1.setAttribute('x1', '0');
                div1.setAttribute('y1', yOffset);
                div1.setAttribute('x2', dim.width);
                div1.setAttribute('y2', yOffset);
                div1.setAttribute('stroke', 'var(--node-stroke)');
                div1.setAttribute('stroke-width', '1');
                g.appendChild(div1);

                // Fields section
                const fields = cls.members.filter(m => !m.isMethod);
                if (fields.length === 0) {
                    yOffset += CLS_MEMBER_HEIGHT;
                } else {
                    fields.forEach((f, fi) => {
                        yOffset += CLS_MEMBER_HEIGHT;
                        const memberIdx = cls.members.indexOf(f);
                        // Highlight rect for selected member row
                        if (clsSelectedClassId === cls.id && clsSelectedMemberIndex === memberIdx) {
                            const hlRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                            hlRect.setAttribute('x', '1');
                            hlRect.setAttribute('y', yOffset - CLS_MEMBER_HEIGHT + 4);
                            hlRect.setAttribute('width', dim.width - 2);
                            hlRect.setAttribute('height', CLS_MEMBER_HEIGHT);
                            hlRect.setAttribute('fill', 'var(--accent-color)');
                            hlRect.setAttribute('opacity', '0.2');
                            hlRect.setAttribute('rx', '2');
                            g.appendChild(hlRect);
                        }
                        const fText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        fText.setAttribute('x', CLS_BOX_PADDING);
                        fText.setAttribute('y', yOffset);
                        fText.setAttribute('fill', 'var(--node-text)');
                        fText.setAttribute('font-size', '12');
                        fText.setAttribute('font-family', 'monospace');
                        fText.textContent = clsFormatMember(f);
                        fText.classList.add('cls-member');
                        fText.setAttribute('data-class-id', cls.id);
                        fText.setAttribute('data-member-index', memberIdx);
                        g.appendChild(fText);
                    });
                }

                // Divider line
                yOffset += 6;
                const div2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                div2.setAttribute('x1', '0');
                div2.setAttribute('y1', yOffset);
                div2.setAttribute('x2', dim.width);
                div2.setAttribute('y2', yOffset);
                div2.setAttribute('stroke', 'var(--node-stroke)');
                div2.setAttribute('stroke-width', '1');
                g.appendChild(div2);

                // Methods section
                const methods = cls.members.filter(m => m.isMethod);
                if (methods.length === 0) {
                    yOffset += CLS_MEMBER_HEIGHT;
                } else {
                    methods.forEach((m, mi) => {
                        yOffset += CLS_MEMBER_HEIGHT;
                        const memberIdx = cls.members.indexOf(m);
                        // Highlight rect for selected member row
                        if (clsSelectedClassId === cls.id && clsSelectedMemberIndex === memberIdx) {
                            const hlRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                            hlRect.setAttribute('x', '1');
                            hlRect.setAttribute('y', yOffset - CLS_MEMBER_HEIGHT + 4);
                            hlRect.setAttribute('width', dim.width - 2);
                            hlRect.setAttribute('height', CLS_MEMBER_HEIGHT);
                            hlRect.setAttribute('fill', 'var(--accent-color)');
                            hlRect.setAttribute('opacity', '0.2');
                            hlRect.setAttribute('rx', '2');
                            g.appendChild(hlRect);
                        }
                        const mText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        mText.setAttribute('x', CLS_BOX_PADDING);
                        mText.setAttribute('y', yOffset);
                        mText.setAttribute('fill', 'var(--node-text)');
                        mText.setAttribute('font-size', '12');
                        mText.setAttribute('font-family', 'monospace');
                        mText.textContent = clsFormatMember(m);
                        mText.classList.add('cls-member');
                        mText.setAttribute('data-class-id', cls.id);
                        mText.setAttribute('data-member-index', memberIdx);
                        g.appendChild(mText);
                    });
                }

                // Connection grip points (N, S, E, W)
                const gripPositions = [
                    { cx: dim.width / 2, cy: 0 },          // North
                    { cx: dim.width / 2, cy: dim.height },  // South
                    { cx: 0, cy: dim.height / 2 },          // West
                    { cx: dim.width, cy: dim.height / 2 }   // East
                ];
                gripPositions.forEach(gp => {
                    const grip = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    grip.setAttribute('cx', gp.cx);
                    grip.setAttribute('cy', gp.cy);
                    grip.setAttribute('r', '3');
                    grip.classList.add('connection-grip', 'cls-grip');
                    grip.setAttribute('data-class-id', cls.id);
                    g.appendChild(grip);
                });

                nodesLayer.appendChild(g);
            });

            // ===== Render Relationships =====
            clsDiagram.relationships.forEach((rel, relIdx) => {
                const fromPos = clsClassPositions[rel.fromId];
                const toPos = clsClassPositions[rel.toId];
                if (!fromPos || !toPos) return;

                const fromCx = fromPos.x + (fromPos.width || CLS_BOX_MIN_WIDTH) / 2;
                const fromCy = fromPos.y + (fromPos.height || 100) / 2;
                const toCx = toPos.x + (toPos.width || CLS_BOX_MIN_WIDTH) / 2;
                const toCy = toPos.y + (toPos.height || 100) / 2;

                // Get best edge points based on direction
                const pts = clsGetEdgePoints(fromPos, toPos);

                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.classList.add('cls-relationship');
                g.setAttribute('data-rel-index', relIdx);

                // Line
                const clsEdgePathD = buildEdgePath(pts);
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                line.setAttribute('d', clsEdgePathD);
                line.setAttribute('fill', 'none');
                line.setAttribute('stroke', 'var(--edge-color)');
                line.setAttribute('stroke-width', clsSelectedRelIndex === relIdx ? '2.5' : '1.5');

                // Dashed for dashed link style (C# enum: ClassLinkStyle.Dashed)
                if (rel.linkStyle === 'Dashed') {
                    line.setAttribute('stroke-dasharray', '6,4');
                }
                g.appendChild(line);

                // Hit area (wider invisible path for clicking)
                const hitLine = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                hitLine.setAttribute('d', clsEdgePathD);
                hitLine.setAttribute('fill', 'none');
                hitLine.setAttribute('stroke', 'transparent');
                hitLine.setAttribute('stroke-width', '12');
                hitLine.style.cursor = 'pointer';
                g.appendChild(hitLine);

                // Arrow markers at endpoints — use Bézier tangent angles so arrowheads
                // align with the curve direction, not the straight line between endpoints.
                const tangents = edgeTangentAngles(pts);

                // Create a separate marker group that will be rendered on top of nodes
                // so markers aren't hidden behind class boxes (edges-layer is behind nodes-layer).
                const markerG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                markerG.classList.add('cls-relationship-markers');
                markerG.setAttribute('data-rel-index', relIdx);

                // Left end marker (at source/from side) - marker shape extends away from source
                // startAngle points away from source along curve; marker base extends along this direction
                clsDrawRelationEndMarker(markerG, pts.x1, pts.y1, tangents.startAngle, rel.leftEnd);

                // Right end marker (at target/to side) - marker shape extends away from target
                // endAngle points away from target along curve; marker base extends along this direction
                clsDrawRelationEndMarker(markerG, pts.x2, pts.y2, tangents.endAngle, rel.rightEnd);

                // Append marker group to nodes layer so it renders on top of class boxes
                nodesLayer.appendChild(markerG);

                // Label — offset perpendicular to line to avoid overlap with other relationship labels
                if (rel.label) {
                    const clsMid = edgeMidpoint(pts);
                    const midX = clsMid.x;
                    const midY = clsMid.y;
                    // Perpendicular offset to separate overlapping labels
                    const lineLen = Math.sqrt((pts.x2 - pts.x1) ** 2 + (pts.y2 - pts.y1) ** 2);
                    const perpX = lineLen > 0 ? -(pts.y2 - pts.y1) / lineLen : 0;
                    const perpY = lineLen > 0 ? (pts.x2 - pts.x1) / lineLen : 0;
                    const offsetDist = 14 * (relIdx % 2 === 0 ? 1 : -1); // alternate above/below
                    const labelX = midX + perpX * offsetDist;
                    const labelY = midY + perpY * offsetDist;
                    const labelBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    const labelText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    labelText.setAttribute('x', labelX);
                    labelText.setAttribute('y', labelY - 4);
                    labelText.setAttribute('text-anchor', 'middle');
                    labelText.setAttribute('fill', 'var(--node-text)');
                    labelText.setAttribute('font-size', '11');
                    labelText.setAttribute('font-style', 'italic');
                    setTextWithLineBreaks(labelText, rel.label);
                    // Background rect
                    const estWidth = estimateLabelWidth(rel.label, 7) + 8;
                    labelBg.setAttribute('x', labelX - estWidth / 2);
                    labelBg.setAttribute('y', labelY - 16);
                    labelBg.setAttribute('width', estWidth);
                    labelBg.setAttribute('height', '16');
                    labelBg.setAttribute('fill', 'var(--bg-color)');
                    labelBg.setAttribute('rx', '2');
                    g.appendChild(labelBg);
                    g.appendChild(labelText);
                }

                // Cardinality labels
                if (rel.fromCardinality) {
                    const cardText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    const dx = (pts.x2 - pts.x1) * 0.15;
                    const dy = (pts.y2 - pts.y1) * 0.15;
                    cardText.setAttribute('x', pts.x1 + dx);
                    cardText.setAttribute('y', pts.y1 + dy - 8);
                    cardText.setAttribute('text-anchor', 'middle');
                    cardText.setAttribute('fill', 'var(--node-text)');
                    cardText.setAttribute('font-size', '10');
                    cardText.textContent = rel.fromCardinality;
                    g.appendChild(cardText);
                }
                if (rel.toCardinality) {
                    const cardText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    const dx = (pts.x2 - pts.x1) * 0.85;
                    const dy = (pts.y2 - pts.y1) * 0.85;
                    cardText.setAttribute('x', pts.x1 + dx);
                    cardText.setAttribute('y', pts.y1 + dy - 8);
                    cardText.setAttribute('text-anchor', 'middle');
                    cardText.setAttribute('fill', 'var(--node-text)');
                    cardText.setAttribute('font-size', '10');
                    cardText.textContent = rel.toCardinality;
                    g.appendChild(cardText);
                }

                edgesLayer.appendChild(g);
            });

            // ===== Render Notes =====
            clsDiagram.notes.forEach(note => {
                const targetCls = note.forClass ? clsClassPositions[note.forClass] : null;
                const noteX = targetCls ? targetCls.x + (targetCls.width || CLS_BOX_MIN_WIDTH) + 20 : CLS_LEFT_MARGIN;
                const noteY = targetCls ? targetCls.y : CLS_TOP_MARGIN;

                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.setAttribute('transform', `translate(${noteX}, ${noteY})`);

                const noteRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                noteRect.setAttribute('x', '0');
                noteRect.setAttribute('y', '0');
                noteRect.setAttribute('width', '140');
                noteRect.setAttribute('height', '40');
                noteRect.setAttribute('fill', '#ffffcc');
                noteRect.setAttribute('stroke', '#cccc00');
                noteRect.setAttribute('rx', '2');
                g.appendChild(noteRect);

                const noteText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                noteText.setAttribute('x', '8');
                noteText.setAttribute('y', '24');
                noteText.setAttribute('fill', '#333');
                noteText.setAttribute('font-size', '11');
                const displayNoteText = note.text.length > 25 ? note.text.substring(0, 25) + '...' : note.text;
                setTextWithLineBreaks(noteText, displayNoteText);
                g.appendChild(noteText);

                subgraphsLayer.appendChild(g);
            });

            updateMinimap();
            updateCanvasAndZoomLimits();
        }

        function clsGetEdgePoints(fromPos, toPos) {
            const fw = fromPos.width || CLS_BOX_MIN_WIDTH;
            const fh = fromPos.height || 100;
            const tw = toPos.width || CLS_BOX_MIN_WIDTH;
            const th = toPos.height || 100;

            const fromCx = fromPos.x + fw / 2;
            const fromCy = fromPos.y + fh / 2;
            const toCx = toPos.x + tw / 2;
            const toCy = toPos.y + th / 2;

            const dx = toCx - fromCx;
            const dy = toCy - fromCy;

            let x1, y1, x2, y2, fromDir, toDir;

            // Determine best exit/entry ports based on direction
            if (Math.abs(dy) > Math.abs(dx)) {
                // Predominantly vertical
                if (dy > 0) {
                    // Target is below
                    x1 = fromCx; y1 = fromPos.y + fh; fromDir = 'S';
                    x2 = toCx; y2 = toPos.y; toDir = 'N';
                } else {
                    // Target is above
                    x1 = fromCx; y1 = fromPos.y; fromDir = 'N';
                    x2 = toCx; y2 = toPos.y + th; toDir = 'S';
                }
            } else {
                // Predominantly horizontal
                if (dx > 0) {
                    // Target is to the right
                    x1 = fromPos.x + fw; y1 = fromCy; fromDir = 'E';
                    x2 = toPos.x; y2 = toCy; toDir = 'W';
                } else {
                    // Target is to the left
                    x1 = fromPos.x; y1 = fromCy; fromDir = 'W';
                    x2 = toPos.x + tw; y2 = toCy; toDir = 'E';
                }
            }

            return { x1, y1, x2, y2, fromDir, toDir };
        }

        function clsDrawRelationEndMarker(g, px, py, angle, endType) {
            const size = 10;
            switch (endType) {
                case 'Inheritance': {
                    // Hollow triangle arrowhead (inheritance/realization)
                    const p1x = px + size * Math.cos(angle + 0.5);
                    const p1y = py + size * Math.sin(angle + 0.5);
                    const p2x = px + size * Math.cos(angle - 0.5);
                    const p2y = py + size * Math.sin(angle - 0.5);
                    const tri = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                    tri.setAttribute('points', `${px},${py} ${p1x},${p1y} ${p2x},${p2y}`);
                    tri.setAttribute('fill', 'var(--bg-color)');
                    tri.setAttribute('stroke', 'var(--edge-color)');
                    tri.setAttribute('stroke-width', '1.5');
                    g.appendChild(tri);
                    break;
                }
                case 'Arrow': {
                    // Open chevron arrowhead (association/dependency)
                    const p1x = px + size * Math.cos(angle + 0.5);
                    const p1y = py + size * Math.sin(angle + 0.5);
                    const p2x = px + size * Math.cos(angle - 0.5);
                    const p2y = py + size * Math.sin(angle - 0.5);
                    const chevron = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
                    chevron.setAttribute('points', `${p1x},${p1y} ${px},${py} ${p2x},${p2y}`);
                    chevron.setAttribute('fill', 'none');
                    chevron.setAttribute('stroke', 'var(--edge-color)');
                    chevron.setAttribute('stroke-width', '1.5');
                    g.appendChild(chevron);
                    break;
                }
                case 'Composition': {
                    // Filled diamond (composition)
                    const tipX = px;
                    const tipY = py;
                    const midX = px + size * Math.cos(angle);
                    const midY = py + size * Math.sin(angle);
                    const backX = px + size * 2 * Math.cos(angle);
                    const backY = py + size * 2 * Math.sin(angle);
                    const leftX = midX + (size * 0.5) * Math.cos(angle + Math.PI / 2);
                    const leftY = midY + (size * 0.5) * Math.sin(angle + Math.PI / 2);
                    const rightX = midX + (size * 0.5) * Math.cos(angle - Math.PI / 2);
                    const rightY = midY + (size * 0.5) * Math.sin(angle - Math.PI / 2);
                    const diamond = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                    diamond.setAttribute('points', `${tipX},${tipY} ${leftX},${leftY} ${backX},${backY} ${rightX},${rightY}`);
                    diamond.setAttribute('fill', 'var(--edge-color)');
                    diamond.setAttribute('stroke', 'var(--edge-color)');
                    diamond.setAttribute('stroke-width', '1');
                    g.appendChild(diamond);
                    break;
                }
                case 'Aggregation': {
                    // Open diamond (aggregation)
                    const tipX = px;
                    const tipY = py;
                    const midX = px + size * Math.cos(angle);
                    const midY = py + size * Math.sin(angle);
                    const backX = px + size * 2 * Math.cos(angle);
                    const backY = py + size * 2 * Math.sin(angle);
                    const leftX = midX + (size * 0.5) * Math.cos(angle + Math.PI / 2);
                    const leftY = midY + (size * 0.5) * Math.sin(angle + Math.PI / 2);
                    const rightX = midX + (size * 0.5) * Math.cos(angle - Math.PI / 2);
                    const rightY = midY + (size * 0.5) * Math.sin(angle - Math.PI / 2);
                    const diamond = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                    diamond.setAttribute('points', `${tipX},${tipY} ${leftX},${leftY} ${backX},${backY} ${rightX},${rightY}`);
                    diamond.setAttribute('fill', 'var(--bg-color)');
                    diamond.setAttribute('stroke', 'var(--edge-color)');
                    diamond.setAttribute('stroke-width', '1.5');
                    g.appendChild(diamond);
                    break;
                }
                case 'Lollipop': {
                    // Lollipop / interface marker (circle at end of line)
                    const lolli = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    lolli.setAttribute('cx', px + 8 * Math.cos(angle));
                    lolli.setAttribute('cy', py + 8 * Math.sin(angle));
                    lolli.setAttribute('r', '6');
                    lolli.setAttribute('fill', 'var(--bg-color)');
                    lolli.setAttribute('stroke', 'var(--edge-color)');
                    lolli.setAttribute('stroke-width', '1.5');
                    g.appendChild(lolli);
                    break;
                }
                case 'Star': {
                    // Star marker
                    const star = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    star.setAttribute('cx', px + 6 * Math.cos(angle));
                    star.setAttribute('cy', py + 6 * Math.sin(angle));
                    star.setAttribute('r', '4');
                    star.setAttribute('fill', 'var(--edge-color)');
                    g.appendChild(star);
                    break;
                }
                case 'None':
                default:
                    break;
            }
        }

        // ===== Class Diagram Interactions =====

        // Click to select class
        svg.addEventListener('mousedown', function(e) {
            if (currentDiagramType !== 'class') return;
            if (!clsDiagram) return;

            const classGroup = e.target.closest('.cls-class');
            const relGroup = e.target.closest('.cls-relationship');
            const gripEl = e.target.closest('.cls-grip');

            // Start drawing relationship from grip
            if (gripEl) {
                const classId = gripEl.getAttribute('data-class-id');
                clsDrawingRelFrom = classId;
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            // Handle toolbar "Add Relationship" two-click mode
            if (clsPickingRelSource) {
                if (classGroup) {
                    const classId = classGroup.getAttribute('data-class-id');
                    if (!clsPickingRelFrom) {
                        // First click: pick source — highlight it, wait for second click
                        clsPickingRelFrom = classId;
                        clsSelectedClassId = classId;
                        renderClassDiagram();
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    } else if (classId !== clsPickingRelFrom) {
                        // Second click: pick target — create relationship
                        postMessage({
                            type: 'cls_relationshipCreated',
                            fromId: clsPickingRelFrom,
                            toId: classId,
                            leftEnd: 'None',
                            rightEnd: 'Arrow',
                            linkStyle: 'Solid'
                        });
                        clsPickingRelSource = false;
                        clsPickingRelFrom = null;
                        document.body.style.cursor = '';
                        document.getElementById('tb-cls-add-relation').classList.remove('active');
                        tempEdge.style.display = 'none';
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    } else {
                        // Clicked source again: deselect source, wait for new source
                        clsPickingRelFrom = null;
                        tempEdge.style.display = 'none';
                        clsSelectedClassId = null;
                        renderClassDiagram();
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    }
                } else {
                    // Clicked empty space: cancel pick mode
                    clsPickingRelSource = false;
                    clsPickingRelFrom = null;
                    document.body.style.cursor = '';
                    document.getElementById('tb-cls-add-relation').classList.remove('active');
                    tempEdge.style.display = 'none';
                }
            }

            const memberEl = e.target.closest('.cls-member');
            if (memberEl && classGroup) {
                // Clicked a member row — highlight just that row (not the whole class box)
                const classId = classGroup.getAttribute('data-class-id');
                const memberIdx = parseInt(memberEl.getAttribute('data-member-index'), 10);
                clsSelectedClassId = classId;
                clsSelectedMemberIndex = memberIdx;
                clsSelectedRelIndex = -1;
                renderClassDiagram();
                clsShowMemberPropertyPanel(classId, memberIdx);
                e.stopPropagation();
            } else if (classGroup) {
                const classId = classGroup.getAttribute('data-class-id');
                clsSelectedClassId = classId;
                clsSelectedMemberIndex = -1;
                clsSelectedRelIndex = -1;

                // Start dragging
                const pos = clsClassPositions[classId];
                if (pos) {
                    const svgPt = getSVGPoint(e);
                    clsDraggingClassId = classId;
                    clsDragOffsetX = svgPt.x - pos.x;
                    clsDragOffsetY = svgPt.y - pos.y;
                }

                renderClassDiagram();
                clsShowClassPropertyPanel(classId);
                e.stopPropagation();
            } else if (relGroup) {
                const relIdx = parseInt(relGroup.getAttribute('data-rel-index'), 10);
                clsSelectedRelIndex = relIdx;
                clsSelectedClassId = null;
                clsSelectedMemberIndex = -1;
                renderClassDiagram();
                clsShowRelPropertyPanel(relIdx);
                e.stopPropagation();
            } else if (clsDiagram) {
                // Clicked on empty space
                clsSelectedClassId = null;
                clsSelectedMemberIndex = -1;
                clsSelectedRelIndex = -1;
                propertyPanel.classList.remove('visible');
                renderClassDiagram();
            }
        });

        // Drag class boxes
        svg.addEventListener('mousemove', function(e) {
            if (currentDiagramType !== 'class') return;
            if (!clsDiagram) return;

            if (clsDraggingClassId) {
                const svgPt = getSVGPoint(e);
                const pos = clsClassPositions[clsDraggingClassId];
                if (pos) {
                    pos.x = Math.round((svgPt.x - clsDragOffsetX) / SNAP_GRID) * SNAP_GRID;
                    pos.y = Math.round((svgPt.y - clsDragOffsetY) / SNAP_GRID) * SNAP_GRID;
                    renderClassDiagram();
                }
            }

            if (clsDrawingRelFrom) {
                // Show temp edge line for grip-drag mode
                const fromPos = clsClassPositions[clsDrawingRelFrom];
                if (fromPos) {
                    const svgPt = getSVGPoint(e);
                    tempEdge.setAttribute('x1', fromPos.x + (fromPos.width || CLS_BOX_MIN_WIDTH) / 2);
                    tempEdge.setAttribute('y1', fromPos.y + (fromPos.height || 100) / 2);
                    tempEdge.setAttribute('x2', svgPt.x);
                    tempEdge.setAttribute('y2', svgPt.y);
                    tempEdge.style.display = '';
                }
            }

            // Show temp edge line for toolbar two-click workflow
            if (clsPickingRelFrom) {
                const fromPos = clsClassPositions[clsPickingRelFrom];
                if (fromPos) {
                    const svgPt = getSVGPoint(e);
                    tempEdge.setAttribute('x1', fromPos.x + (fromPos.width || CLS_BOX_MIN_WIDTH) / 2);
                    tempEdge.setAttribute('y1', fromPos.y + (fromPos.height || 100) / 2);
                    tempEdge.setAttribute('x2', svgPt.x);
                    tempEdge.setAttribute('y2', svgPt.y);
                    tempEdge.style.display = '';
                }
            }
        });

        svg.addEventListener('mouseup', function(e) {
            if (currentDiagramType !== 'class') return;
            if (!clsDiagram) return;

            if (clsDraggingClassId) {
                const pos = clsClassPositions[clsDraggingClassId];
                if (pos) {
                    postMessage({
                        type: 'cls_classMoved',
                        classId: clsDraggingClassId,
                        x: pos.x,
                        y: pos.y
                    });
                }
                clsDraggingClassId = null;
            }

            if (clsDrawingRelFrom) {
                tempEdge.style.display = 'none';
                const targetGroup = e.target.closest('.cls-class');
                if (targetGroup) {
                    const toId = targetGroup.getAttribute('data-class-id');
                    if (toId && toId !== clsDrawingRelFrom) {
                        postMessage({
                            type: 'cls_relationshipCreated',
                            fromId: clsDrawingRelFrom,
                            toId: toId,
                            leftEnd: 'None',
                            rightEnd: 'Arrow',
                            linkStyle: 'Solid'
                        });
                    }
                }
                clsDrawingRelFrom = null;
                // Reset pick mode after relationship created
                clsPickingRelSource = false;
                clsPickingRelFrom = null;
                document.body.style.cursor = '';
            }
        });

        // Double-click to edit class name
        svg.addEventListener('dblclick', function(e) {
            if (currentDiagramType !== 'class') return;
            if (!clsDiagram) return;

            const classGroup = e.target.closest('.cls-class');
            const memberEl = e.target.closest('.cls-member');

            if (memberEl) {
                const classId = memberEl.getAttribute('data-class-id');
                const memberIdx = parseInt(memberEl.getAttribute('data-member-index'), 10);
                const cls = clsDiagram.classes.find(c => c.id === classId);
                if (cls && memberIdx >= 0 && memberIdx < cls.members.length) {
                    clsShowMemberPropertyPanel(classId, memberIdx);
                }
                e.stopPropagation();
            } else if (classGroup) {
                const classId = classGroup.getAttribute('data-class-id');
                const cls = clsDiagram.classes.find(c => c.id === classId);
                if (cls) clsShowClassPropertyPanel(classId);
                e.stopPropagation();
            }
        });

        function clsStartClassNameInlineEdit(classId, cls, event) {
            const pos = clsClassPositions[classId];
            if (!pos) return;

            const input = document.createElement('input');
            input.type = 'text';
            input.value = cls.label || cls.id;
            input.className = 'inline-edit-input';

            const transform = panzoomInstance ? panzoomInstance.getTransform() : { x: 0, y: 0, scale: 1 };
            const rect = canvasContainer.getBoundingClientRect();
            const screenX = pos.x * transform.scale + transform.x + rect.left;
            const screenY = pos.y * transform.scale + transform.y + rect.top;

            input.style.position = 'fixed';
            input.style.left = screenX + 'px';
            input.style.top = screenY + 'px';
            input.style.width = (pos.width || CLS_BOX_MIN_WIDTH) * transform.scale + 'px';
            input.style.fontSize = (13 * transform.scale) + 'px';
            input.style.fontFamily = 'monospace';
            input.style.fontWeight = 'bold';
            input.style.textAlign = 'center';
            input.style.zIndex = '10000';
            input.style.padding = '2px 4px';
            input.style.border = '2px solid var(--accent-color)';
            input.style.background = 'var(--bg-color)';
            input.style.color = 'var(--node-text)';

            document.body.appendChild(input);
            input.focus();
            input.select();

            function commit() {
                const newLabel = input.value.trim();
                if (newLabel && newLabel !== (cls.label || cls.id)) {
                    postMessage({ type: 'cls_classEdited', classId: classId, label: newLabel });
                }
                input.remove();
            }

            input.addEventListener('blur', commit);
            input.addEventListener('keydown', function(ke) {
                if (ke.key === 'Enter') { ke.preventDefault(); commit(); }
                if (ke.key === 'Escape') { input.remove(); }
            });
        }

        function clsShowMemberPropertyPanel(classId, memberIdx) {
            const cls = clsDiagram.classes.find(c => c.id === classId);
            if (!cls || memberIdx < 0 || memberIdx >= cls.members.length) return;
            const member = cls.members[memberIdx];

            const visOptions = ['Public', 'Private', 'Protected', 'Package'];
            const visSymbols = { 'Public': '+', 'Private': '-', 'Protected': '#', 'Package': '~' };
            const visOpts = visOptions.map(v => `<option value="${v}" ${(member.visibility || '') === v ? 'selected' : ''}>${visSymbols[v]} ${v}</option>`).join('');

            const classifierOpts = `
                <option value="None" ${(!member.classifier || member.classifier === 'None') ? 'selected' : ''}>(none)</option>
                <option value="Abstract" ${member.classifier === 'Abstract' ? 'selected' : ''}>Abstract *</option>
                <option value="Static" ${member.classifier === 'Static' ? 'selected' : ''}>Static $</option>
            `;

            propPanelTitle.textContent = member.isMethod ? 'Method' : 'Field';
            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Class</div>
                    <input class="property-input" value="${escapeHtml(cls.label || cls.id)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Visibility</div>
                    <select class="property-input" id="cls-mem-vis">${visOpts}</select>
                </div>
                <div class="property-row">
                    <div class="property-label">Name</div>
                    <input class="property-input" id="cls-mem-name" value="${escapeHtml(member.name || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">${member.isMethod ? 'Return Type' : 'Type'}</div>
                    <input class="property-input" id="cls-mem-type" value="${escapeHtml(member.type || '')}" placeholder="e.g. String" />
                </div>
                ${member.isMethod ? `<div class="property-row">
                    <div class="property-label">Parameters</div>
                    <input class="property-input" id="cls-mem-params" value="${escapeHtml(member.parameters || '')}" placeholder="e.g. x int, y int" />
                </div>` : ''}
                <div class="property-row">
                    <div class="property-label">Classifier</div>
                    <select class="property-input" id="cls-mem-classifier">${classifierOpts}</select>
                </div>
            `;

            // Helper to rebuild rawText from structured fields and send edit
            function commitMemberEdit() {
                const vis = document.getElementById('cls-mem-vis').value;
                const name = document.getElementById('cls-mem-name').value.trim();
                const type = document.getElementById('cls-mem-type').value.trim();
                const params = member.isMethod ? (document.getElementById('cls-mem-params')?.value || '') : '';
                const classifier = document.getElementById('cls-mem-classifier').value;

                // Build rawText in Mermaid format
                let prefix = visSymbols[vis] || '+';
                let rawText = prefix;
                if (member.isMethod) {
                    if (type) rawText += type + ' ';
                    rawText += name + '(' + params + ')';
                } else {
                    if (type) rawText += type + ' ';
                    rawText += name;
                }
                if (classifier === 'Abstract') rawText += '*';
                else if (classifier === 'Static') rawText += '$';

                postMessage({ type: 'cls_memberEdited', classId: classId, memberIndex: memberIdx, rawText: rawText });
            }

            document.getElementById('cls-mem-vis').addEventListener('change', commitMemberEdit);
            document.getElementById('cls-mem-name').addEventListener('change', commitMemberEdit);
            document.getElementById('cls-mem-type').addEventListener('change', commitMemberEdit);
            if (member.isMethod && document.getElementById('cls-mem-params')) {
                document.getElementById('cls-mem-params').addEventListener('change', commitMemberEdit);
            }
            document.getElementById('cls-mem-classifier').addEventListener('change', commitMemberEdit);

            propertyPanel.classList.add('visible');
        }

        // Property panel for classes
        function clsShowClassPropertyPanel(classId) {
            const cls = clsDiagram.classes.find(c => c.id === classId);
            if (!cls) return;

            propPanelTitle.textContent = 'Class: ' + (cls.label || cls.id);
            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Name</div>
                    <input class="property-input" id="cls-name" value="${escapeHtml(cls.label || cls.id)}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Annotation</div>
                    <select class="property-input" id="cls-annotation">
                        <option value="" ${!cls.annotation ? 'selected' : ''}>(none)</option>
                        <option value="interface" ${cls.annotation === 'interface' ? 'selected' : ''}>&#60;&#60;interface&#62;&#62;</option>
                        <option value="abstract" ${cls.annotation === 'abstract' ? 'selected' : ''}>&#60;&#60;abstract&#62;&#62;</option>
                        <option value="service" ${cls.annotation === 'service' ? 'selected' : ''}>&#60;&#60;service&#62;&#62;</option>
                        <option value="enumeration" ${cls.annotation === 'enumeration' ? 'selected' : ''}>&#60;&#60;enumeration&#62;&#62;</option>
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Generic Type</div>
                    <input class="property-input" id="cls-generic" value="${escapeHtml(cls.genericType || '')}" placeholder="e.g. List~T~" />
                </div>
                <div class="property-row" style="margin-top:8px">
                    <button class="property-btn" id="cls-add-field" style="flex:1;padding:4px 8px;cursor:pointer;">+ Field</button>
                    <button class="property-btn" id="cls-add-method" style="flex:1;padding:4px 8px;cursor:pointer;margin-left:4px;">+ Method</button>
                </div>
            `;

            document.getElementById('cls-name').addEventListener('change', function() {
                postMessage({ type: 'cls_classEdited', classId: classId, label: this.value });
            });
            document.getElementById('cls-annotation').addEventListener('change', function() {
                postMessage({ type: 'cls_classEdited', classId: classId, annotation: this.value || null });
            });
            document.getElementById('cls-generic').addEventListener('change', function() {
                postMessage({ type: 'cls_classEdited', classId: classId, genericType: this.value || null });
            });
            document.getElementById('cls-add-field').addEventListener('click', function() {
                postMessage({ type: 'cls_memberAdded', classId: classId, rawText: '+newField' });
            });
            document.getElementById('cls-add-method').addEventListener('click', function() {
                postMessage({ type: 'cls_memberAdded', classId: classId, rawText: '+newMethod()' });
            });

            propertyPanel.classList.add('visible');
        }

        // Property panel for relationships
        function clsShowRelPropertyPanel(relIdx) {
            const rel = clsDiagram.relationships[relIdx];
            if (!rel) return;

            propPanelTitle.textContent = 'Relationship';
            const propContent = propertyPanel.querySelector('.property-panel-body');
            const classOpts = clsDiagram.classes.map(c => `<option value="${escapeHtml(c.id)}">${escapeHtml(c.label || c.id)}</option>`).join('');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">From</div>
                    <select class="property-select" id="cls-rel-from">${classOpts}</select>
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <select class="property-select" id="cls-rel-to">${classOpts}</select>
                </div>
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-input" id="cls-rel-type">
                        <option value="inheritance" ${(rel.leftEnd === 'Inheritance' || rel.rightEnd === 'Inheritance') && rel.linkStyle === 'Solid' ? 'selected' : ''}>Inheritance (<|--)</option>
                        <option value="realization" ${(rel.leftEnd === 'Inheritance' || rel.rightEnd === 'Inheritance') && rel.linkStyle === 'Dashed' ? 'selected' : ''}>Realization (<|..)</option>
                        <option value="composition" ${rel.leftEnd === 'Composition' || rel.rightEnd === 'Composition' ? 'selected' : ''}>Composition (*--)</option>
                        <option value="aggregation" ${rel.leftEnd === 'Aggregation' || rel.rightEnd === 'Aggregation' ? 'selected' : ''}>Aggregation (o--)</option>
                        <option value="association" ${(rel.leftEnd === 'Arrow' || rel.rightEnd === 'Arrow') && rel.linkStyle === 'Solid' ? 'selected' : ''}>Association (-->)</option>
                        <option value="dependency" ${(rel.leftEnd === 'Arrow' || rel.rightEnd === 'Arrow') && rel.linkStyle === 'Dashed' ? 'selected' : ''}>Dependency (..>)</option>
                        <option value="link-solid" ${rel.leftEnd === 'None' && rel.rightEnd === 'None' && rel.linkStyle === 'Solid' ? 'selected' : ''}>Link (--)</option>
                        <option value="link-dashed" ${rel.leftEnd === 'None' && rel.rightEnd === 'None' && rel.linkStyle === 'Dashed' ? 'selected' : ''}>Link (..)</option>
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Label</div>
                    <input class="property-input" id="cls-rel-label" value="${escapeHtml(rel.label || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">From Card.</div>
                    <input class="property-input" id="cls-rel-from-card" value="${escapeHtml(rel.fromCardinality || '')}" placeholder='e.g. "1"' />
                </div>
                <div class="property-row">
                    <div class="property-label">To Card.</div>
                    <input class="property-input" id="cls-rel-to-card" value="${escapeHtml(rel.toCardinality || '')}" placeholder='e.g. "*"' />
                </div>
            `;

            // Set selected values for From/To dropdowns
            document.getElementById('cls-rel-from').value = rel.fromId;
            document.getElementById('cls-rel-to').value = rel.toId;
            document.getElementById('cls-rel-from').addEventListener('change', function() {
                rel.fromId = this.value;
                renderClassDiagram();
                postMessage({ type: 'cls_relationshipEdited', relationshipIndex: relIdx, fromId: this.value });
            });
            document.getElementById('cls-rel-to').addEventListener('change', function() {
                rel.toId = this.value;
                renderClassDiagram();
                postMessage({ type: 'cls_relationshipEdited', relationshipIndex: relIdx, toId: this.value });
            });
            document.getElementById('cls-rel-type').addEventListener('change', function() {
                const typeMap = {
                    inheritance: { leftEnd: 'None', rightEnd: 'Inheritance', linkStyle: 'Solid' },
                    realization: { leftEnd: 'None', rightEnd: 'Inheritance', linkStyle: 'Dashed' },
                    composition: { leftEnd: 'None', rightEnd: 'Composition', linkStyle: 'Solid' },
                    aggregation: { leftEnd: 'None', rightEnd: 'Aggregation', linkStyle: 'Solid' },
                    association: { leftEnd: 'None', rightEnd: 'Arrow', linkStyle: 'Solid' },
                    dependency: { leftEnd: 'None', rightEnd: 'Arrow', linkStyle: 'Dashed' },
                    'link-solid': { leftEnd: 'None', rightEnd: 'None', linkStyle: 'Solid' },
                    'link-dashed': { leftEnd: 'None', rightEnd: 'None', linkStyle: 'Dashed' }
                };
                const t = typeMap[this.value] || typeMap.association;
                rel.leftEnd = t.leftEnd;
                rel.rightEnd = t.rightEnd;
                rel.linkStyle = t.linkStyle;
                renderClassDiagram();
                postMessage({ type: 'cls_relationshipEdited', relationshipIndex: relIdx, leftEnd: t.leftEnd, rightEnd: t.rightEnd, linkStyle: t.linkStyle });
            });
            document.getElementById('cls-rel-label').addEventListener('change', function() {
                rel.label = this.value || null;
                renderClassDiagram();
                postMessage({ type: 'cls_relationshipEdited', relationshipIndex: relIdx, label: this.value });
            });
            document.getElementById('cls-rel-from-card').addEventListener('change', function() {
                rel.fromCardinality = this.value || null;
                renderClassDiagram();
                postMessage({ type: 'cls_relationshipEdited', relationshipIndex: relIdx, fromCardinality: this.value });
            });
            document.getElementById('cls-rel-to-card').addEventListener('change', function() {
                rel.toCardinality = this.value || null;
                renderClassDiagram();
                postMessage({ type: 'cls_relationshipEdited', relationshipIndex: relIdx, toCardinality: this.value });
            });

            propertyPanel.classList.add('visible');
        }

        // ===== Class Diagram Context Menu =====

        svg.addEventListener('contextmenu', function(e) {
            if (currentDiagramType !== 'class') return;
            if (!clsDiagram) return;
            e.preventDefault();
            e.stopPropagation();

            const classGroup = e.target.closest('.cls-class');
            const relGroup = e.target.closest('.cls-relationship');
            const memberEl = e.target.closest('.cls-member');

            contextMenu.innerHTML = '';

            if (memberEl) {
                const classId = memberEl.getAttribute('data-class-id');
                const memberIdx = parseInt(memberEl.getAttribute('data-member-index'), 10);
                const cls = clsDiagram.classes.find(c => c.id === classId);
                const memberCount = cls ? cls.members.length : 0;

                addClsContextMenuItem('Edit Member', () => {
                    clsShowMemberPropertyPanel(classId, memberIdx);
                });
                if (memberIdx > 0) {
                    addClsContextMenuItem('\u2191 Move Up', () => {
                        postMessage({ type: 'cls_memberMoved', classId: classId, memberIndex: memberIdx, direction: 'up' });
                    });
                }
                if (memberIdx < memberCount - 1) {
                    addClsContextMenuItem('\u2193 Move Down', () => {
                        postMessage({ type: 'cls_memberMoved', classId: classId, memberIndex: memberIdx, direction: 'down' });
                    });
                }
                addClsContextMenuSeparator();
                addClsContextMenuItem('Delete Member', () => {
                    postMessage({ type: 'cls_memberDeleted', classId: classId, memberIndex: memberIdx });
                });
                // Member-level copy/paste
                addClsContextMenuSeparator();
                addClsContextMenuItem('\u{1F4CB} Copy Member', () => {
                    clsSelectedClassId = classId;
                    clsSelectedMemberIndex = memberIdx;
                    copySelected();
                });
                if (clipboard && clipboard.diagramType === 'class' && clipboard.type === 'cls_member') {
                    addClsContextMenuItem('\u{1F4CB} Paste Member', () => {
                        clsSelectedClassId = classId;
                        pasteClassMember();
                    });
                }
            } else if (classGroup) {
                const classId = classGroup.getAttribute('data-class-id');
                clsSelectedClassId = classId;
                clsSelectedMemberIndex = -1;

                addClsContextMenuItem('Edit Class', () => {
                    clsShowClassPropertyPanel(classId);
                });
                addClsContextMenuItem('Add Field', () => {
                    postMessage({ type: 'cls_memberAdded', classId: classId, rawText: '+newField' });
                });
                addClsContextMenuItem('Add Method', () => {
                    postMessage({ type: 'cls_memberAdded', classId: classId, rawText: '+newMethod()' });
                });
                addClsContextMenuSeparator();
                addClsContextMenuItem('Add Relationship', () => {
                    clsDrawingRelFrom = classId;
                    svg.classList.add('edge-drawing-active');
                });
                addClsContextMenuSeparator();
                addClsContextMenuItem('Delete Class', () => {
                    postMessage({ type: 'cls_classDeleted', classId: classId });
                });
            } else if (relGroup) {
                const relIdx = parseInt(relGroup.getAttribute('data-rel-index'), 10);
                clsSelectedRelIndex = relIdx;

                addClsContextMenuItem('Delete Relationship', () => {
                    postMessage({ type: 'cls_relationshipDeleted', relationshipIndex: relIdx });
                });
            } else {
                // Empty space context menu
                addClsContextMenuItem('Add Class', () => {
                    const svgPt = getSVGPoint(e);
                    const newId = 'NewClass' + (clsDiagram.classes.length + 1);
                    clsClassPositions[newId] = {
                        x: Math.round(svgPt.x / SNAP_GRID) * SNAP_GRID,
                        y: Math.round(svgPt.y / SNAP_GRID) * SNAP_GRID
                    };
                    postMessage({ type: 'cls_classCreated', classId: newId });
                });
            }

            // Copy/Paste for class diagrams (class-level and entity-level)
            if (clsSelectedClassId && clsSelectedMemberIndex < 0) {
                addClsContextMenuSeparator();
                addClsContextMenuItem('\u{1F4CB} Copy Class', () => { copySelected(); });
            }
            if (clipboard && clipboard.diagramType === 'class' && clipboard.type === 'cls_class') {
                addClsContextMenuItem('\u{1F4CB} Paste Class', () => { pasteClipboard(0, 0); });
            }
            // Show "Paste Member" on class right-click when clipboard has a member
            if (clsSelectedClassId && clipboard && clipboard.diagramType === 'class' && clipboard.type === 'cls_member') {
                if (clsSelectedMemberIndex < 0) addClsContextMenuSeparator();
                addClsContextMenuItem('\u{1F4CB} Paste Member', () => { pasteClassMember(); });
            }

            positionContextMenu(contextMenu, e.clientX, e.clientY);
            renderClassDiagram();
        });

        function addClsContextMenuItem(label, onClick) {
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

        function addClsContextMenuSeparator() {
            const sep = document.createElement('div');
            sep.classList.add('context-menu-separator');
            contextMenu.appendChild(sep);
        }

        // Keyboard shortcuts for class diagram elements
        document.addEventListener('keydown', function(e) {
            if (currentDiagramType !== 'class') return;
            if (!clsDiagram) return;
            if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'SELECT')) return;

            if (e.key === 'Delete' || e.key === 'Backspace') {
                if (clsSelectedClassId) {
                    postMessage({ type: 'cls_classDeleted', classId: clsSelectedClassId });
                    clsSelectedClassId = null;
                    e.preventDefault();
                } else if (clsSelectedRelIndex >= 0) {
                    postMessage({ type: 'cls_relationshipDeleted', relationshipIndex: clsSelectedRelIndex });
                    clsSelectedRelIndex = -1;
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

        // ===== Class diagram toolbar buttons =====
        document.getElementById('tb-cls-add-class').addEventListener('click', function() {
            if (!clsDiagram) return;
            postMessage({ type: 'cls_classCreated', classId: 'NewClass' + (clsDiagram.classes.length + 1) });
        });

        let clsPickingRelSource = false; // true when toolbar Add Relationship is waiting for source click
        let clsPickingRelFrom = null; // source class id when picking target
        document.getElementById('tb-cls-add-relation').addEventListener('click', function() {
            if (!clsDiagram || clsDiagram.classes.length < 2) return;
            clsPickingRelSource = true;
            clsPickingRelFrom = null;
            document.body.style.cursor = 'crosshair';
            this.classList.add('active');
        });

        document.getElementById('tb-cls-auto-layout').addEventListener('click', function() {
            clsDagreAutoLayout();
            renderClassDiagram();
            const positions = [];
            clsDiagram.classes.forEach(cls => {
                const pos = clsClassPositions[cls.id];
                if (pos) {
                    positions.push({ classId: cls.id, x: pos.x, y: pos.y, width: pos.width || 0, height: pos.height || 0 });
                }
            });
            postMessage({ type: 'cls_autoLayoutComplete', positions: positions });
        });

