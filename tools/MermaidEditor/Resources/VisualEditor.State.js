        // ========== State Diagram Visual Editor ==========

        let stDiagram = null; // When non-null, we're in state diagram mode
        let stSelectedStateId = null;
        let stSelectedTransIndex = -1;
        let stSelectedNestedTrans = null; // { parentId, idx } when a nested transition is selected
        let stDraggingStateId = null;
        let stDragOffsetX = 0;
        let stDragOffsetY = 0;
        let stDrawingTransFrom = null; // stateId when drawing a transition
        let stDrawingTransToEnd = false; // true when drawing a transition TO [*] end
        let stDrawingNestedParent = null; // parentId when drawing a nested transition from composite context menu
        let stPickingTransSource = false; // true when toolbar Add Transition is waiting for source click
        let stDraggingPseudoId = null; // pseudo-state id when dragging a [*] circle
        let stSelectedPseudoId = null; // pseudo-state id when selected (for keyboard delete)
        let stDraggingNoteIdx = -1; // note index when dragging a note
        let stSelectedNoteIdx = -1; // currently selected note index
        let stNotePositions = {}; // { noteIndex: { x, y } } - custom note positions from dragging
        let stManualNotePositions = new Set(); // indices of notes that were manually dragged (vs auto-computed)
        let stStatePositions = {}; // { stateId: { x, y, width, height } }
        let curvedEdges = true; // When true, render edges as Bézier curves (shared across all diagram types)

        const ST_BOX_MIN_WIDTH = 120;
        const ST_BOX_HEIGHT = 40;
        const ST_BOX_PADDING = 12;
        const ST_TOP_MARGIN = 60;
        const ST_LEFT_MARGIN = 60;
        const ST_GAP_X = 180;
        const ST_GAP_Y = 80;
        const ST_SPECIAL_RADIUS = 12;
        const ST_NOTE_WIDTH = 140;
        const ST_NOTE_HEIGHT = 60;
        const ST_COMPOSITE_PADDING = 30;

        window.loadStateDiagram = function(jsonData) {
            try {
                currentDiagramType = 'state';
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                stDiagram = {
                    direction: data.direction || null,
                    isV2: data.isV2 !== false,
                    states: (data.states || []).map(s => stParseState(s)),
                    transitions: (data.transitions || []).map(t => ({
                        fromId: t.fromId || '',
                        toId: t.toId || '',
                        label: t.label || null
                    })),
                    notes: (data.notes || []).map(n => ({
                        text: n.text || '',
                        stateId: n.stateId || '',
                        position: n.position || 'RightOf'
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

                // Hide property panel from previous diagram
                propertyPanel.classList.remove('visible');

                // Hydrate saved positions from C# model (if HasManualPosition)
                let hasHydratedPositions = false;
                function stHydratePositions(states) {
                    states.forEach(s => {
                        if (s.hasManualPosition && (s.x || s.y)) {
                            stStatePositions[s.id] = {
                                x: s.x,
                                y: s.y,
                                width: (s.width > 0) ? s.width : (stStatePositions[s.id] ? stStatePositions[s.id].width : ST_BOX_MIN_WIDTH),
                                height: (s.height > 0) ? s.height : (stStatePositions[s.id] ? stStatePositions[s.id].height : ST_BOX_HEIGHT)
                            };
                            hasHydratedPositions = true;
                        }
                        if (s.nestedStates && s.nestedStates.length > 0) {
                            stHydratePositions(s.nestedStates);
                        }
                    });
                }
                stHydratePositions(stDiagram.states);

                // Hydrate pseudo-node positions from C# model ([*]_start, [*]_end, etc.)
                if (data.pseudoNodePositions) {
                    Object.keys(data.pseudoNodePositions).forEach(key => {
                        const pos = data.pseudoNodePositions[key];
                        if (pos && pos.length >= 2) {
                            stStatePositions[key] = {
                                x: pos[0], y: pos[1],
                                width: 20, height: 20
                            };
                            hasHydratedPositions = true;
                        }
                    });
                }

                // Hydrate note positions from C# model (note_0, note_1, etc.)
                stManualNotePositions = new Set();
                if (data.notePositions) {
                    Object.keys(data.notePositions).forEach(key => {
                        const pos = data.notePositions[key];
                        if (pos && pos.length >= 2) {
                            // Extract note index from "note_0", "note_1", etc.
                            const idx = parseInt(key.replace('note_', ''), 10);
                            if (!isNaN(idx)) {
                                stNotePositions[idx] = { x: pos[0], y: pos[1] };
                                stManualNotePositions.add(idx);
                            }
                            hasHydratedPositions = true;
                        }
                    });
                }

                if (hasHydratedPositions) {
                    // Positions were restored from @pos comments — skip dagre auto-layout.
                    // Only place states that don't already have positions using grid fallback.
                    stPlaceStatesInGrid(stDiagram.states, ST_LEFT_MARGIN, ST_TOP_MARGIN);
                    // Place [*] pseudo-nodes (start/end circles) based on transitions
                    // (only places pseudo nodes that don't already have saved positions)
                    stPlacePseudoNodes();
                    // Expand composites to fit their nested children
                    function expandAllComposites(states) {
                        states.forEach(s => {
                            if (s.nestedStates && s.nestedStates.length > 0) {
                                expandAllComposites(s.nestedStates);
                                stExpandCompositeToFitChildren(s.id);
                            }
                        });
                    }
                    expandAllComposites(stDiagram.states);
                } else {
                    stAutoLayoutStates();
                }
                updateToolbarForDiagramType();
                renderStateDiagram();
                centerView();
                // Deferred re-render: on first load the container may not have its final
                // dimensions yet (WebView still sizing), causing a scrunched layout.
                setTimeout(function() { if (stDiagram) { renderStateDiagram(); centerView(); } }, 150);
            } catch (err) {
                console.error('Failed to load state diagram:', err);
            }
        };

        function stParseState(s) {
            return {
                id: s.id || '',
                label: s.label || null,
                type: s.type || 'Simple',
                cssClass: s.cssClass || null,
                isExplicit: s.isExplicit !== false,
                nestedStates: (s.nestedStates || []).map(ns => stParseState(ns)),
                nestedTransitions: (s.nestedTransitions || []).map(t => ({
                    fromId: t.fromId || '',
                    toId: t.toId || '',
                    label: t.label || null
                })),
                x: s.x || 0,
                y: s.y || 0,
                width: s.width || 0,
                height: s.height || 0,
                hasManualPosition: s.hasManualPosition || false
            };
        }

        // Refresh state diagram data without resetting view (called after C# model changes)
        window.refreshStateDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                stDiagram = {
                    direction: data.direction || null,
                    isV2: data.isV2 !== false,
                    states: (data.states || []).map(s => stParseState(s)),
                    transitions: (data.transitions || []).map(t => ({
                        fromId: t.fromId || '',
                        toId: t.toId || '',
                        label: t.label || null
                    })),
                    notes: (data.notes || []).map(n => ({
                        text: n.text || '',
                        stateId: n.stateId || '',
                        position: n.position || 'RightOf'
                    })),
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };
                // Place only new states that don't have positions yet (don't re-run dagre)
                stPlaceNewStatesOnly(stDiagram.states);
                // Clean up orphaned pseudo node positions (e.g. after state deletion)
                stCleanOrphanedPseudoPositions();
                // Re-place pseudo nodes for any new transitions
                stPlacePseudoNodes();
                renderStateDiagram();
            } catch (err) {
                console.error('Failed to refresh state diagram:', err);
            }
        };

        // Restore state diagram for undo/redo — forces ALL positions from model data
        window.restoreStateDiagram = function(jsonData) {
            try {
                const data = typeof jsonData === 'string' ? JSON.parse(jsonData) : jsonData;
                stDiagram = {
                    direction: data.direction || null,
                    isV2: data.isV2 !== false,
                    states: (data.states || []).map(s => stParseState(s)),
                    transitions: (data.transitions || []).map(t => ({
                        fromId: t.fromId || '',
                        toId: t.toId || '',
                        label: t.label || null
                    })),
                    notes: (data.notes || []).map(n => ({
                        text: n.text || '',
                        stateId: n.stateId || '',
                        position: n.position || 'RightOf'
                    })),
                    preambleLines: data.preambleLines || [],
                    declarationLineIndex: data.declarationLineIndex || 0
                };

                // Force-hydrate ALL positions from model data (not just hasManualPosition)
                stStatePositions = {};
                stNotePositions = {};
                stManualNotePositions = new Set();
                function stForceHydratePositions(states) {
                    states.forEach(s => {
                        if (s.x || s.y) {
                            stStatePositions[s.id] = {
                                x: s.x,
                                y: s.y,
                                width: (s.width > 0) ? s.width : ST_BOX_MIN_WIDTH,
                                height: (s.height > 0) ? s.height : ST_BOX_HEIGHT
                            };
                        }
                        if (s.nestedStates && s.nestedStates.length > 0) {
                            stForceHydratePositions(s.nestedStates);
                        }
                    });
                }
                stForceHydratePositions(stDiagram.states);

                // Force-hydrate pseudo-node positions
                if (data.pseudoNodePositions) {
                    Object.keys(data.pseudoNodePositions).forEach(key => {
                        const pos = data.pseudoNodePositions[key];
                        if (pos && pos.length >= 2) {
                            stStatePositions[key] = { x: pos[0], y: pos[1], width: 20, height: 20 };
                        }
                    });
                }

                // Force-hydrate note positions
                if (data.notePositions) {
                    Object.keys(data.notePositions).forEach(key => {
                        const pos = data.notePositions[key];
                        if (pos && pos.length >= 2) {
                            const idx = parseInt(key.replace('note_', ''), 10);
                            if (!isNaN(idx)) {
                                stNotePositions[idx] = { x: pos[0], y: pos[1] };
                                stManualNotePositions.add(idx);
                            }
                        }
                    });
                }

                // Place any states that still don't have positions (new states with 0,0)
                stPlaceStatesInGrid(stDiagram.states, ST_LEFT_MARGIN, ST_TOP_MARGIN);
                stPlacePseudoNodes();
                // Expand composites to fit their nested children
                function expandAllComposites(states) {
                    states.forEach(s => {
                        if (s.nestedStates && s.nestedStates.length > 0) {
                            expandAllComposites(s.nestedStates);
                            stExpandCompositeToFitChildren(s.id);
                        }
                    });
                }
                expandAllComposites(stDiagram.states);
                renderStateDiagram();
            } catch (err) {
                console.error('Failed to restore state diagram:', err);
            }
        };

        // updateToolbarForStateDiagram removed - using unified updateToolbarForDiagramType()

        // Place only states that don't already have positions (used on refresh after model changes)
        function stPlaceNewStatesOnly(states) {
            states.forEach(s => {
                if (!stStatePositions[s.id]) {
                    // Find a reasonable position: below the last existing state
                    let maxY = ST_TOP_MARGIN;
                    Object.values(stStatePositions).forEach(pos => {
                        const bottom = (pos.y || 0) + (pos.height || ST_BOX_HEIGHT);
                        if (bottom > maxY) maxY = bottom;
                    });
                    const size = (() => {
                        const canvas = document.createElement('canvas');
                        const ctx = canvas.getContext('2d');
                        ctx.font = '13px sans-serif';
                        const textWidth = ctx.measureText(s.label || s.id).width + ST_BOX_PADDING * 2 + 10;
                        return { width: Math.max(ST_BOX_MIN_WIDTH, Math.ceil(textWidth)), height: ST_BOX_HEIGHT };
                    })();
                    stStatePositions[s.id] = {
                        x: ST_LEFT_MARGIN,
                        y: maxY + 30,
                        width: size.width,
                        height: size.height
                    };
                }
                // Recurse for nested states
                if (s.nestedStates && s.nestedStates.length > 0) {
                    s.nestedStates.forEach(ns => {
                        if (!stStatePositions[ns.id]) {
                            const parentPos = stStatePositions[s.id];
                            let maxNY = (parentPos.y || 0) + 35 + ST_COMPOSITE_PADDING;
                            s.nestedStates.forEach(existing => {
                                const ep = stStatePositions[existing.id];
                                if (ep) {
                                    const bottom = (ep.y || 0) + (ep.height || ST_BOX_HEIGHT);
                                    if (bottom > maxNY) maxNY = bottom;
                                }
                            });
                            stStatePositions[ns.id] = {
                                x: (parentPos.x || 0) + ST_COMPOSITE_PADDING,
                                y: maxNY + 20,
                                width: ST_BOX_MIN_WIDTH,
                                height: ST_BOX_HEIGHT
                            };
                        }
                    });
                    // Auto-expand composite to fit all nested children
                    stExpandCompositeToFitChildren(s.id);
                }
            });
        }

        // Remove orphaned pseudo node positions that no longer have matching transitions.
        // Called on refresh after model changes (e.g. state deletion) to clean up stale entries.
        function stCleanOrphanedPseudoPositions() {
            if (!stDiagram) return;

            // Collect which pseudo IDs are still needed
            const neededPseudos = new Set();

            // Top-level transitions
            stDiagram.transitions.forEach(trans => {
                if (trans.fromId === '[*]') neededPseudos.add('[*]_start');
                if (trans.toId === '[*]') neededPseudos.add('[*]_end');
            });

            // Nested transitions
            function collectNestedPseudos(states) {
                states.forEach(state => {
                    if (state.nestedTransitions) {
                        state.nestedTransitions.forEach(trans => {
                            if (trans.fromId === '[*]') neededPseudos.add('[*]_start_' + state.id);
                            if (trans.toId === '[*]') neededPseudos.add('[*]_end_' + state.id);
                        });
                    }
                    if (state.nestedStates && state.nestedStates.length > 0) {
                        collectNestedPseudos(state.nestedStates);
                    }
                });
            }
            collectNestedPseudos(stDiagram.states);

            // Remove positions for pseudo nodes that are no longer needed
            Object.keys(stStatePositions).forEach(key => {
                if (key.startsWith('[*]_') && !neededPseudos.has(key)) {
                    delete stStatePositions[key];
                }
            });

            // Also remove positions for states that no longer exist in the model
            const allStateIds = new Set();
            function collectStateIds(states) {
                states.forEach(s => {
                    allStateIds.add(s.id);
                    if (s.nestedStates) collectStateIds(s.nestedStates);
                });
            }
            collectStateIds(stDiagram.states);

            Object.keys(stStatePositions).forEach(key => {
                if (!key.startsWith('[*]_') && !allStateIds.has(key)) {
                    delete stStatePositions[key];
                }
            });
        }

        // Place [*] pseudo-nodes by examining transitions.
        // Called after hydrating positions from @pos comments (which only cover real states).
        // For each transition referencing [*], create a position entry for the pseudo-node
        // relative to the connected state so transitions render correctly.
        function stPlacePseudoNodes() {
            if (!stDiagram) return;
            const PSEUDO_SIZE = 20;
            const PSEUDO_GAP = 40; // gap above/below connected state

            // Top-level transitions
            stDiagram.transitions.forEach(trans => {
                if (trans.fromId === '[*]' && !stStatePositions['[*]_start']) {
                    // Place start pseudo above the target state
                    const toPos = stStatePositions[trans.toId];
                    if (toPos) {
                        stStatePositions['[*]_start'] = {
                            x: toPos.x + (toPos.width || ST_BOX_MIN_WIDTH) / 2 - PSEUDO_SIZE / 2,
                            y: toPos.y - PSEUDO_GAP - PSEUDO_SIZE,
                            width: PSEUDO_SIZE, height: PSEUDO_SIZE
                        };
                    } else {
                        stStatePositions['[*]_start'] = {
                            x: ST_LEFT_MARGIN, y: ST_TOP_MARGIN - PSEUDO_GAP,
                            width: PSEUDO_SIZE, height: PSEUDO_SIZE
                        };
                    }
                }
                if (trans.toId === '[*]' && !stStatePositions['[*]_end']) {
                    // Place end pseudo below the source state
                    const fromPos = stStatePositions[trans.fromId];
                    if (fromPos) {
                        stStatePositions['[*]_end'] = {
                            x: fromPos.x + (fromPos.width || ST_BOX_MIN_WIDTH) / 2 - PSEUDO_SIZE / 2,
                            y: fromPos.y + (fromPos.height || ST_BOX_HEIGHT) + PSEUDO_GAP,
                            width: PSEUDO_SIZE, height: PSEUDO_SIZE
                        };
                    } else {
                        stStatePositions['[*]_end'] = {
                            x: ST_LEFT_MARGIN, y: ST_TOP_MARGIN + 200,
                            width: PSEUDO_SIZE, height: PSEUDO_SIZE
                        };
                    }
                }
            });

            // Nested transitions inside composite states
            function placeNestedPseudos(states) {
                states.forEach(state => {
                    if (state.nestedTransitions && state.nestedTransitions.length > 0) {
                        const startId = '[*]_start_' + state.id;
                        const endId = '[*]_end_' + state.id;
                        const parentPos = stStatePositions[state.id];

                        state.nestedTransitions.forEach(trans => {
                            if (trans.fromId === '[*]' && !stStatePositions[startId]) {
                                // Place start pseudo at top of composite, above target
                                const toPos = stStatePositions[trans.toId];
                                if (toPos) {
                                    stStatePositions[startId] = {
                                        x: toPos.x + (toPos.width || ST_BOX_MIN_WIDTH) / 2 - PSEUDO_SIZE / 2,
                                        y: toPos.y - PSEUDO_GAP + 10,
                                        width: PSEUDO_SIZE, height: PSEUDO_SIZE
                                    };
                                } else if (parentPos) {
                                    stStatePositions[startId] = {
                                        x: parentPos.x + (parentPos.width || 200) / 2 - PSEUDO_SIZE / 2,
                                        y: parentPos.y + 35 + 5,
                                        width: PSEUDO_SIZE, height: PSEUDO_SIZE
                                    };
                                }
                            }
                            if (trans.toId === '[*]' && !stStatePositions[endId]) {
                                // Place end pseudo below source inside composite
                                const fromPos = stStatePositions[trans.fromId];
                                if (fromPos) {
                                    stStatePositions[endId] = {
                                        x: fromPos.x + (fromPos.width || ST_BOX_MIN_WIDTH) / 2 - PSEUDO_SIZE / 2,
                                        y: fromPos.y + (fromPos.height || ST_BOX_HEIGHT) + PSEUDO_GAP - 10,
                                        width: PSEUDO_SIZE, height: PSEUDO_SIZE
                                    };
                                } else if (parentPos) {
                                    stStatePositions[endId] = {
                                        x: parentPos.x + (parentPos.width || 200) / 2 - PSEUDO_SIZE / 2,
                                        y: parentPos.y + (parentPos.height || 100) - PSEUDO_SIZE - 5,
                                        width: PSEUDO_SIZE, height: PSEUDO_SIZE
                                    };
                                }
                            }
                        });
                    }
                    // Recurse for deeply nested composites
                    if (state.nestedStates && state.nestedStates.length > 0) {
                        placeNestedPseudos(state.nestedStates);
                    }
                });
            }
            placeNestedPseudos(stDiagram.states);
        }

        function stAutoLayoutStates() {
            if (!stDiagram) return;
            // First pass: place states in grid so every state has a position
            stPlaceStatesInGrid(stDiagram.states, ST_LEFT_MARGIN, ST_TOP_MARGIN);
            // Second pass: if dagre is available, use compound layout for proper nesting
            if (typeof dagre !== 'undefined') {
                try {
                    stDagreAutoLayout();
                } catch (e) {
                    console.warn('Initial dagre layout failed, using grid fallback:', e);
                }
            }
        }

        function stPlaceStatesInGrid(states, offsetX, offsetY) {
            const cols = Math.max(1, Math.ceil(Math.sqrt(states.length)));
            states.forEach((s, idx) => {
                if (!stStatePositions[s.id]) {
                    const col = idx % cols;
                    const row = Math.floor(idx / cols);
                    stStatePositions[s.id] = {
                        x: offsetX + col * ST_GAP_X,
                        y: offsetY + row * ST_GAP_Y
                    };
                }
                // Place nested states inside the composite parent
                if (s.nestedStates && s.nestedStates.length > 0) {
                    const parentPos = stStatePositions[s.id];
                    stPlaceStatesInGrid(s.nestedStates, parentPos.x + ST_COMPOSITE_PADDING, parentPos.y + 35);
                }
            });
        }

        // Dagre-based auto layout for state diagrams.
        // Uses 3-phase approach:
        //   Phase 0: Pre-compute composite sizes bottom-up using sub-dagre graphs
        //   Phase 1: Layout top-level states with a flat dagre graph (correct composite sizes)
        //   Phase 2: Position nested states inside composites using pre-computed sub-dagre results
        function stDagreAutoLayout() {
            if (!stDiagram || typeof dagre === 'undefined') return;

            // Helper: compute state dimensions without needing positions
            function computeStateSize(state) {
                if (state.type === 'Fork' || state.type === 'Join') return { width: 80, height: 6 };
                if (state.type === 'Choice') return { width: 30, height: 30 };
                const canvas = document.createElement('canvas');
                const ctx = canvas.getContext('2d');
                ctx.font = '13px sans-serif';
                const textWidth = ctx.measureText(state.label || state.id).width + ST_BOX_PADDING * 2 + 10;
                return { width: Math.max(ST_BOX_MIN_WIDTH, Math.ceil(textWidth)), height: ST_BOX_HEIGHT };
            }

            const HEADER = 35;
            const PAD = ST_COMPOSITE_PADDING;

            // --- Phase 0: Pre-compute composite sizes bottom-up ---
            const compositeData = {}; // stateId -> { width, height, subGraph }

            function preComputeComposite(state) {
                const nested = state.nestedStates || [];
                if (nested.length === 0) return;

                // Recurse first (bottom-up: compute deeply nested composites first)
                nested.forEach(ns => preComputeComposite(ns));

                const nestedTrans = state.nestedTransitions || [];

                const ng = new dagre.graphlib.Graph();
                ng.setGraph({ rankdir: 'TB', nodesep: 50, ranksep: 60, edgesep: 20, marginx: PAD, marginy: PAD });
                ng.setDefaultEdgeLabel(function() { return {}; });

                // Register nested states with their sizes (use pre-computed for deeply nested composites)
                nested.forEach(ns => {
                    const cd = compositeData[ns.id];
                    const size = cd ? { width: cd.width, height: cd.height } : computeStateSize(ns);
                    ng.setNode(ns.id, { width: size.width, height: size.height });
                });

                // Register [*] pseudo-nodes for nested transitions
                nestedTrans.forEach(trans => {
                    if (trans.fromId === '[*]' && !ng.hasNode('[*]_start_' + state.id)) {
                        ng.setNode('[*]_start_' + state.id, { width: 20, height: 20 });
                    }
                    if (trans.toId === '[*]' && !ng.hasNode('[*]_end_' + state.id)) {
                        ng.setNode('[*]_end_' + state.id, { width: 20, height: 20 });
                    }
                });

                // Ensure all nested transition endpoints exist
                nestedTrans.forEach(trans => {
                    const from = trans.fromId === '[*]' ? '[*]_start_' + state.id : trans.fromId;
                    const to = trans.toId === '[*]' ? '[*]_end_' + state.id : trans.toId;
                    if (!ng.hasNode(from)) ng.setNode(from, { width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT });
                    if (!ng.hasNode(to)) ng.setNode(to, { width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT });
                });

                // Register edges
                nestedTrans.forEach(trans => {
                    const from = trans.fromId === '[*]' ? '[*]_start_' + state.id : trans.fromId;
                    const to = trans.toId === '[*]' ? '[*]_end_' + state.id : trans.toId;
                    if (ng.hasNode(from) && ng.hasNode(to)) {
                        ng.setEdge(from, to);
                    }
                });

                try {
                    dagre.layout(ng);
                    const gi = ng.graph();
                    compositeData[state.id] = {
                        width: Math.max(200, (gi.width || 200) + PAD * 2),
                        height: Math.max(100, (gi.height || 100) + HEADER + PAD),
                        subGraph: ng
                    };
                } catch (e) {
                    console.warn('Pre-compute sub-dagre failed for ' + state.id + ':', e);
                    // Fallback estimation
                    const cols = Math.max(1, Math.ceil(Math.sqrt(nested.length)));
                    const rows = Math.ceil(nested.length / cols);
                    compositeData[state.id] = {
                        width: Math.max(200, cols * (ST_BOX_MIN_WIDTH + 40) + PAD * 2),
                        height: Math.max(100, rows * (ST_BOX_HEIGHT + 40) + HEADER + PAD),
                        subGraph: null
                    };
                }
            }

            stDiagram.states.forEach(state => preComputeComposite(state));

            // Collect all nested state IDs to avoid registering them at top level
            const nestedIds = new Set();
            function collectNestedIds(states) {
                states.forEach(s => {
                    if (s.nestedStates) {
                        s.nestedStates.forEach(ns => {
                            nestedIds.add(ns.id);
                            collectNestedIds([ns]);
                        });
                    }
                });
            }
            collectNestedIds(stDiagram.states);

            // --- Phase 1: Layout top-level states with correct sizes ---
            const g = new dagre.graphlib.Graph();
            g.setGraph({
                rankdir: 'TB',
                nodesep: 80,
                ranksep: 100,
                edgesep: 30,
                marginx: ST_LEFT_MARGIN,
                marginy: ST_TOP_MARGIN
            });
            g.setDefaultEdgeLabel(function() { return {}; });

            stDiagram.states.forEach(state => {
                const cd = compositeData[state.id];
                if (cd) {
                    g.setNode(state.id, { width: cd.width, height: cd.height });
                } else {
                    const size = computeStateSize(state);
                    g.setNode(state.id, { width: size.width, height: size.height });
                }
            });

            // Register [*] pseudo-nodes for top-level transitions
            stDiagram.transitions.forEach(trans => {
                if (trans.fromId === '[*]' && !g.hasNode('[*]_start')) {
                    g.setNode('[*]_start', { width: 20, height: 20 });
                }
                if (trans.toId === '[*]' && !g.hasNode('[*]_end')) {
                    g.setNode('[*]_end', { width: 20, height: 20 });
                }
            });

            // Ensure transition endpoints exist but skip nested state IDs
            stDiagram.transitions.forEach(trans => {
                const from = trans.fromId === '[*]' ? '[*]_start' : trans.fromId;
                const to = trans.toId === '[*]' ? '[*]_end' : trans.toId;
                if (!g.hasNode(from) && !nestedIds.has(trans.fromId)) {
                    g.setNode(from, { width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT });
                }
                if (!g.hasNode(to) && !nestedIds.has(trans.toId)) {
                    g.setNode(to, { width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT });
                }
            });

            // Register edges only between nodes in the top-level graph
            // Include label dimensions so dagre reserves space for transition labels
            stDiagram.transitions.forEach(trans => {
                const from = trans.fromId === '[*]' ? '[*]_start' : trans.fromId;
                const to = trans.toId === '[*]' ? '[*]_end' : trans.toId;
                if (g.hasNode(from) && g.hasNode(to)) {
                    const edgeOpts = {};
                    if (trans.label) {
                        edgeOpts.labelpos = 'c';
                        edgeOpts.width = Math.max(40, trans.label.length * 7 + 10);
                        edgeOpts.height = 20;
                    }
                    g.setEdge(from, to, edgeOpts);
                }
            });

            try {
                dagre.layout(g);
            } catch (e) {
                console.error('State diagram dagre layout failed:', e);
                return;
            }

            // Apply positions to top-level states
            stDiagram.states.forEach(state => {
                const n = g.node(state.id);
                if (n) {
                    stStatePositions[state.id] = {
                        x: n.x - (n.width || 0) / 2,
                        y: n.y - (n.height || 0) / 2,
                        width: n.width,
                        height: n.height
                    };
                }
            });

            // Store top-level [*] pseudo-node positions
            ['[*]_start', '[*]_end'].forEach(id => {
                const node = g.node(id);
                if (node) {
                    stStatePositions[id] = {
                        x: node.x - node.width / 2,
                        y: node.y - node.height / 2,
                        width: node.width,
                        height: node.height
                    };
                }
            });

            // --- Phase 2: Position nested states inside composites ---
            function positionNested(parentState) {
                const nested = parentState.nestedStates || [];
                if (nested.length === 0) return;

                const parentPos = stStatePositions[parentState.id];
                if (!parentPos) return;

                const cd = compositeData[parentState.id];
                const ng = cd ? cd.subGraph : null;

                const ox = parentPos.x;
                const oy = parentPos.y + HEADER;

                if (ng) {
                    // Use pre-computed sub-dagre positions (always overwrite)
                    nested.forEach(ns => {
                        const ln = ng.node(ns.id);
                        if (ln) {
                            stStatePositions[ns.id] = {
                                x: ox + ln.x - (ln.width || 0) / 2,
                                y: oy + ln.y - (ln.height || 0) / 2,
                                width: ln.width,
                                height: ln.height
                            };
                        }
                    });

                    // Position nested [*] pseudo-nodes
                    ['start', 'end'].forEach(type => {
                        const pid = '[*]_' + type + '_' + parentState.id;
                        const n = ng.node(pid);
                        if (n) {
                            stStatePositions[pid] = {
                                x: ox + n.x - n.width / 2,
                                y: oy + n.y - n.height / 2,
                                width: n.width,
                                height: n.height
                            };
                        }
                    });
                } else {
                    // Fallback: grid placement inside parent (always overwrite)
                    const cols = Math.max(1, Math.ceil(Math.sqrt(nested.length)));
                    nested.forEach((ns, i) => {
                        const col = i % cols;
                        const row = Math.floor(i / cols);
                        const size = computeStateSize(ns);
                        stStatePositions[ns.id] = {
                            x: ox + PAD + col * (size.width + 40),
                            y: oy + PAD + row * (size.height + 40),
                            width: size.width,
                            height: size.height
                        };
                    });
                }

                // Update parent size from pre-computed data
                if (cd) {
                    stStatePositions[parentState.id] = {
                        ...parentPos,
                        width: Math.max(parentPos.width || 0, cd.width),
                        height: Math.max(parentPos.height || 0, cd.height)
                    };
                }

                // Recurse for deeply nested composites
                nested.forEach(ns => positionNested(ns));
            }

            stDiagram.states.forEach(state => positionNested(state));
        }

        // Helper: send ALL state, pseudo, and manually-dragged note positions to C#
        function stSendAllPositionsUpdate() {
            const allPositions = [];
            const allStates = stGetAllFlatStates();
            allStates.forEach(state => {
                const sp = stStatePositions[state.id];
                if (sp) {
                    allPositions.push({ stateId: state.id, x: sp.x, y: sp.y, width: sp.width || 0, height: sp.height || 0 });
                }
            });
            // Include pseudo-state positions
            Object.keys(stStatePositions).forEach(key => {
                if (key.startsWith('[*]_')) {
                    const sp = stStatePositions[key];
                    allPositions.push({ stateId: key, x: sp.x, y: sp.y, width: sp.width || 0, height: sp.height || 0 });
                }
            });
            // Include only manually-dragged note positions (not auto-computed defaults)
            stManualNotePositions.forEach(idx => {
                const np = stNotePositions[idx];
                if (np) {
                    allPositions.push({ stateId: 'note_' + idx, x: np.x, y: np.y, width: 0, height: 0 });
                }
            });
            postMessage({ type: 'st_allPositionsUpdate', positions: allPositions });
        }

        function stGetAllFlatStates() {
            if (!stDiagram) return [];
            const result = [];
            function collect(states) {
                states.forEach(s => {
                    result.push(s);
                    if (s.nestedStates && s.nestedStates.length > 0) {
                        collect(s.nestedStates);
                    }
                });
            }
            collect(stDiagram.states);
            return result;
        }

        function stFindState(stateId) {
            const allStates = stGetAllFlatStates();
            return allStates.find(s => s.id === stateId) || null;
        }

        // Extract the parent composite ID from a pseudo node ID (e.g., '[*]_start_Processing' → 'Processing')
        // Returns null for top-level pseudo nodes ('[*]_start', '[*]_end') that have no parent composite.
        function stGetPseudoParentId(pseudoId) {
            if (!pseudoId) return null;
            const startPrefix = '[*]_start_';
            const endPrefix = '[*]_end_';
            if (pseudoId.startsWith(startPrefix) && pseudoId.length > startPrefix.length) {
                return pseudoId.substring(startPrefix.length);
            }
            if (pseudoId.startsWith(endPrefix) && pseudoId.length > endPrefix.length) {
                return pseudoId.substring(endPrefix.length);
            }
            return null;
        }

        // Find the parent composite state that contains a given child state ID
        function stFindParentComposite(childId) {
            if (!stDiagram) return null;
            function search(states) {
                for (const s of states) {
                    if (s.nestedStates && s.nestedStates.length > 0) {
                        if (s.nestedStates.some(ns => ns.id === childId)) return s;
                        const deeper = search(s.nestedStates);
                        if (deeper) return deeper;
                    }
                }
                return null;
            }
            return search(stDiagram.states);
        }

        // Expand a composite and all its ancestor composites (for nested composite support)
        function stExpandCompositeChain(compositeId) {
            let current = compositeId;
            while (current) {
                stExpandCompositeToFitChildren(current);
                const parent = stFindParentComposite(current);
                current = parent ? parent.id : null;
            }
        }

        // Generate a unique state ID with the given prefix (avoids collisions)
        function stNextUniqueId(prefix) {
            const allIds = new Set(stGetAllFlatStates().map(s => s.id));
            let counter = stDiagram.states.length + 1;
            let id = prefix + counter;
            while (allIds.has(id)) { counter++; id = prefix + counter; }
            return id;
        }

        // Recursively move nested states and their pseudo-states by a delta (used when dragging composite parent)
        function stMoveNestedStates(nestedStates, dx, dy, parentId) {
            // Move nested pseudo-states for this parent
            if (parentId) {
                ['[*]_start_' + parentId, '[*]_end_' + parentId].forEach(pid => {
                    const pos = stStatePositions[pid];
                    if (pos) {
                        stStatePositions[pid] = { ...pos, x: pos.x + dx, y: pos.y + dy };
                    }
                });
            }
            nestedStates.forEach(ns => {
                const pos = stStatePositions[ns.id];
                if (pos) {
                    stStatePositions[ns.id] = {
                        ...pos,
                        x: pos.x + dx,
                        y: pos.y + dy
                    };
                }
                if (ns.nestedStates && ns.nestedStates.length > 0) {
                    stMoveNestedStates(ns.nestedStates, dx, dy, ns.id);
                }
            });
        }

        // Resize a composite state's stored dimensions to exactly fit its nested children + pseudo-states.
        // This both expands (when child moved outside) and shrinks (when child moved inward).
        function stExpandCompositeToFitChildren(parentId) {
            const parentState = stFindState(parentId);
            if (!parentState) return;
            const parentPos = stStatePositions[parentId];
            if (!parentPos) return;

            const PAD = ST_COMPOSITE_PADDING;
            const HEADER = 35; // space for label + divider
            let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;

            // Collect bounds from nested states
            (parentState.nestedStates || []).forEach(ns => {
                const cp = stStatePositions[ns.id];
                if (!cp) return;
                const w = cp.width || ST_BOX_MIN_WIDTH;
                const h = cp.height || ST_BOX_HEIGHT;
                if (cp.x < minX) minX = cp.x;
                if (cp.y < minY) minY = cp.y;
                if (cp.x + w > maxX) maxX = cp.x + w;
                if (cp.y + h > maxY) maxY = cp.y + h;
            });

            // Also consider nested pseudo-states ([*] start/end for this parent)
            ['[*]_start_' + parentId, '[*]_end_' + parentId].forEach(pid => {
                const pp = stStatePositions[pid];
                if (!pp) return;
                const w = pp.width || 20;
                const h = pp.height || 20;
                if (pp.x < minX) minX = pp.x;
                if (pp.y < minY) minY = pp.y;
                if (pp.x + w > maxX) maxX = pp.x + w;
                if (pp.y + h > maxY) maxY = pp.y + h;
            });

            if (minX === Infinity) return; // no children positioned

            // Required composite bounds to encompass all children
            const requiredLeft = minX - PAD;
            const requiredTop = minY - HEADER - PAD;
            const requiredRight = maxX + PAD;
            const requiredBottom = maxY + PAD;

            // Fit composite exactly to children (both expand and shrink)
            // Use minimum label width so composite is never narrower than its title
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            ctx.font = 'bold 13px sans-serif';
            const labelWidth = ctx.measureText(parentState.label || parentState.id).width + PAD * 2 + 20;
            const minWidth = Math.max(200, labelWidth);
            const minHeight = 100;

            const newX = Math.min(parentPos.x, requiredLeft);
            const newY = Math.min(parentPos.y, requiredTop);
            const newRight = Math.max(newX + minWidth, requiredRight);
            const newBottom = Math.max(newY + minHeight, requiredBottom);

            stStatePositions[parentId] = {
                ...parentPos,
                x: newX,
                y: newY,
                width: newRight - newX,
                height: newBottom - newY
            };
        }

        function stGetStateBox(state) {
            const pos = stStatePositions[state.id];
            if (!pos) return { x: 0, y: 0, width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT };

            if (state.type === 'Fork' || state.type === 'Join') {
                return { x: pos.x, y: pos.y, width: 80, height: 6 };
            }
            if (state.type === 'Choice') {
                return { x: pos.x, y: pos.y, width: 30, height: 30 };
            }

            // Composite state - use dagre-computed dimensions if available, else estimate
            if (state.type === 'Composite' || (state.nestedStates && state.nestedStates.length > 0)) {
                if (pos.width && pos.height) {
                    return { x: pos.x, y: pos.y, width: pos.width, height: pos.height };
                }
                const canvas = document.createElement('canvas');
                const ctx = canvas.getContext('2d');
                ctx.font = 'bold 13px sans-serif';
                const labelWidth = ctx.measureText(state.label || state.id).width + ST_BOX_PADDING * 2 + 20;
                const nestedCount = state.nestedStates ? state.nestedStates.length : 0;
                const innerCols = Math.max(1, Math.ceil(Math.sqrt(nestedCount)));
                const innerRows = Math.ceil(nestedCount / innerCols);
                const width = Math.max(200, labelWidth, innerCols * ST_GAP_X + ST_COMPOSITE_PADDING * 2);
                const height = Math.max(100, ST_BOX_HEIGHT + innerRows * ST_GAP_Y + ST_COMPOSITE_PADDING);
                return { x: pos.x, y: pos.y, width, height };
            }

            // Simple state - measure text width
            const canvas = document.createElement('canvas');
            const ctx = canvas.getContext('2d');
            ctx.font = '13px sans-serif';
            const textWidth = ctx.measureText(state.label || state.id).width + ST_BOX_PADDING * 2 + 10;
            const width = Math.max(ST_BOX_MIN_WIDTH, Math.ceil(textWidth));
            return { x: pos.x, y: pos.y, width, height: ST_BOX_HEIGHT };
        }

        function renderStateDiagram() {
            if (!stDiagram) return;

            // Show diagram-svg, hide editorCanvas when rendering standard diagram types
            var dSvg = document.getElementById('diagram-svg');
            var eCanvas = document.getElementById('editorCanvas');
            if (dSvg) dSvg.style.display = '';
            if (eCanvas) { eCanvas.style.display = 'none'; eCanvas.innerHTML = ''; }

            nodesLayer.innerHTML = '';
            edgesLayer.innerHTML = '';
            subgraphsLayer.innerHTML = '';

            // Render states first (composites append early, nested states on top)
            stDiagram.states.forEach(state => stRenderState(state));

            // Render pseudo-states ON TOP of states (on nodesLayer so they're above composite fills)
            stRenderAllPseudoStates();

            // Render top-level transitions
            stDiagram.transitions.forEach((trans, idx) => {
                stRenderTransition(trans, idx);
            });

            // Render nested transitions within composite states
            stRenderNestedTransitions(stDiagram.states);

            // Render notes
            stDiagram.notes.forEach((note, idx) => {
                stRenderNote(note, idx);
            });

            updateMinimap();
            updateCanvasAndZoomLimits();
        }

        // Render all [*] pseudo-state circles as draggable groups (deduplicated)
        function stRenderAllPseudoStates() {
            // Top-level pseudo-states
            if (stStatePositions['[*]_start']) {
                stRenderPseudoCircle('[*]_start', 'start');
            }
            if (stStatePositions['[*]_end']) {
                stRenderPseudoCircle('[*]_end', 'end');
            }
            // Nested pseudo-states inside composites
            stGetAllFlatStates().forEach(state => {
                const startId = '[*]_start_' + state.id;
                const endId = '[*]_end_' + state.id;
                if (stStatePositions[startId]) {
                    stRenderPseudoCircle(startId, 'start');
                }
                if (stStatePositions[endId]) {
                    stRenderPseudoCircle(endId, 'end');
                }
            });
        }

        function stRenderPseudoCircle(pseudoId, type) {
            const pos = stStatePositions[pseudoId];
            if (!pos) return;

            const cx = pos.x + (pos.width || 20) / 2;
            const cy = pos.y + (pos.height || 20) / 2;

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('st-pseudo-node');
            g.setAttribute('data-pseudo-id', pseudoId);
            g.style.cursor = 'move';

            if (type === 'start') {
                const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                circle.setAttribute('cx', cx);
                circle.setAttribute('cy', cy);
                circle.setAttribute('r', '8');
                circle.setAttribute('fill', 'var(--node-text)');
                g.appendChild(circle);
            } else {
                // End: outer ring + inner dot
                const outer = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                outer.setAttribute('cx', cx);
                outer.setAttribute('cy', cy);
                outer.setAttribute('r', '10');
                outer.setAttribute('fill', 'none');
                outer.setAttribute('stroke', 'var(--node-text)');
                outer.setAttribute('stroke-width', '2');
                g.appendChild(outer);

                const inner = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                inner.setAttribute('cx', cx);
                inner.setAttribute('cy', cy);
                inner.setAttribute('r', '6');
                inner.setAttribute('fill', 'var(--node-text)');
                g.appendChild(inner);
            }

            // Invisible larger hitbox for easier grabbing
            const hitbox = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            hitbox.setAttribute('cx', cx);
            hitbox.setAttribute('cy', cy);
            hitbox.setAttribute('r', '14');
            hitbox.setAttribute('fill', 'transparent');
            g.appendChild(hitbox);

            nodesLayer.appendChild(g);
        }

        function stRenderState(state) {
            const box = stGetStateBox(state);
            if (!stStatePositions[state.id]) return;
            stStatePositions[state.id].width = box.width;
            stStatePositions[state.id].height = box.height;

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('node-group', 'st-state');
            g.setAttribute('data-state-id', state.id);
            g.setAttribute('transform', `translate(${box.x}, ${box.y})`);

            if (state.type === 'Fork' || state.type === 'Join') {
                // Horizontal bar
                const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('x', '0');
                rect.setAttribute('y', '0');
                rect.setAttribute('width', box.width);
                rect.setAttribute('height', box.height);
                rect.setAttribute('rx', '3');
                rect.setAttribute('fill', 'var(--node-text)');
                rect.setAttribute('stroke', 'var(--node-text)');
                if (stSelectedStateId === state.id) rect.classList.add('selected');
                g.appendChild(rect);
            } else if (state.type === 'Choice') {
                // Diamond
                const diamond = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                const cx = box.width / 2;
                const cy = box.height / 2;
                diamond.setAttribute('points', `${cx},0 ${box.width},${cy} ${cx},${box.height} 0,${cy}`);
                diamond.classList.add('node-shape');
                if (stSelectedStateId === state.id) diamond.classList.add('selected');
                g.appendChild(diamond);
            } else if (state.type === 'Composite' || (state.nestedStates && state.nestedStates.length > 0)) {
                // Composite state box with dashed border
                const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('x', '0');
                rect.setAttribute('y', '0');
                rect.setAttribute('width', box.width);
                rect.setAttribute('height', box.height);
                rect.setAttribute('rx', '8');
                rect.classList.add('node-shape');
                rect.setAttribute('stroke-dasharray', '5,3');
                if (stSelectedStateId === state.id) rect.classList.add('selected');
                g.appendChild(rect);

                // Label at top
                const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                label.setAttribute('x', ST_BOX_PADDING);
                label.setAttribute('y', '20');
                label.setAttribute('fill', 'var(--node-text)');
                label.setAttribute('font-size', '13');
                label.setAttribute('font-weight', 'bold');
                setTextWithLineBreaks(label, state.label || state.id);
                g.appendChild(label);

                // Divider
                const divider = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                divider.setAttribute('x1', '0');
                divider.setAttribute('y1', '28');
                divider.setAttribute('x2', box.width);
                divider.setAttribute('y2', '28');
                divider.setAttribute('stroke', 'var(--node-stroke)');
                divider.setAttribute('stroke-width', '1');
                g.appendChild(divider);

                // Add grip points to composite BEFORE appending
                const compositeGrips = [
                    { cx: box.width / 2, cy: 0 },
                    { cx: box.width / 2, cy: box.height },
                    { cx: box.width, cy: box.height / 2 },
                    { cx: 0, cy: box.height / 2 }
                ];
                compositeGrips.forEach(gp => {
                    const grip = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                    grip.setAttribute('cx', gp.cx);
                    grip.setAttribute('cy', gp.cy);
                    grip.setAttribute('r', '3');
                    grip.classList.add('grip-point');
                    grip.setAttribute('data-state-id', state.id);
                    g.appendChild(grip);
                });

                // Append composite background to subgraphsLayer (behind transitions)
                subgraphsLayer.appendChild(g);

                // Render nested states on nodesLayer (they appear on top of transitions)
                if (state.nestedStates) {
                    state.nestedStates.forEach(ns => stRenderState(ns));
                }
                return; // Don't hit final nodesLayer.appendChild(g) again
            } else {
                // Simple state - rounded rectangle
                const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                rect.setAttribute('x', '0');
                rect.setAttribute('y', '0');
                rect.setAttribute('width', box.width);
                rect.setAttribute('height', box.height);
                rect.setAttribute('rx', '8');
                rect.classList.add('node-shape');
                if (stSelectedStateId === state.id) rect.classList.add('selected');
                g.appendChild(rect);

                // State name
                const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                text.setAttribute('x', box.width / 2);
                text.setAttribute('y', box.height / 2 + 5);
                text.setAttribute('text-anchor', 'middle');
                text.setAttribute('fill', 'var(--node-text)');
                text.setAttribute('font-size', '13');
                setTextWithLineBreaks(text, state.label || state.id);
                g.appendChild(text);
            }

            // Connection grip points (N, S, E, W)
            const grips = [
                { cx: box.width / 2, cy: 0 },
                { cx: box.width / 2, cy: box.height },
                { cx: box.width, cy: box.height / 2 },
                { cx: 0, cy: box.height / 2 }
            ];
            grips.forEach(gp => {
                const grip = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                grip.setAttribute('cx', gp.cx);
                grip.setAttribute('cy', gp.cy);
                grip.setAttribute('r', '3');
                grip.classList.add('grip-point');
                grip.setAttribute('data-state-id', state.id);
                g.appendChild(grip);
            });

            nodesLayer.appendChild(g);
        }

        function stRenderTransition(trans, idx) {
            const fromState = stFindState(trans.fromId);
            const toState = stFindState(trans.toId);

            let fromBox, toBox;

            // Handle [*] pseudo-states (start/end) - circles rendered by stRenderAllPseudoStates
            if (trans.fromId === '[*]') {
                const pseudoPos = stStatePositions['[*]_start'];
                if (pseudoPos) {
                    fromBox = { x: pseudoPos.x, y: pseudoPos.y, width: pseudoPos.width || 20, height: pseudoPos.height || 20 };
                } else {
                    const toS = stFindState(trans.toId);
                    const toPos = toS ? stStatePositions[toS.id] : null;
                    fromBox = {
                        x: (toPos ? toPos.x + 40 : ST_LEFT_MARGIN),
                        y: (toPos ? toPos.y - 40 : ST_TOP_MARGIN - 40),
                        width: 16, height: 16
                    };
                }
            } else {
                fromBox = fromState ? stGetStateBox(fromState) : null;
            }

            if (trans.toId === '[*]') {
                const pseudoPos = stStatePositions['[*]_end'];
                if (pseudoPos) {
                    toBox = { x: pseudoPos.x, y: pseudoPos.y, width: pseudoPos.width || 20, height: pseudoPos.height || 20 };
                } else {
                    const fromS = stFindState(trans.fromId);
                    const fromPos = fromS ? stStatePositions[fromS.id] : null;
                    toBox = {
                        x: (fromPos ? fromPos.x + 40 : ST_LEFT_MARGIN),
                        y: (fromPos ? fromPos.y + (fromPos.height || ST_BOX_HEIGHT) + 30 : ST_TOP_MARGIN + 100),
                        width: 20, height: 20
                    };
                }
            } else {
                toBox = toState ? stGetStateBox(toState) : null;
            }

            if (!fromBox || !toBox) return;

            // Calculate edge points
            const fromCx = fromBox.x + fromBox.width / 2;
            const fromCy = fromBox.y + fromBox.height / 2;
            const toCx = toBox.x + toBox.width / 2;
            const toCy = toBox.y + toBox.height / 2;

            const ports = stGetEdgePorts(fromBox, toBox, fromCx, fromCy, toCx, toCy);

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('st-transition');
            g.setAttribute('data-trans-index', idx);

            const pathD = buildEdgePath(ports);
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            line.setAttribute('d', pathD);
            line.setAttribute('fill', 'none');
            line.setAttribute('stroke', stSelectedTransIndex === idx ? 'var(--edge-selected)' : 'var(--edge-color)');
            line.setAttribute('stroke-width', stSelectedTransIndex === idx ? '3' : '2');
            line.setAttribute('marker-end', 'url(#arrowhead)');
            g.appendChild(line);

            // Clickable hitbox
            const hitbox = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            hitbox.setAttribute('d', pathD);
            hitbox.setAttribute('fill', 'none');
            hitbox.setAttribute('stroke', 'transparent');
            hitbox.setAttribute('stroke-width', '12');
            hitbox.style.cursor = 'pointer';
            g.appendChild(hitbox);

            // Label
            if (trans.label) {
                const mid = edgeMidpoint(ports);
                const labelText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                labelText.setAttribute('x', mid.x);
                labelText.setAttribute('y', mid.y - 6);
                labelText.setAttribute('text-anchor', 'middle');
                labelText.setAttribute('fill', 'var(--node-text)');
                labelText.setAttribute('font-size', '11');
                labelText.setAttribute('font-style', 'italic');
                setTextWithLineBreaks(labelText, trans.label);
                g.appendChild(labelText);
            }

            edgesLayer.appendChild(g);
        }

        function stGetEdgePorts(fromBox, toBox, fromCx, fromCy, toCx, toCy) {
            const fromPorts = [
                { x: fromBox.x + fromBox.width / 2, y: fromBox.y, dir: 'N' },
                { x: fromBox.x + fromBox.width / 2, y: fromBox.y + fromBox.height, dir: 'S' },
                { x: fromBox.x + fromBox.width, y: fromBox.y + fromBox.height / 2, dir: 'E' },
                { x: fromBox.x, y: fromBox.y + fromBox.height / 2, dir: 'W' }
            ];
            const toPorts = [
                { x: toBox.x + toBox.width / 2, y: toBox.y, dir: 'N' },
                { x: toBox.x + toBox.width / 2, y: toBox.y + toBox.height, dir: 'S' },
                { x: toBox.x + toBox.width, y: toBox.y + toBox.height / 2, dir: 'E' },
                { x: toBox.x, y: toBox.y + toBox.height / 2, dir: 'W' }
            ];

            let bestDist = Infinity;
            let best = { x1: fromCx, y1: fromCy, x2: toCx, y2: toCy, fromDir: 'S', toDir: 'N' };
            fromPorts.forEach(fp => {
                toPorts.forEach(tp => {
                    const d = Math.hypot(fp.x - tp.x, fp.y - tp.y);
                    if (d < bestDist) {
                        bestDist = d;
                        best = { x1: fp.x, y1: fp.y, x2: tp.x, y2: tp.y, fromDir: fp.dir, toDir: tp.dir };
                    }
                });
            });
            return best;
        }

        // Compute a cubic Bézier control point offset based on port direction
        function edgeDirOffset(dir, dist) {
            const d = Math.max(30, Math.min(dist * 0.4, 80));
            switch (dir) {
                case 'N': return { dx: 0, dy: -d };
                case 'S': return { dx: 0, dy: d };
                case 'E': return { dx: d, dy: 0 };
                case 'W': return { dx: -d, dy: 0 };
                default: return { dx: 0, dy: -d };
            }
        }

        // Build an SVG path string: straight line or cubic Bézier
        function buildEdgePath(ports) {
            if (!curvedEdges) {
                return 'M ' + ports.x1 + ',' + ports.y1 + ' L ' + ports.x2 + ',' + ports.y2;
            }
            const dist = Math.hypot(ports.x2 - ports.x1, ports.y2 - ports.y1);
            const c1 = edgeDirOffset(ports.fromDir, dist);
            const c2 = edgeDirOffset(ports.toDir, dist);
            const cx1 = ports.x1 + c1.dx;
            const cy1 = ports.y1 + c1.dy;
            const cx2 = ports.x2 + c2.dx;
            const cy2 = ports.y2 + c2.dy;
            return 'M ' + ports.x1 + ',' + ports.y1 +
                   ' C ' + cx1 + ',' + cy1 + ' ' + cx2 + ',' + cy2 + ' ' + ports.x2 + ',' + ports.y2;
        }

        // Compute tangent angles at both endpoints of the edge path.
        // For curved Bézier edges, the tangent differs from the straight-line angle.
        // Returns { startAngle, endAngle } where startAngle points FROM source toward curve,
        // and endAngle points FROM target toward curve (reversed for marker orientation).
        function edgeTangentAngles(ports) {
            const straightAngle = Math.atan2(ports.y2 - ports.y1, ports.x2 - ports.x1);
            if (!curvedEdges) {
                return { startAngle: straightAngle, endAngle: straightAngle + Math.PI };
            }
            const dist = Math.hypot(ports.x2 - ports.x1, ports.y2 - ports.y1);
            const c1 = edgeDirOffset(ports.fromDir, dist);
            const c2 = edgeDirOffset(ports.toDir, dist);
            const cx1 = ports.x1 + c1.dx;
            const cy1 = ports.y1 + c1.dy;
            const cx2 = ports.x2 + c2.dx;
            const cy2 = ports.y2 + c2.dy;
            // Tangent at t=0: direction from P0 toward cp1
            const startAngle = Math.atan2(cy1 - ports.y1, cx1 - ports.x1);
            // Tangent at t=1: direction from cp2 toward P3 — reversed so marker points toward node
            const endAngle = Math.atan2(cy2 - ports.y2, cx2 - ports.x2);
            return { startAngle, endAngle };
        }

        // Compute the midpoint (and tangent angle) of the Bézier at t=0.5 for label placement
        function edgeMidpoint(ports) {
            if (!curvedEdges) {
                return { x: (ports.x1 + ports.x2) / 2, y: (ports.y1 + ports.y2) / 2 };
            }
            const dist = Math.hypot(ports.x2 - ports.x1, ports.y2 - ports.y1);
            const c1 = edgeDirOffset(ports.fromDir, dist);
            const c2 = edgeDirOffset(ports.toDir, dist);
            const cx1 = ports.x1 + c1.dx;
            const cy1 = ports.y1 + c1.dy;
            const cx2 = ports.x2 + c2.dx;
            const cy2 = ports.y2 + c2.dy;
            // Cubic Bézier at t=0.5
            const t = 0.5;
            const mt = 1 - t;
            const x = mt*mt*mt*ports.x1 + 3*mt*mt*t*cx1 + 3*mt*t*t*cx2 + t*t*t*ports.x2;
            const y = mt*mt*mt*ports.y1 + 3*mt*mt*t*cy1 + 3*mt*t*t*cy2 + t*t*t*ports.y2;
            return { x, y };
        }

        // Render transitions within composite states (nested transitions)
        function stRenderNestedTransitions(states) {
            states.forEach(state => {
                if (state.nestedTransitions && state.nestedTransitions.length > 0) {
                    state.nestedTransitions.forEach((trans, nIdx) => {
                        stRenderNestedTransition(trans, state.id, nIdx);
                    });
                }
                if (state.nestedStates && state.nestedStates.length > 0) {
                    stRenderNestedTransitions(state.nestedStates);
                }
            });
        }

        function stRenderNestedTransition(trans, parentId, nestedIdx) {
            let fromBox, toBox;

            // Pseudo circles are rendered by stRenderAllPseudoStates - just compute boxes here
            if (trans.fromId === '[*]') {
                const pseudoId = '[*]_start_' + parentId;
                const pos = stStatePositions[pseudoId];
                if (pos) {
                    fromBox = { x: pos.x, y: pos.y, width: pos.width || 20, height: pos.height || 20 };
                } else {
                    const toS = stFindState(trans.toId);
                    const toPos = toS ? stStatePositions[toS.id] : null;
                    fromBox = {
                        x: (toPos ? toPos.x + 40 : ST_LEFT_MARGIN),
                        y: (toPos ? toPos.y - 30 : ST_TOP_MARGIN),
                        width: 16, height: 16
                    };
                }
            } else {
                const fromState = stFindState(trans.fromId);
                fromBox = fromState ? stGetStateBox(fromState) : null;
            }

            if (trans.toId === '[*]') {
                const pseudoId = '[*]_end_' + parentId;
                const pos = stStatePositions[pseudoId];
                if (pos) {
                    toBox = { x: pos.x, y: pos.y, width: pos.width || 20, height: pos.height || 20 };
                } else {
                    const fromS = stFindState(trans.fromId);
                    const fromPos = fromS ? stStatePositions[fromS.id] : null;
                    toBox = {
                        x: (fromPos ? fromPos.x + 40 : ST_LEFT_MARGIN),
                        y: (fromPos ? fromPos.y + (fromPos.height || ST_BOX_HEIGHT) + 20 : ST_TOP_MARGIN + 100),
                        width: 20, height: 20
                    };
                }
            } else {
                const toState = stFindState(trans.toId);
                toBox = toState ? stGetStateBox(toState) : null;
            }

            if (!fromBox || !toBox) return;

            const fromCx = fromBox.x + fromBox.width / 2;
            const fromCy = fromBox.y + fromBox.height / 2;
            const toCx = toBox.x + toBox.width / 2;
            const toCy = toBox.y + toBox.height / 2;

            const ports = stGetEdgePorts(fromBox, toBox, fromCx, fromCy, toCx, toCy);

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('st-transition', 'st-nested-transition');
            g.setAttribute('data-parent-id', parentId);
            g.setAttribute('data-nested-trans-index', nestedIdx);

            const nestedPathD = buildEdgePath(ports);
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            line.setAttribute('d', nestedPathD);
            line.setAttribute('fill', 'none');
            line.setAttribute('stroke', stSelectedNestedTrans && stSelectedNestedTrans.parentId === parentId && stSelectedNestedTrans.idx === nestedIdx ? 'var(--edge-selected)' : 'var(--edge-color)');
            line.setAttribute('stroke-width', stSelectedNestedTrans && stSelectedNestedTrans.parentId === parentId && stSelectedNestedTrans.idx === nestedIdx ? '3' : '2');
            line.setAttribute('marker-end', 'url(#arrowhead)');
            g.appendChild(line);

            // Clickable hitbox (same as top-level transitions)
            const hitbox = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            hitbox.setAttribute('d', nestedPathD);
            hitbox.setAttribute('fill', 'none');
            hitbox.setAttribute('stroke', 'transparent');
            hitbox.setAttribute('stroke-width', '12');
            hitbox.style.cursor = 'pointer';
            g.appendChild(hitbox);

            if (trans.label) {
                const mid = edgeMidpoint(ports);
                const labelText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                labelText.setAttribute('x', mid.x);
                labelText.setAttribute('y', mid.y - 6);
                labelText.setAttribute('text-anchor', 'middle');
                labelText.setAttribute('fill', 'var(--node-text)');
                labelText.setAttribute('font-size', '11');
                labelText.setAttribute('font-style', 'italic');
                setTextWithLineBreaks(labelText, trans.label);
                g.appendChild(labelText);
            }

            // Render on nodesLayer so nested transitions appear above nested state rects
            nodesLayer.appendChild(g);
        }

        function stRenderNote(note, idx) {
            const state = stFindState(note.stateId);
            const pos = state ? stStatePositions[state.id] : null;
            if (!pos) return;
            const box = state ? stGetStateBox(state) : { x: 0, y: 0, width: 100, height: 40 };

            // Use custom drag position if available, otherwise compute default position
            // Default matches Mermaid renderer: note below the state, offset to the right or left
            let noteX, noteY;
            const customPos = stNotePositions[idx];
            if (customPos) {
                noteX = customPos.x;
                noteY = customPos.y;
            } else {
                if (note.position === 'LeftOf') {
                    noteX = box.x - ST_NOTE_WIDTH / 2;
                    noteY = box.y + box.height + 15;
                } else {
                    noteX = box.x + box.width / 2;
                    noteY = box.y + box.height + 15;
                }
                // Store the computed default position for dragging
                stNotePositions[idx] = { x: noteX, y: noteY };
            }

            const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            g.classList.add('st-note');
            g.setAttribute('data-note-index', idx);
            g.style.cursor = 'move';

            const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', noteX);
            rect.setAttribute('y', noteY);
            rect.setAttribute('width', ST_NOTE_WIDTH);
            rect.setAttribute('height', ST_NOTE_HEIGHT);
            rect.setAttribute('rx', '2');
            rect.setAttribute('fill', 'var(--note-fill, #ffffcc)');
            rect.setAttribute('stroke', 'var(--note-stroke, #cccc00)');
            rect.setAttribute('stroke-width', '1');
            if (stSelectedNoteIdx === idx) {
                rect.setAttribute('stroke', 'var(--node-selected-stroke, #0066ff)');
                rect.setAttribute('stroke-width', '2');
            }
            g.appendChild(rect);

            // Note text
            const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            text.setAttribute('x', noteX + 6);
            text.setAttribute('y', noteY + 16);
            text.setAttribute('fill', 'var(--node-text)');
            text.setAttribute('font-size', '11');
            const lines = (note.text || '').split('\n');
            lines.forEach((line, lineIdx) => {
                const tspan = document.createElementNS('http://www.w3.org/2000/svg', 'tspan');
                tspan.setAttribute('x', noteX + 6);
                tspan.setAttribute('dy', lineIdx === 0 ? '0' : '14');
                tspan.textContent = line;
                text.appendChild(tspan);
            });
            g.appendChild(text);

            // Connector line from note to state using nearest-edge ports (clean vertical/horizontal lines)
            const connLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            const noteBox = { x: noteX, y: noteY, width: ST_NOTE_WIDTH, height: ST_NOTE_HEIGHT };
            const noteCx = noteX + ST_NOTE_WIDTH / 2;
            const noteCy = noteY + ST_NOTE_HEIGHT / 2;
            const stateCx = box.x + box.width / 2;
            const stateCy = box.y + box.height / 2;
            // Note ports: top, bottom, left, right center edges
            const notePorts = [
                { x: noteCx, y: noteY },           // top
                { x: noteCx, y: noteY + ST_NOTE_HEIGHT }, // bottom
                { x: noteX, y: noteCy },            // left
                { x: noteX + ST_NOTE_WIDTH, y: noteCy }   // right
            ];
            // State ports: top, bottom, left, right center edges
            const statePorts = [
                { x: stateCx, y: box.y },           // top
                { x: stateCx, y: box.y + box.height }, // bottom
                { x: box.x, y: stateCy },           // left
                { x: box.x + box.width, y: stateCy }  // right
            ];
            let bestDist = Infinity;
            let bestFrom = notePorts[0], bestTo = statePorts[0];
            notePorts.forEach(np => {
                statePorts.forEach(sp => {
                    const d = Math.hypot(np.x - sp.x, np.y - sp.y);
                    if (d < bestDist) {
                        bestDist = d;
                        bestFrom = np;
                        bestTo = sp;
                    }
                });
            });
            connLine.setAttribute('x1', bestFrom.x);
            connLine.setAttribute('y1', bestFrom.y);
            connLine.setAttribute('x2', bestTo.x);
            connLine.setAttribute('y2', bestTo.y);
            connLine.setAttribute('stroke', 'var(--note-stroke, #cccc00)');
            connLine.setAttribute('stroke-width', '1');
            connLine.setAttribute('stroke-dasharray', '4,4');
            g.appendChild(connLine);

            nodesLayer.appendChild(g);
        }

        // ===== State Diagram Mouse Interaction =====

        svg.addEventListener('mousedown', function(e) {
            if (currentDiagramType !== 'state') return;
            if (!stDiagram) return;

            const stateGroup = e.target.closest('.st-state');
            const transGroup = e.target.closest('.st-transition');
            const pseudoNode = e.target.closest('.st-pseudo-node');
            const gripPoint = e.target.closest('.grip-point');

            // Pseudo-state dragging ([*] circles)
            if (pseudoNode) {
                const pseudoId = pseudoNode.getAttribute('data-pseudo-id');
                if (pseudoId && stStatePositions[pseudoId]) {
                    const svgPt = getSVGPoint(e);
                    stDraggingPseudoId = pseudoId;
                    stSelectedPseudoId = pseudoId;
                    stSelectedStateId = null;
                    stSelectedTransIndex = -1;
                    stSelectedNestedTrans = null;
                    stSelectedNoteIdx = -1;
                    stDragOffsetX = svgPt.x - stStatePositions[pseudoId].x;
                    stDragOffsetY = svgPt.y - stStatePositions[pseudoId].y;
                    propertyPanel.classList.remove('visible');
                    renderStateDiagram();
                    e.preventDefault();
                    e.stopPropagation();
                    return;
                }
            }

            // Note dragging
            const noteGroup = e.target.closest('.st-note');
            if (noteGroup) {
                const noteIdx = parseInt(noteGroup.getAttribute('data-note-index'), 10);
                if (noteIdx >= 0 && stDiagram.notes && noteIdx < stDiagram.notes.length) {
                    const svgPt = getSVGPoint(e);
                    stDraggingNoteIdx = noteIdx;
                    const note = stDiagram.notes[noteIdx];
                    const notePos = stNotePositions[noteIdx];
                    if (notePos) {
                        stDragOffsetX = svgPt.x - notePos.x;
                        stDragOffsetY = svgPt.y - notePos.y;
                    } else {
                        stDragOffsetX = 0;
                        stDragOffsetY = 0;
                    }
                    stSelectedNoteIdx = noteIdx;
                    stShowNoteProperties(noteIdx);
                    renderStateDiagram();
                    e.preventDefault();
                    e.stopPropagation();
                    return;
                }
            }

            // Handle toolbar "Add Transition" pick-source mode
            if (stPickingTransSource && stateGroup) {
                const stateId = stateGroup.getAttribute('data-state-id');
                stPickingTransSource = false;
                document.body.style.cursor = '';
                const stTransBtn = document.getElementById('tb-st-add-transition');
                if (stTransBtn) stTransBtn.classList.remove('active');
                stDrawingTransFrom = stateId;
                const state = stFindState(stateId);
                if (state) {
                    const box = stGetStateBox(state);
                    const cx = box.x + box.width / 2;
                    const cy = box.y + box.height / 2;
                    tempEdge.setAttribute('x1', cx);
                    tempEdge.setAttribute('y1', cy);
                    tempEdge.setAttribute('x2', cx);
                    tempEdge.setAttribute('y2', cy);
                    tempEdge.style.display = '';
                }
                e.preventDefault();
                return;
            }

            if (gripPoint && gripPoint.getAttribute('data-state-id')) {
                // Start drawing a transition
                const stateId = gripPoint.getAttribute('data-state-id');
                stDrawingTransFrom = stateId;
                const state = stFindState(stateId);
                if (state) {
                    const box = stGetStateBox(state);
                    const cx = box.x + box.width / 2;
                    const cy = box.y + box.height / 2;
                    tempEdge.setAttribute('x1', cx);
                    tempEdge.setAttribute('y1', cy);
                    tempEdge.setAttribute('x2', cx);
                    tempEdge.setAttribute('y2', cy);
                    tempEdge.style.display = '';
                }
                e.preventDefault();
                return;
            }

            // Check transitions BEFORE states because transitions inside composite
            // states overlap with the composite's area, so we must prioritize them.
            if (transGroup) {
                // Check if it's a nested transition (has data-parent-id)
                const parentId = transGroup.getAttribute('data-parent-id');
                const nestedIdx = parseInt(transGroup.getAttribute('data-nested-trans-index'), 10);
                if (parentId && !isNaN(nestedIdx)) {
                    stSelectedNestedTrans = { parentId, idx: nestedIdx };
                    stSelectedTransIndex = -1;
                    stSelectedStateId = null;
                    stSelectedPseudoId = null;
                    stShowNestedTransitionProperties(parentId, nestedIdx);
                    renderStateDiagram();
                    e.preventDefault();
                    return;
                }
                const idx = parseInt(transGroup.getAttribute('data-trans-index'), 10);
                stSelectedTransIndex = idx;
                stSelectedNestedTrans = null;
                stSelectedStateId = null;
                stSelectedPseudoId = null;
                stShowTransitionProperties(idx);
                renderStateDiagram();
                e.preventDefault();
                return;
            }

            if (stateGroup) {
                // If we're in transition-drawing mode, don't start dragging — let mouseup handle it
                if (stDrawingTransFrom || stDrawingTransToEnd) {
                    e.preventDefault();
                    return;
                }

                const stateId = stateGroup.getAttribute('data-state-id');
                stSelectedStateId = stateId;
                stSelectedTransIndex = -1;
                stSelectedNestedTrans = null;
                stSelectedPseudoId = null;

                // Start dragging
                const svgPt = getSVGPoint(e);
                const pos = stStatePositions[stateId];
                if (pos) {
                    stDraggingStateId = stateId;
                    stDragOffsetX = svgPt.x - pos.x;
                    stDragOffsetY = svgPt.y - pos.y;
                }

                stShowStateProperties(stateId);
                renderStateDiagram();
                e.preventDefault();
                return;
            }

            // Click on empty space — cancel pick-source mode if active
            if (stPickingTransSource) {
                stPickingTransSource = false;
                document.body.style.cursor = '';
                const stTransBtn = document.getElementById('tb-st-add-transition');
                if (stTransBtn) stTransBtn.classList.remove('active');
            }
            stSelectedStateId = null;
            stSelectedTransIndex = -1;
            stSelectedNestedTrans = null;
            stSelectedPseudoId = null;
            stSelectedNoteIdx = -1;
            propertyPanel.classList.remove('visible');
            renderStateDiagram();
        });

        svg.addEventListener('mousemove', function(e) {
            if (currentDiagramType !== 'state') return;
            if (!stDiagram) return;
            const svgPt = getSVGPoint(e);

            if (stDrawingTransFrom || stDrawingTransToEnd) {
                tempEdge.setAttribute('x2', svgPt.x);
                tempEdge.setAttribute('y2', svgPt.y);
                return;
            }

            if (stDraggingPseudoId) {
                const newX = Math.round((svgPt.x - stDragOffsetX) / SNAP_GRID) * SNAP_GRID;
                const newY = Math.round((svgPt.y - stDragOffsetY) / SNAP_GRID) * SNAP_GRID;
                stStatePositions[stDraggingPseudoId] = {
                    ...stStatePositions[stDraggingPseudoId],
                    x: newX,
                    y: newY
                };
                // Auto-expand/shrink parent composite to fit the moved pseudo node
                const pseudoParentId = stGetPseudoParentId(stDraggingPseudoId);
                if (pseudoParentId) stExpandCompositeChain(pseudoParentId);
                renderStateDiagram();
                return;
            }

            if (stDraggingNoteIdx >= 0) {
                const newX = Math.round((svgPt.x - stDragOffsetX) / SNAP_GRID) * SNAP_GRID;
                const newY = Math.round((svgPt.y - stDragOffsetY) / SNAP_GRID) * SNAP_GRID;
                stNotePositions[stDraggingNoteIdx] = { x: newX, y: newY };
                stManualNotePositions.add(stDraggingNoteIdx);
                renderStateDiagram();
                return;
            }

            if (stDraggingStateId) {
                const newX = Math.round((svgPt.x - stDragOffsetX) / SNAP_GRID) * SNAP_GRID;
                const newY = Math.round((svgPt.y - stDragOffsetY) / SNAP_GRID) * SNAP_GRID;
                const oldPos = stStatePositions[stDraggingStateId];
                const dx = newX - (oldPos ? oldPos.x : 0);
                const dy = newY - (oldPos ? oldPos.y : 0);
                stStatePositions[stDraggingStateId] = {
                    ...stStatePositions[stDraggingStateId],
                    x: newX,
                    y: newY
                };
                // Move nested states and their pseudo-states along with composite parent
                const draggedState = stFindState(stDraggingStateId);
                if (draggedState && draggedState.nestedStates && draggedState.nestedStates.length > 0) {
                    stMoveNestedStates(draggedState.nestedStates, dx, dy, stDraggingStateId);
                }
                // Auto-expand parent composite(s) to fit the moved child
                const parentComp = stFindParentComposite(stDraggingStateId);
                if (parentComp) stExpandCompositeChain(parentComp.id);
                renderStateDiagram();
            }
        });

        svg.addEventListener('mouseup', function(e) {
            if (currentDiagramType !== 'state') return;
            if (!stDiagram) return;

            if (stDrawingTransFrom) {
                const svgPt = getSVGPoint(e);
                // Find the state under the cursor
                const targetGroup = e.target.closest('.st-state');
                if (targetGroup) {
                    const toStateId = targetGroup.getAttribute('data-state-id');
                    if (toStateId && toStateId !== stDrawingTransFrom) {
                        if (stDrawingNestedParent) {
                            postMessage({ type: 'st_nestedTransitionCreated', parentId: stDrawingNestedParent, fromId: stDrawingTransFrom, toId: toStateId });
                        } else {
                            postMessage({ type: 'st_transitionCreated', fromId: stDrawingTransFrom, toId: toStateId });
                        }
                    }
                }
                tempEdge.style.display = 'none';
                stDrawingTransFrom = null;
                stDrawingNestedParent = null;
                document.body.style.cursor = '';
                const stStartBtn = document.getElementById('tb-st-add-start');
                if (stStartBtn) stStartBtn.classList.remove('active');
                return;
            }

            if (stDrawingTransToEnd) {
                // Drawing a transition TO [*] end - user clicks source state
                const sourceGroup = e.target.closest('.st-state');
                if (sourceGroup) {
                    const fromStateId = sourceGroup.getAttribute('data-state-id');
                    if (fromStateId) {
                        if (stDrawingNestedParent) {
                            postMessage({ type: 'st_nestedTransitionCreated', parentId: stDrawingNestedParent, fromId: fromStateId, toId: '[*]' });
                        } else {
                            postMessage({ type: 'st_transitionCreated', fromId: fromStateId, toId: '[*]' });
                        }
                    }
                }
                tempEdge.style.display = 'none';
                stDrawingTransToEnd = false;
                stDrawingNestedParent = null;
                document.body.style.cursor = '';
                const stEndBtn = document.getElementById('tb-st-add-end');
                if (stEndBtn) stEndBtn.classList.remove('active');
                return;
            }

            if (stDraggingPseudoId) {
                // Send all positions to C# so the pseudo node position is persisted
                stSendAllPositionsUpdate();
                stDraggingPseudoId = null;
            }

            if (stDraggingNoteIdx >= 0) {
                // Auto-update note position (LeftOf/RightOf) based on where the note was dragged
                const draggedNote = stDiagram.notes[stDraggingNoteIdx];
                if (draggedNote) {
                    const notePos = stNotePositions[stDraggingNoteIdx];
                    const targetState = stFindState(draggedNote.stateId);
                    if (notePos && targetState) {
                        const box = stGetStateBox(targetState);
                        const noteCenterX = notePos.x + ST_NOTE_WIDTH / 2;
                        const stateCenterX = box.x + box.width / 2;
                        const newPosition = noteCenterX < stateCenterX ? 'LeftOf' : 'RightOf';
                        if (newPosition !== draggedNote.position) {
                            postMessage({ type: 'st_noteEdited', index: stDraggingNoteIdx, position: newPosition });
                            // Update local model immediately so property panel reflects the change
                            draggedNote.position = newPosition;
                            if (stSelectedNoteIdx === stDraggingNoteIdx) {
                                stShowNoteProperties(stDraggingNoteIdx);
                            }
                        }
                    }
                }
                // Send all positions to C# so the note position is persisted
                stSendAllPositionsUpdate();
                stDraggingNoteIdx = -1;
            }

            if (stDraggingStateId) {
                const pos = stStatePositions[stDraggingStateId];
                if (pos) {
                    postMessage({
                        type: 'st_stateMoved',
                        stateId: stDraggingStateId,
                        x: pos.x,
                        y: pos.y
                    });
                }
                // Send ALL state positions to C# so every state gets HasManualPosition=true
                // and @pos comments are written for the entire layout, not just the dragged state
                stSendAllPositionsUpdate();
                stDraggingStateId = null;
            }
        });

        // ===== State Diagram Double-Click to Edit =====

        svg.addEventListener('dblclick', function(e) {
            if (currentDiagramType !== 'state') return;
            if (!stDiagram) return;

            const stateGroup = e.target.closest('.st-state');
            const transGroup = e.target.closest('.st-transition');
            const noteGroup = e.target.closest('.st-note');

            // Check transitions BEFORE states (transitions inside composites overlap
            // with the composite's area, so we must prioritize them)
            if (transGroup) {
                // Check if it's a nested transition
                const parentId = transGroup.getAttribute('data-parent-id');
                const nestedIdx = parseInt(transGroup.getAttribute('data-nested-trans-index'), 10);
                if (parentId && !isNaN(nestedIdx)) {
                    stSelectedNestedTrans = { parentId, idx: nestedIdx };
                    stSelectedTransIndex = -1;
                    stShowNestedTransitionProperties(parentId, nestedIdx);
                    e.preventDefault();
                    return;
                }
                const idx = parseInt(transGroup.getAttribute('data-trans-index'), 10);
                if (idx >= 0 && idx < stDiagram.transitions.length) {
                    stSelectedTransIndex = idx;
                    stSelectedNestedTrans = null;
                    stShowTransitionProperties(idx);
                }
                e.preventDefault();
                return;
            }

            if (stateGroup) {
                const stateId = stateGroup.getAttribute('data-state-id');
                stSelectedStateId = stateId;
                stShowStateProperties(stateId);
                e.preventDefault();
                return;
            }

            if (noteGroup) {
                const idx = parseInt(noteGroup.getAttribute('data-note-index'), 10);
                if (idx >= 0 && stDiagram.notes && idx < stDiagram.notes.length) {
                    stShowNoteProperties(idx);
                }
                e.preventDefault();
                return;
            }
        });

        // ===== State Diagram Property Panel =====

        function stShowStateProperties(stateId) {
            const state = stFindState(stateId);
            if (!state) return;

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">ID</div>
                    <input class="property-input" id="st-state-id" value="${escapeHtml(state.id)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Label</div>
                    <input class="property-input" id="st-state-label" value="${escapeHtml(state.label || '')}" placeholder="State label" />
                </div>
                <div class="property-row">
                    <div class="property-label">Type</div>
                    <select class="property-input" id="st-state-type">
                        <option value="Simple" ${state.type === 'Simple' ? 'selected' : ''}>Simple</option>
                        <option value="Composite" ${state.type === 'Composite' ? 'selected' : ''}>Composite</option>
                        <option value="Fork" ${state.type === 'Fork' ? 'selected' : ''}>Fork</option>
                        <option value="Join" ${state.type === 'Join' ? 'selected' : ''}>Join</option>
                        <option value="Choice" ${state.type === 'Choice' ? 'selected' : ''}>Choice</option>
                    </select>
                </div>
            `;

            document.getElementById('st-state-label').addEventListener('change', function() {
                // Update local model immediately so clicking off/on shows correct value
                const s = stFindState(stateId);
                if (s) s.label = this.value || null;
                postMessage({ type: 'st_stateEdited', stateId: stateId, label: this.value });
            });
            document.getElementById('st-state-type').addEventListener('change', function() {
                // Update local model immediately so clicking off/on shows correct type
                const s = stFindState(stateId);
                if (s) {
                    s.type = this.value;
                    s.isExplicit = true;
                    // Ensure arrays exist when changing to Composite
                    if (this.value === 'Composite') {
                        if (!s.nestedStates) s.nestedStates = [];
                        if (!s.nestedTransitions) s.nestedTransitions = [];
                    }
                }
                postMessage({ type: 'st_stateEdited', stateId: stateId, stateType: this.value });
                renderStateDiagram();
            });

            propPanelTitle.textContent = 'State: ' + (state.label || state.id);
            propertyPanel.classList.add('visible');
        }

        function stShowTransitionProperties(idx) {
            if (!stDiagram || idx < 0 || idx >= stDiagram.transitions.length) return;
            const trans = stDiagram.transitions[idx];
            if (!trans) return;

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">From</div>
                    <input class="property-input" value="${escapeHtml(trans.fromId)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <input class="property-input" value="${escapeHtml(trans.toId)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Label</div>
                    <input class="property-input" id="st-trans-label" value="${escapeHtml(trans.label || '')}" placeholder="Transition label" />
                </div>
            `;

            document.getElementById('st-trans-label').addEventListener('change', function() {
                postMessage({ type: 'st_transitionEdited', index: idx, label: this.value });
            });

            propPanelTitle.textContent = 'Transition';
            propertyPanel.classList.add('visible');
        }

        function stShowNestedTransitionProperties(parentId, nestedIdx) {
            if (!stDiagram) return;
            const parentState = stFindState(parentId);
            if (!parentState || !parentState.nestedTransitions || nestedIdx < 0 || nestedIdx >= parentState.nestedTransitions.length) return;
            const trans = parentState.nestedTransitions[nestedIdx];
            if (!trans) return;

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Parent</div>
                    <input class="property-input" value="${escapeHtml(parentId)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">From</div>
                    <input class="property-input" value="${escapeHtml(trans.fromId)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">To</div>
                    <input class="property-input" value="${escapeHtml(trans.toId)}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Label</div>
                    <input class="property-input" id="st-nested-trans-label" value="${escapeHtml(trans.label || '')}" placeholder="Transition label" />
                </div>
            `;

            document.getElementById('st-nested-trans-label').addEventListener('change', function() {
                postMessage({ type: 'st_nestedTransitionEdited', parentId: parentId, index: nestedIdx, label: this.value });
            });

            propPanelTitle.textContent = 'Nested Transition';
            propertyPanel.classList.add('visible');
        }

        function stShowNoteProperties(idx) {
            if (!stDiagram || idx < 0 || idx >= stDiagram.notes.length) return;
            const note = stDiagram.notes[idx];

            const propContent = propertyPanel.querySelector('.property-panel-body');
            propContent.innerHTML = `
                <div class="property-row">
                    <div class="property-label">Text</div>
                    <textarea class="property-input" id="st-note-text" rows="3" style="resize:vertical">${escapeHtml(note.text || '')}</textarea>
                </div>
                <div class="property-row">
                    <div class="property-label">State</div>
                    <input class="property-input" value="${escapeHtml(note.stateId || '')}" readonly />
                </div>
                <div class="property-row">
                    <div class="property-label">Position</div>
                    <select class="property-input" id="st-note-position">
                        <option value="RightOf" ${note.position === 'RightOf' ? 'selected' : ''}>Right of</option>
                        <option value="LeftOf" ${note.position === 'LeftOf' ? 'selected' : ''}>Left of</option>
                    </select>
                </div>
            `;

            document.getElementById('st-note-text').addEventListener('change', function() {
                postMessage({ type: 'st_noteEdited', index: idx, text: this.value });
            });
            document.getElementById('st-note-position').addEventListener('change', function() {
                postMessage({ type: 'st_noteEdited', index: idx, position: this.value });
            });

            propPanelTitle.textContent = 'Note';
            propertyPanel.classList.add('visible');
        }

        // ===== State Diagram Context Menu =====

        svg.addEventListener('contextmenu', function(e) {
            if (currentDiagramType !== 'state') return;
            if (!stDiagram) return;
            e.preventDefault();
            e.stopPropagation();

            const stateGroup = e.target.closest('.st-state');
            const transGroup = e.target.closest('.st-transition');
            const noteGroup = e.target.closest('.st-note');
            const pseudoNode = e.target.closest('.st-pseudo-node');

            contextMenu.innerHTML = '';

            // Check pseudo nodes first (they may overlap with composites)
            if (pseudoNode) {
                const pseudoId = pseudoNode.getAttribute('data-pseudo-id');
                if (pseudoId) {
                    const pseudoType = pseudoId.includes('_start') ? 'Start [*]' : 'End [*]';
                    addStContextMenuItem('Delete ' + pseudoType, () => {
                        const isStart = pseudoId.includes('_start');
                        const parentId = stGetPseudoParentId(pseudoId);
                        if (parentId) {
                            // Nested pseudo — delete nested transitions involving [*]
                            const parent = stFindState(parentId);
                            if (parent && parent.nestedTransitions) {
                                for (let i = parent.nestedTransitions.length - 1; i >= 0; i--) {
                                    const t = parent.nestedTransitions[i];
                                    if ((isStart && t.fromId === '[*]') || (!isStart && t.toId === '[*]')) {
                                        postMessage({ type: 'st_nestedTransitionDeleted', parentId: parentId, index: i });
                                    }
                                }
                            }
                        } else {
                            // Top-level pseudo — delete top-level transitions involving [*]
                            if (stDiagram && stDiagram.transitions) {
                                for (let i = stDiagram.transitions.length - 1; i >= 0; i--) {
                                    const t = stDiagram.transitions[i];
                                    if ((isStart && t.fromId === '[*]') || (!isStart && t.toId === '[*]')) {
                                        postMessage({ type: 'st_transitionDeleted', index: i });
                                    }
                                }
                            }
                        }
                        delete stStatePositions[pseudoId];
                    });
                }
            // Check transitions BEFORE states (transitions inside composites overlap
            // with the composite's area, so we must prioritize them)
            } else if (transGroup) {
                // Check if it's a nested transition
                const parentId = transGroup.getAttribute('data-parent-id');
                const nestedIdx = parseInt(transGroup.getAttribute('data-nested-trans-index'), 10);
                if (parentId && !isNaN(nestedIdx)) {
                    stSelectedNestedTrans = { parentId, idx: nestedIdx };
                    stSelectedTransIndex = -1;

                    addStContextMenuItem('Edit Label', () => {
                        stShowNestedTransitionProperties(parentId, nestedIdx);
                    });
                    // Insert State on nested edge - splits nested transition into two
                    addStContextMenuItem('Insert State', () => {
                        const parentState = stFindState(parentId);
                        if (parentState && parentState.nestedTransitions && parentState.nestedTransitions[nestedIdx]) {
                            const trans = parentState.nestedTransitions[nestedIdx];
                            const newId = stNextUniqueId('State');
                            // Position the new state at the midpoint of the transition
                            const fromState = stFindState(trans.fromId);
                            const toState = stFindState(trans.toId);
                            const fromBox = fromState ? stGetStateBox(fromState) : null;
                            const toBox = toState ? stGetStateBox(toState) : null;
                            if (fromBox && toBox) {
                                const midX = (fromBox.x + fromBox.width / 2 + toBox.x + toBox.width / 2) / 2 - ST_BOX_MIN_WIDTH / 2;
                                const midY = (fromBox.y + fromBox.height / 2 + toBox.y + toBox.height / 2) / 2 - ST_BOX_HEIGHT / 2;
                                stStatePositions[newId] = {
                                    x: Math.round(midX / SNAP_GRID) * SNAP_GRID,
                                    y: Math.round(midY / SNAP_GRID) * SNAP_GRID,
                                    width: ST_BOX_MIN_WIDTH,
                                    height: ST_BOX_HEIGHT
                                };
                            }
                            postMessage({ type: 'st_insertStateOnNestedEdge', parentId: parentId, index: nestedIdx, stateId: newId, label: newId });
                            stExpandCompositeChain(parentId);
                        }
                    });
                    // Insert [*] Start/End on nested edge
                    addStContextMenuItem('Insert [*] (Start/End)', () => {
                        const parentState = stFindState(parentId);
                        if (parentState && parentState.nestedTransitions && parentState.nestedTransitions[nestedIdx]) {
                            const trans = parentState.nestedTransitions[nestedIdx];
                            const fromState = stFindState(trans.fromId);
                            const toState = stFindState(trans.toId);
                            const fromBox = fromState ? stGetStateBox(fromState) : null;
                            const toBox = toState ? stGetStateBox(toState) : null;
                            let midX = ST_LEFT_MARGIN, midY = ST_TOP_MARGIN;
                            if (fromBox && toBox) {
                                midX = (fromBox.x + fromBox.width / 2 + toBox.x + toBox.width / 2) / 2;
                                midY = (fromBox.y + fromBox.height / 2 + toBox.y + toBox.height / 2) / 2;
                            }
                            const endKey = '[*]_end_nested_' + parentId + '_' + nestedIdx;
                            const startKey = '[*]_start_nested_' + parentId + '_' + nestedIdx;
                            stStatePositions[endKey] = {
                                x: Math.round(midX / SNAP_GRID) * SNAP_GRID - 10,
                                y: Math.round((midY - 20) / SNAP_GRID) * SNAP_GRID,
                                width: 20, height: 20
                            };
                            stStatePositions[startKey] = {
                                x: Math.round(midX / SNAP_GRID) * SNAP_GRID - 8,
                                y: Math.round((midY + 10) / SNAP_GRID) * SNAP_GRID,
                                width: 16, height: 16
                            };
                            postMessage({ type: 'st_insertPseudoOnNestedEdge', parentId: parentId, index: nestedIdx });
                            stExpandCompositeChain(parentId);
                        }
                    });
                    // Insert special states (Fork/Join/Choice) on nested edge
                    ['Fork', 'Join', 'Choice'].forEach(sType => {
                        addStContextMenuItem('Insert ' + sType, () => {
                            const parentState = stFindState(parentId);
                            if (parentState && parentState.nestedTransitions && parentState.nestedTransitions[nestedIdx]) {
                                const trans = parentState.nestedTransitions[nestedIdx];
                                const newId = stNextUniqueId(sType);
                                const fromState = stFindState(trans.fromId);
                                const toState = stFindState(trans.toId);
                                const fromBox = fromState ? stGetStateBox(fromState) : null;
                                const toBox = toState ? stGetStateBox(toState) : null;
                                if (fromBox && toBox) {
                                    const midX = (fromBox.x + fromBox.width / 2 + toBox.x + toBox.width / 2) / 2;
                                    const midY = (fromBox.y + fromBox.height / 2 + toBox.y + toBox.height / 2) / 2;
                                    stStatePositions[newId] = {
                                        x: Math.round(midX / SNAP_GRID) * SNAP_GRID - 20,
                                        y: Math.round(midY / SNAP_GRID) * SNAP_GRID - 5,
                                        width: 40, height: 10
                                    };
                                }
                                postMessage({ type: 'st_insertSpecialStateOnNestedEdge', parentId: parentId, index: nestedIdx, stateId: newId, stateType: sType });
                                stExpandCompositeChain(parentId);
                            }
                        });
                    });
                    addStContextMenuSeparator();
                    addStContextMenuItem('Delete Transition', () => {
                        postMessage({ type: 'st_nestedTransitionDeleted', parentId: parentId, index: nestedIdx });
                        stSelectedNestedTrans = null;
                    });
                } else {
                const idx = parseInt(transGroup.getAttribute('data-trans-index'), 10);
                stSelectedTransIndex = idx;
                stSelectedNestedTrans = null;

                addStContextMenuItem('Edit Label', () => {
                    stShowTransitionProperties(idx);
                });
                // Insert State on Edge - splits transition into two
                addStContextMenuItem('Insert State', () => {
                    const trans = stDiagram.transitions[idx];
                    if (trans) {
                        const newId = stNextUniqueId('State');
                        // Position the new state at the midpoint of the transition
                        const fromState = stFindState(trans.fromId);
                        const toState = stFindState(trans.toId);
                        const fromBox = fromState ? stGetStateBox(fromState) : null;
                        const toBox = toState ? stGetStateBox(toState) : null;
                        if (fromBox && toBox) {
                            const midX = (fromBox.x + fromBox.width / 2 + toBox.x + toBox.width / 2) / 2 - ST_BOX_MIN_WIDTH / 2;
                            const midY = (fromBox.y + fromBox.height / 2 + toBox.y + toBox.height / 2) / 2 - ST_BOX_HEIGHT / 2;
                            stStatePositions[newId] = {
                                x: Math.round(midX / SNAP_GRID) * SNAP_GRID,
                                y: Math.round(midY / SNAP_GRID) * SNAP_GRID,
                                width: ST_BOX_MIN_WIDTH,
                                height: ST_BOX_HEIGHT
                            };
                        }
                        postMessage({ type: 'st_insertStateOnEdge', index: idx, stateId: newId, label: newId });
                    }
                });
                // Insert [*] Start/End on Edge - replaces A-->B with A-->[*] and [*]-->B
                addStContextMenuItem('Insert [*] (Start/End)', () => {
                    const trans = stDiagram.transitions[idx];
                    if (trans) {
                        // Position the pseudo circles at the midpoint of the transition
                        const fromState = stFindState(trans.fromId);
                        const toState = stFindState(trans.toId);
                        const fromBox = fromState ? stGetStateBox(fromState) : null;
                        const toBox = toState ? stGetStateBox(toState) : null;
                        let midX = ST_LEFT_MARGIN, midY = ST_TOP_MARGIN;
                        if (fromBox && toBox) {
                            midX = (fromBox.x + fromBox.width / 2 + toBox.x + toBox.width / 2) / 2;
                            midY = (fromBox.y + fromBox.height / 2 + toBox.y + toBox.height / 2) / 2;
                        }
                        // Place end pseudo (top) and start pseudo (bottom) near midpoint
                        const endKey = '[*]_end_' + idx;
                        const startKey = '[*]_start_' + idx;
                        stStatePositions[endKey] = {
                            x: Math.round(midX / SNAP_GRID) * SNAP_GRID - 10,
                            y: Math.round((midY - 20) / SNAP_GRID) * SNAP_GRID,
                            width: 20, height: 20
                        };
                        stStatePositions[startKey] = {
                            x: Math.round(midX / SNAP_GRID) * SNAP_GRID - 8,
                            y: Math.round((midY + 10) / SNAP_GRID) * SNAP_GRID,
                            width: 16, height: 16
                        };
                        postMessage({ type: 'st_insertPseudoOnEdge', index: idx });
                    }
                });
                // Insert special states (Fork/Join/Choice) on Edge
                ['Fork', 'Join', 'Choice'].forEach(sType => {
                    addStContextMenuItem('Insert ' + sType, () => {
                        const trans = stDiagram.transitions[idx];
                        if (trans) {
                            const newId = stNextUniqueId(sType);
                            const fromState = stFindState(trans.fromId);
                            const toState = stFindState(trans.toId);
                            const fromBox = fromState ? stGetStateBox(fromState) : null;
                            const toBox = toState ? stGetStateBox(toState) : null;
                            if (fromBox && toBox) {
                                const midX = (fromBox.x + fromBox.width / 2 + toBox.x + toBox.width / 2) / 2;
                                const midY = (fromBox.y + fromBox.height / 2 + toBox.y + toBox.height / 2) / 2;
                                stStatePositions[newId] = {
                                    x: Math.round(midX / SNAP_GRID) * SNAP_GRID - 20,
                                    y: Math.round(midY / SNAP_GRID) * SNAP_GRID - 5,
                                    width: 40, height: 10
                                };
                            }
                            postMessage({ type: 'st_insertSpecialStateOnEdge', index: idx, stateId: newId, stateType: sType });
                        }
                    });
                });
                addStContextMenuSeparator();
                addStContextMenuItem('Delete Transition', () => {
                    postMessage({ type: 'st_transitionDeleted', index: idx });
                });
                }
            } else if (stateGroup) {
                const stateId = stateGroup.getAttribute('data-state-id');
                stSelectedStateId = stateId;

                addStContextMenuItem('Edit State', () => {
                    stShowStateProperties(stateId);
                });
                addStContextMenuItem('Add Note', () => {
                    postMessage({ type: 'st_noteCreated', stateId: stateId, text: 'Note', position: 'RightOf' });
                });
                // Show nested element options for composite states
                const clickedState = stFindState(stateId);
                if (clickedState && (clickedState.type === 'Composite' || (clickedState.nestedStates && clickedState.nestedStates.length > 0))) {
                    // Helper: compute the next Y position inside this composite
                    function nestedNextY() {
                        const parentPos = stStatePositions[stateId];
                        if (!parentPos) return 200;
                        let maxNY = (parentPos.y || 0) + 35 + ST_COMPOSITE_PADDING;
                        if (clickedState.nestedStates) {
                            clickedState.nestedStates.forEach(ns => {
                                const ep = stStatePositions[ns.id];
                                if (ep) {
                                    const bottom = (ep.y || 0) + (ep.height || ST_BOX_HEIGHT);
                                    if (bottom > maxNY) maxNY = bottom;
                                }
                            });
                        }
                        return maxNY + 20;
                    }
                    function nestedBaseX() {
                        const parentPos = stStatePositions[stateId];
                        return parentPos ? (parentPos.x || 0) + ST_COMPOSITE_PADDING : 100;
                    }

                    addStContextMenuItem('Add Nested State', () => {
                        const newId = stNextUniqueId('State');
                        stStatePositions[newId] = {
                            x: nestedBaseX(), y: nestedNextY(),
                            width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT
                        };
                        if (!clickedState.nestedStates) clickedState.nestedStates = [];
                        clickedState.nestedStates.push({ id: newId, label: newId, type: 'Simple', nestedStates: [], nestedTransitions: [] });
                        postMessage({ type: 'st_nestedStateCreated', parentId: stateId, stateId: newId, label: newId });
                        stExpandCompositeChain(stateId);
                        renderStateDiagram();
                    });
                    addStContextMenuItem('Add Nested Fork/Join', () => {
                        const newId = stNextUniqueId('ForkJoin');
                        stStatePositions[newId] = {
                            x: nestedBaseX(), y: nestedNextY(),
                            width: 80, height: 6
                        };
                        if (!clickedState.nestedStates) clickedState.nestedStates = [];
                        clickedState.nestedStates.push({ id: newId, label: null, type: 'Fork', nestedStates: [], nestedTransitions: [] });
                        postMessage({ type: 'st_nestedStateCreated', parentId: stateId, stateId: newId, label: null, stateType: 'Fork' });
                        stExpandCompositeChain(stateId);
                        renderStateDiagram();
                    });
                    addStContextMenuItem('Add Nested Choice', () => {
                        const newId = stNextUniqueId('Choice');
                        stStatePositions[newId] = {
                            x: nestedBaseX(), y: nestedNextY(),
                            width: 30, height: 30
                        };
                        if (!clickedState.nestedStates) clickedState.nestedStates = [];
                        clickedState.nestedStates.push({ id: newId, label: null, type: 'Choice', nestedStates: [], nestedTransitions: [] });
                        postMessage({ type: 'st_nestedStateCreated', parentId: stateId, stateId: newId, label: null, stateType: 'Choice' });
                        stExpandCompositeChain(stateId);
                        renderStateDiagram();
                    });
                    addStContextMenuSeparator();
                    addStContextMenuItem('Add Nested Start \u2192 (select target)', () => {
                        // Start drawing a nested [*] start transition — source is [*] inside this composite
                        stDrawingTransFrom = '[*]';
                        stDrawingNestedParent = stateId;
                        const parentPos = stStatePositions[stateId];
                        const cx = parentPos ? parentPos.x + 30 : 100;
                        const cy = parentPos ? parentPos.y + 50 : 100;
                        tempEdge.setAttribute('x1', cx);
                        tempEdge.setAttribute('y1', cy);
                        tempEdge.setAttribute('x2', cx);
                        tempEdge.setAttribute('y2', cy);
                        tempEdge.style.display = '';
                    });
                    addStContextMenuItem('Add Nested \u2192 End (select source)', () => {
                        // Start drawing a nested [*] end transition — target is [*] inside this composite
                        stDrawingTransToEnd = true;
                        stDrawingNestedParent = stateId;
                        const parentPos = stStatePositions[stateId];
                        const cx = parentPos ? parentPos.x + 30 : 100;
                        const cy = parentPos ? parentPos.y + 50 : 100;
                        tempEdge.setAttribute('x1', cx);
                        tempEdge.setAttribute('y1', cy);
                        tempEdge.setAttribute('x2', cx);
                        tempEdge.setAttribute('y2', cy);
                        tempEdge.style.display = '';
                    });
                }
                addStContextMenuSeparator();
                addStContextMenuItem('Delete State', () => {
                    postMessage({ type: 'st_stateDeleted', stateId: stateId });
                });
            } else if (noteGroup) {
                const idx = parseInt(noteGroup.getAttribute('data-note-index'), 10);
                addStContextMenuItem('Edit Note', () => {
                    stShowNoteProperties(idx);
                });
                addStContextMenuSeparator();
                addStContextMenuItem('Delete Note', () => {
                    postMessage({ type: 'st_noteDeleted', index: idx });
                });
            } else {
                // Empty space
                addStContextMenuItem('Add State', () => {
                    const svgPt = getSVGPoint(e);
                    const newId = stNextUniqueId('State');
                    stStatePositions[newId] = {
                        x: Math.round(svgPt.x / SNAP_GRID) * SNAP_GRID,
                        y: Math.round(svgPt.y / SNAP_GRID) * SNAP_GRID
                    };
                    postMessage({ type: 'st_stateCreated', stateId: newId, label: newId, stateType: 'Simple' });
                });
                addStContextMenuItem('Add Fork/Join', () => {
                    const svgPt = getSVGPoint(e);
                    const newId = stNextUniqueId('ForkJoin');
                    stStatePositions[newId] = {
                        x: Math.round(svgPt.x / SNAP_GRID) * SNAP_GRID,
                        y: Math.round(svgPt.y / SNAP_GRID) * SNAP_GRID
                    };
                    postMessage({ type: 'st_stateCreated', stateId: newId, stateType: 'Fork' });
                });
                addStContextMenuItem('Add Choice', () => {
                    const svgPt = getSVGPoint(e);
                    const newId = stNextUniqueId('Choice');
                    stStatePositions[newId] = {
                        x: Math.round(svgPt.x / SNAP_GRID) * SNAP_GRID,
                        y: Math.round(svgPt.y / SNAP_GRID) * SNAP_GRID
                    };
                    postMessage({ type: 'st_stateCreated', stateId: newId, stateType: 'Choice' });
                });
                addStContextMenuSeparator();
                // [*] pseudo-state transitions
                addStContextMenuItem('Add Start \u2192 (select target)', () => {
                    // Enter transition drawing mode from [*] start
                    stDrawingTransFrom = '[*]';
                    const svgPt = getSVGPoint(e);
                    tempEdge.setAttribute('x1', svgPt.x);
                    tempEdge.setAttribute('y1', svgPt.y);
                    tempEdge.setAttribute('x2', svgPt.x);
                    tempEdge.setAttribute('y2', svgPt.y);
                    tempEdge.style.display = '';
                });
                addStContextMenuItem('Add \u2192 End (select source)', () => {
                    // Enter transition drawing mode to [*] end
                    stDrawingTransToEnd = true;
                    const svgPt = getSVGPoint(e);
                    tempEdge.setAttribute('x1', svgPt.x);
                    tempEdge.setAttribute('y1', svgPt.y);
                    tempEdge.setAttribute('x2', svgPt.x);
                    tempEdge.setAttribute('y2', svgPt.y);
                    tempEdge.style.display = '';
                });
            }

            // Copy/Paste for state diagrams
            if (stSelectedStateId) {
                addStContextMenuSeparator();
                addStContextMenuItem('\u{1F4CB} Copy', () => { copySelected(); });
            }
            if (clipboard && clipboard.diagramType === 'state') {
                addStContextMenuItem('\u{1F4CB} Paste', () => { pasteClipboard(0, 0); });
            }

            positionContextMenu(contextMenu, e.clientX, e.clientY);
            renderStateDiagram();
        });

        function addStContextMenuItem(label, onClick) {
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

        function addStContextMenuSeparator() {
            const sep = document.createElement('div');
            sep.classList.add('context-menu-separator');
            contextMenu.appendChild(sep);
        }

        // Keyboard shortcuts for state diagram elements
        document.addEventListener('keydown', function(e) {
            if (currentDiagramType !== 'state') return;
            if (!stDiagram) return;
            if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'SELECT')) return;

            if (e.key === 'Delete' || e.key === 'Backspace') {
                if (stSelectedPseudoId) {
                    // Delete pseudo node and its associated transitions
                    const pseudoId = stSelectedPseudoId;
                    const isStart = pseudoId.includes('_start');
                    const parentId = stGetPseudoParentId(pseudoId);
                    if (parentId) {
                        const parent = stFindState(parentId);
                        if (parent && parent.nestedTransitions) {
                            for (let i = parent.nestedTransitions.length - 1; i >= 0; i--) {
                                const t = parent.nestedTransitions[i];
                                if ((isStart && t.fromId === '[*]') || (!isStart && t.toId === '[*]')) {
                                    postMessage({ type: 'st_nestedTransitionDeleted', parentId: parentId, index: i });
                                }
                            }
                        }
                    } else {
                        if (stDiagram && stDiagram.transitions) {
                            for (let i = stDiagram.transitions.length - 1; i >= 0; i--) {
                                const t = stDiagram.transitions[i];
                                if ((isStart && t.fromId === '[*]') || (!isStart && t.toId === '[*]')) {
                                    postMessage({ type: 'st_transitionDeleted', index: i });
                                }
                            }
                        }
                    }
                    delete stStatePositions[pseudoId];
                    stSelectedPseudoId = null;
                    e.preventDefault();
                } else if (stSelectedStateId) {
                    postMessage({ type: 'st_stateDeleted', stateId: stSelectedStateId });
                    stSelectedStateId = null;
                    e.preventDefault();
                } else if (stSelectedNestedTrans) {
                    postMessage({ type: 'st_nestedTransitionDeleted', parentId: stSelectedNestedTrans.parentId, index: stSelectedNestedTrans.idx });
                    stSelectedNestedTrans = null;
                    e.preventDefault();
                } else if (stSelectedTransIndex >= 0) {
                    postMessage({ type: 'st_transitionDeleted', index: stSelectedTransIndex });
                    stSelectedTransIndex = -1;
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

        // ===== State diagram toolbar buttons =====
        // Helper: get SVG coordinates at the center of the current viewport
        function stGetViewportCenterSVG() {
            const rect = canvasContainer.getBoundingClientRect();
            const cx = rect.width / 2;
            const cy = rect.height / 2;
            const ctm = svg.getScreenCTM();
            if (!ctm) return { x: 200, y: 200 };
            return {
                x: Math.round(((cx - ctm.e) / ctm.a) / SNAP_GRID) * SNAP_GRID,
                y: Math.round(((cy - ctm.f) / ctm.d) / SNAP_GRID) * SNAP_GRID
            };
        }

        document.getElementById('tb-st-add-state').addEventListener('click', function() {
            if (!stDiagram) return;
            const newId = stNextUniqueId('State');
            const center = stGetViewportCenterSVG();
            stStatePositions[newId] = { x: center.x, y: center.y, width: ST_BOX_MIN_WIDTH, height: ST_BOX_HEIGHT };
            postMessage({ type: 'st_stateCreated', stateId: newId, label: newId, stateType: 'Simple' });
        });

        document.getElementById('tb-st-add-fork').addEventListener('click', function() {
            if (!stDiagram) return;
            const newId = stNextUniqueId('ForkJoin');
            const center = stGetViewportCenterSVG();
            stStatePositions[newId] = { x: center.x, y: center.y };
            postMessage({ type: 'st_stateCreated', stateId: newId, stateType: 'Fork' });
        });

        document.getElementById('tb-st-add-choice').addEventListener('click', function() {
            if (!stDiagram) return;
            const newId = stNextUniqueId('Choice');
            const center = stGetViewportCenterSVG();
            stStatePositions[newId] = { x: center.x, y: center.y };
            postMessage({ type: 'st_stateCreated', stateId: newId, stateType: 'Choice' });
        });

        // Start → (select target): enter draw mode from [*] start
        document.getElementById('tb-st-add-start').addEventListener('click', function() {
            if (!stDiagram || stDiagram.states.length < 1) return;
            stDrawingTransFrom = '[*]';
            const center = stGetViewportCenterSVG();
            tempEdge.setAttribute('x1', center.x);
            tempEdge.setAttribute('y1', center.y);
            tempEdge.setAttribute('x2', center.x);
            tempEdge.setAttribute('y2', center.y);
            tempEdge.style.display = '';
            document.body.style.cursor = 'crosshair';
            this.classList.add('active');
        });

        // → End (select source): enter draw mode to [*] end
        document.getElementById('tb-st-add-end').addEventListener('click', function() {
            if (!stDiagram || stDiagram.states.length < 1) return;
            stDrawingTransToEnd = true;
            const center = stGetViewportCenterSVG();
            tempEdge.setAttribute('x1', center.x);
            tempEdge.setAttribute('y1', center.y);
            tempEdge.setAttribute('x2', center.x);
            tempEdge.setAttribute('y2', center.y);
            tempEdge.style.display = '';
            document.body.style.cursor = 'crosshair';
            this.classList.add('active');
        });

        document.getElementById('tb-st-add-transition').addEventListener('click', function() {
            if (!stDiagram || stDiagram.states.length < 2) return;
            // Enter pick-source mode: next click on a state picks the source, then drag to target
            stPickingTransSource = true;
            document.body.style.cursor = 'crosshair';
            this.classList.add('active');
        });

        document.getElementById('tb-st-auto-layout').addEventListener('click', function() {
            stNotePositions = {}; // Clear custom note positions so they re-compute from new layout
            stManualNotePositions = new Set(); // Clear manual tracking so auto-layout notes aren't persisted
            stDagreAutoLayout();
            renderStateDiagram();
            const positions = [];
            const allStates = stGetAllFlatStates();
            allStates.forEach(state => {
                const pos = stStatePositions[state.id];
                if (pos) {
                    positions.push({ stateId: state.id, x: pos.x, y: pos.y, width: pos.width || 0, height: pos.height || 0 });
                }
            });
            // Include pseudo-state positions in auto-layout result
            Object.keys(stStatePositions).forEach(key => {
                if (key.startsWith('[*]_')) {
                    const pos = stStatePositions[key];
                    positions.push({ stateId: key, x: pos.x, y: pos.y, width: pos.width || 0, height: pos.height || 0 });
                }
            });
            postMessage({ type: 'st_autoLayoutComplete', positions: positions });
        });

