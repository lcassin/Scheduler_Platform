        // ========== Sequence Diagram Visual Editor ==========

        let seqDiagram = null; // When non-null, we're in sequence diagram mode
        let seqSelectedParticipantId = null;
        let seqSelectedMessageIndex = -1;
        let seqSelectedFragmentIndex = -1;
        let seqSelectedFragInnerMsg = null; // { fragIdx, secIdx, subIdx } when a fragment-inner message is selected
        let seqSelectedNoteIndex = -1;
        let _seqPendingNoteSelect = false; // Auto-select newly created notes
        let seqDraggingParticipantId = null;
        let seqDragStartX = 0;
        let seqDragParticipantStartIdx = 0;
        // Fragment grip dragging state
        let seqDraggingFragGrip = null; // { elIdx, side: 'left'|'right' }
        let seqFragGripDragStartX = 0;
        const SEQ_PARTICIPANT_WIDTH = 120;
        const SEQ_PARTICIPANT_HEIGHT = 40;
        const SEQ_PARTICIPANT_GAP = 180;
        const SEQ_MESSAGE_SPACING = 50;
        const SEQ_TOP_MARGIN = 60;
        const SEQ_LEFT_MARGIN = 100;
        const SEQ_LIFELINE_START_Y = SEQ_TOP_MARGIN + SEQ_PARTICIPANT_HEIGHT + 20;

        window.loadSequenceDiagram = function(jsonData) {
            try {
                currentDiagramType = 'sequence';
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                seqDiagram = {
                    participants: (data.participants || []).map(p => ({
                        id: p.id,
                        alias: p.alias || null,
                        type: p.type || 'Participant',
                        isExplicit: p.isExplicit !== false,
                        isDestroyed: p.isDestroyed || false
                    })),
                    elements: data.elements || [],
                    autoNumber: data.autoNumber || false,
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Clear flowchart diagram state
                diagram.nodes = [];
                diagram.edges = [];
                diagram.subgraphs = [];

                // Hide property panel from previous diagram
                propertyPanel.classList.remove('visible');

                // Update toolbar buttons for sequence diagram
                updateToolbarForDiagramType();

                renderSequenceDiagram();
                centerView();
                // Deferred re-render: on first load the container may not have its final
                // dimensions yet (WebView still sizing), causing a scrunched layout.
                setTimeout(function() { if (seqDiagram) { renderSequenceDiagram(); centerView(); } }, 150);
            } catch (err) {
                console.error('Failed to load sequence diagram:', err);
            }
        };

        // Refresh sequence diagram data without resetting view (called after C# model changes)
        window.refreshSequenceDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                seqDiagram = {
                    participants: (data.participants || []).map(p => ({
                        id: p.id,
                        alias: p.alias || null,
                        type: p.type || 'Participant',
                        isExplicit: p.isExplicit !== false,
                        isDestroyed: p.isDestroyed || false
                    })),
                    elements: data.elements || [],
                    autoNumber: data.autoNumber || false,
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Auto-select newly created note so property panel targets the correct one
                if (_seqPendingNoteSelect) {
                    _seqPendingNoteSelect = false;
                    for (let i = seqDiagram.elements.length - 1; i >= 0; i--) {
                        if (seqDiagram.elements[i].elementType === 'note') {
                            seqSelectedNoteIndex = i;
                            seqSelectedParticipantId = null;
                            seqSelectedMessageIndex = -1;
                            seqSelectedFragmentIndex = -1;
                            seqSelectedFragInnerMsg = null;
                            renderSequenceDiagram();
                            showSeqNotePropertyPanel(i);
                            return;
                        }
                    }
                }

                renderSequenceDiagram();
            } catch (err) {
                console.error('Failed to refresh sequence diagram:', err);
            }
        };

        // updateToolbarForSequenceDiagram removed - using unified updateToolbarForDiagramType()

        function getParticipantX(index) {
            return SEQ_LEFT_MARGIN + index * SEQ_PARTICIPANT_GAP;
        }

        function getParticipantLabel(p) {
            return p.alias || p.id;
        }

        function getParticipantIndex(participantId) {
            if (!seqDiagram) return -1;
            return seqDiagram.participants.findIndex(p => p.id === participantId);
        }

        // Resolve a fragment-inner message element from data attributes
        // (must be in outer scope so property panel and context menu handlers can access it)
        function getFragmentInnerMessage(fragIdx, sectionIdx, subIdx) {
            if (!seqDiagram) return null;
            const frag = seqDiagram.elements[fragIdx];
            if (!frag || frag.elementType !== 'fragment') return null;
            if (!frag.sections || sectionIdx < 0 || sectionIdx >= frag.sections.length) return null;
            const sec = frag.sections[sectionIdx];
            if (!sec.elements || subIdx < 0 || subIdx >= sec.elements.length) return null;
            return sec.elements[subIdx];
        }

        function getMessageY(msgIndex) {
            return SEQ_LIFELINE_START_Y + 30 + msgIndex * SEQ_MESSAGE_SPACING;
        }

        // Count only messages (not notes, fragments, activations) for vertical positioning
        function getVisualMessageIndex(elementIndex) {
            if (!seqDiagram) return 0;
            let visualIdx = 0;
            for (let i = 0; i < elementIndex; i++) {
                const el = seqDiagram.elements[i];
                if (el.elementType === 'message') visualIdx++;
            }
            return visualIdx;
        }

        function getTotalVisualHeight() {
            if (!seqDiagram) return 400;
            let y = SEQ_LIFELINE_START_Y + 30;
            seqDiagram.elements.forEach(el => {
                if (el.elementType === 'message') {
                    y += SEQ_MESSAGE_SPACING;
                } else if (el.elementType === 'fragment') {
                    y += 25; // fragment header
                    if (el.sections) {
                        el.sections.forEach((s, sIdx) => {
                            if (sIdx > 0) y += 20; // section divider
                            if (s.elements) {
                                s.elements.forEach(se => { if (se.elementType === 'message') y += SEQ_MESSAGE_SPACING; });
                            }
                        });
                    }
                    y += 15; // fragment bottom padding
                } else if (el.elementType === 'note') {
                    y += 40;
                }
            });
            return y + SEQ_PARTICIPANT_HEIGHT + 30;
        }

        function renderSequenceDiagram() {
            if (!seqDiagram) return;

            // Show diagram-svg, hide editorCanvas when rendering standard diagram types
            var dSvg = document.getElementById('diagram-svg');
            var eCanvas = document.getElementById('editorCanvas');
            if (dSvg) dSvg.style.display = '';
            if (eCanvas) { eCanvas.style.display = 'none'; eCanvas.innerHTML = ''; }

            nodesLayer.innerHTML = '';
            edgesLayer.innerHTML = '';
            subgraphsLayer.innerHTML = '';

            const totalHeight = getTotalVisualHeight();
            const participants = seqDiagram.participants;

            // ===== Render Participants (top boxes) =====
            participants.forEach((p, idx) => {
                const x = getParticipantX(idx);
                const y = SEQ_TOP_MARGIN;
                const isActor = p.type === 'Actor';

                const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                g.classList.add('node-group', 'seq-participant');
                g.setAttribute('data-participant-id', p.id);
                g.setAttribute('transform', `translate(${x}, ${y})`);

                if (isActor) {
                    // Draw stick figure for Actor
                    const actorG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                    // Head
                    const head = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    head.setAttribute('cx', '0');
                    head.setAttribute('cy', '-12');
                    head.setAttribute('r', '8');
                    head.classList.add('node-shape');
                    if (seqSelectedParticipantId === p.id) head.classList.add('selected');
                    actorG.appendChild(head);
                    // Body
                    const body = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    body.setAttribute('x1', '0'); body.setAttribute('y1', '-4');
                    body.setAttribute('x2', '0'); body.setAttribute('y2', '10');
                    body.setAttribute('stroke', 'var(--node-stroke)'); body.setAttribute('stroke-width', '1.5');
                    actorG.appendChild(body);
                    // Arms
                    const arms = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    arms.setAttribute('x1', '-10'); arms.setAttribute('y1', '0');
                    arms.setAttribute('x2', '10'); arms.setAttribute('y2', '0');
                    arms.setAttribute('stroke', 'var(--node-stroke)'); arms.setAttribute('stroke-width', '1.5');
                    actorG.appendChild(arms);
                    // Legs
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
                    // Draw box for Participant
                    const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    rect.setAttribute('x', -SEQ_PARTICIPANT_WIDTH / 2);
                    rect.setAttribute('y', -SEQ_PARTICIPANT_HEIGHT / 2);
                    rect.setAttribute('width', SEQ_PARTICIPANT_WIDTH);
                    rect.setAttribute('height', SEQ_PARTICIPANT_HEIGHT);
                    rect.setAttribute('rx', '4');
                    rect.classList.add('node-shape');
                    if (seqSelectedParticipantId === p.id) rect.classList.add('selected');
                    g.appendChild(rect);
                }

                // Label
                const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text.classList.add('node-label');
                text.setAttribute('x', '0');
                text.setAttribute('y', isActor ? '30' : '0');
                text.textContent = getParticipantLabel(p);
                g.appendChild(text);

                nodesLayer.appendChild(g);

                // ===== Bottom participant box (mirror) =====
                const bottomY = totalHeight;
                const gBottom = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                gBottom.classList.add('node-group', 'seq-participant-bottom');
                gBottom.setAttribute('data-participant-id', p.id);
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
                    rect2.setAttribute('x', -SEQ_PARTICIPANT_WIDTH / 2);
                    rect2.setAttribute('y', -SEQ_PARTICIPANT_HEIGHT / 2);
                    rect2.setAttribute('width', SEQ_PARTICIPANT_WIDTH);
                    rect2.setAttribute('height', SEQ_PARTICIPANT_HEIGHT);
                    rect2.setAttribute('rx', '4');
                    rect2.classList.add('node-shape');
                    gBottom.appendChild(rect2);
                }

                const text2 = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text2.classList.add('node-label');
                text2.setAttribute('x', '0');
                text2.setAttribute('y', isActor ? '30' : '0');
                text2.textContent = getParticipantLabel(p);
                gBottom.appendChild(text2);
                nodesLayer.appendChild(gBottom);
            });

            // ===== Render Lifelines =====
            participants.forEach((p, idx) => {
                const x = getParticipantX(idx);
                const startY = SEQ_LIFELINE_START_Y;
                const endY = totalHeight - SEQ_PARTICIPANT_HEIGHT / 2 - 10;

                const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                line.setAttribute('x1', x);
                line.setAttribute('y1', startY);
                line.setAttribute('x2', x);
                line.setAttribute('y2', endY);
                line.setAttribute('stroke', 'var(--edge-color)');
                line.setAttribute('stroke-width', '1');
                line.setAttribute('stroke-dasharray', '6,3');
                line.classList.add('seq-lifeline');
                subgraphsLayer.appendChild(line);
            });

            // ===== Render Elements (messages, notes, fragments) =====
            let currentY = SEQ_LIFELINE_START_Y + 30; // cumulative Y position tracker
            let msgNumber = seqDiagram.autoNumber ? 1 : 0;
            const activationStacks = {}; // participantId -> [{startY}]

            // Helper: render a single message arrow (used for both top-level and fragment-inner messages)
            // For fragment-inner messages, pass sectionIdx and subIdx to tag them appropriately
            function renderMessageArrow(el, elIdx, absY, sectionIdx, subIdx) {
                const fromIdx = getParticipantIndex(el.fromId);
                const toIdx = getParticipantIndex(el.toId);
                if (fromIdx < 0 || toIdx < 0) return;

                const fromX = getParticipantX(fromIdx);
                const toX = getParticipantX(toIdx);
                const y = absY;
                const isSelfMessage = el.fromId === el.toId;
                const isFragmentInner = (sectionIdx !== undefined && subIdx !== undefined);
                // Determine if this specific message is selected
                const isThisMsgSelected = isFragmentInner
                    ? (seqSelectedFragInnerMsg && seqSelectedFragInnerMsg.fragIdx === elIdx && seqSelectedFragInnerMsg.secIdx === sectionIdx && seqSelectedFragInnerMsg.subIdx === subIdx)
                    : (seqSelectedMessageIndex === elIdx);

                const msgGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
                msgGroup.classList.add('edge-group', 'seq-message');
                msgGroup.setAttribute('data-element-index', elIdx);
                msgGroup.style.cursor = 'pointer';
                if (isFragmentInner) {
                    msgGroup.setAttribute('data-section-index', sectionIdx);
                    msgGroup.setAttribute('data-sub-index', subIdx);
                    msgGroup.classList.add('seq-fragment-inner-message');
                }

                // Add a transparent hit-area rect as the first child so the entire
                // message region is clickable (prevents clicks falling through to
                // the fragment background underneath for fragment-inner messages).
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

                if (isSelfMessage) {
                    // Self-message: loop arrow
                    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                    const loopW = 40;
                    const loopH = 30;
                    path.setAttribute('d', `M ${fromX} ${y} H ${fromX + loopW} V ${y + loopH} H ${fromX}`);
                    path.classList.add('edge-line');
                    if (isThisMsgSelected) path.classList.add('selected');
                    applyArrowStyle(path, el.arrowType);
                    msgGroup.appendChild(path);

                    // Invisible wider hit area for self-message
                    const hitPath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                    hitPath.setAttribute('d', `M ${fromX} ${y} H ${fromX + loopW} V ${y + loopH} H ${fromX}`);
                    hitPath.setAttribute('stroke', 'transparent');
                    hitPath.setAttribute('stroke-width', '12');
                    hitPath.setAttribute('fill', 'none');
                    hitPath.setAttribute('cursor', 'pointer');
                    hitPath.setAttribute('pointer-events', 'all');
                    msgGroup.appendChild(hitPath);

                    // Arrowhead for self-message
                    const arrowPoly = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                    arrowPoly.setAttribute('points', `${fromX + 8},${y + loopH - 4} ${fromX},${y + loopH} ${fromX + 8},${y + loopH + 4}`);
                    arrowPoly.classList.add('edge-arrowhead');
                    if (isThisMsgSelected) arrowPoly.classList.add('selected');
                    msgGroup.appendChild(arrowPoly);

                    // Label
                    if (el.text) {
                        const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                        label.classList.add('edge-label');
                        label.setAttribute('x', fromX + loopW + 5);
                        label.setAttribute('y', y + loopH / 2);
                        label.setAttribute('text-anchor', 'start');
                        setTextWithLineBreaks(label, (msgNumber > 0 ? msgNumber + '. ' : '') + el.text);
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
                    if (isThisMsgSelected) line.classList.add('selected');
                    applyArrowStyle(line, el.arrowType);
                    msgGroup.appendChild(line);

                    // Arrowhead
                    const isOpen = (el.arrowType || '').includes('Open') || (el.arrowType || '').includes('Async');
                    const isCross = (el.arrowType || '').includes('Cross');
                    const dir = toX > fromX ? 1 : -1;

                    if (isCross) {
                        // Cross (X) arrowhead
                        const size = 6;
                        const cx = toX;
                        const cy = y;
                        const cross1 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                        cross1.setAttribute('x1', cx - size); cross1.setAttribute('y1', cy - size);
                        cross1.setAttribute('x2', cx + size); cross1.setAttribute('y2', cy + size);
                        cross1.setAttribute('stroke', 'var(--edge-color)'); cross1.setAttribute('stroke-width', '2');
                        if (isThisMsgSelected) cross1.setAttribute('stroke', 'var(--node-selected-stroke)');
                        msgGroup.appendChild(cross1);
                        const cross2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                        cross2.setAttribute('x1', cx - size); cross2.setAttribute('y1', cy + size);
                        cross2.setAttribute('x2', cx + size); cross2.setAttribute('y2', cy - size);
                        cross2.setAttribute('stroke', 'var(--edge-color)'); cross2.setAttribute('stroke-width', '2');
                        if (isThisMsgSelected) cross2.setAttribute('stroke', 'var(--node-selected-stroke)');
                        msgGroup.appendChild(cross2);
                    } else if (isOpen) {
                        // Open arrowhead (V shape, no fill)
                        const arrow = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
                        arrow.setAttribute('points', `${toX - dir * 10},${y - 5} ${toX},${y} ${toX - dir * 10},${y + 5}`);
                        arrow.setAttribute('fill', 'none');
                        arrow.setAttribute('stroke', 'var(--edge-color)');
                        arrow.setAttribute('stroke-width', '1.5');
                        if (isThisMsgSelected) arrow.setAttribute('stroke', 'var(--node-selected-stroke)');
                        msgGroup.appendChild(arrow);
                    } else {
                        // Filled arrowhead
                        const arrowPoly = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                        arrowPoly.setAttribute('points', `${toX - dir * 10},${y - 5} ${toX},${y} ${toX - dir * 10},${y + 5}`);
                        arrowPoly.classList.add('edge-arrowhead');
                        if (isThisMsgSelected) arrowPoly.classList.add('selected');
                        msgGroup.appendChild(arrowPoly);
                    }

                    // Invisible wider hit area for easier click/right-click target
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
                    if (el.text) {
                        const midX = (fromX + toX) / 2;
                        const labelBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                        const labelText = (msgNumber > 0 ? msgNumber + '. ' : '') + el.text;
                        const textWidth = estimateLabelWidth(labelText, 7) + 12;
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

                // Handle activation
                if (el.activateTarget) {
                    if (!activationStacks[el.toId]) activationStacks[el.toId] = [];
                    activationStacks[el.toId].push({ startY: y });
                }
                if (el.deactivateSource) {
                    const stack = activationStacks[el.fromId];
                    if (stack && stack.length > 0) {
                        const activation = stack.pop();
                        renderActivationBox(el.fromId, activation.startY, y);
                    }
                }
            }

            seqDiagram.elements.forEach((el, elIdx) => {
                if (el.elementType === 'message') {
                    const fromIdx = getParticipantIndex(el.fromId);
                    const toIdx = getParticipantIndex(el.toId);
                    if (fromIdx < 0 || toIdx < 0) { currentY += SEQ_MESSAGE_SPACING; return; }

                    renderMessageArrow(el, elIdx, currentY);

                    if (msgNumber > 0) msgNumber++;
                    currentY += SEQ_MESSAGE_SPACING;
                }
                else if (el.elementType === 'note') {
                    renderSequenceNote(el, currentY, elIdx);
                    currentY += 40; // note height + spacing
                }
                else if (el.elementType === 'fragment') {
                    const fragStartY = currentY;
                    currentY += 25; // fragment header height
                    // Render messages and notes inside fragment sections
                    if (el.sections) {
                        el.sections.forEach((s, sIdx) => {
                            if (sIdx > 0) currentY += 20; // section divider space
                            if (s.elements) {
                                s.elements.forEach((se, seIdx) => {
                                    if (se.elementType === 'message') {
                                        renderMessageArrow(se, elIdx, currentY, sIdx, seIdx);
                                        if (msgNumber > 0) msgNumber++;
                                        currentY += SEQ_MESSAGE_SPACING;
                                    } else if (se.elementType === 'note') {
                                        renderSequenceNote(se, currentY, elIdx);
                                        currentY += 40;
                                    }
                                });
                            }
                        });
                    }
                    currentY += 15; // fragment bottom padding
                    renderSequenceFragment(el, elIdx, fragStartY, currentY);
                }
                else if (el.elementType === 'activate') {
                    if (!activationStacks[el.participantId]) activationStacks[el.participantId] = [];
                    activationStacks[el.participantId].push({ startY: currentY });
                }
                else if (el.elementType === 'deactivate') {
                    const stack = activationStacks[el.participantId];
                    if (stack && stack.length > 0) {
                        const activation = stack.pop();
                        renderActivationBox(el.participantId, activation.startY, currentY);
                    }
                }
            });

            // Close any remaining open activations
            Object.keys(activationStacks).forEach(pid => {
                const stack = activationStacks[pid];
                while (stack.length > 0) {
                    const activation = stack.pop();
                    renderActivationBox(pid, activation.startY, getTotalVisualHeight() - 60);
                }
            });

            // ===== Render clickable zones between lifelines for adding messages =====
            if (participants.length >= 2) {
                for (let i = 0; i < participants.length - 1; i++) {
                    const x1 = getParticipantX(i);
                    const x2 = getParticipantX(i + 1);
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
                    addZone.addEventListener('click', function(e) {
                        e.stopPropagation();
                        postMessage({
                            type: 'seq_messageCreated',
                            fromId: fromId,
                            toId: toId,
                            text: 'Message',
                            arrowType: 'SolidArrow'
                        });
                    });
                    addText.addEventListener('click', function(e) {
                        e.stopPropagation();
                        postMessage({
                            type: 'seq_messageCreated',
                            fromId: fromId,
                            toId: toId,
                            text: 'Message',
                            arrowType: 'SolidArrow'
                        });
                    });

                    nodesLayer.appendChild(addZone);
                    nodesLayer.appendChild(addText);
                }
            }

            updateMinimap();
            updateCanvasAndZoomLimits();
        }

        function applyArrowStyle(lineEl, arrowType) {
            arrowType = arrowType || 'SolidArrow';
            if (arrowType.startsWith('Dotted')) {
                lineEl.setAttribute('stroke-dasharray', '6,3');
            }
        }

        function renderActivationBox(participantId, startY, endY) {
            const idx = getParticipantIndex(participantId);
            if (idx < 0) return;
            const x = getParticipantX(idx);
            const boxWidth = 12;

            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', x - boxWidth / 2);
            rect.setAttribute('y', startY);
            rect.setAttribute('width', boxWidth);
            rect.setAttribute('height', Math.max(endY - startY, 10));
            rect.setAttribute('fill', 'var(--node-fill)');
            rect.setAttribute('stroke', 'var(--node-stroke)');
            rect.setAttribute('stroke-width', '1.5');
            rect.classList.add('seq-activation');
            subgraphsLayer.appendChild(rect);
        }

        function renderSequenceNote(note, absY, elIdx) {
            if (!seqDiagram) return;
            const y = absY - 10;

            // Parse overParticipants to find positioning
            const overParts = (note.overParticipants || '').split(',').map(s => s.trim()).filter(Boolean);
            let noteX = SEQ_LEFT_MARGIN;
            let noteWidth = 120;

            if (overParts.length > 0) {
                const indices = overParts.map(pid => getParticipantIndex(pid)).filter(i => i >= 0);
                if (indices.length > 0) {
                    const minIdx = Math.min(...indices);
                    const maxIdx = Math.max(...indices);
                    noteX = (getParticipantX(minIdx) + getParticipantX(maxIdx)) / 2;
                }
            }

            const position = note.notePosition || 'RightOf';
            if (position === 'LeftOf' && overParts.length > 0) {
                const pIdx = getParticipantIndex(overParts[0]);
                if (pIdx >= 0) {
                    const px = getParticipantX(pIdx);
                    if (pIdx > 0) {
                        // Center note between previous lifeline and this one
                        const prevPx = getParticipantX(pIdx - 1);
                        noteX = (prevPx + px) / 2;
                    } else {
                        // First participant — place to the left of the lifeline
                        noteX = px - 10 - noteWidth / 2;
                    }
                }
            } else if (position === 'RightOf' && overParts.length > 0) {
                const pIdx = getParticipantIndex(overParts[0]);
                if (pIdx >= 0) {
                    const px = getParticipantX(pIdx);
                    if (pIdx < seqDiagram.participants.length - 1) {
                        // Center note between this lifeline and the next one
                        const nextPx = getParticipantX(pIdx + 1);
                        noteX = (px + nextPx) / 2;
                    } else {
                        // Last participant — place to the right of the lifeline
                        noteX = px + 10 + noteWidth / 2;
                    }
                }
            }

            const isSelected = (seqSelectedNoteIndex === elIdx);

            const noteG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            noteG.classList.add('seq-note');
            noteG.setAttribute('data-element-index', elIdx);
            noteG.style.cursor = 'pointer';

            const noteRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            noteRect.setAttribute('x', noteX - noteWidth / 2);
            noteRect.setAttribute('y', y);
            noteRect.setAttribute('width', noteWidth);
            noteRect.setAttribute('height', 30);
            noteRect.setAttribute('rx', '2');
            noteRect.setAttribute('fill', '#FFFFCC');
            noteRect.setAttribute('stroke', isSelected ? 'var(--node-selected-stroke)' : '#999900');
            noteRect.setAttribute('stroke-width', isSelected ? '2.5' : '1');
            noteG.appendChild(noteRect);

            // Fold corner
            const fold = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            const fx = noteX + noteWidth / 2;
            fold.setAttribute('d', `M ${fx - 10} ${y} L ${fx} ${y + 10} L ${fx - 10} ${y + 10} Z`);
            fold.setAttribute('fill', '#EEEE88');
            fold.setAttribute('stroke', '#999900');
            fold.setAttribute('stroke-width', '0.5');
            noteG.appendChild(fold);

            const noteText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            noteText.setAttribute('x', noteX);
            noteText.setAttribute('y', y + 18);
            noteText.setAttribute('text-anchor', 'middle');
            noteText.setAttribute('font-size', '11');
            noteText.setAttribute('fill', '#333');
            setTextWithLineBreaks(noteText, note.text || '');
            noteG.appendChild(noteText);

            edgesLayer.appendChild(noteG);
        }

        function renderSequenceFragment(fragment, elIdx, fragStartY, fragEndY) {
            if (!seqDiagram) return;
            const startY = fragStartY - 10;
            const endY = fragEndY;

            // Determine horizontal extent based on participants used by messages inside the fragment
            const participants = seqDiagram.participants;
            let minPIdx = participants.length - 1;
            let maxPIdx = 0;

            // First, auto-calculate from inner messages (always needed as fallback)
            let autoMinP = participants.length - 1;
            let autoMaxP = 0;
            if (fragment.sections) {
                fragment.sections.forEach(sec => {
                    if (sec.elements) {
                        sec.elements.forEach(se => {
                            if (se.elementType === 'message') {
                                const fi = getParticipantIndex(se.fromId);
                                const ti = getParticipantIndex(se.toId);
                                if (fi >= 0) { autoMinP = Math.min(autoMinP, fi); autoMaxP = Math.max(autoMaxP, fi); }
                                if (ti >= 0) { autoMinP = Math.min(autoMinP, ti); autoMaxP = Math.max(autoMaxP, ti); }
                            }
                        });
                    }
                });
            }
            // If no messages found, fall back to spanning all participants
            if (autoMinP > autoMaxP) { autoMinP = 0; autoMaxP = Math.max(0, participants.length - 1); }

            // Check if manual participant range is set (overParticipantStart / overParticipantEnd)
            const hasManualStart = fragment.overParticipantStart && fragment.overParticipantStart.length > 0;
            const hasManualEnd = fragment.overParticipantEnd && fragment.overParticipantEnd.length > 0;
            if (hasManualStart) {
                const si = getParticipantIndex(fragment.overParticipantStart);
                if (si >= 0) minPIdx = si;
                else minPIdx = autoMinP;
            } else {
                minPIdx = autoMinP;
            }
            if (hasManualEnd) {
                const ei = getParticipantIndex(fragment.overParticipantEnd);
                if (ei >= 0) maxPIdx = ei;
                else maxPIdx = autoMaxP;
            } else {
                maxPIdx = autoMaxP;
            }
            // Ensure minPIdx <= maxPIdx (swap if user set them backwards)
            if (minPIdx > maxPIdx) { const tmp = minPIdx; minPIdx = maxPIdx; maxPIdx = tmp; }
            const leftX = participants.length > 0 ? getParticipantX(minPIdx) - SEQ_PARTICIPANT_WIDTH / 2 - 10 : SEQ_LEFT_MARGIN - 40;
            const rightX = participants.length > 0 ? getParticipantX(maxPIdx) + SEQ_PARTICIPANT_WIDTH / 2 + 10 : SEQ_LEFT_MARGIN + 200;

            const fragG = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            fragG.classList.add('seq-fragment');
            fragG.setAttribute('data-element-index', elIdx);
            fragG.style.cursor = 'pointer';

            const isSelected = (seqSelectedFragmentIndex === elIdx);

            // Main rectangle (pointer-events: none so clicks pass through to messages in edgesLayer)
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

            // Clickable border frame around the fragment (thin strips along edges)
            // so users can still click the fragment border to select it
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

            // Drag grips on left and right edges (only when selected)
            if (isSelected) {
                const gripWidth = 6;
                const gripHeight = 30;
                const gripY = startY + (endY - startY) / 2 - gripHeight / 2;
                ['left', 'right'].forEach(side => {
                    const grip = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    const gx = side === 'left' ? leftX - gripWidth / 2 : rightX - gripWidth / 2;
                    grip.setAttribute('x', gx);
                    grip.setAttribute('y', gripY);
                    grip.setAttribute('width', gripWidth);
                    grip.setAttribute('height', gripHeight);
                    grip.setAttribute('rx', '2');
                    grip.setAttribute('fill', 'var(--node-selected-stroke)');
                    grip.setAttribute('fill-opacity', '0.7');
                    grip.setAttribute('stroke', 'var(--node-selected-stroke)');
                    grip.setAttribute('stroke-width', '1');
                    grip.setAttribute('pointer-events', 'all');
                    grip.style.cursor = 'col-resize';
                    grip.classList.add('seq-frag-grip');
                    grip.setAttribute('data-grip-side', side);
                    grip.setAttribute('data-element-index', elIdx);
                    fragG.appendChild(grip);
                });
            }

            // Fragment type label (e.g., "loop", "alt", "opt")
            const typeLabel = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            typeLabel.setAttribute('pointer-events', 'none');
            const typeBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            const fragTypeText = (fragment.fragmentType || fragment.type || 'loop').toLowerCase();
            const typeWidth = fragTypeText.length * 8 + 16;
            typeBg.setAttribute('x', leftX);
            typeBg.setAttribute('y', startY);
            typeBg.setAttribute('width', typeWidth);
            typeBg.setAttribute('height', 20);
            typeBg.setAttribute('fill', 'var(--toolbar-bg)');
            typeBg.setAttribute('stroke', 'var(--subgraph-stroke)');
            typeBg.setAttribute('stroke-width', '1');
            typeLabel.appendChild(typeBg);

            const typeText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            typeText.setAttribute('x', leftX + 8);
            typeText.setAttribute('y', startY + 14);
            typeText.setAttribute('font-size', '11');
            typeText.setAttribute('font-weight', 'bold');
            typeText.setAttribute('fill', 'var(--toolbar-text)');
            typeText.textContent = fragTypeText;
            typeLabel.appendChild(typeText);
            fragG.appendChild(typeLabel);

            // Condition label
            if (fragment.text) {
                const condText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                condText.setAttribute('x', leftX + typeWidth + 10);
                condText.setAttribute('y', startY + 14);
                condText.setAttribute('font-size', '11');
                condText.setAttribute('fill', 'var(--subgraph-text)');
                condText.setAttribute('pointer-events', 'none');
                condText.textContent = '[' + fragment.text + ']';
                fragG.appendChild(condText);
            }

            // Section dividers (for alt/else etc.)
            if (fragment.sections && fragment.sections.length > 1) {
                // Compute divider positions by walking through sections
                let divY = startY + 25; // after fragment header
                for (let si = 0; si < fragment.sections.length; si++) {
                    // Count messages in this section
                    let secMsgCount = 0;
                    if (fragment.sections[si].elements) {
                        fragment.sections[si].elements.forEach(se => { if (se.elementType === 'message') secMsgCount++; });
                    }
                    divY += secMsgCount * SEQ_MESSAGE_SPACING;

                    if (si < fragment.sections.length - 1) {
                        // Draw divider between sections
                        const divider = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                        divider.setAttribute('x1', leftX);
                        divider.setAttribute('y1', divY);
                        divider.setAttribute('x2', rightX);
                        divider.setAttribute('y2', divY);
                        divider.setAttribute('stroke', 'var(--subgraph-stroke)');
                        divider.setAttribute('stroke-width', '1');
                        divider.setAttribute('stroke-dasharray', '5,3');
                        divider.setAttribute('pointer-events', 'none');
                        fragG.appendChild(divider);

                        // Section label
                        if (fragment.sections[si + 1].label) {
                            const secLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                            secLabel.setAttribute('x', leftX + 10);
                            secLabel.setAttribute('y', divY + 14);
                            secLabel.setAttribute('font-size', '11');
                            secLabel.setAttribute('fill', 'var(--subgraph-text)');
                            secLabel.setAttribute('pointer-events', 'none');
                            secLabel.textContent = '[' + fragment.sections[si + 1].label + ']';
                            fragG.appendChild(secLabel);
                        }

                        divY += 20; // section divider space
                    }
                }
            }

            subgraphsLayer.appendChild(fragG);
        }

        // ===== Sequence Diagram Interaction Handlers =====

        // Participant drag to reorder + Fragment grip drag to resize
        svg.addEventListener('mousedown', function(e) {
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;

            // Check if mousedown is on a fragment grip
            const gripEl = e.target.closest('.seq-frag-grip');
            if (gripEl) {
                const elIdx = parseInt(gripEl.getAttribute('data-element-index'), 10);
                const side = gripEl.getAttribute('data-grip-side');
                seqDraggingFragGrip = { elIdx, side };
                seqFragGripDragStartX = e.clientX;
                e.preventDefault();
                e.stopPropagation();
                return;
            }

            const participantGroup = e.target.closest('.seq-participant') || e.target.closest('.seq-participant-bottom');
            if (participantGroup) {
                const pid = participantGroup.getAttribute('data-participant-id');
                if (pid) {
                    seqDraggingParticipantId = pid;
                    seqDragStartX = e.clientX;
                    seqDragParticipantStartIdx = getParticipantIndex(pid);
                    e.preventDefault();
                    e.stopPropagation();
                }
            }
        });

        svg.addEventListener('mousemove', function(e) {
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;
            // Fragment grip drag visual feedback (cursor)
            if (seqDraggingFragGrip) {
                e.preventDefault();
                return;
            }
            if (!seqDraggingParticipantId) return;
            // Visual feedback during drag would require re-rendering on each mouse move
            // For now, we just track the drag
        });

        svg.addEventListener('mouseup', function(e) {
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;

            // Handle fragment grip drag end
            if (seqDraggingFragGrip) {
                const dx = e.clientX - seqFragGripDragStartX;
                const transform = panzoomInstance ? panzoomInstance.getTransform() : { scale: 1 };
                const svgDx = dx / transform.scale;
                const positionsShifted = Math.round(svgDx / SEQ_PARTICIPANT_GAP);

                if (positionsShifted !== 0) {
                    const elIdx = seqDraggingFragGrip.elIdx;
                    const side = seqDraggingFragGrip.side;
                    const fragment = seqDiagram.elements[elIdx];
                    if (fragment) {
                        const participants = seqDiagram.participants;
                        // Get current start/end indices
                        let curStartIdx = 0;
                        let curEndIdx = participants.length - 1;
                        if (fragment.overParticipantStart) {
                            const si = getParticipantIndex(fragment.overParticipantStart);
                            if (si >= 0) curStartIdx = si;
                        } else {
                            // Auto-calculate current bounds from inner messages
                            let autoMin = participants.length - 1, autoMax = 0;
                            if (fragment.sections) {
                                fragment.sections.forEach(sec => {
                                    if (sec.elements) sec.elements.forEach(se => {
                                        if (se.elementType === 'message') {
                                            const fi = getParticipantIndex(se.fromId);
                                            const ti = getParticipantIndex(se.toId);
                                            if (fi >= 0) { autoMin = Math.min(autoMin, fi); autoMax = Math.max(autoMax, fi); }
                                            if (ti >= 0) { autoMin = Math.min(autoMin, ti); autoMax = Math.max(autoMax, ti); }
                                        }
                                    });
                                });
                            }
                            if (autoMin > autoMax) { autoMin = 0; autoMax = participants.length - 1; }
                            curStartIdx = autoMin;
                            curEndIdx = autoMax;
                        }
                        if (fragment.overParticipantEnd) {
                            const ei = getParticipantIndex(fragment.overParticipantEnd);
                            if (ei >= 0) curEndIdx = ei;
                        } else if (!fragment.overParticipantStart) {
                            // Already set above from auto-calc
                        }

                        let newStartIdx = curStartIdx;
                        let newEndIdx = curEndIdx;
                        if (side === 'left') {
                            newStartIdx = Math.max(0, Math.min(participants.length - 1, curStartIdx + positionsShifted));
                        } else {
                            newEndIdx = Math.max(0, Math.min(participants.length - 1, curEndIdx + positionsShifted));
                        }
                        // Ensure start <= end
                        if (newStartIdx > newEndIdx) {
                            const tmp = newStartIdx; newStartIdx = newEndIdx; newEndIdx = tmp;
                        }
                        const newStartId = participants[newStartIdx].id;
                        const newEndId = participants[newEndIdx].id;
                        postMessage({
                            type: 'seq_fragmentEdited',
                            elementIndex: elIdx,
                            overParticipantStart: newStartId,
                            overParticipantEnd: newEndId
                        });
                    }
                }
                seqDraggingFragGrip = null;
                return;
            }

            if (!seqDraggingParticipantId) return;
            const dx = e.clientX - seqDragStartX;
            const transform = panzoomInstance ? panzoomInstance.getTransform() : { scale: 1 };
            const svgDx = dx / transform.scale;

            // Calculate how many positions to shift
            const positionsShifted = Math.round(svgDx / SEQ_PARTICIPANT_GAP);
            if (positionsShifted !== 0) {
                const newIdx = Math.max(0, Math.min(seqDiagram.participants.length - 1,
                    seqDragParticipantStartIdx + positionsShifted));
                if (newIdx !== seqDragParticipantStartIdx) {
                    // Reorder participants
                    const newOrder = seqDiagram.participants.map(p => p.id);
                    const [removed] = newOrder.splice(seqDragParticipantStartIdx, 1);
                    newOrder.splice(newIdx, 0, removed);
                    postMessage({ type: 'seq_participantReordered', participantIds: newOrder });
                }
            }
            seqDraggingParticipantId = null;
        });

        // Click on participant to select
        svg.addEventListener('click', function(e) {
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;
            const participantGroup = e.target.closest('.seq-participant') || e.target.closest('.seq-participant-bottom');
            const messageGroup = e.target.closest('.seq-message');

            const fragmentGroup = e.target.closest('.seq-fragment');

            if (participantGroup) {
                const pid = participantGroup.getAttribute('data-participant-id');
                seqSelectedParticipantId = pid;
                seqSelectedMessageIndex = -1;
                seqSelectedFragmentIndex = -1;
                seqSelectedFragInnerMsg = null;
                seqSelectedNoteIndex = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderSequenceDiagram();
                showSeqParticipantPropertyPanel(pid);
                e.stopPropagation();
                return;
            }

            if (messageGroup) {
                const elIdx = parseInt(messageGroup.getAttribute('data-element-index'), 10);
                const isFragInner = messageGroup.hasAttribute('data-section-index');
                seqSelectedParticipantId = null;
                seqSelectedNoteIndex = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                if (isFragInner) {
                    const secIdx = parseInt(messageGroup.getAttribute('data-section-index'), 10);
                    const subIdx = parseInt(messageGroup.getAttribute('data-sub-index'), 10);
                    seqSelectedMessageIndex = -1;
                    seqSelectedFragmentIndex = -1;
                    seqSelectedFragInnerMsg = { fragIdx: elIdx, secIdx: secIdx, subIdx: subIdx };
                    renderSequenceDiagram();
                    showSeqFragmentInnerMessagePropertyPanel(elIdx, secIdx, subIdx);
                } else {
                    seqSelectedMessageIndex = elIdx;
                    seqSelectedFragmentIndex = -1;
                    seqSelectedFragInnerMsg = null;
                    renderSequenceDiagram();
                    showSeqMessagePropertyPanel(elIdx);
                }
                e.stopPropagation();
                return;
            }

            if (fragmentGroup) {
                const elIdx = parseInt(fragmentGroup.getAttribute('data-element-index'), 10);
                seqSelectedFragmentIndex = elIdx;
                seqSelectedFragInnerMsg = null;
                seqSelectedParticipantId = null;
                seqSelectedMessageIndex = -1;
                seqSelectedNoteIndex = -1;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderSequenceDiagram();
                showSeqFragmentPropertyPanel(elIdx);
                e.stopPropagation();
                return;
            }

            const noteGroup = e.target.closest('.seq-note');
            if (noteGroup) {
                const elIdx = parseInt(noteGroup.getAttribute('data-element-index'), 10);
                seqSelectedNoteIndex = elIdx;
                seqSelectedParticipantId = null;
                seqSelectedMessageIndex = -1;
                seqSelectedFragmentIndex = -1;
                seqSelectedFragInnerMsg = null;
                selectedNodeId = null;
                selectedEdgeIndex = -1;
                renderSequenceDiagram();
                showSeqNotePropertyPanel(elIdx);
                e.stopPropagation();
                return;
            }

            // Clicked on empty space — deselect
            if (seqDiagram) {
                seqSelectedParticipantId = null;
                seqSelectedMessageIndex = -1;
                seqSelectedFragmentIndex = -1;
                seqSelectedFragInnerMsg = null;
                seqSelectedNoteIndex = -1;
                propertyPanel.classList.remove('visible');
                renderSequenceDiagram();
            }
        });

        // Double-click to edit message text inline
        svg.addEventListener('dblclick', function(e) {
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;
            const messageGroup = e.target.closest('.seq-message');
            if (messageGroup) {
                const elIdx = parseInt(messageGroup.getAttribute('data-element-index'), 10);
                const isFragInner = messageGroup.hasAttribute('data-section-index');
                if (isFragInner) {
                    const secIdx = parseInt(messageGroup.getAttribute('data-section-index'), 10);
                    const subIdx = parseInt(messageGroup.getAttribute('data-sub-index'), 10);
                    showSeqFragmentInnerMessagePropertyPanel(elIdx, secIdx, subIdx);
                } else {
                    showSeqMessagePropertyPanel(elIdx);
                }
                e.stopPropagation();
                return;
            }

            const participantGroup = e.target.closest('.seq-participant') || e.target.closest('.seq-participant-bottom');
            if (participantGroup) {
                const pid = participantGroup.getAttribute('data-participant-id');
                const p = seqDiagram.participants.find(pp => pp.id === pid);
                if (p) {
                    startSeqParticipantInlineEdit(pid, p, e);
                }
                e.stopPropagation();
            }
        });

        function startSeqInlineEdit(elIdx, el, e) {
            const rect = svg.getBoundingClientRect();
            inlineEditor.style.display = '';
            inlineEditor.style.left = (e.clientX - 50) + 'px';
            inlineEditor.style.top = (e.clientY - 15) + 'px';
            inlineEditor.value = el.text || '';
            inlineEditor.focus();
            inlineEditor.select();

            function finishEdit() {
                const newText = inlineEditor.value.trim();
                inlineEditor.style.display = 'none';
                inlineEditor.removeEventListener('blur', finishEdit);
                inlineEditor.removeEventListener('keydown', onKey);
                if (newText !== (el.text || '')) {
                    postMessage({ type: 'seq_messageEdited', elementIndex: elIdx, text: newText });
                }
            }
            function onKey(e) {
                if (e.key === 'Enter') { finishEdit(); e.preventDefault(); }
                if (e.key === 'Escape') { inlineEditor.style.display = 'none'; inlineEditor.removeEventListener('blur', finishEdit); inlineEditor.removeEventListener('keydown', onKey); }
            }
            inlineEditor.addEventListener('blur', finishEdit);
            inlineEditor.addEventListener('keydown', onKey);
        }

        // Inline edit for messages inside fragments
        function startSeqFragmentInnerInlineEdit(fragIdx, sectionIdx, subIdx, el, e) {
            inlineEditor.style.display = '';
            inlineEditor.style.left = (e.clientX - 50) + 'px';
            inlineEditor.style.top = (e.clientY - 15) + 'px';
            inlineEditor.value = el.text || '';
            inlineEditor.focus();
            inlineEditor.select();

            function finishEdit() {
                const newText = inlineEditor.value.trim();
                inlineEditor.style.display = 'none';
                inlineEditor.removeEventListener('blur', finishEdit);
                inlineEditor.removeEventListener('keydown', onKey);
                if (newText !== (el.text || '')) {
                    postMessage({ type: 'seq_fragmentInnerMessageEdited', elementIndex: fragIdx, sectionIndex: sectionIdx, subIndex: subIdx, text: newText });
                }
            }
            function onKey(e) {
                if (e.key === 'Enter') { finishEdit(); e.preventDefault(); }
                if (e.key === 'Escape') { inlineEditor.style.display = 'none'; inlineEditor.removeEventListener('blur', finishEdit); inlineEditor.removeEventListener('keydown', onKey); }
            }
            inlineEditor.addEventListener('blur', finishEdit);
            inlineEditor.addEventListener('keydown', onKey);
        }

        function startSeqParticipantInlineEdit(pid, p, e) {
            inlineEditor.style.display = '';
            inlineEditor.style.left = (e.clientX - 50) + 'px';
            inlineEditor.style.top = (e.clientY - 15) + 'px';
            inlineEditor.value = p.alias || p.id;
            inlineEditor.focus();
            inlineEditor.select();

            function finishEdit() {
                const newAlias = inlineEditor.value.trim();
                inlineEditor.style.display = 'none';
                inlineEditor.removeEventListener('blur', finishEdit);
                inlineEditor.removeEventListener('keydown', onKey);
                if (newAlias && newAlias !== (p.alias || p.id)) {
                    postMessage({ type: 'seq_participantEdited', participantId: pid, alias: newAlias });
                }
            }
            function onKey(e) {
                if (e.key === 'Enter') { finishEdit(); e.preventDefault(); }
                if (e.key === 'Escape') { inlineEditor.style.display = 'none'; inlineEditor.removeEventListener('blur', finishEdit); inlineEditor.removeEventListener('keydown', onKey); }
            }
            inlineEditor.addEventListener('blur', finishEdit);
            inlineEditor.addEventListener('keydown', onKey);
        }

        // ===== Sequence Diagram Property Panels =====

        function showSeqParticipantPropertyPanel(pid) {
            if (!seqDiagram) return;
            const p = seqDiagram.participants.find(pp => pp.id === pid);
            if (!p) return;

            propPanelTitle.textContent = 'Participant';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">ID</div>
                    <input class="property-input" id="seq-prop-id" value="${escapeHtml(p.id)}" readonly style="opacity:0.6" />
                </div>
                <div class="property-row">
                    <div class="property-label">Display Name</div>
                    <input class="property-input" id="seq-prop-alias" value="${escapeHtml(p.alias || '')}" placeholder="(uses ID if empty)" />
                </div>
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-select" id="seq-prop-type">
                        <option value="Participant" ${p.type === 'Participant' ? 'selected' : ''}>Participant (Box)</option>
                        <option value="Actor" ${p.type === 'Actor' ? 'selected' : ''}>Actor (Stick Figure)</option>
                    </select>
                </div>
            `;

            document.getElementById('seq-prop-alias').addEventListener('change', function() {
                postMessage({ type: 'seq_participantEdited', participantId: pid, alias: this.value.trim() || null });
            });
            document.getElementById('seq-prop-type').addEventListener('change', function() {
                postMessage({ type: 'seq_participantEdited', participantId: pid, type: this.value });
            });

            propertyPanel.classList.add('visible');
        }

        function showSeqMessagePropertyPanel(elIdx) {
            if (!seqDiagram) return;
            const el = seqDiagram.elements[elIdx];
            if (!el || el.elementType !== 'message') return;

            propPanelTitle.textContent = 'Message';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">From</div>
                    <select class="property-select" id="seq-msg-from">
                        ${seqDiagram.participants.map(p => `<option value="${escapeHtml(p.id)}" ${el.fromId === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <select class="property-select" id="seq-msg-to">
                        ${seqDiagram.participants.map(p => `<option value="${escapeHtml(p.id)}" ${el.toId === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Text</div>
                    <input class="property-input" id="seq-msg-text" value="${escapeHtml(el.text || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Arrow Type</div>
                    <select class="property-select" id="seq-msg-arrow">
                        <option value="SolidArrow" ${el.arrowType === 'SolidArrow' ? 'selected' : ''}>Solid Arrow (->>) </option>
                        <option value="DottedArrow" ${el.arrowType === 'DottedArrow' ? 'selected' : ''}>Dotted Arrow (-->>)</option>
                        <option value="SolidOpen" ${el.arrowType === 'SolidOpen' ? 'selected' : ''}>Solid Open (->)</option>
                        <option value="DottedOpen" ${el.arrowType === 'DottedOpen' ? 'selected' : ''}>Dotted Open (-->)</option>
                        <option value="SolidCross" ${el.arrowType === 'SolidCross' ? 'selected' : ''}>Solid Cross (-x)</option>
                        <option value="DottedCross" ${el.arrowType === 'DottedCross' ? 'selected' : ''}>Dotted Cross (--x)</option>
                        <option value="SolidAsync" ${el.arrowType === 'SolidAsync' ? 'selected' : ''}>Solid Async (-))</option>
                        <option value="DottedAsync" ${el.arrowType === 'DottedAsync' ? 'selected' : ''}>Dotted Async (--))</option>
                    </select>
                </div>
            `;

            document.getElementById('seq-msg-from').addEventListener('change', function() {
                postMessage({ type: 'seq_messageEdited', elementIndex: elIdx, fromId: this.value });
            });
            document.getElementById('seq-msg-to').addEventListener('change', function() {
                postMessage({ type: 'seq_messageEdited', elementIndex: elIdx, toId: this.value });
            });
            document.getElementById('seq-msg-text').addEventListener('change', function() {
                postMessage({ type: 'seq_messageEdited', elementIndex: elIdx, text: this.value });
            });
            document.getElementById('seq-msg-arrow').addEventListener('change', function() {
                postMessage({ type: 'seq_messageEdited', elementIndex: elIdx, arrowType: this.value });
            });

            propertyPanel.classList.add('visible');
        }

        // Property panel for messages inside fragments
        function showSeqFragmentInnerMessagePropertyPanel(fragIdx, sectionIdx, subIdx) {
            if (!seqDiagram) return;
            const el = getFragmentInnerMessage(fragIdx, sectionIdx, subIdx);
            if (!el || el.elementType !== 'message') return;

            propPanelTitle.textContent = 'Message (in Fragment)';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">From</div>
                    <select class="property-select" id="seq-msg-from">
                        ${seqDiagram.participants.map(p => `<option value="${escapeHtml(p.id)}" ${el.fromId === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <select class="property-select" id="seq-msg-to">
                        ${seqDiagram.participants.map(p => `<option value="${escapeHtml(p.id)}" ${el.toId === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Text</div>
                    <input class="property-input" id="seq-msg-text" value="${escapeHtml(el.text || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Arrow Type</div>
                    <select class="property-select" id="seq-msg-arrow">
                        <option value="SolidArrow" ${el.arrowType === 'SolidArrow' ? 'selected' : ''}>Solid Arrow (->>) </option>
                        <option value="DottedArrow" ${el.arrowType === 'DottedArrow' ? 'selected' : ''}>Dotted Arrow (-->>)</option>
                        <option value="SolidOpen" ${el.arrowType === 'SolidOpen' ? 'selected' : ''}>Solid Open (->)</option>
                        <option value="DottedOpen" ${el.arrowType === 'DottedOpen' ? 'selected' : ''}>Dotted Open (-->)</option>
                        <option value="SolidCross" ${el.arrowType === 'SolidCross' ? 'selected' : ''}>Solid Cross (-x)</option>
                        <option value="DottedCross" ${el.arrowType === 'DottedCross' ? 'selected' : ''}>Dotted Cross (--x)</option>
                        <option value="SolidAsync" ${el.arrowType === 'SolidAsync' ? 'selected' : ''}>Solid Async (-))</option>
                        <option value="DottedAsync" ${el.arrowType === 'DottedAsync' ? 'selected' : ''}>Dotted Async (--))</option>
                    </select>
                </div>
            `;

            document.getElementById('seq-msg-from').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentInnerMessageEdited', elementIndex: fragIdx, sectionIndex: sectionIdx, subIndex: subIdx, fromId: this.value });
            });
            document.getElementById('seq-msg-to').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentInnerMessageEdited', elementIndex: fragIdx, sectionIndex: sectionIdx, subIndex: subIdx, toId: this.value });
            });
            document.getElementById('seq-msg-text').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentInnerMessageEdited', elementIndex: fragIdx, sectionIndex: sectionIdx, subIndex: subIdx, text: this.value });
            });
            document.getElementById('seq-msg-arrow').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentInnerMessageEdited', elementIndex: fragIdx, sectionIndex: sectionIdx, subIndex: subIdx, arrowType: this.value });
            });

            propertyPanel.classList.add('visible');
        }

        function showSeqFragmentPropertyPanel(elIdx) {
            if (!seqDiagram) return;
            const el = seqDiagram.elements[elIdx];
            if (!el || el.elementType !== 'fragment') return;

            const fragType = (el.fragmentType || el.type || 'loop').toLowerCase();
            const fragTypes = ['loop', 'alt', 'opt', 'par', 'critical', 'break'];

            // Multi-section types: alt (else), par (and), critical (option)
            const multiSectionTypes = ['alt', 'par', 'critical'];
            const isMultiSection = multiSectionTypes.includes(fragType);
            // Section divider keyword varies by type
            const sectionKeyword = fragType === 'par' ? 'and' : fragType === 'critical' ? 'option' : 'else';

            propPanelTitle.textContent = 'Fragment';
            const body = document.querySelector('.property-panel-body');

            let sectionsHtml = '';
            if (el.sections && el.sections.length > 0 && isMultiSection) {
                sectionsHtml = '<div class="property-row"><div class="property-label">Sections</div></div>';
                el.sections.forEach((sec, si) => {
                    const secLabel = sec.label || (si === 0 ? '(primary)' : '');
                    sectionsHtml += `
                        <div class="property-row" style="padding-left:10px">
                            <div class="property-label">${si === 0 ? 'Condition' : sectionKeyword + ' label'} ${si + 1}</div>
                            <input class="property-input seq-frag-sec-label" data-sec-index="${si}" value="${escapeHtml(secLabel)}" placeholder="${si === 0 ? 'condition' : sectionKeyword + ' label'}" />
                        </div>`;
                });
                sectionsHtml += `<div class="property-row" style="padding-left:10px"><button class="property-btn" id="seq-frag-add-section">+ Add ${sectionKeyword} Section</button></div>`;
            }

            // Build participant options for Start/End dropdowns
            const participantOptions = seqDiagram.participants.map(p =>
                `<option value="${escapeHtml(p.id)}">${escapeHtml(getParticipantLabel(p))}</option>`
            ).join('');
            const autoOption = '<option value="">(Auto)</option>';
            const curStart = el.overParticipantStart || '';
            const curEnd = el.overParticipantEnd || '';
            const startOptions = seqDiagram.participants.map(p =>
                `<option value="${escapeHtml(p.id)}" ${curStart === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`
            ).join('');
            const endOptions = seqDiagram.participants.map(p =>
                `<option value="${escapeHtml(p.id)}" ${curEnd === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`
            ).join('');

            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-select" id="seq-frag-type">
                        ${fragTypes.map(t => `<option value="${t}" ${fragType === t ? 'selected' : ''}>${t}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Condition</div>
                    <input class="property-input" id="seq-frag-text" value="${escapeHtml(el.text || '')}" placeholder="e.g. is valid" />
                </div>
                <div class="property-row">
                    <div class="property-label">Start Participant</div>
                    <select class="property-select" id="seq-frag-start-participant">
                        <option value="" ${!curStart ? 'selected' : ''}>(Auto)</option>
                        ${startOptions}
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">End Participant</div>
                    <select class="property-select" id="seq-frag-end-participant">
                        <option value="" ${!curEnd ? 'selected' : ''}>(Auto)</option>
                        ${endOptions}
                    </select>
                </div>
                ${sectionsHtml}
                <div class="property-row">
                    <button class="property-btn property-btn-danger" id="seq-frag-delete">Delete Fragment</button>
                </div>
            `;

            document.getElementById('seq-frag-type').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentEdited', elementIndex: elIdx, fragmentType: this.value });
            });
            document.getElementById('seq-frag-text').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentEdited', elementIndex: elIdx, text: this.value });
            });
            document.getElementById('seq-frag-start-participant').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentEdited', elementIndex: elIdx, overParticipantStart: this.value || null, overParticipantEnd: document.getElementById('seq-frag-end-participant').value || null });
            });
            document.getElementById('seq-frag-end-participant').addEventListener('change', function() {
                postMessage({ type: 'seq_fragmentEdited', elementIndex: elIdx, overParticipantStart: document.getElementById('seq-frag-start-participant').value || null, overParticipantEnd: this.value || null });
            });
            document.getElementById('seq-frag-delete').addEventListener('click', function() {
                postMessage({ type: 'seq_fragmentDeleted', elementIndex: elIdx });
                seqSelectedFragmentIndex = -1;
                propertyPanel.classList.remove('visible');
            });

            // Section label editors
            document.querySelectorAll('.seq-frag-sec-label').forEach(input => {
                input.addEventListener('change', function() {
                    const secIdx = parseInt(this.getAttribute('data-sec-index'), 10);
                    postMessage({ type: 'seq_fragmentSectionEdited', elementIndex: elIdx, sectionIndex: secIdx, label: this.value });
                });
            });

            // Add section button
            const addSecBtn = document.getElementById('seq-frag-add-section');
            if (addSecBtn) {
                addSecBtn.addEventListener('click', function() {
                    postMessage({ type: 'seq_fragmentSectionAdded', elementIndex: elIdx });
                });
            }

            propertyPanel.classList.add('visible');
        }

        function showSeqNotePropertyPanel(elIdx) {
            if (!seqDiagram) return;
            const el = seqDiagram.elements[elIdx];
            if (!el || el.elementType !== 'note') return;

            const notePosition = el.notePosition || 'RightOf';
            const overParts = (el.overParticipants || '').split(',').map(s => s.trim()).filter(Boolean);
            const firstParticipant = overParts.length > 0 ? overParts[0] : (seqDiagram.participants.length > 0 ? seqDiagram.participants[0].id : '');

            propPanelTitle.textContent = 'Note';
            const body = document.querySelector('.property-panel-body');
            body.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Text</div>
                    <input class="property-input" id="seq-note-text" value="${escapeHtml(el.text || '')}" />
                </div>
                <div class="property-row">
                    <div class="property-label">Position</div>
                    <select class="property-select" id="seq-note-position">
                        <option value="RightOf" ${notePosition === 'RightOf' ? 'selected' : ''}>Right of</option>
                        <option value="LeftOf" ${notePosition === 'LeftOf' ? 'selected' : ''}>Left of</option>
                        <option value="Over" ${notePosition === 'Over' ? 'selected' : ''}>Over</option>
                    </select>
                </div>
                <div class="property-row">
                    <div class="property-label">Participant</div>
                    <select class="property-select" id="seq-note-participant">
                        ${seqDiagram.participants.map(p => `<option value="${escapeHtml(p.id)}" ${firstParticipant === p.id ? 'selected' : ''}>${escapeHtml(getParticipantLabel(p))}</option>`).join('')}
                    </select>
                </div>
                <div class="property-row">
                    <button class="property-btn property-btn-danger" id="seq-note-delete">Delete Note</button>
                </div>
            `;

            document.getElementById('seq-note-text').addEventListener('change', function() {
                postMessage({ type: 'seq_noteEdited', elementIndex: elIdx, text: this.value });
            });
            document.getElementById('seq-note-position').addEventListener('change', function() {
                postMessage({ type: 'seq_noteEdited', elementIndex: elIdx, position: this.value, overParticipants: document.getElementById('seq-note-participant').value });
            });
            document.getElementById('seq-note-participant').addEventListener('change', function() {
                postMessage({ type: 'seq_noteEdited', elementIndex: elIdx, position: document.getElementById('seq-note-position').value, overParticipants: this.value });
            });
            document.getElementById('seq-note-delete').addEventListener('click', function() {
                postMessage({ type: 'seq_noteDeleted', elementIndex: elIdx });
                seqSelectedNoteIndex = -1;
                propertyPanel.classList.remove('visible');
            });

            propertyPanel.classList.add('visible');
        }

        function escapeHtml(str) {
            return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        }

        // ===== Sequence Diagram Context Menu =====
        // Uses mousedown(button=2) instead of contextmenu event because
        // the contextmenu event is unreliable in WebView2 for SVG elements
        // inside fragments. mousedown fires reliably and e.target is correct.

        let _seqRightClickHandled = false;

        svg.addEventListener('mousedown', function(e) {
            if (e.button !== 2) return;
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;

            _seqRightClickHandled = true;
            e.preventDefault(); // Suppress native context menu early
            const target = e.target;

            const participantGroup = target.closest('.seq-participant') || target.closest('.seq-participant-bottom');
            let messageGroup = target.closest('.seq-message');
            let fragmentGroup = target.closest('.seq-fragment');
            const noteGroup = target.closest('.seq-note');

            // Fallback 1: elementsFromPoint - find messages at the click coordinates
            if (!messageGroup) {
                const allEls = document.elementsFromPoint(e.clientX, e.clientY);
                for (const el of allEls) {
                    const msg = el.closest('.seq-message');
                    if (msg) { messageGroup = msg; break; }
                }
            }

            // Fallback 2: check all rendered message groups via SVG bounding box.
            // Always run this if no message was found yet — even if we found a fragment
            // or other element, because fragment-inner messages sit inside fragment rects
            // and the click target may be the fragment background instead.
            if (!messageGroup && !participantGroup && !noteGroup) {
                const svgPt = getSVGPoint(e);
                const allMsgs = svg.querySelectorAll('.seq-message');
                let bestMsg = null;
                let bestArea = Infinity;
                for (const msg of allMsgs) {
                    try {
                        const bbox = msg.getBBox();
                        if (svgPt.x >= bbox.x && svgPt.x <= bbox.x + bbox.width &&
                            svgPt.y >= bbox.y && svgPt.y <= bbox.y + bbox.height) {
                            // Prefer the smallest (most specific) bounding box match
                            const area = bbox.width * bbox.height;
                            if (area < bestArea) {
                                bestMsg = msg;
                                bestArea = area;
                            }
                        }
                    } catch(ex) { /* ignore getBBox errors */ }
                }
                if (bestMsg) messageGroup = bestMsg;
            }

            // If we found a message, clear fragment selection to avoid confusion
            if (messageGroup) fragmentGroup = null;

            contextMenu.innerHTML = '';

            if (participantGroup) {
                const pid = participantGroup.getAttribute('data-participant-id');
                seqSelectedParticipantId = pid;

                addContextMenuItem('Edit Participant', () => {
                    const p = seqDiagram.participants.find(pp => pp.id === pid);
                    if (p) startSeqParticipantInlineEdit(pid, p, e);
                });
                addContextMenuItem('Delete Participant', () => {
                    postMessage({ type: 'seq_participantDeleted', participantId: pid });
                });
                addContextMenuSeparator();
                addContextMenuItem('Add Participant After', () => {
                    const newId = 'Participant' + (seqDiagram.participants.length + 1);
                    postMessage({ type: 'seq_participantCreated', participantId: newId });
                });
            } else if (noteGroup) {
                const elIdx = parseInt(noteGroup.getAttribute('data-element-index'), 10);
                seqSelectedNoteIndex = elIdx;

                addContextMenuItem('Edit Note', () => {
                    showSeqNotePropertyPanel(elIdx);
                });
                addContextMenuItem('Delete Note', () => {
                    postMessage({ type: 'seq_noteDeleted', elementIndex: elIdx });
                    seqSelectedNoteIndex = -1;
                });
                addContextMenuSeparator();
                if (elIdx > 0) {
                    addContextMenuItem('\u2191 Move Up', () => {
                        postMessage({ type: 'seq_elementReordered', fromIndex: elIdx, toIndex: elIdx - 1 });
                    });
                }
                if (elIdx < seqDiagram.elements.length - 1) {
                    addContextMenuItem('\u2193 Move Down', () => {
                        postMessage({ type: 'seq_elementReordered', fromIndex: elIdx, toIndex: elIdx + 1 });
                    });
                }
                addContextMenuSeparator();
                addContextMenuItem('Insert Message Above', () => {
                    postMessage({ type: 'seq_messageCreated', fromId: seqDiagram.participants[0].id, toId: seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id, text: 'Message', arrowType: 'SolidArrow', insertIndex: elIdx });
                });
                addContextMenuItem('Insert Message Below', () => {
                    postMessage({ type: 'seq_messageCreated', fromId: seqDiagram.participants[0].id, toId: seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id, text: 'Message', arrowType: 'SolidArrow', insertIndex: elIdx + 1 });
                });
                addContextMenuItem('Insert Note Above', () => {
                    _seqPendingNoteSelect = true;
                    postMessage({ type: 'seq_noteCreated', position: 'RightOf', overParticipants: seqDiagram.participants[0].id, text: 'Note', insertIndex: elIdx });
                });
                addContextMenuItem('Insert Note Below', () => {
                    _seqPendingNoteSelect = true;
                    postMessage({ type: 'seq_noteCreated', position: 'RightOf', overParticipants: seqDiagram.participants[0].id, text: 'Note', insertIndex: elIdx + 1 });
                });
            } else if (messageGroup) {
                const elIdx = parseInt(messageGroup.getAttribute('data-element-index'), 10);
                const isFragInner = messageGroup.hasAttribute('data-section-index');

                if (isFragInner) {
                    // Message inside a fragment — show fragment-inner message context menu
                    const secIdx = parseInt(messageGroup.getAttribute('data-section-index'), 10);
                    const subIdx = parseInt(messageGroup.getAttribute('data-sub-index'), 10);
                    const innerEl = getFragmentInnerMessage(elIdx, secIdx, subIdx);
                    seqSelectedFragmentIndex = -1;
                    seqSelectedFragInnerMsg = { fragIdx: elIdx, secIdx: secIdx, subIdx: subIdx };
                    seqSelectedMessageIndex = -1;

                    addContextMenuItem('Edit Message', () => {
                        showSeqFragmentInnerMessagePropertyPanel(elIdx, secIdx, subIdx);
                    });
                    addContextMenuItem('Delete Message', () => {
                        postMessage({ type: 'seq_fragmentInnerMessageDeleted', elementIndex: elIdx, sectionIndex: secIdx, subIndex: subIdx });
                    });
                    addContextMenuSeparator();
                    addContextMenuItem('Insert Message Above', () => {
                        const fromId = innerEl ? innerEl.fromId : seqDiagram.participants[0].id;
                        const toId = innerEl ? innerEl.toId : (seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id);
                        postMessage({ type: 'seq_fragmentInnerMessageCreated', elementIndex: elIdx, sectionIndex: secIdx, subIndex: subIdx, fromId: fromId, toId: toId, text: 'Message', arrowType: 'SolidArrow' });
                    });
                    addContextMenuItem('Insert Message Below', () => {
                        const fromId = innerEl ? innerEl.fromId : seqDiagram.participants[0].id;
                        const toId = innerEl ? innerEl.toId : (seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id);
                        postMessage({ type: 'seq_fragmentInnerMessageCreated', elementIndex: elIdx, sectionIndex: secIdx, subIndex: subIdx + 1, fromId: fromId, toId: toId, text: 'Message', arrowType: 'SolidArrow' });
                    });
                } else {
                    // Top-level message — original behavior
                    seqSelectedMessageIndex = elIdx;

                    addContextMenuItem('Edit Message', () => {
                        showSeqMessagePropertyPanel(elIdx);
                    });
                    addContextMenuItem('Delete Message', () => {
                        postMessage({ type: 'seq_messageDeleted', elementIndex: elIdx });
                    });
                    addContextMenuSeparator();
                    if (elIdx > 0) {
                        addContextMenuItem('\u2191 Move Up', () => {
                            postMessage({ type: 'seq_elementReordered', fromIndex: elIdx, toIndex: elIdx - 1 });
                        });
                    }
                    if (elIdx < seqDiagram.elements.length - 1) {
                        addContextMenuItem('\u2193 Move Down', () => {
                            postMessage({ type: 'seq_elementReordered', fromIndex: elIdx, toIndex: elIdx + 1 });
                        });
                    }
                    addContextMenuSeparator();
                    addContextMenuItem('Insert Message Above', () => {
                        const el = seqDiagram.elements[elIdx];
                        if (el && el.elementType === 'message') {
                            postMessage({ type: 'seq_messageCreated', fromId: el.fromId, toId: el.toId, text: 'Message', arrowType: 'SolidArrow', insertIndex: elIdx });
                        }
                    });
                    addContextMenuItem('Insert Message Below', () => {
                        const el = seqDiagram.elements[elIdx];
                        if (el && el.elementType === 'message') {
                            postMessage({ type: 'seq_messageCreated', fromId: el.fromId, toId: el.toId, text: 'Message', arrowType: 'SolidArrow', insertIndex: elIdx + 1 });
                        }
                    });
                    addContextMenuItem('Insert Note Above', () => {
                        _seqPendingNoteSelect = true;
                        postMessage({ type: 'seq_noteCreated', position: 'RightOf', overParticipants: seqDiagram.participants[0].id, text: 'Note', insertIndex: elIdx });
                    });
                    addContextMenuItem('Insert Note Below', () => {
                        _seqPendingNoteSelect = true;
                        postMessage({ type: 'seq_noteCreated', position: 'RightOf', overParticipants: seqDiagram.participants[0].id, text: 'Note', insertIndex: elIdx + 1 });
                    });
                    addContextMenuItem('Insert Fragment Below', () => {
                        postMessage({ type: 'seq_fragmentCreated', fragmentType: 'loop', condition: 'condition', sections: [{ label: 'condition', elements: [] }], insertIndex: elIdx + 1 });
                    });
                    // "Move to Fragment" submenu — only show if there are fragments in the diagram
                    // Lists every section within each fragment as a separate target
                    const fragments = seqDiagram.elements
                        .map((el, idx) => ({ el, idx }))
                        .filter(item => item.el.elementType === 'fragment');
                    if (fragments.length > 0) {
                        addContextMenuSeparator();
                        const parentItem = document.createElement('div');
                        parentItem.classList.add('context-menu-item', 'has-submenu');
                        parentItem.textContent = 'Move to Fragment';
                        const submenu = document.createElement('div');
                        submenu.classList.add('context-submenu');
                        fragments.forEach(f => {
                            if (f.el.sections && f.el.sections.length > 0) {
                                f.el.sections.forEach((sec, secIdx) => {
                                    const typeName = f.el.fragmentType.charAt(0).toUpperCase() + f.el.fragmentType.slice(1);
                                    const secLabel = typeName + (sec.label ? ' [' + sec.label + ']' : (secIdx > 0 ? ' [else]' : ''));
                                    const subItem = document.createElement('div');
                                    subItem.classList.add('context-menu-item');
                                    subItem.textContent = secLabel;
                                    subItem.addEventListener('click', function(ev) {
                                        ev.stopPropagation();
                                        contextMenu.classList.remove('visible');
                                        postMessage({ type: 'seq_messageMovedToFragment', messageIndex: elIdx, fragmentIndex: f.idx, sectionIndex: secIdx });
                                    });
                                    submenu.appendChild(subItem);
                                });
                            } else {
                                const fragLabel = f.el.fragmentType + (f.el.sections && f.el.sections[0] && f.el.sections[0].label ? ' [' + f.el.sections[0].label + ']' : '');
                                const subItem = document.createElement('div');
                                subItem.classList.add('context-menu-item');
                                subItem.textContent = fragLabel;
                                subItem.addEventListener('click', function(ev) {
                                    ev.stopPropagation();
                                    contextMenu.classList.remove('visible');
                                    postMessage({ type: 'seq_messageMovedToFragment', messageIndex: elIdx, fragmentIndex: f.idx });
                                });
                                submenu.appendChild(subItem);
                            }
                        });
                        parentItem.appendChild(submenu);
                        contextMenu.appendChild(parentItem);
                    }
                }
            } else if (fragmentGroup) {
                const elIdx = parseInt(fragmentGroup.getAttribute('data-element-index'), 10);
                seqSelectedFragmentIndex = elIdx;
                seqSelectedFragInnerMsg = null;

                addContextMenuItem('Edit Fragment', () => {
                    showSeqFragmentPropertyPanel(elIdx);
                });
                addContextMenuItem('Delete Fragment', () => {
                    postMessage({ type: 'seq_fragmentDeleted', elementIndex: elIdx });
                    seqSelectedFragmentIndex = -1;
                });
                addContextMenuSeparator();
                if (elIdx > 0) {
                    addContextMenuItem('\u2191 Move Up', () => {
                        postMessage({ type: 'seq_elementReordered', fromIndex: elIdx, toIndex: elIdx - 1 });
                    });
                }
                if (elIdx < seqDiagram.elements.length - 1) {
                    addContextMenuItem('\u2193 Move Down', () => {
                        postMessage({ type: 'seq_elementReordered', fromIndex: elIdx, toIndex: elIdx + 1 });
                    });
                }
                addContextMenuSeparator();
                // Add message INTO the fragment (first section, at the end)
                const frag = seqDiagram.elements[elIdx];
                if (frag && frag.sections) {
                    frag.sections.forEach((sec, secIdx) => {
                        const secLabel = sec.label || ('Section ' + (secIdx + 1));
                        const subIdx = sec.elements ? sec.elements.length : 0;
                        // Determine default from/to: use fragment participant range if set,
                        // otherwise use existing messages, otherwise fall back to first two participants
                        let defFrom = seqDiagram.participants[0].id;
                        let defTo = seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id;
                        if (sec.elements && sec.elements.length > 0) {
                            const lastMsg = sec.elements[sec.elements.length - 1];
                            if (lastMsg.fromId) defFrom = lastMsg.fromId;
                            if (lastMsg.toId) defTo = lastMsg.toId;
                        } else if (frag.overParticipantStart || frag.overParticipantEnd) {
                            // No existing messages — use fragment's participant range
                            if (frag.overParticipantStart) defFrom = frag.overParticipantStart;
                            if (frag.overParticipantEnd) defTo = frag.overParticipantEnd;
                            else defTo = defFrom; // If only start is set, self-message
                            if (!frag.overParticipantStart && frag.overParticipantEnd) defFrom = frag.overParticipantEnd;
                        }
                        const menuLabel = frag.sections.length > 1
                            ? 'Add Message Into [' + secLabel + ']'
                            : 'Add Message Into Fragment';
                        addContextMenuItem(menuLabel, () => {
                            postMessage({ type: 'seq_fragmentInnerMessageCreated', elementIndex: elIdx, sectionIndex: secIdx, subIndex: subIdx, fromId: defFrom, toId: defTo, text: 'Message', arrowType: 'SolidArrow' });
                        });
                    });
                }
                addContextMenuSeparator();
                addContextMenuItem('Insert Message Above', () => {
                    postMessage({ type: 'seq_messageCreated', fromId: seqDiagram.participants[0].id, toId: seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id, text: 'Message', arrowType: 'SolidArrow', insertIndex: elIdx });
                });
                addContextMenuItem('Insert Message Below', () => {
                    postMessage({ type: 'seq_messageCreated', fromId: seqDiagram.participants[0].id, toId: seqDiagram.participants.length > 1 ? seqDiagram.participants[1].id : seqDiagram.participants[0].id, text: 'Message', arrowType: 'SolidArrow', insertIndex: elIdx + 1 });
                });
            } else {
                // Empty space context menu
                addContextMenuItem('Add Participant', () => {
                    const newId = 'Participant' + (seqDiagram.participants.length + 1);
                    postMessage({ type: 'seq_participantCreated', participantId: newId });
                });
                if (seqDiagram.participants.length >= 2) {
                    addContextMenuItem('Add Message', () => {
                        postMessage({
                            type: 'seq_messageCreated',
                            fromId: seqDiagram.participants[0].id,
                            toId: seqDiagram.participants[1].id,
                            text: 'Message',
                            arrowType: 'SolidArrow'
                        });
                    });
                    addContextMenuItem('Add Note', () => {
                        _seqPendingNoteSelect = true;
                        postMessage({
                            type: 'seq_noteCreated',
                            position: 'RightOf',
                            overParticipants: seqDiagram.participants[0].id,
                            text: 'Note'
                        });
                    });
                    addContextMenuItem('Add Fragment', () => {
                        postMessage({
                            type: 'seq_fragmentCreated',
                            fragmentType: 'loop',
                            condition: 'condition',
                            sections: [{ label: 'condition', elements: [] }]
                        });
                    });
                }
            }

            // Copy/Paste for sequence diagrams
            if (seqSelectedParticipantId || seqSelectedMessageIndex >= 0 || seqSelectedNoteIndex >= 0 || seqSelectedFragmentIndex >= 0 || seqSelectedFragInnerMsg) {
                addContextMenuSeparator();
                addContextMenuItem('\u{1F4CB} Copy', () => { copySelected(); });
            }
            if (clipboard && clipboard.diagramType === 'sequence') {
                addContextMenuItem('\u{1F4CB} Paste', () => { pasteClipboard(0, 0); });
            }

            positionContextMenu(contextMenu, e.clientX, e.clientY);
            renderSequenceDiagram();
        });

        // The contextmenu event just suppresses the native menu — all logic is in mousedown above
        svg.addEventListener('contextmenu', function(e) {
            if (currentDiagramType !== 'sequence') return;
            // Always suppress native context menu for sequence diagrams on the SVG
            e.preventDefault();
            e.stopPropagation();
            _seqRightClickHandled = false;
            return false;
        }, true);

        function addContextMenuItem(label, onClick) {
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

        function addContextMenuSeparator() {
            const sep = document.createElement('div');
            sep.classList.add('context-menu-separator');
            contextMenu.appendChild(sep);
        }

        // Keyboard shortcuts for sequence elements
        document.addEventListener('keydown', function(e) {
            if (currentDiagramType !== 'sequence') return;
            if (!seqDiagram) return;
            if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'SELECT')) return;

            if (e.key === 'Delete' || e.key === 'Backspace') {
                if (seqSelectedParticipantId) {
                    postMessage({ type: 'seq_participantDeleted', participantId: seqSelectedParticipantId });
                    seqSelectedParticipantId = null;
                    e.preventDefault();
                } else if (seqSelectedMessageIndex >= 0) {
                    postMessage({ type: 'seq_messageDeleted', elementIndex: seqSelectedMessageIndex });
                    seqSelectedMessageIndex = -1;
                    e.preventDefault();
                } else if (seqSelectedFragInnerMsg) {
                    postMessage({ type: 'seq_fragmentInnerMessageDeleted', elementIndex: seqSelectedFragInnerMsg.fragIdx, sectionIndex: seqSelectedFragInnerMsg.secIdx, subIndex: seqSelectedFragInnerMsg.subIdx });
                    seqSelectedFragInnerMsg = null;
                    e.preventDefault();
                } else if (seqSelectedFragmentIndex >= 0) {
                    postMessage({ type: 'seq_fragmentDeleted', elementIndex: seqSelectedFragmentIndex });
                    seqSelectedFragmentIndex = -1;
                    e.preventDefault();
                } else if (seqSelectedNoteIndex >= 0) {
                    postMessage({ type: 'seq_noteDeleted', elementIndex: seqSelectedNoteIndex });
                    seqSelectedNoteIndex = -1;
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

        // ===== Sequence diagram toolbar buttons =====
        document.getElementById('tb-seq-add-participant').addEventListener('click', function() {
            if (!seqDiagram) return;
            postMessage({ type: 'seq_participantCreated', participantId: 'Participant' + (seqDiagram.participants.length + 1), participantType: 'Participant' });
        });

        document.getElementById('tb-seq-add-message').addEventListener('click', function() {
            if (!seqDiagram || seqDiagram.participants.length < 2) return;
            const fromId = seqDiagram.participants[0].id;
            const toId = seqDiagram.participants[1].id;
            postMessage({ type: 'seq_messageCreated', fromId: fromId, toId: toId, text: 'Message', arrowType: 'SolidArrow' });
        });

        document.getElementById('tb-seq-add-note').addEventListener('click', function() {
            if (!seqDiagram || seqDiagram.participants.length < 1) return;
            const overId = seqDiagram.participants[0].id;
            _seqPendingNoteSelect = true;
            postMessage({ type: 'seq_noteCreated', position: 'RightOf', overParticipants: overId, text: 'Note' });
        });

        document.getElementById('tb-seq-add-fragment').addEventListener('click', function() {
            if (!seqDiagram || seqDiagram.participants.length < 2) return;
            const fromId = seqDiagram.participants[0].id;
            const toId = seqDiagram.participants[1].id;
            postMessage({ type: 'seq_fragmentCreated', fragmentType: 'alt', condition: 'condition', sections: [
                { label: 'condition', elements: [{ elementType: 'message', fromId: fromId, toId: toId, text: 'Message', arrowType: 'SolidArrow' }] },
                { label: 'else', elements: [{ elementType: 'message', fromId: toId, toId: fromId, text: 'Response', arrowType: 'DottedArrow' }] }
            ]});
        });

