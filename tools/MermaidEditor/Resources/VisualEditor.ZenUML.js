        // ========== ZenUML Visual Editor ==========
        // Based on the Sequence Diagram visual editor, adapted for ZenUML syntax.
        // ZenUML uses code-like syntax: participants with annotators (@Actor, @Boundary, etc.),
        // method calls (A->B.method() { }), return statements, and fragments (if/else, while, try/catch).

        let zuDiagram = null; // When non-null, we're in ZenUML mode
        let zuSelectedParticipantIdx = -1;
        let zuSelectedElementIdx = -1; // index into flat elements array
        let zuDraggingParticipantIdx = -1;
        let zuDragStartX = 0;
        let zuDragParticipantStartIdx = -1;
        const ZU_PARTICIPANT_WIDTH = 120;
        const ZU_PARTICIPANT_HEIGHT = 40;
        const ZU_PARTICIPANT_GAP = 180;
        const ZU_MESSAGE_SPACING = 50;
        const ZU_TOP_MARGIN = 60;
        const ZU_LEFT_MARGIN = 100;
        const ZU_LIFELINE_START_Y = ZU_TOP_MARGIN + ZU_PARTICIPANT_HEIGHT + 20;

        // Annotator icon labels
        const ZU_ANNOTATOR_ICONS = {
            'None': '',
            'Actor': '\u{1F464}',
            'Boundary': '\u{1F5D4}',
            'Control': '\u{2699}',
            'Entity': '\u{1F4BE}',
            'Database': '\u{1F5C3}'
        };

        const ZU_ANNOTATOR_LABELS = {
            'None': 'Participant',
            'Actor': '@Actor',
            'Boundary': '@Boundary',
            'Control': '@Control',
            'Entity': '@Entity',
            'Database': '@Database'
        };

        window.loadZenUMLDiagram = function(jsonData) {
            try {
                currentDiagramType = 'zenuml';
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                zuDiagram = {
                    title: data.title || null,
                    participants: (data.participants || []).map(p => ({
                        id: p.id,
                        alias: p.alias || null,
                        annotator: p.annotator || 'None',
                        isExplicit: p.isExplicit !== false
                    })),
                    elements: data.elements || [],
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Clear flowchart diagram state
                diagram.nodes = [];
                diagram.edges = [];
                diagram.subgraphs = [];

                // Hide property panel from previous diagram
                propertyPanel.classList.remove('visible');

                // Update toolbar buttons for ZenUML
                updateToolbarForDiagramType();

                renderZenUMLDiagram();
                centerView();
            } catch (err) {
                console.error('Failed to load ZenUML diagram:', err);
            }
        };

        window.refreshZenUMLDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                zuDiagram = {
                    title: data.title || null,
                    participants: (data.participants || []).map(p => ({
                        id: p.id,
                        alias: p.alias || null,
                        annotator: p.annotator || 'None',
                        isExplicit: p.isExplicit !== false
                    })),
                    elements: data.elements || [],
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };
                renderZenUMLDiagram();
            } catch (err) {
                console.error('Failed to refresh ZenUML diagram:', err);
            }
        };

        window.restoreZenUMLDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                zuDiagram = {
                    title: data.title || null,
                    participants: (data.participants || []).map(p => ({
                        id: p.id,
                        alias: p.alias || null,
                        annotator: p.annotator || 'None',
                        isExplicit: p.isExplicit !== false
                    })),
                    elements: data.elements || [],
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };
                renderZenUMLDiagram();
            } catch (err) {
                console.error('Failed to restore ZenUML diagram:', err);
            }
        };

        function zuGetParticipantX(index) {
            return ZU_LEFT_MARGIN + index * ZU_PARTICIPANT_GAP;
        }

        function zuGetParticipantLabel(p) {
            return p.alias || p.id;
        }

        function zuGetParticipantIndex(participantId) {
            if (!zuDiagram) return -1;
            return zuDiagram.participants.findIndex(p => p.id === participantId);
        }

        // Build a structured tree from the flat elements for rendering
        function zuBuildElementTree(flatElements) {
            const tree = [];
            let i = 0;
            function buildLevel(depth) {
                const items = [];
                while (i < flatElements.length) {
                    const el = flatElements[i];
                    if (el.elementType === 'blockEnd' || el.elementType === 'fragmentEnd') {
                        i++; // consume the end marker
                        return items;
                    }
                    if (el.elementType === 'message') {
                        const msgNode = { ...el, flatIndex: i, children: [] };
                        i++;
                        if (el.hasBlock) {
                            msgNode.children = buildLevel(el.depth + 1);
                        }
                        items.push(msgNode);
                    } else if (el.elementType === 'fragment') {
                        const fragNode = { ...el, flatIndex: i, sectionChildren: [] };
                        i++;
                        // Build children for each section from nested elements
                        if (el.sections && el.sections.length > 0) {
                            fragNode.sectionChildren = el.sections.map(sec => {
                                if (sec.elements && sec.elements.length > 0) {
                                    return zuBuildElementTree(sec.elements);
                                }
                                return [];
                            });
                        }
                        // Also build a flat children array for backward compat with rendering
                        fragNode.children = [];
                        fragNode.sectionChildren.forEach(sc => {
                            fragNode.children = fragNode.children.concat(sc);
                        });
                        items.push(fragNode);
                    } else if (el.elementType === 'return') {
                        items.push({ ...el, flatIndex: i });
                        i++;
                    } else if (el.elementType === 'comment') {
                        items.push({ ...el, flatIndex: i });
                        i++;
                    } else {
                        i++; // skip unknown
                    }
                }
                return items;
            }
            const result = buildLevel(0);
            return result;
        }

        // Calculate total visual height for the diagram
        function zuGetTotalVisualHeight() {
            if (!zuDiagram) return 400;
            let y = ZU_LIFELINE_START_Y + 30;
            const tree = zuBuildElementTree(zuDiagram.elements);
            function walkTree(items) {
                items.forEach(item => {
                    if (item.elementType === 'message') {
                        y += ZU_MESSAGE_SPACING;
                        if (item.children && item.children.length > 0) {
                            walkTree(item.children);
                            y += 15; // block closing space
                        }
                    } else if (item.elementType === 'return') {
                        y += 35;
                    } else if (item.elementType === 'fragment') {
                        y += 25; // fragment header
                        if (item.children && item.children.length > 0) {
                            walkTree(item.children);
                        }
                        y += 15; // fragment bottom
                    } else if (item.elementType === 'comment') {
                        y += 30;
                    }
                });
            }
            walkTree(tree);
            return y + ZU_PARTICIPANT_HEIGHT + 30;
        }

        function renderZenUMLDiagram() {
            if (!zuDiagram) return;

            // Show diagram-svg, hide editorCanvas
            var dSvg = document.getElementById('diagram-svg');
            var eCanvas = document.getElementById('editorCanvas');
            if (dSvg) dSvg.style.display = '';
            if (eCanvas) { eCanvas.style.display = 'none'; eCanvas.innerHTML = ''; }

            nodesLayer.innerHTML = '';
            edgesLayer.innerHTML = '';
            subgraphsLayer.innerHTML = '';

            const totalHeight = zuGetTotalVisualHeight();
            const participants = zuDiagram.participants;

            // ===== Render Title =====
            if (zuDiagram.title) {
                const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                titleText.classList.add('node-label');
                titleText.setAttribute('x', ZU_LEFT_MARGIN + (participants.length > 1 ? (participants.length - 1) * ZU_PARTICIPANT_GAP / 2 : 0));
                titleText.setAttribute('y', '25');
                titleText.setAttribute('font-size', '16');
                titleText.setAttribute('font-weight', 'bold');
                titleText.textContent = zuDiagram.title;
                nodesLayer.appendChild(titleText);
            }

            // ===== Render Participants (top boxes) =====
            participants.forEach((p, idx) => {
                const x = zuGetParticipantX(idx);
                const y = ZU_TOP_MARGIN;
                const annotator = p.annotator || 'None';
                const isActor = annotator === 'Actor';

                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.classList.add('node-group', 'zu-participant');
                g.setAttribute('data-participant-index', idx);
                g.setAttribute('transform', `translate(${x}, ${y})`);

                if (isActor) {
                    // Draw stick figure for Actor
                    const actorG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                    const head = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    head.setAttribute('cx', '0'); head.setAttribute('cy', '-12'); head.setAttribute('r', '8');
                    head.classList.add('node-shape');
                    if (zuSelectedParticipantIdx === idx) head.classList.add('selected');
                    actorG.appendChild(head);
                    const body = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    body.setAttribute('x1', '0'); body.setAttribute('y1', '-4');
                    body.setAttribute('x2', '0'); body.setAttribute('y2', '10');
                    body.setAttribute('stroke', 'var(--node-stroke)'); body.setAttribute('stroke-width', '1.5');
                    actorG.appendChild(body);
                    const arms = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    arms.setAttribute('x1', '-10'); arms.setAttribute('y1', '0');
                    arms.setAttribute('x2', '10'); arms.setAttribute('y2', '0');
                    arms.setAttribute('stroke', 'var(--node-stroke)'); arms.setAttribute('stroke-width', '1.5');
                    actorG.appendChild(arms);
                    const legL = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    legL.setAttribute('x1', '0'); legL.setAttribute('y1', '10');
                    legL.setAttribute('x2', '-8'); legL.setAttribute('y2', '20');
                    legL.setAttribute('stroke', 'var(--node-stroke)'); legL.setAttribute('stroke-width', '1.5');
                    actorG.appendChild(legL);
                    const legR = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    legR.setAttribute('x1', '0'); legR.setAttribute('y1', '10');
                    legR.setAttribute('x2', '8'); legR.setAttribute('y2', '20');
                    legR.setAttribute('stroke', 'var(--node-stroke)'); legR.setAttribute('stroke-width', '1.5');
                    actorG.appendChild(legR);
                    g.appendChild(actorG);
                } else {
                    // Draw box with annotator badge
                    const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    rect.setAttribute('x', -ZU_PARTICIPANT_WIDTH / 2);
                    rect.setAttribute('y', -ZU_PARTICIPANT_HEIGHT / 2);
                    rect.setAttribute('width', ZU_PARTICIPANT_WIDTH);
                    rect.setAttribute('height', ZU_PARTICIPANT_HEIGHT);
                    rect.setAttribute('rx', '4');
                    rect.classList.add('node-shape');
                    if (zuSelectedParticipantIdx === idx) rect.classList.add('selected');
                    g.appendChild(rect);

                    // Annotator icon badge (top-left corner)
                    if (annotator !== 'None') {
                        const badge = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        badge.setAttribute('x', -ZU_PARTICIPANT_WIDTH / 2 + 6);
                        badge.setAttribute('y', -ZU_PARTICIPANT_HEIGHT / 2 + 12);
                        badge.setAttribute('font-size', '10');
                        badge.setAttribute('fill', 'var(--node-stroke)');
                        badge.setAttribute('pointer-events', 'none');
                        badge.textContent = ZU_ANNOTATOR_ICONS[annotator] || '';
                        g.appendChild(badge);
                    }
                }

                // Label
                const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text.classList.add('node-label');
                text.setAttribute('x', '0');
                text.setAttribute('y', isActor ? '30' : '0');
                text.textContent = zuGetParticipantLabel(p);
                g.appendChild(text);

                // Annotator sub-label (below name, for non-actor types with annotations)
                if (annotator !== 'None' && annotator !== 'Actor') {
                    const subLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    subLabel.setAttribute('x', '0');
                    subLabel.setAttribute('y', '14');
                    subLabel.setAttribute('text-anchor', 'middle');
                    subLabel.setAttribute('font-size', '9');
                    subLabel.setAttribute('fill', 'var(--subgraph-text)');
                    subLabel.setAttribute('pointer-events', 'none');
                    subLabel.textContent = ZU_ANNOTATOR_LABELS[annotator] || '';
                    g.appendChild(subLabel);
                }

                nodesLayer.appendChild(g);

                // ===== Bottom participant box (mirror) =====
                const bottomY = totalHeight;
                const gBottom = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                gBottom.classList.add('node-group', 'zu-participant-bottom');
                gBottom.setAttribute('data-participant-index', idx);
                gBottom.setAttribute('transform', `translate(${x}, ${bottomY})`);

                if (isActor) {
                    const head2 = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    head2.setAttribute('cx', '0'); head2.setAttribute('cy', '-12'); head2.setAttribute('r', '8');
                    head2.classList.add('node-shape');
                    gBottom.appendChild(head2);
                    const body2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    body2.setAttribute('x1', '0'); body2.setAttribute('y1', '-4');
                    body2.setAttribute('x2', '0'); body2.setAttribute('y2', '10');
                    body2.setAttribute('stroke', 'var(--node-stroke)'); body2.setAttribute('stroke-width', '1.5');
                    gBottom.appendChild(body2);
                    const arms2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    arms2.setAttribute('x1', '-10'); arms2.setAttribute('y1', '0');
                    arms2.setAttribute('x2', '10'); arms2.setAttribute('y2', '0');
                    arms2.setAttribute('stroke', 'var(--node-stroke)'); arms2.setAttribute('stroke-width', '1.5');
                    gBottom.appendChild(arms2);
                    const legL2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    legL2.setAttribute('x1', '0'); legL2.setAttribute('y1', '10');
                    legL2.setAttribute('x2', '-8'); legL2.setAttribute('y2', '20');
                    legL2.setAttribute('stroke', 'var(--node-stroke)'); legL2.setAttribute('stroke-width', '1.5');
                    gBottom.appendChild(legL2);
                    const legR2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    legR2.setAttribute('x1', '0'); legR2.setAttribute('y1', '10');
                    legR2.setAttribute('x2', '8'); legR2.setAttribute('y2', '20');
                    legR2.setAttribute('stroke', 'var(--node-stroke)'); legR2.setAttribute('stroke-width', '1.5');
                    gBottom.appendChild(legR2);
                } else {
                    const rect2 = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    rect2.setAttribute('x', -ZU_PARTICIPANT_WIDTH / 2);
                    rect2.setAttribute('y', -ZU_PARTICIPANT_HEIGHT / 2);
                    rect2.setAttribute('width', ZU_PARTICIPANT_WIDTH);
                    rect2.setAttribute('height', ZU_PARTICIPANT_HEIGHT);
                    rect2.setAttribute('rx', '4');
                    rect2.classList.add('node-shape');
                    gBottom.appendChild(rect2);
                }

                const text2 = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text2.classList.add('node-label');
                text2.setAttribute('x', '0');
                text2.setAttribute('y', isActor ? '30' : '0');
                text2.textContent = zuGetParticipantLabel(p);
                gBottom.appendChild(text2);
                nodesLayer.appendChild(gBottom);
            });

            // ===== Render Lifelines =====
            participants.forEach((p, idx) => {
                const x = zuGetParticipantX(idx);
                const startY = ZU_LIFELINE_START_Y;
                const endY = totalHeight - ZU_PARTICIPANT_HEIGHT / 2 - 10;

                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', x);
                line.setAttribute('y1', startY);
                line.setAttribute('x2', x);
                line.setAttribute('y2', endY);
                line.setAttribute('stroke', 'var(--edge-color)');
                line.setAttribute('stroke-width', '1');
                line.setAttribute('stroke-dasharray', '6,3');
                line.classList.add('zu-lifeline');
                subgraphsLayer.appendChild(line);
            });

            // ===== Render Elements =====
            const tree = zuBuildElementTree(zuDiagram.elements);
            let currentY = ZU_LIFELINE_START_Y + 30;
            const activationStacks = {}; // participantId -> [{startY}]

            function renderTree(items, indent, parentMsg) {
                items.forEach(item => {
                    if (item.elementType === 'message') {
                        zuRenderMessage(item, currentY, indent);
                        // Activation tracking
                        const toId = item.toId;
                        if (item.hasBlock) {
                            if (!activationStacks[toId]) activationStacks[toId] = [];
                            activationStacks[toId].push({ startY: currentY });
                        }
                        currentY += ZU_MESSAGE_SPACING;
                        if (item.children && item.children.length > 0) {
                            renderTree(item.children, indent + 1, item);
                            // Close activation
                            if (item.hasBlock) {
                                const stack = activationStacks[toId];
                                if (stack && stack.length > 0) {
                                    const activation = stack.pop();
                                    zuRenderActivationBox(toId, activation.startY, currentY);
                                }
                            }
                            currentY += 15; // block closing space
                        }
                    } else if (item.elementType === 'return') {
                        zuRenderReturn(item, currentY, parentMsg);
                        currentY += 35;
                    } else if (item.elementType === 'fragment') {
                        const fragStartY = currentY;
                        currentY += 25; // fragment header
                        if (item.children && item.children.length > 0) {
                            renderTree(item.children, indent + 1);
                        }
                        currentY += 15; // fragment bottom
                        zuRenderFragment(item, fragStartY, currentY);
                    } else if (item.elementType === 'comment') {
                        zuRenderComment(item, currentY);
                        currentY += 30;
                    }
                });
            }
            renderTree(tree, 0, null);

            // Close remaining activations
            Object.keys(activationStacks).forEach(pid => {
                const stack = activationStacks[pid];
                while (stack.length > 0) {
                    const activation = stack.pop();
                    zuRenderActivationBox(pid, activation.startY, totalHeight - 60);
                }
            });

            // ===== Render + zones between participants =====
            if (participants.length >= 2) {
                for (let i = 0; i < participants.length - 1; i++) {
                    const x1 = zuGetParticipantX(i);
                    const x2 = zuGetParticipantX(i + 1);
                    const midX = (x1 + x2) / 2;
                    const addZone = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    addZone.setAttribute('x', midX - 15);
                    addZone.setAttribute('y', totalHeight - 50);
                    addZone.setAttribute('width', 30);
                    addZone.setAttribute('height', 20);
                    addZone.setAttribute('rx', '4');
                    addZone.setAttribute('fill', 'var(--toolbar-bg)');
                    addZone.setAttribute('stroke', 'var(--toolbar-border)');
                    addZone.setAttribute('cursor', 'pointer');
                    addZone.setAttribute('opacity', '0.6');
                    const addText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    addText.setAttribute('x', midX);
                    addText.setAttribute('y', totalHeight - 37);
                    addText.setAttribute('text-anchor', 'middle');
                    addText.setAttribute('font-size', '14');
                    addText.setAttribute('fill', 'var(--toolbar-text)');
                    addText.setAttribute('cursor', 'pointer');
                    addText.textContent = '+';

                    const fromId = participants[i].id;
                    const toId = participants[i + 1].id;
                    const clickHandler = function(e) {
                        e.stopPropagation();
                        postMessage({
                            type: 'zu_messageCreated',
                            fromId: fromId,
                            toId: toId,
                            text: 'method()',
                            messageType: 'Sync',
                            hasBlock: true
                        });
                    };
                    addZone.addEventListener('click', clickHandler);
                    addText.addEventListener('click', clickHandler);

                    nodesLayer.appendChild(addZone);
                    nodesLayer.appendChild(addText);
                }
            }

            updateMinimap();
            updateCanvasAndZoomLimits();
        }

        function zuRenderMessage(item, absY, indent) {
            if (!zuDiagram) return;
            const fromIdx = zuGetParticipantIndex(item.fromId);
            const toIdx = zuGetParticipantIndex(item.toId);
            if (fromIdx < 0 || toIdx < 0) return;

            const fromX = zuGetParticipantX(fromIdx);
            const toX = zuGetParticipantX(toIdx);
            const y = absY;
            const isSelfMessage = item.fromId === item.toId;
            const isSelected = (zuSelectedElementIdx === item.flatIndex);

            const msgGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            msgGroup.classList.add('edge-group', 'zu-message');
            msgGroup.setAttribute('data-flat-index', item.flatIndex);
            msgGroup.style.cursor = 'pointer';

            // Hit area
            const hitRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            if (isSelfMessage) {
                hitRect.setAttribute('x', fromX - 2);
                hitRect.setAttribute('y', y - 6);
                hitRect.setAttribute('width', 50);
                hitRect.setAttribute('height', 42);
            } else {
                const minX = Math.min(fromX, toX);
                const maxX = Math.max(fromX, toX);
                hitRect.setAttribute('x', minX);
                hitRect.setAttribute('y', y - 20);
                hitRect.setAttribute('width', maxX - minX);
                hitRect.setAttribute('height', 28);
            }
            hitRect.setAttribute('fill', 'transparent');
            hitRect.setAttribute('pointer-events', 'all');
            msgGroup.appendChild(hitRect);

            // Determine arrow style based on messageType
            const msgType = item.messageType || 'Sync';
            const isDotted = msgType === 'Async' || msgType === 'Reply';
            const isOpenArrow = msgType === 'Async';

            if (isSelfMessage) {
                // Self-message: loop arrow
                const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                const loopW = 40;
                const loopH = 30;
                path.setAttribute('d', `M ${fromX} ${y} H ${fromX + loopW} V ${y + loopH} H ${fromX}`);
                path.classList.add('edge-line');
                if (isSelected) path.classList.add('selected');
                if (isDotted) path.setAttribute('stroke-dasharray', '6,3');
                msgGroup.appendChild(path);

                // Arrowhead
                const arrowPoly = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                arrowPoly.setAttribute('points', `${fromX + 8},${y + loopH - 4} ${fromX},${y + loopH} ${fromX + 8},${y + loopH + 4}`);
                arrowPoly.classList.add('edge-arrowhead');
                if (isSelected) arrowPoly.classList.add('selected');
                msgGroup.appendChild(arrowPoly);

                // Label
                if (item.text) {
                    const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    label.classList.add('edge-label');
                    label.setAttribute('x', fromX + loopW + 5);
                    label.setAttribute('y', y + loopH / 2);
                    label.setAttribute('text-anchor', 'start');
                    setTextWithLineBreaks(label, item.text);
                    msgGroup.appendChild(label);
                }
            } else {
                // Normal message: horizontal arrow
                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', fromX);
                line.setAttribute('y1', y);
                line.setAttribute('x2', toX);
                line.setAttribute('y2', y);
                line.classList.add('edge-line');
                if (isSelected) line.classList.add('selected');
                if (isDotted) line.setAttribute('stroke-dasharray', '6,3');
                msgGroup.appendChild(line);

                // Arrowhead
                const dir = toX > fromX ? 1 : -1;
                if (isOpenArrow) {
                    const arrow = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
                    arrow.setAttribute('points', `${toX - dir * 10},${y - 5} ${toX},${y} ${toX - dir * 10},${y + 5}`);
                    arrow.setAttribute('fill', 'none');
                    arrow.setAttribute('stroke', 'var(--edge-color)');
                    arrow.setAttribute('stroke-width', '1.5');
                    if (isSelected) arrow.setAttribute('stroke', 'var(--node-selected-stroke)');
                    msgGroup.appendChild(arrow);
                } else {
                    const arrowPoly = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                    arrowPoly.setAttribute('points', `${toX - dir * 10},${y - 5} ${toX},${y} ${toX - dir * 10},${y + 5}`);
                    arrowPoly.classList.add('edge-arrowhead');
                    if (isSelected) arrowPoly.classList.add('selected');
                    msgGroup.appendChild(arrowPoly);
                }

                // Invisible wider hit area
                const hitLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                hitLine.setAttribute('x1', fromX);
                hitLine.setAttribute('y1', y);
                hitLine.setAttribute('x2', toX);
                hitLine.setAttribute('y2', y);
                hitLine.setAttribute('stroke', 'transparent');
                hitLine.setAttribute('stroke-width', '12');
                hitLine.setAttribute('cursor', 'pointer');
                hitLine.setAttribute('pointer-events', 'all');
                msgGroup.appendChild(hitLine);

                // Label
                if (item.text) {
                    const midX = (fromX + toX) / 2;
                    const labelText = item.text;
                    const textWidth = estimateLabelWidth(labelText, 7) + 12;
                    const labelBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    labelBg.setAttribute('x', midX - textWidth / 2);
                    labelBg.setAttribute('y', y - 18);
                    labelBg.setAttribute('width', textWidth);
                    labelBg.setAttribute('height', 16);
                    labelBg.setAttribute('rx', '2');
                    labelBg.classList.add('edge-label-bg');
                    msgGroup.appendChild(labelBg);

                    const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    label.classList.add('edge-label');
                    label.setAttribute('x', midX);
                    label.setAttribute('y', y - 10);
                    setTextWithLineBreaks(label, labelText);
                    msgGroup.appendChild(label);
                }
            }

            edgesLayer.appendChild(msgGroup);
        }

        function zuRenderReturn(item, absY, parentMsg) {
            if (!zuDiagram) return;
            const y = absY;
            const isSelected = (zuSelectedElementIdx === item.flatIndex);

            // Position return between the correct caller and callee from the parent message
            const participants = zuDiagram.participants;
            var startX, endX;
            if (parentMsg && parentMsg.fromId && parentMsg.toId) {
                // Return goes from callee (toId) back to caller (fromId)
                const calleeIdx = zuGetParticipantIndex(parentMsg.toId);
                const callerIdx = zuGetParticipantIndex(parentMsg.fromId);
                if (calleeIdx >= 0 && callerIdx >= 0) {
                    startX = zuGetParticipantX(calleeIdx);
                    endX = zuGetParticipantX(callerIdx);
                } else {
                    startX = participants.length > 0 ? zuGetParticipantX(0) : ZU_LEFT_MARGIN;
                    endX = participants.length > 1 ? zuGetParticipantX(participants.length - 1) : startX + 100;
                }
            } else {
                // Fallback: span the diagram if no parent context
                startX = participants.length > 0 ? zuGetParticipantX(0) : ZU_LEFT_MARGIN;
                endX = participants.length > 1 ? zuGetParticipantX(participants.length - 1) : startX + 100;
            }
            const midX = (startX + endX) / 2;

            const retG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            retG.classList.add('zu-return');
            retG.setAttribute('data-flat-index', item.flatIndex);
            retG.style.cursor = 'pointer';

            // Dashed return line
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', startX);
            line.setAttribute('y1', y);
            line.setAttribute('x2', endX);
            line.setAttribute('y2', y);
            line.setAttribute('stroke', isSelected ? 'var(--node-selected-stroke)' : 'var(--edge-color)');
            line.setAttribute('stroke-width', isSelected ? '2' : '1');
            line.setAttribute('stroke-dasharray', '4,3');
            retG.appendChild(line);

            // Return label
            const labelText = 'return ' + (item.text || '');
            const textWidth = estimateLabelWidth(labelText, 7) + 12;
            const labelBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            labelBg.setAttribute('x', midX - textWidth / 2);
            labelBg.setAttribute('y', y - 18);
            labelBg.setAttribute('width', textWidth);
            labelBg.setAttribute('height', 16);
            labelBg.setAttribute('rx', '2');
            labelBg.classList.add('edge-label-bg');
            retG.appendChild(labelBg);

            const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.classList.add('edge-label');
            text.setAttribute('x', midX);
            text.setAttribute('y', y - 10);
            text.setAttribute('font-style', 'italic');
            setTextWithLineBreaks(text, labelText);
            retG.appendChild(text);

            // Arrowhead pointing toward the endX (caller)
            const dir = endX > startX ? 1 : -1;
            const arrowPoly = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
            arrowPoly.setAttribute('points', `${endX - dir * 8},${y - 4} ${endX},${y} ${endX - dir * 8},${y + 4}`);
            arrowPoly.setAttribute('fill', isSelected ? 'var(--node-selected-stroke)' : 'var(--edge-color)');
            arrowPoly.setAttribute('pointer-events', 'none');
            retG.appendChild(arrowPoly);

            // Hit area
            const hitMinX = Math.min(startX, endX);
            const hitWidth = Math.abs(endX - startX) || 100;
            const hitRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            hitRect.setAttribute('x', hitMinX);
            hitRect.setAttribute('y', y - 20);
            hitRect.setAttribute('width', hitWidth);
            hitRect.setAttribute('height', 28);
            hitRect.setAttribute('fill', 'transparent');
            hitRect.setAttribute('pointer-events', 'all');
            retG.appendChild(hitRect);

            edgesLayer.appendChild(retG);
        }

        function zuRenderFragment(item, fragStartY, fragEndY) {
            if (!zuDiagram) return;
            const startY = fragStartY - 10;
            const endY = fragEndY;
            const participants = zuDiagram.participants;
            const isSelected = (zuSelectedElementIdx === item.flatIndex);

            // Span all participants
            const leftX = participants.length > 0 ? zuGetParticipantX(0) - ZU_PARTICIPANT_WIDTH / 2 - 10 : ZU_LEFT_MARGIN - 40;
            const rightX = participants.length > 0 ? zuGetParticipantX(participants.length - 1) + ZU_PARTICIPANT_WIDTH / 2 + 10 : ZU_LEFT_MARGIN + 200;

            const fragG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            fragG.classList.add('zu-fragment');
            fragG.setAttribute('data-flat-index', item.flatIndex);
            fragG.style.cursor = 'pointer';

            // Main rectangle
            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', leftX);
            rect.setAttribute('y', startY);
            rect.setAttribute('width', rightX - leftX);
            rect.setAttribute('height', endY - startY);
            rect.setAttribute('fill', 'var(--subgraph-fill)');
            rect.setAttribute('stroke', isSelected ? 'var(--node-selected-stroke)' : 'var(--subgraph-stroke)');
            rect.setAttribute('stroke-width', isSelected ? '2.5' : '1');
            rect.setAttribute('rx', '2');
            rect.setAttribute('pointer-events', 'none');
            fragG.appendChild(rect);

            // Clickable border strips
            const borderW = 8;
            const fragW = rightX - leftX;
            const fragH = endY - startY;
            ['top', 'bottom', 'left', 'right'].forEach(side => {
                const strip = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                if (side === 'top') {
                    strip.setAttribute('x', leftX); strip.setAttribute('y', startY);
                    strip.setAttribute('width', fragW); strip.setAttribute('height', borderW);
                } else if (side === 'bottom') {
                    strip.setAttribute('x', leftX); strip.setAttribute('y', endY - borderW);
                    strip.setAttribute('width', fragW); strip.setAttribute('height', borderW);
                } else if (side === 'left') {
                    strip.setAttribute('x', leftX); strip.setAttribute('y', startY);
                    strip.setAttribute('width', borderW); strip.setAttribute('height', fragH);
                } else {
                    strip.setAttribute('x', rightX - borderW); strip.setAttribute('y', startY);
                    strip.setAttribute('width', borderW); strip.setAttribute('height', fragH);
                }
                strip.setAttribute('fill', 'transparent');
                strip.setAttribute('pointer-events', 'all');
                fragG.appendChild(strip);
            });

            // Fragment type label
            const fragTypeText = (item.fragmentType || 'If').toLowerCase();
            const typeWidth = fragTypeText.length * 8 + 16;
            const typeBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            typeBg.setAttribute('x', leftX);
            typeBg.setAttribute('y', startY);
            typeBg.setAttribute('width', typeWidth);
            typeBg.setAttribute('height', 20);
            typeBg.setAttribute('fill', 'var(--toolbar-bg)');
            typeBg.setAttribute('stroke', 'var(--subgraph-stroke)');
            typeBg.setAttribute('stroke-width', '1');
            typeBg.setAttribute('pointer-events', 'none');
            fragG.appendChild(typeBg);

            const typeText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            typeText.setAttribute('x', leftX + 8);
            typeText.setAttribute('y', startY + 14);
            typeText.setAttribute('font-size', '11');
            typeText.setAttribute('font-weight', 'bold');
            typeText.setAttribute('fill', 'var(--toolbar-text)');
            typeText.setAttribute('pointer-events', 'none');
            typeText.textContent = fragTypeText;
            fragG.appendChild(typeText);

            // Condition label
            if (item.text) {
                const condText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                condText.setAttribute('x', leftX + typeWidth + 10);
                condText.setAttribute('y', startY + 14);
                condText.setAttribute('font-size', '11');
                condText.setAttribute('fill', 'var(--subgraph-text)');
                condText.setAttribute('pointer-events', 'none');
                condText.textContent = '(' + item.text + ')';
                fragG.appendChild(condText);
            }

            subgraphsLayer.appendChild(fragG);
        }

        function zuRenderComment(item, absY) {
            if (!zuDiagram) return;
            const y = absY;
            const isSelected = (zuSelectedElementIdx === item.flatIndex);
            const participants = zuDiagram.participants;
            const midX = participants.length > 0
                ? (zuGetParticipantX(0) + zuGetParticipantX(Math.max(0, participants.length - 1))) / 2
                : ZU_LEFT_MARGIN;

            const commentG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            commentG.classList.add('zu-comment');
            commentG.setAttribute('data-flat-index', item.flatIndex);

            const commentText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            commentText.setAttribute('x', midX);
            commentText.setAttribute('y', y);
            commentText.setAttribute('text-anchor', 'middle');
            commentText.setAttribute('font-size', '10');
            commentText.setAttribute('font-style', 'italic');
            commentText.setAttribute('fill', isSelected ? 'var(--node-selected-stroke)' : 'var(--subgraph-text)');
            commentText.textContent = '// ' + (item.text || '');
            commentG.appendChild(commentText);

            edgesLayer.appendChild(commentG);
        }

        function zuRenderActivationBox(participantId, startY, endY) {
            const idx = zuGetParticipantIndex(participantId);
            if (idx < 0) return;
            const x = zuGetParticipantX(idx);
            const boxWidth = 12;

            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', x - boxWidth / 2);
            rect.setAttribute('y', startY);
            rect.setAttribute('width', boxWidth);
            rect.setAttribute('height', Math.max(endY - startY, 10));
            rect.setAttribute('fill', 'var(--node-fill)');
            rect.setAttribute('stroke', 'var(--node-stroke)');
            rect.setAttribute('stroke-width', '1.5');
            rect.classList.add('zu-activation');
            subgraphsLayer.appendChild(rect);
        }

        // ===== ZenUML Interaction Handlers =====

        // Participant drag to reorder
        svg.addEventListener('mousedown', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            if (!zuDiagram) return;

            const participantGroup = e.target.closest('.zu-participant') || e.target.closest('.zu-participant-bottom');
            if (participantGroup) {
                const idx = parseInt(participantGroup.getAttribute('data-participant-index'), 10);
                if (idx >= 0) {
                    zuDraggingParticipantIdx = idx;
                    zuDragStartX = e.clientX;
                    zuDragParticipantStartIdx = idx;
                    e.preventDefault();
                    e.stopPropagation();
                }
            }
        });

        svg.addEventListener('mousemove', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            if (zuDraggingParticipantIdx < 0) return;
            // Visual feedback could be added here
        });

        svg.addEventListener('mouseup', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            if (!zuDiagram) return;
            if (zuDraggingParticipantIdx < 0) return;

            const dx = e.clientX - zuDragStartX;
            const transform = panzoomInstance ? panzoomInstance.getTransform() : { scale: 1 };
            const svgDx = dx / transform.scale;
            const positionsShifted = Math.round(svgDx / ZU_PARTICIPANT_GAP);

            if (positionsShifted !== 0) {
                const newIdx = Math.max(0, Math.min(zuDiagram.participants.length - 1,
                    zuDragParticipantStartIdx + positionsShifted));
                if (newIdx !== zuDragParticipantStartIdx) {
                    postMessage({ type: 'zu_participantReordered', fromIndex: zuDragParticipantStartIdx, toIndex: newIdx });
                }
            }
            zuDraggingParticipantIdx = -1;
        });

        // Click on participant/message/fragment/return to select
        svg.addEventListener('click', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            if (!zuDiagram) return;

            const participantGroup = e.target.closest('.zu-participant') || e.target.closest('.zu-participant-bottom');
            const messageGroup = e.target.closest('.zu-message');
            const returnGroup = e.target.closest('.zu-return');
            const fragmentGroup = e.target.closest('.zu-fragment');

            if (participantGroup) {
                const idx = parseInt(participantGroup.getAttribute('data-participant-index'), 10);
                zuSelectedParticipantIdx = idx;
                zuSelectedElementIdx = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderZenUMLDiagram();
                zuShowParticipantPropertyPanel(idx);
                e.stopPropagation();
                return;
            }

            if (messageGroup) {
                const flatIdx = parseInt(messageGroup.getAttribute('data-flat-index'), 10);
                zuSelectedElementIdx = flatIdx;
                zuSelectedParticipantIdx = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderZenUMLDiagram();
                zuShowMessagePropertyPanel(flatIdx);
                e.stopPropagation();
                return;
            }

            if (returnGroup) {
                const flatIdx = parseInt(returnGroup.getAttribute('data-flat-index'), 10);
                zuSelectedElementIdx = flatIdx;
                zuSelectedParticipantIdx = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderZenUMLDiagram();
                zuShowReturnPropertyPanel(flatIdx);
                e.stopPropagation();
                return;
            }

            if (fragmentGroup) {
                const flatIdx = parseInt(fragmentGroup.getAttribute('data-flat-index'), 10);
                zuSelectedElementIdx = flatIdx;
                zuSelectedParticipantIdx = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderZenUMLDiagram();
                zuShowFragmentPropertyPanel(flatIdx);
                e.stopPropagation();
                return;
            }

            // Clicked on empty space — deselect
            if (zuDiagram) {
                zuSelectedParticipantIdx = -1;
                zuSelectedElementIdx = -1;
                propertyPanel.classList.remove('visible');
                renderZenUMLDiagram();
            }
        });

        // Double-click to show property panel
        svg.addEventListener('dblclick', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            if (!zuDiagram) return;

            const participantGroup = e.target.closest('.zu-participant') || e.target.closest('.zu-participant-bottom');
            if (participantGroup) {
                const idx = parseInt(participantGroup.getAttribute('data-participant-index'), 10);
                zuShowParticipantPropertyPanel(idx);
                e.stopPropagation();
                return;
            }

            const messageGroup = e.target.closest('.zu-message');
            if (messageGroup) {
                const flatIdx = parseInt(messageGroup.getAttribute('data-flat-index'), 10);
                zuShowMessagePropertyPanel(flatIdx);
                e.stopPropagation();
                return;
            }

            const returnGroup = e.target.closest('.zu-return');
            if (returnGroup) {
                const flatIdx = parseInt(returnGroup.getAttribute('data-flat-index'), 10);
                zuShowReturnPropertyPanel(flatIdx);
                e.stopPropagation();
                return;
            }
        });

        // ===== ZenUML Property Panels =====

        function zuEscapeHtml(str) {
            return (str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        }

        function zuShowParticipantPropertyPanel(idx) {
            if (!zuDiagram || idx < 0 || idx >= zuDiagram.participants.length) return;
            const p = zuDiagram.participants[idx];

            propPanelTitle.textContent = 'ZenUML Participant';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">ID</div>
                    <input class="property-input" id="zu-prop-id" value="${zuEscapeHtml(p.id)}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Display Name</div>
                    <input class="property-input" id="zu-prop-alias" value="${zuEscapeHtml(p.alias || '')}" placeholder="(uses ID if empty)" />
                </div>
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-select" id="zu-prop-annotator">
                        <option value="None" ${p.annotator === 'None' ? 'selected' : ''}>Participant</option>
                        <option value="Actor" ${p.annotator === 'Actor' ? 'selected' : ''}>@Actor</option>
                        <option value="Boundary" ${p.annotator === 'Boundary' ? 'selected' : ''}>@Boundary</option>
                        <option value="Control" ${p.annotator === 'Control' ? 'selected' : ''}>@Control</option>
                        <option value="Entity" ${p.annotator === 'Entity' ? 'selected' : ''}>@Entity</option>
                        <option value="Database" ${p.annotator === 'Database' ? 'selected' : ''}>@Database</option>
                    </select>
                </div>
                <div class="property-row">
                    <button class="property-btn property-btn-danger" id="zu-prop-delete">Delete Participant</button>
                </div>
            `;

            document.getElementById('zu-prop-id').addEventListener('change', function() {
                const newId = this.value.trim();
                if (newId) {
                    postMessage({ type: 'zu_participantEdited', index: idx, id: newId });
                }
            });
            document.getElementById('zu-prop-alias').addEventListener('change', function() {
                postMessage({ type: 'zu_participantEdited', index: idx, alias: this.value.trim() || null });
            });
            document.getElementById('zu-prop-annotator').addEventListener('change', function() {
                postMessage({ type: 'zu_participantEdited', index: idx, annotator: this.value });
            });
            document.getElementById('zu-prop-delete').addEventListener('click', function() {
                postMessage({ type: 'zu_participantDeleted', index: idx });
                zuSelectedParticipantIdx = -1;
                propertyPanel.classList.remove('visible');
            });

            propertyPanel.classList.add('visible');
        }

        function zuShowMessagePropertyPanel(flatIdx) {
            if (!zuDiagram) return;
            const el = zuDiagram.elements[flatIdx];
            if (!el || el.elementType !== 'message') return;

            // Find the path to this element in the nested model tree
            const elementPath = zuFindElementPath(flatIdx);

            propPanelTitle.textContent = 'ZenUML Message';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">From</div>
                    <select class="property-select" id="zu-msg-from">
                        ${zuDiagram.participants.map(p => `<option value="${zuEscapeHtml(p.id)}" ${el.fromId === p.id ? 'selected' : ''}>${zuEscapeHtml(zuGetParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <select class="property-select" id="zu-msg-to">
                        ${zuDiagram.participants.map(p => `<option value="${zuEscapeHtml(p.id)}" ${el.toId === p.id ? 'selected' : ''}>${zuEscapeHtml(zuGetParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Method Call</div>
                    <input class="property-input" id="zu-msg-text" value="${zuEscapeHtml(el.text || '')}" placeholder="e.g. method(params)" />
                </div>
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-select" id="zu-msg-type">
                        <option value="Sync" ${(el.messageType || 'Sync') === 'Sync' ? 'selected' : ''}>Sync (solid arrow)</option>
                        <option value="Async" ${el.messageType === 'Async' ? 'selected' : ''}>Async (open arrow)</option>
                        <option value="Creation" ${el.messageType === 'Creation' ? 'selected' : ''}>Creation (new)</option>
                        <option value="SelfCall" ${el.messageType === 'SelfCall' ? 'selected' : ''}>Self Call</option>
                    </select>
                </div>
                <div class="property-row">
                    <label style="display:flex;align-items:center;gap:6px;color:var(--toolbar-text)">
                        <input type="checkbox" id="zu-msg-block" ${el.hasBlock ? 'checked' : ''} />
                        Has Block { }
                    </label>
                </div>
                <div class="property-row">
                    <button class="property-btn property-btn-danger" id="zu-msg-delete">Delete Message</button>
                </div>
            `;

            document.getElementById('zu-msg-from').addEventListener('change', function() {
                postMessage({ type: 'zu_messageEdited', elementPath: elementPath, fromId: this.value });
            });
            document.getElementById('zu-msg-to').addEventListener('change', function() {
                postMessage({ type: 'zu_messageEdited', elementPath: elementPath, toId: this.value });
            });
            document.getElementById('zu-msg-text').addEventListener('change', function() {
                postMessage({ type: 'zu_messageEdited', elementPath: elementPath, text: this.value });
            });
            document.getElementById('zu-msg-type').addEventListener('change', function() {
                postMessage({ type: 'zu_messageEdited', elementPath: elementPath, messageType: this.value });
            });
            document.getElementById('zu-msg-block').addEventListener('change', function() {
                postMessage({ type: 'zu_messageEdited', elementPath: elementPath, hasBlock: this.checked });
            });
            document.getElementById('zu-msg-delete').addEventListener('click', function() {
                postMessage({ type: 'zu_messageDeleted', elementPath: elementPath });
                zuSelectedElementIdx = -1;
                propertyPanel.classList.remove('visible');
            });

            propertyPanel.classList.add('visible');
        }

        function zuShowReturnPropertyPanel(flatIdx) {
            if (!zuDiagram) return;
            const el = zuDiagram.elements[flatIdx];
            if (!el || el.elementType !== 'return') return;

            const elementPath = zuFindElementPath(flatIdx);

            propPanelTitle.textContent = 'ZenUML Return';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Return Value</div>
                    <input class="property-input" id="zu-ret-value" value="${zuEscapeHtml(el.text || '')}" placeholder="e.g. orderId" />
                </div>
                <div class="property-row">
                    <button class="property-btn property-btn-danger" id="zu-ret-delete">Delete Return</button>
                </div>
            `;

            document.getElementById('zu-ret-value').addEventListener('change', function() {
                postMessage({ type: 'zu_returnEdited', elementPath: elementPath, value: this.value });
            });
            document.getElementById('zu-ret-delete').addEventListener('click', function() {
                postMessage({ type: 'zu_returnDeleted', elementPath: elementPath });
                zuSelectedElementIdx = -1;
                propertyPanel.classList.remove('visible');
            });

            propertyPanel.classList.add('visible');
        }

        function zuShowFragmentPropertyPanel(flatIdx) {
            if (!zuDiagram) return;
            const el = zuDiagram.elements[flatIdx];
            if (!el || el.elementType !== 'fragment') return;

            const elementPath = zuFindElementPath(flatIdx);
            const fragType = (el.fragmentType || 'If').toLowerCase();
            const fragTypes = ['if', 'while', 'for', 'forEach', 'loop', 'opt', 'par', 'try'];

            propPanelTitle.textContent = 'ZenUML Fragment';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-select" id="zu-frag-type">
                        ${fragTypes.map(t => `<option value="${t}" ${fragType === t ? 'selected' : ''}>${t}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Condition/Label</div>
                    <input class="property-input" id="zu-frag-label" value="${zuEscapeHtml(el.text || '')}" placeholder="e.g. condition" />
                </div>
                <div class="property-row">
                    <button class="property-btn property-btn-danger" id="zu-frag-delete">Delete Fragment</button>
                </div>
            `;

            document.getElementById('zu-frag-type').addEventListener('change', function() {
                postMessage({ type: 'zu_fragmentEdited', elementPath: elementPath, fragmentType: this.value });
            });
            document.getElementById('zu-frag-label').addEventListener('change', function() {
                postMessage({ type: 'zu_fragmentEdited', elementPath: elementPath, label: this.value });
            });
            document.getElementById('zu-frag-delete').addEventListener('click', function() {
                postMessage({ type: 'zu_fragmentDeleted', elementPath: elementPath });
                zuSelectedElementIdx = -1;
                propertyPanel.classList.remove('visible');
            });

            propertyPanel.classList.add('visible');
        }

        // Build a path array from a flat index to address nested elements.
        // Returns an array like [2, 0] meaning "top-level element 2, nested element 0".
        // The C# bridge uses this path to traverse the model tree.
        function zuFindElementPath(flatIdx) {
            if (!zuDiagram || flatIdx < 0) return [];

            // Walk the flat array, tracking indices at each depth level
            var depthCounters = [0]; // index counter for each depth level
            for (let i = 0; i < zuDiagram.elements.length; i++) {
                const el = zuDiagram.elements[i];
                if (el.elementType === 'blockEnd' || el.elementType === 'fragmentEnd') {
                    // Pop back up a level
                    if (depthCounters.length > 1) depthCounters.pop();
                    continue;
                }

                // Ensure we have a counter for this depth
                while (depthCounters.length <= el.depth) depthCounters.push(0);
                // Trim if we jumped back up
                while (depthCounters.length > el.depth + 1) depthCounters.pop();

                if (i === flatIdx) {
                    // Build path from depth counters
                    return depthCounters.slice();
                }

                // Increment counter at this depth
                depthCounters[el.depth]++;

                // If this element has a block (children), prepare the next depth level
                if (el.hasBlock && el.elementType === 'message') {
                    while (depthCounters.length <= el.depth + 1) depthCounters.push(0);
                    depthCounters[el.depth + 1] = 0;
                }
            }
            return [];
        }

        // ===== ZenUML Context Menu =====
        let _zuRightClickHandled = false;

        svg.addEventListener('mousedown', function(e) {
            if (e.button !== 2) return;
            if (currentDiagramType !== 'zenuml') return;
            if (!zuDiagram) return;

            _zuRightClickHandled = true;
            e.preventDefault();
            const target = e.target;

            const participantGroup = target.closest('.zu-participant') || target.closest('.zu-participant-bottom');
            let messageGroup = target.closest('.zu-message');
            let returnGroup = target.closest('.zu-return');
            let fragmentGroup = target.closest('.zu-fragment');

            // Fallback: elementsFromPoint
            if (!messageGroup && !participantGroup && !returnGroup) {
                const allEls = document.elementsFromPoint(e.clientX, e.clientY);
                for (const el of allEls) {
                    const msg = el.closest('.zu-message');
                    if (msg) { messageGroup = msg; break; }
                    const ret = el.closest('.zu-return');
                    if (ret) { returnGroup = ret; break; }
                }
            }

            if (messageGroup) fragmentGroup = null;
            if (returnGroup) fragmentGroup = null;

            contextMenu.innerHTML = '';

            if (participantGroup) {
                const idx = parseInt(participantGroup.getAttribute('data-participant-index'), 10);
                zuSelectedParticipantIdx = idx;
                zuSelectedElementIdx = -1;

                zuAddContextMenuItem('Edit Participant', () => {
                    zuShowParticipantPropertyPanel(idx);
                });
                zuAddContextMenuItem('Delete Participant', () => {
                    postMessage({ type: 'zu_participantDeleted', index: idx });
                });
                zuAddContextMenuSeparator();
                zuAddContextMenuItem('Add Participant After', () => {
                    const newId = 'Participant' + (zuDiagram.participants.length + 1);
                    postMessage({ type: 'zu_participantCreated', id: newId, annotator: 'None', insertAtIndex: idx + 1 });
                });
                if (idx > 0) {
                    zuAddContextMenuItem('\u2191 Move Left', () => {
                        postMessage({ type: 'zu_participantReordered', fromIndex: idx, toIndex: idx - 1 });
                    });
                }
                if (idx < zuDiagram.participants.length - 1) {
                    zuAddContextMenuItem('\u2193 Move Right', () => {
                        postMessage({ type: 'zu_participantReordered', fromIndex: idx, toIndex: idx + 1 });
                    });
                }
            } else if (messageGroup) {
                const flatIdx = parseInt(messageGroup.getAttribute('data-flat-index'), 10);
                const realIdx = zuFindRealElementIndex(flatIdx);
                zuSelectedElementIdx = flatIdx;
                zuSelectedParticipantIdx = -1;

                zuAddContextMenuItem('Edit Message', () => {
                    zuShowMessagePropertyPanel(flatIdx);
                });
                zuAddContextMenuItem('Delete Message', () => {
                    postMessage({ type: 'zu_messageDeleted', elementIndex: realIdx });
                    zuSelectedElementIdx = -1;
                });
                zuAddContextMenuSeparator();
                if (realIdx > 0) {
                    zuAddContextMenuItem('\u2191 Move Up', () => {
                        postMessage({ type: 'zu_elementReordered', fromIndex: realIdx, toIndex: realIdx - 1 });
                    });
                }
                zuAddContextMenuItem('\u2193 Move Down', () => {
                    postMessage({ type: 'zu_elementReordered', fromIndex: realIdx, toIndex: realIdx + 1 });
                });
                zuAddContextMenuSeparator();
                zuAddContextMenuItem('Insert Message Above', () => {
                    const el = zuDiagram.elements[flatIdx];
                    const fromId = el ? el.fromId : zuDiagram.participants[0].id;
                    const toId = el ? el.toId : (zuDiagram.participants.length > 1 ? zuDiagram.participants[1].id : zuDiagram.participants[0].id);
                    postMessage({ type: 'zu_messageCreated', fromId: fromId, toId: toId, text: 'method()', messageType: 'Sync', hasBlock: true, insertAtIndex: realIdx });
                });
                zuAddContextMenuItem('Insert Message Below', () => {
                    const el = zuDiagram.elements[flatIdx];
                    const fromId = el ? el.fromId : zuDiagram.participants[0].id;
                    const toId = el ? el.toId : (zuDiagram.participants.length > 1 ? zuDiagram.participants[1].id : zuDiagram.participants[0].id);
                    postMessage({ type: 'zu_messageCreated', fromId: fromId, toId: toId, text: 'method()', messageType: 'Sync', hasBlock: true, insertAtIndex: realIdx + 1 });
                });
                zuAddContextMenuItem('Insert Return Below', () => {
                    postMessage({ type: 'zu_returnCreated', value: 'result', insertAtIndex: realIdx + 1 });
                });
            } else if (returnGroup) {
                const flatIdx = parseInt(returnGroup.getAttribute('data-flat-index'), 10);
                const realIdx = zuFindRealElementIndex(flatIdx);
                zuSelectedElementIdx = flatIdx;
                zuSelectedParticipantIdx = -1;

                zuAddContextMenuItem('Edit Return', () => {
                    zuShowReturnPropertyPanel(flatIdx);
                });
                zuAddContextMenuItem('Delete Return', () => {
                    postMessage({ type: 'zu_returnDeleted', elementIndex: realIdx });
                    zuSelectedElementIdx = -1;
                });
                zuAddContextMenuSeparator();
                zuAddContextMenuItem('Insert Message Above', () => {
                    const fromId = zuDiagram.participants[0].id;
                    const toId = zuDiagram.participants.length > 1 ? zuDiagram.participants[1].id : zuDiagram.participants[0].id;
                    postMessage({ type: 'zu_messageCreated', fromId: fromId, toId: toId, text: 'method()', messageType: 'Sync', hasBlock: true, insertAtIndex: realIdx });
                });
                zuAddContextMenuItem('Insert Return Above', () => {
                    postMessage({ type: 'zu_returnCreated', value: 'result', insertAtIndex: realIdx });
                });
            } else if (fragmentGroup) {
                const flatIdx = parseInt(fragmentGroup.getAttribute('data-flat-index'), 10);
                const realIdx = zuFindRealElementIndex(flatIdx);
                zuSelectedElementIdx = flatIdx;
                zuSelectedParticipantIdx = -1;

                zuAddContextMenuItem('Edit Fragment', () => {
                    zuShowFragmentPropertyPanel(flatIdx);
                });
                zuAddContextMenuItem('Delete Fragment', () => {
                    postMessage({ type: 'zu_fragmentDeleted', elementIndex: realIdx });
                    zuSelectedElementIdx = -1;
                });
                zuAddContextMenuSeparator();
                if (realIdx > 0) {
                    zuAddContextMenuItem('\u2191 Move Up', () => {
                        postMessage({ type: 'zu_elementReordered', fromIndex: realIdx, toIndex: realIdx - 1 });
                    });
                }
                zuAddContextMenuItem('\u2193 Move Down', () => {
                    postMessage({ type: 'zu_elementReordered', fromIndex: realIdx, toIndex: realIdx + 1 });
                });
                zuAddContextMenuSeparator();
                zuAddContextMenuItem('Insert Message Above', () => {
                    const fromId = zuDiagram.participants[0].id;
                    const toId = zuDiagram.participants.length > 1 ? zuDiagram.participants[1].id : zuDiagram.participants[0].id;
                    postMessage({ type: 'zu_messageCreated', fromId: fromId, toId: toId, text: 'method()', messageType: 'Sync', hasBlock: true, insertAtIndex: realIdx });
                });
                zuAddContextMenuItem('Insert Message Below', () => {
                    const fromId = zuDiagram.participants[0].id;
                    const toId = zuDiagram.participants.length > 1 ? zuDiagram.participants[1].id : zuDiagram.participants[0].id;
                    postMessage({ type: 'zu_messageCreated', fromId: fromId, toId: toId, text: 'method()', messageType: 'Sync', hasBlock: true, insertAtIndex: realIdx + 1 });
                });
            } else {
                // Empty space context menu
                zuAddContextMenuItem('Add Participant', () => {
                    const newId = 'Participant' + (zuDiagram.participants.length + 1);
                    postMessage({ type: 'zu_participantCreated', id: newId, annotator: 'None' });
                });
                if (zuDiagram.participants.length >= 2) {
                    zuAddContextMenuItem('Add Message', () => {
                        postMessage({
                            type: 'zu_messageCreated',
                            fromId: zuDiagram.participants[0].id,
                            toId: zuDiagram.participants[1].id,
                            text: 'method()',
                            messageType: 'Sync',
                            hasBlock: true
                        });
                    });
                    zuAddContextMenuItem('Add Return', () => {
                        postMessage({ type: 'zu_returnCreated', value: 'result' });
                    });
                    zuAddContextMenuItem('Add Fragment', () => {
                        postMessage({ type: 'zu_fragmentCreated', fragmentType: 'If', label: 'condition' });
                    });
                }
                zuAddContextMenuSeparator();
                zuAddContextMenuItem('Settings', () => {
                    zuShowSettingsPanel();
                });
            }

            // Copy/Paste
            if (zuSelectedParticipantIdx >= 0 || zuSelectedElementIdx >= 0) {
                zuAddContextMenuSeparator();
                zuAddContextMenuItem('\u{1F4CB} Copy', () => { copySelected(); });
            }
            if (clipboard && clipboard.diagramType === 'zenuml') {
                zuAddContextMenuItem('\u{1F4CB} Paste', () => { pasteClipboard(0, 0); });
            }

            positionContextMenu(contextMenu, e.clientX, e.clientY);
            renderZenUMLDiagram();
        });

        svg.addEventListener('contextmenu', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            e.preventDefault();
            e.stopPropagation();
            _zuRightClickHandled = false;
            return false;
        }, true);

        function zuAddContextMenuItem(label, onClick) {
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

        function zuAddContextMenuSeparator() {
            const sep = document.createElement('div');
            sep.classList.add('context-menu-separator');
            contextMenu.appendChild(sep);
        }

        // ===== Settings Panel =====
        function zuShowSettingsPanel() {
            if (!zuDiagram) return;

            propPanelTitle.textContent = 'ZenUML Settings';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Title</div>
                    <input class="property-input" id="zu-settings-title" value="${zuEscapeHtml(zuDiagram.title || '')}" placeholder="Diagram title" />
                </div>
            `;

            document.getElementById('zu-settings-title').addEventListener('change', function() {
                postMessage({ type: 'zu_settingsChanged', title: this.value || null });
            });

            propertyPanel.classList.add('visible');
        }

        // ===== Keyboard Shortcuts =====
        document.addEventListener('keydown', function(e) {
            if (currentDiagramType !== 'zenuml') return;
            if (!zuDiagram) return;
            // Skip if typing in an input/select/textarea
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT') return;

            if (e.key === 'Delete' || e.key === 'Backspace') {
                if (zuSelectedParticipantIdx >= 0) {
                    postMessage({ type: 'zu_participantDeleted', index: zuSelectedParticipantIdx });
                    zuSelectedParticipantIdx = -1;
                    propertyPanel.classList.remove('visible');
                    e.preventDefault();
                } else if (zuSelectedElementIdx >= 0) {
                    const el = zuDiagram.elements[zuSelectedElementIdx];
                    const realIdx = zuFindRealElementIndex(zuSelectedElementIdx);
                    if (el) {
                        if (el.elementType === 'message') {
                            postMessage({ type: 'zu_messageDeleted', elementIndex: realIdx });
                        } else if (el.elementType === 'return') {
                            postMessage({ type: 'zu_returnDeleted', elementIndex: realIdx });
                        } else if (el.elementType === 'fragment') {
                            postMessage({ type: 'zu_fragmentDeleted', elementIndex: realIdx });
                        }
                    }
                    zuSelectedElementIdx = -1;
                    propertyPanel.classList.remove('visible');
                    e.preventDefault();
                }
            }

            if (e.key === 'Escape') {
                zuSelectedParticipantIdx = -1;
                zuSelectedElementIdx = -1;
                propertyPanel.classList.remove('visible');
                contextMenu.classList.remove('visible');
                renderZenUMLDiagram();
                e.preventDefault();
            }
        });
