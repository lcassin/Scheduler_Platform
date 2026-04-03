// ============================================================
// VisualEditor.GitGraph.js - GitGraph Diagram Visual Editor
// Git commit history with branches, merges, tags, cherry-picks
// ============================================================

// ========== GitGraph State ==========
let gitGraphModel = null;
let ggSelectedCommand = null; // index or null
let ggClipboard = null; // { data: {...} } or null

// ========== GitGraph Load/Restore ==========

window.loadGitGraphDiagram = function(jsonStr) {
    try {
        currentDiagramType = 'gitGraph';
        gitGraphModel = JSON.parse(jsonStr);
        ggSelectedCommand = null;
        editorCanvasZoom = 1;
        updateToolbarForDiagramType();
        renderGitGraphDiagram();
    } catch (e) {
        console.error('Failed to load gitGraph diagram:', e);
    }
};

window.restoreGitGraphDiagram = function(jsonStr) {
    try {
        gitGraphModel = JSON.parse(jsonStr);
        renderGitGraphDiagram();
    } catch (e) {
        console.error('Failed to restore gitGraph diagram:', e);
    }
};

window.refreshGitGraphDiagram = function(jsonStr) {
    try {
        gitGraphModel = JSON.parse(jsonStr);
        renderGitGraphDiagram();
    } catch (e) {
        console.error('Failed to refresh gitGraph diagram:', e);
    }
};

// ========== GitGraph Rendering ==========

function renderGitGraphDiagram() {
    const canvas = document.getElementById('editorCanvas');
    if (!canvas || !gitGraphModel) return;

    const diagramSvg = document.getElementById('diagram-svg');
    if (diagramSvg) diagramSvg.style.display = 'none';
    canvas.style.display = 'block';
    canvas.innerHTML = '';

    // Theme colors
    const cs = getComputedStyle(document.body);
    const cv = (v) => cs.getPropertyValue(v).trim();
    const bgColor = cv('--bg-color') || '#1E1E1E';
    const textColor = cv('--node-text') || '#D4D4D4';
    const borderColor = cv('--node-stroke') || '#3E3E42';
    const isLight = document.body.classList.contains('theme-light');
    const isTwilight = document.body.classList.contains('theme-twilight');

    // Branch colors (rotating palette)
    const branchColors = isLight
        ? ['#2196f3', '#4caf50', '#ff9800', '#9c27b0', '#f44336', '#009688', '#ff5722', '#3f51b5']
        : isTwilight
        ? ['#4A90D9', '#5A9E6F', '#D19A66', '#B48EAD', '#E06C75', '#56B6C2', '#C678DD', '#61AFEF']
        : ['#89b4fa', '#a6e3a1', '#f9e2af', '#cba6f7', '#f38ba8', '#94e2d5', '#fab387', '#74c7ec'];

    // Commit type colors
    const typeColors = {
        NORMAL: null, // uses branch color
        REVERSE: isLight ? '#f44336' : isTwilight ? '#E06C75' : '#f38ba8',
        HIGHLIGHT: isLight ? '#ff9800' : isTwilight ? '#D19A66' : '#f9e2af'
    };

    // Layout constants
    const padding = 30;
    const rowHeight = 50;
    const branchLaneWidth = 40;
    const commitRadius = 10;
    const labelLeftOffset = 20;

    // Build branch state by replaying commands
    const branches = {}; // name -> { colorIdx, lane }
    let currentBranch = 'main';
    let nextColorIdx = 0;
    let nextLane = 0;
    branches['main'] = { colorIdx: 0, lane: 0 };
    nextColorIdx = 1;
    nextLane = 1;

    // First pass: assign lanes to branches
    const commands = gitGraphModel.commands || [];
    commands.forEach(cmd => {
        if (cmd.type === 'branch' && cmd.branchName && !branches[cmd.branchName]) {
            branches[cmd.branchName] = { colorIdx: nextColorIdx % branchColors.length, lane: nextLane };
            nextColorIdx++;
            nextLane++;
        }
    });

    const totalLanes = Math.max(nextLane, 1);
    const graphWidth = totalLanes * branchLaneWidth;
    const labelStartX = padding + graphWidth + labelLeftOffset;
    const totalWidth = Math.max(700, labelStartX + 400);
    const titleHeight = gitGraphModel.title ? 40 : 0;
    const totalHeight = padding + titleHeight + commands.length * rowHeight + 80;

    // Create SVG
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', totalWidth);
    svg.setAttribute('height', totalHeight);
    svg.setAttribute('viewBox', `0 0 ${totalWidth} ${totalHeight}`);
    svg.style.display = 'block';

    // Background
    const bg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    bg.setAttribute('width', totalWidth);
    bg.setAttribute('height', totalHeight);
    bg.setAttribute('fill', bgColor);
    bg.setAttribute('rx', '8');
    svg.appendChild(bg);

    bg.addEventListener('click', () => {
        ggSelectedCommand = null;
        renderGitGraphDiagram();
    });

    let currentY = padding;

    // Title
    if (gitGraphModel.title) {
        const titleText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        titleText.setAttribute('x', totalWidth / 2);
        titleText.setAttribute('y', currentY + 20);
        titleText.setAttribute('text-anchor', 'middle');
        titleText.setAttribute('fill', textColor);
        titleText.setAttribute('font-size', '18');
        titleText.setAttribute('font-weight', 'bold');
        titleText.textContent = gitGraphModel.title;
        titleText.style.cursor = 'pointer';
        titleText.addEventListener('dblclick', (e) => { e.stopPropagation(); editGitGraphSettings(); });
        svg.appendChild(titleText);
        currentY += titleHeight;
    }

    // Column headers
    const headers = [
        { label: 'Graph', x: padding + graphWidth / 2, anchor: 'middle' },
        { label: 'Command', x: labelStartX, anchor: 'start' }
    ];
    headers.forEach(h => {
        const hText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        hText.setAttribute('x', h.x);
        hText.setAttribute('y', currentY + 14);
        hText.setAttribute('text-anchor', h.anchor);
        hText.setAttribute('fill', textColor);
        hText.setAttribute('font-size', '11');
        hText.setAttribute('font-weight', 'bold');
        hText.setAttribute('opacity', '0.5');
        hText.textContent = h.label;
        svg.appendChild(hText);
    });
    currentY += 22;

    // Header separator
    const sep = document.createElementNS('http://www.w3.org/2000/svg', 'line');
    sep.setAttribute('x1', padding);
    sep.setAttribute('y1', currentY);
    sep.setAttribute('x2', totalWidth - padding);
    sep.setAttribute('y2', currentY);
    sep.setAttribute('stroke', borderColor);
    sep.setAttribute('stroke-opacity', '0.3');
    sep.setAttribute('stroke-width', '1');
    svg.appendChild(sep);
    currentY += 4;

    // Second pass: render commands with graph visualization
    let activeBranch = 'main';
    const commitPositions = []; // { x, y, branch, index }

    commands.forEach((cmd, idx) => {
        const rowY = currentY + idx * rowHeight;
        const rowCenterY = rowY + rowHeight / 2;
        const isSelected = ggSelectedCommand === idx;

        // Row background
        const rowBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rowBg.setAttribute('x', padding);
        rowBg.setAttribute('y', rowY);
        rowBg.setAttribute('width', totalWidth - padding * 2);
        rowBg.setAttribute('height', rowHeight);
        rowBg.setAttribute('fill', isSelected ? (isLight ? 'rgba(33,150,243,0.12)' : 'rgba(137,180,250,0.12)') : 'transparent');
        rowBg.setAttribute('rx', '4');
        rowBg.style.cursor = 'pointer';
        rowBg.setAttribute('data-gg-index', String(idx));
        rowBg.addEventListener('click', (e) => { e.stopPropagation(); selectGitGraphCommand(idx); });
        rowBg.addEventListener('dblclick', (e) => { e.stopPropagation(); editGitGraphCommand(idx); });
        rowBg.addEventListener('mouseenter', () => {
            if (!isSelected) rowBg.setAttribute('fill', isLight ? 'rgba(0,0,0,0.03)' : 'rgba(255,255,255,0.03)');
        });
        rowBg.addEventListener('mouseleave', () => {
            if (!isSelected) rowBg.setAttribute('fill', 'transparent');
        });
        svg.appendChild(rowBg);

        // Get branch info for this command
        let branchInfo = branches[activeBranch] || branches['main'] || { colorIdx: 0, lane: 0 };
        let branchColor = branchColors[branchInfo.colorIdx % branchColors.length];

        if (cmd.type === 'branch') {
            // Branch command - show on parent branch, new branch starts after
            const newBranchInfo = branches[cmd.branchName];
            if (newBranchInfo) {
                const newColor = branchColors[newBranchInfo.colorIdx % branchColors.length];
                const fromX = padding + branchInfo.lane * branchLaneWidth + branchLaneWidth / 2;
                const toX = padding + newBranchInfo.lane * branchLaneWidth + branchLaneWidth / 2;

                // Draw branch creation line
                const brLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                brLine.setAttribute('x1', fromX);
                brLine.setAttribute('y1', rowCenterY);
                brLine.setAttribute('x2', toX);
                brLine.setAttribute('y2', rowCenterY);
                brLine.setAttribute('stroke', newColor);
                brLine.setAttribute('stroke-width', '2');
                brLine.setAttribute('stroke-dasharray', '4,3');
                brLine.style.pointerEvents = 'none';
                svg.appendChild(brLine);

                // Branch dot at the new lane
                const dot = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                dot.setAttribute('cx', toX);
                dot.setAttribute('cy', rowCenterY);
                dot.setAttribute('r', 6);
                dot.setAttribute('fill', newColor);
                dot.setAttribute('stroke', isLight ? '#fff' : '#1E1E1E');
                dot.setAttribute('stroke-width', '2');
                dot.style.pointerEvents = 'none';
                svg.appendChild(dot);
            }
        } else if (cmd.type === 'checkout') {
            if (cmd.branchName && branches[cmd.branchName]) {
                const targetInfo = branches[cmd.branchName];
                const fromX = padding + branchInfo.lane * branchLaneWidth + branchLaneWidth / 2;
                const toX = padding + targetInfo.lane * branchLaneWidth + branchLaneWidth / 2;

                if (fromX !== toX) {
                    const chLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    chLine.setAttribute('x1', fromX);
                    chLine.setAttribute('y1', rowCenterY);
                    chLine.setAttribute('x2', toX);
                    chLine.setAttribute('y2', rowCenterY);
                    chLine.setAttribute('stroke', branchColors[targetInfo.colorIdx % branchColors.length]);
                    chLine.setAttribute('stroke-width', '1.5');
                    chLine.setAttribute('stroke-dasharray', '2,2');
                    chLine.style.pointerEvents = 'none';
                    svg.appendChild(chLine);
                }

                // Arrow indicator
                const arrow = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                const arrowX = toX;
                arrow.setAttribute('points', `${arrowX-4},${rowCenterY-4} ${arrowX+4},${rowCenterY} ${arrowX-4},${rowCenterY+4}`);
                arrow.setAttribute('fill', branchColors[targetInfo.colorIdx % branchColors.length]);
                arrow.style.pointerEvents = 'none';
                svg.appendChild(arrow);

                activeBranch = cmd.branchName;
                branchInfo = targetInfo;
                branchColor = branchColors[branchInfo.colorIdx % branchColors.length];
            }
        } else if (cmd.type === 'commit') {
            const cx = padding + branchInfo.lane * branchLaneWidth + branchLaneWidth / 2;
            let fillColor = branchColor;
            if (cmd.commitType === 'REVERSE') fillColor = typeColors.REVERSE;
            else if (cmd.commitType === 'HIGHLIGHT') fillColor = typeColors.HIGHLIGHT;

            // Connect to previous commit on same branch
            const prevOnBranch = _ggFindPrevCommitOnBranch(commitPositions, activeBranch);
            if (prevOnBranch) {
                const connLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                connLine.setAttribute('x1', prevOnBranch.x);
                connLine.setAttribute('y1', prevOnBranch.y);
                connLine.setAttribute('x2', cx);
                connLine.setAttribute('y2', rowCenterY);
                connLine.setAttribute('stroke', branchColor);
                connLine.setAttribute('stroke-width', '2');
                connLine.style.pointerEvents = 'none';
                svg.appendChild(connLine);
            }

            // Commit circle
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', cx);
            circle.setAttribute('cy', rowCenterY);
            circle.setAttribute('r', commitRadius);
            circle.setAttribute('fill', fillColor);
            circle.setAttribute('stroke', isLight ? '#fff' : '#1E1E1E');
            circle.setAttribute('stroke-width', '2.5');
            circle.style.pointerEvents = 'none';
            svg.appendChild(circle);

            // Reverse commit: X inside
            if (cmd.commitType === 'REVERSE') {
                const xSize = 5;
                const x1 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                x1.setAttribute('x1', cx - xSize); x1.setAttribute('y1', rowCenterY - xSize);
                x1.setAttribute('x2', cx + xSize); x1.setAttribute('y2', rowCenterY + xSize);
                x1.setAttribute('stroke', isLight ? '#fff' : '#1E1E1E'); x1.setAttribute('stroke-width', '2');
                x1.style.pointerEvents = 'none'; svg.appendChild(x1);
                const x2 = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                x2.setAttribute('x1', cx + xSize); x2.setAttribute('y1', rowCenterY - xSize);
                x2.setAttribute('x2', cx - xSize); x2.setAttribute('y2', rowCenterY + xSize);
                x2.setAttribute('stroke', isLight ? '#fff' : '#1E1E1E'); x2.setAttribute('stroke-width', '2');
                x2.style.pointerEvents = 'none'; svg.appendChild(x2);
            }

            // Highlight commit: inner ring
            if (cmd.commitType === 'HIGHLIGHT') {
                const inner = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
                inner.setAttribute('cx', cx);
                inner.setAttribute('cy', rowCenterY);
                inner.setAttribute('r', commitRadius - 4);
                inner.setAttribute('fill', 'none');
                inner.setAttribute('stroke', isLight ? '#fff' : '#1E1E1E');
                inner.setAttribute('stroke-width', '2');
                inner.style.pointerEvents = 'none';
                svg.appendChild(inner);
            }

            commitPositions.push({ x: cx, y: rowCenterY, branch: activeBranch, index: idx });

            // Tag badge
            if (cmd.tag) {
                const tagX = cx + commitRadius + 8;
                const tagBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                tagBg.setAttribute('x', tagX);
                tagBg.setAttribute('y', rowCenterY - 9);
                tagBg.setAttribute('width', cmd.tag.length * 7 + 12);
                tagBg.setAttribute('height', 18);
                tagBg.setAttribute('fill', isLight ? '#e8f5e9' : 'rgba(166,227,161,0.15)');
                tagBg.setAttribute('stroke', isLight ? '#4caf50' : '#a6e3a1');
                tagBg.setAttribute('stroke-width', '1');
                tagBg.setAttribute('rx', '9');
                tagBg.style.pointerEvents = 'none';
                svg.appendChild(tagBg);

                const tagLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                tagLabel.setAttribute('x', tagX + 6);
                tagLabel.setAttribute('y', rowCenterY + 4);
                tagLabel.setAttribute('fill', isLight ? '#2e7d32' : '#a6e3a1');
                tagLabel.setAttribute('font-size', '10');
                tagLabel.setAttribute('font-weight', 'bold');
                tagLabel.textContent = '\uD83C\uDFF7 ' + cmd.tag;
                tagLabel.style.pointerEvents = 'none';
                svg.appendChild(tagLabel);
            }
        } else if (cmd.type === 'merge') {
            const mergeFromInfo = branches[cmd.branchName];
            if (mergeFromInfo) {
                const fromX = padding + mergeFromInfo.lane * branchLaneWidth + branchLaneWidth / 2;
                const toX = padding + branchInfo.lane * branchLaneWidth + branchLaneWidth / 2;
                const mergeColor = branchColors[mergeFromInfo.colorIdx % branchColors.length];

                // Connect to previous commit on current branch
                const prevOnBranch = _ggFindPrevCommitOnBranch(commitPositions, activeBranch);
                if (prevOnBranch) {
                    const connLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                    connLine.setAttribute('x1', prevOnBranch.x);
                    connLine.setAttribute('y1', prevOnBranch.y);
                    connLine.setAttribute('x2', toX);
                    connLine.setAttribute('y2', rowCenterY);
                    connLine.setAttribute('stroke', branchColor);
                    connLine.setAttribute('stroke-width', '2');
                    connLine.style.pointerEvents = 'none';
                    svg.appendChild(connLine);
                }

                // Merge line from source branch
                const mergeLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                mergeLine.setAttribute('x1', fromX);
                mergeLine.setAttribute('y1', rowCenterY - rowHeight / 2);
                mergeLine.setAttribute('x2', toX);
                mergeLine.setAttribute('y2', rowCenterY);
                mergeLine.setAttribute('stroke', mergeColor);
                mergeLine.setAttribute('stroke-width', '2');
                mergeLine.setAttribute('stroke-dasharray', '4,3');
                mergeLine.style.pointerEvents = 'none';
                svg.appendChild(mergeLine);

                // Merge commit (diamond shape)
                const d = commitRadius;
                const diamond = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
                diamond.setAttribute('points',
                    `${toX},${rowCenterY-d} ${toX+d},${rowCenterY} ${toX},${rowCenterY+d} ${toX-d},${rowCenterY}`);
                diamond.setAttribute('fill', branchColor);
                diamond.setAttribute('stroke', isLight ? '#fff' : '#1E1E1E');
                diamond.setAttribute('stroke-width', '2.5');
                diamond.style.pointerEvents = 'none';
                svg.appendChild(diamond);

                commitPositions.push({ x: toX, y: rowCenterY, branch: activeBranch, index: idx });

                // Tag badge for merge
                if (cmd.tag) {
                    const tagX = toX + commitRadius + 8;
                    const tagBg = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
                    tagBg.setAttribute('x', tagX);
                    tagBg.setAttribute('y', rowCenterY - 9);
                    tagBg.setAttribute('width', cmd.tag.length * 7 + 12);
                    tagBg.setAttribute('height', 18);
                    tagBg.setAttribute('fill', isLight ? '#e8f5e9' : 'rgba(166,227,161,0.15)');
                    tagBg.setAttribute('stroke', isLight ? '#4caf50' : '#a6e3a1');
                    tagBg.setAttribute('stroke-width', '1');
                    tagBg.setAttribute('rx', '9');
                    tagBg.style.pointerEvents = 'none';
                    svg.appendChild(tagBg);

                    const tagLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                    tagLabel.setAttribute('x', tagX + 6);
                    tagLabel.setAttribute('y', rowCenterY + 4);
                    tagLabel.setAttribute('fill', isLight ? '#2e7d32' : '#a6e3a1');
                    tagLabel.setAttribute('font-size', '10');
                    tagLabel.setAttribute('font-weight', 'bold');
                    tagLabel.textContent = '\uD83C\uDFF7 ' + cmd.tag;
                    tagLabel.style.pointerEvents = 'none';
                    svg.appendChild(tagLabel);
                }
            }
        } else if (cmd.type === 'cherry-pick') {
            const cx = padding + branchInfo.lane * branchLaneWidth + branchLaneWidth / 2;

            // Connect to previous commit on current branch
            const prevOnBranch = _ggFindPrevCommitOnBranch(commitPositions, activeBranch);
            if (prevOnBranch) {
                const connLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
                connLine.setAttribute('x1', prevOnBranch.x);
                connLine.setAttribute('y1', prevOnBranch.y);
                connLine.setAttribute('x2', cx);
                connLine.setAttribute('y2', rowCenterY);
                connLine.setAttribute('stroke', branchColor);
                connLine.setAttribute('stroke-width', '2');
                connLine.style.pointerEvents = 'none';
                svg.appendChild(connLine);
            }

            // Cherry-pick circle (dashed outline)
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', cx);
            circle.setAttribute('cy', rowCenterY);
            circle.setAttribute('r', commitRadius);
            circle.setAttribute('fill', branchColor);
            circle.setAttribute('stroke', isLight ? '#f44336' : '#f38ba8');
            circle.setAttribute('stroke-width', '2.5');
            circle.setAttribute('stroke-dasharray', '3,2');
            circle.style.pointerEvents = 'none';
            svg.appendChild(circle);

            commitPositions.push({ x: cx, y: rowCenterY, branch: activeBranch, index: idx });
        }

        // Track branch changes
        if (cmd.type === 'checkout' && cmd.branchName && branches[cmd.branchName]) {
            activeBranch = cmd.branchName;
        }

        // Command label text
        const labelText = _ggFormatCommandLabel(cmd);
        const cmdLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        cmdLabel.setAttribute('x', labelStartX);
        cmdLabel.setAttribute('y', rowCenterY + 4);
        cmdLabel.setAttribute('fill', textColor);
        cmdLabel.setAttribute('font-size', '13');
        cmdLabel.setAttribute('font-family', 'monospace');
        cmdLabel.textContent = labelText.length > 60 ? labelText.substring(0, 59) + '\u2026' : labelText;
        cmdLabel.style.pointerEvents = 'none';
        svg.appendChild(cmdLabel);

        // Command type icon/badge
        const typeBadgeColors = {
            'commit': isLight ? '#2196f3' : '#89b4fa',
            'branch': isLight ? '#4caf50' : '#a6e3a1',
            'checkout': isLight ? '#ff9800' : '#f9e2af',
            'merge': isLight ? '#9c27b0' : '#cba6f7',
            'cherry-pick': isLight ? '#f44336' : '#f38ba8'
        };
        const badgeColor = typeBadgeColors[cmd.type] || textColor;
        const typeBadge = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        typeBadge.setAttribute('x', labelStartX - 14);
        typeBadge.setAttribute('y', rowCenterY + 4);
        typeBadge.setAttribute('fill', badgeColor);
        typeBadge.setAttribute('font-size', '11');
        typeBadge.setAttribute('font-weight', 'bold');
        typeBadge.setAttribute('text-anchor', 'end');
        const typeIcons = { 'commit': '\u25CF', 'branch': '\u2934', 'checkout': '\u21B3', 'merge': '\u26D3', 'cherry-pick': '\uD83C\uDF52' };
        typeBadge.textContent = typeIcons[cmd.type] || '\u25CB';
        typeBadge.style.pointerEvents = 'none';
        svg.appendChild(typeBadge);

        // Selection highlight border
        if (isSelected) {
            const selBorder = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            selBorder.setAttribute('x', padding);
            selBorder.setAttribute('y', rowY);
            selBorder.setAttribute('width', totalWidth - padding * 2);
            selBorder.setAttribute('height', rowHeight);
            selBorder.setAttribute('fill', 'none');
            selBorder.setAttribute('stroke', isLight ? '#ff9800' : '#f9e2af');
            selBorder.setAttribute('stroke-width', '2');
            selBorder.setAttribute('rx', '4');
            svg.appendChild(selBorder);
        }
    });

    // Draw continuous branch lanes (vertical lines behind everything)
    // We insert these before the rows by prepending
    const laneGroup = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    Object.entries(branches).forEach(([name, info]) => {
        const laneX = padding + info.lane * branchLaneWidth + branchLaneWidth / 2;
        const laneColor = branchColors[info.colorIdx % branchColors.length];
        const laneLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
        laneLine.setAttribute('x1', laneX);
        laneLine.setAttribute('y1', currentY);
        laneLine.setAttribute('x2', laneX);
        laneLine.setAttribute('y2', currentY + commands.length * rowHeight);
        laneLine.setAttribute('stroke', laneColor);
        laneLine.setAttribute('stroke-width', '1');
        laneLine.setAttribute('stroke-opacity', '0.2');
        laneLine.style.pointerEvents = 'none';
        laneGroup.appendChild(laneLine);

        // Branch name at top of lane
        const brLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        brLabel.setAttribute('x', laneX);
        brLabel.setAttribute('y', currentY - 6);
        brLabel.setAttribute('text-anchor', 'middle');
        brLabel.setAttribute('fill', laneColor);
        brLabel.setAttribute('font-size', '9');
        brLabel.setAttribute('font-weight', 'bold');
        brLabel.setAttribute('opacity', '0.7');
        brLabel.textContent = name;
        laneGroup.appendChild(brLabel);
    });
    // Insert lanes before everything else (but after bg)
    if (svg.children.length > 1) {
        svg.insertBefore(laneGroup, svg.children[1]);
    } else {
        svg.appendChild(laneGroup);
    }

    canvas.appendChild(svg);
    renderGitGraphToolbar(canvas, svg, totalWidth, totalHeight, commands.length);
    updateMinimap();
}

function _ggFindPrevCommitOnBranch(positions, branch) {
    for (let i = positions.length - 1; i >= 0; i--) {
        if (positions[i].branch === branch) return positions[i];
    }
    return null;
}

function _ggFormatCommandLabel(cmd) {
    let label = cmd.type;
    if (cmd.type === 'commit') {
        if (cmd.id) label += ` id: "${cmd.id}"`;
        if (cmd.tag) label += ` tag: "${cmd.tag}"`;
        if (cmd.commitType && cmd.commitType !== 'NORMAL') label += ` type: ${cmd.commitType}`;
    } else if (cmd.type === 'branch') {
        label += ` ${cmd.branchName || ''}`;
        if (cmd.order != null) label += ` order: ${cmd.order}`;
    } else if (cmd.type === 'checkout') {
        label += ` ${cmd.branchName || ''}`;
    } else if (cmd.type === 'merge') {
        label += ` ${cmd.branchName || ''}`;
        if (cmd.id) label += ` id: "${cmd.id}"`;
        if (cmd.tag) label += ` tag: "${cmd.tag}"`;
        if (cmd.commitType && cmd.commitType !== 'NORMAL') label += ` type: ${cmd.commitType}`;
    } else if (cmd.type === 'cherry-pick') {
        if (cmd.id) label += ` id: "${cmd.id}"`;
        if (cmd.parent) label += ` parent: "${cmd.parent}"`;
    }
    return label;
}

// ========== GitGraph Toolbar ==========

function renderGitGraphToolbar(canvas, svg, svgWidth, svgHeight, cmdCount) {
    const toolbarY = svgHeight - 50;
    const buttons = [
        { label: '+ Commit', action: () => createGitGraphCommand('commit') },
        { label: '+ Branch', action: () => createGitGraphCommand('branch') },
        { label: '+ Checkout', action: () => createGitGraphCommand('checkout') },
        { label: '+ Merge', action: () => createGitGraphCommand('merge') },
        { label: '+ Cherry-pick', action: () => createGitGraphCommand('cherry-pick') },
        { label: '\u2699 Settings', action: () => editGitGraphSettings() }
    ];

    let btnX = 30;
    buttons.forEach(btn => {
        const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        g.style.cursor = 'pointer';
        const cs = getComputedStyle(document.body);
        const isLight = document.body.classList.contains('theme-light');

        const width = btn.label.length * 8 + 16;
        const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rect.setAttribute('x', btnX);
        rect.setAttribute('y', toolbarY);
        rect.setAttribute('width', width);
        rect.setAttribute('height', 28);
        rect.setAttribute('rx', '6');
        rect.setAttribute('fill', isLight ? 'rgba(0,0,0,0.06)' : 'rgba(255,255,255,0.08)');
        g.appendChild(rect);

        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', btnX + width / 2);
        text.setAttribute('y', toolbarY + 18);
        text.setAttribute('text-anchor', 'middle');
        text.setAttribute('fill', cs.getPropertyValue('--node-text').trim() || '#D4D4D4');
        text.setAttribute('font-size', '11');
        text.textContent = btn.label;
        g.appendChild(text);

        g.addEventListener('click', (e) => { e.stopPropagation(); btn.action(); });
        g.addEventListener('mouseenter', () => rect.setAttribute('fill', isLight ? 'rgba(0,0,0,0.1)' : 'rgba(255,255,255,0.14)'));
        g.addEventListener('mouseleave', () => rect.setAttribute('fill', isLight ? 'rgba(0,0,0,0.06)' : 'rgba(255,255,255,0.08)'));
        svg.appendChild(g);
        btnX += width + 8;
    });
}

// ========== GitGraph Selection ==========

function selectGitGraphCommand(index) {
    ggSelectedCommand = index;
    renderGitGraphDiagram();
}

// ========== GitGraph CRUD Operations ==========

function _ggGetExistingBranches() {
    if (!gitGraphModel || !gitGraphModel.commands) return ['main'];
    const branches = new Set(['main']);
    gitGraphModel.commands.forEach(cmd => {
        if (cmd.type === 'branch' && cmd.branchName) branches.add(cmd.branchName);
    });
    return Array.from(branches);
}

function _ggGetActiveBranchAtIndex(index) {
    if (!gitGraphModel || !gitGraphModel.commands) return 'main';
    let active = 'main';
    for (let i = 0; i < index && i < gitGraphModel.commands.length; i++) {
        const cmd = gitGraphModel.commands[i];
        if (cmd.type === 'checkout' && cmd.branchName) active = cmd.branchName;
        else if (cmd.type === 'branch' && cmd.branchName) active = cmd.branchName;
    }
    return active;
}

function _ggBuildPositionHtml(cmdCount) {
    const options = ['At end'];
    for (let i = 0; i < cmdCount; i++) {
        options.push(`Before command ${i + 1}`);
    }
    if (options.length <= 1) return '';
    let html = '<div class="property-row"><div class="property-label">Position</div><select class="property-select" id="gg-position">';
    options.forEach((opt, i) => {
        html += `<option value="${i}">${opt}</option>`;
    });
    html += '</select></div>';
    return html;
}

function _ggReadInsertIndex() {
    const sel = document.getElementById('gg-position');
    if (!sel) return null;
    const val = parseInt(sel.value);
    if (val === 0) return null; // "At end"
    return val - 1; // "Before command N" -> index N-1
}

function createGitGraphCommand(type) {
    if (!gitGraphModel) return;
    const branches = _ggGetExistingBranches();
    const cmdCount = (gitGraphModel.commands || []).length;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = `Add ${type}`;
    const body = document.querySelector('.property-panel-body');

    let fieldsHtml = '';
    if (type === 'commit') {
        fieldsHtml = `
            <div class="property-row"><div class="property-label">Commit ID (optional)</div>
            <input class="property-input" id="gg-id" placeholder="e.g. feat-123" /></div>
            <div class="property-row"><div class="property-label">Tag (optional)</div>
            <input class="property-input" id="gg-tag" placeholder="e.g. v1.0" /></div>
            <div class="property-row"><div class="property-label">Type</div>
            <select class="property-select" id="gg-type">
                <option value="NORMAL">Normal</option>
                <option value="REVERSE">Reverse</option>
                <option value="HIGHLIGHT">Highlight</option>
            </select></div>`;
    } else if (type === 'branch') {
        fieldsHtml = `
            <div class="property-row"><div class="property-label">Branch Name</div>
            <input class="property-input" id="gg-branch" placeholder="e.g. feature-x" /></div>
            <div class="property-row"><div class="property-label">Order (optional)</div>
            <input class="property-input" id="gg-order" type="number" /></div>`;
    } else if (type === 'checkout') {
        let branchOpts = branches.map(b => `<option value="${b}">${b}</option>`).join('');
        fieldsHtml = `
            <div class="property-row"><div class="property-label">Switch to Branch</div>
            <select class="property-select" id="gg-branch">${branchOpts}</select></div>`;
    } else if (type === 'merge') {
        let branchOpts = branches.map(b => `<option value="${b}">${b}</option>`).join('');
        fieldsHtml = `
            <div class="property-row"><div class="property-label">Merge from Branch</div>
            <select class="property-select" id="gg-branch">${branchOpts}</select></div>
            <div class="property-row"><div class="property-label">Commit ID (optional)</div>
            <input class="property-input" id="gg-id" /></div>
            <div class="property-row"><div class="property-label">Tag (optional)</div>
            <input class="property-input" id="gg-tag" /></div>
            <div class="property-row"><div class="property-label">Type</div>
            <select class="property-select" id="gg-type">
                <option value="NORMAL">Normal</option>
                <option value="REVERSE">Reverse</option>
                <option value="HIGHLIGHT">Highlight</option>
            </select></div>`;
    } else if (type === 'cherry-pick') {
        fieldsHtml = `
            <div class="property-row"><div class="property-label">Commit ID to cherry-pick</div>
            <input class="property-input" id="gg-id" placeholder="e.g. feat-123" /></div>
            <div class="property-row"><div class="property-label">Parent (optional)</div>
            <input class="property-input" id="gg-parent" /></div>
            <div class="property-row"><div class="property-label">Tag (optional)</div>
            <input class="property-input" id="gg-tag" /></div>`;
    }

    body.innerHTML = `
        ${fieldsHtml}
        ${_ggBuildPositionHtml(cmdCount)}
        <div class="property-row" style="margin-top:8px">
            <button id="gg-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Add ${type}</button>
        </div>`;

    document.getElementById('gg-dlg-ok').addEventListener('click', function() {
        const msg = { type: 'gg_commandCreated', commandType: type };
        const insertIdx = _ggReadInsertIndex();
        if (insertIdx !== null) msg.insertAtIndex = insertIdx;

        if (type === 'commit') {
            const id = document.getElementById('gg-id')?.value.trim();
            const tag = document.getElementById('gg-tag')?.value.trim();
            const ct = document.getElementById('gg-type')?.value;
            if (id) msg.id = id;
            if (tag) msg.tag = tag;
            if (ct && ct !== 'NORMAL') msg.commitType = ct;
        } else if (type === 'branch') {
            const name = document.getElementById('gg-branch')?.value.trim();
            if (!name) return;
            msg.branchName = name;
            const order = document.getElementById('gg-order')?.value;
            if (order !== '' && order !== undefined) msg.order = parseInt(order);
        } else if (type === 'checkout') {
            msg.branchName = document.getElementById('gg-branch')?.value;
        } else if (type === 'merge') {
            msg.branchName = document.getElementById('gg-branch')?.value;
            const id = document.getElementById('gg-id')?.value.trim();
            const tag = document.getElementById('gg-tag')?.value.trim();
            const ct = document.getElementById('gg-type')?.value;
            if (id) msg.id = id;
            if (tag) msg.tag = tag;
            if (ct && ct !== 'NORMAL') msg.commitType = ct;
        } else if (type === 'cherry-pick') {
            const id = document.getElementById('gg-id')?.value.trim();
            if (!id) return;
            msg.id = id;
            const parent = document.getElementById('gg-parent')?.value.trim();
            if (parent) msg.parent = parent;
            const tag = document.getElementById('gg-tag')?.value.trim();
            if (tag) msg.tag = tag;
        }

        window.chrome.webview.postMessage(msg);
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => {
        const firstInput = body.querySelector('input');
        if (firstInput) firstInput.select();
    }, 50);
}

function editGitGraphCommand(index) {
    if (!gitGraphModel || !gitGraphModel.commands) return;
    const cmd = gitGraphModel.commands[index];
    if (!cmd) return;

    const branches = _ggGetExistingBranches();
    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = `Edit ${cmd.type}`;
    const body = document.querySelector('.property-panel-body');

    let fieldsHtml = '';
    if (cmd.type === 'commit' || cmd.type === 'merge' || cmd.type === 'cherry-pick') {
        fieldsHtml += `
            <div class="property-row"><div class="property-label">ID</div>
            <input class="property-input" id="gg-id" value="${cmd.id || ''}" /></div>
            <div class="property-row"><div class="property-label">Tag</div>
            <input class="property-input" id="gg-tag" value="${cmd.tag || ''}" /></div>`;
    }
    if (cmd.type === 'commit' || cmd.type === 'merge') {
        fieldsHtml += `
            <div class="property-row"><div class="property-label">Type</div>
            <select class="property-select" id="gg-type">
                <option value="NORMAL" ${(cmd.commitType || 'NORMAL') === 'NORMAL' ? 'selected' : ''}>Normal</option>
                <option value="REVERSE" ${cmd.commitType === 'REVERSE' ? 'selected' : ''}>Reverse</option>
                <option value="HIGHLIGHT" ${cmd.commitType === 'HIGHLIGHT' ? 'selected' : ''}>Highlight</option>
            </select></div>`;
    }
    if (cmd.type === 'branch' || cmd.type === 'checkout' || cmd.type === 'merge') {
        if (cmd.type === 'checkout' || cmd.type === 'merge') {
            let branchOpts = branches.map(b => `<option value="${b}" ${b === cmd.branchName ? 'selected' : ''}>${b}</option>`).join('');
            fieldsHtml += `
                <div class="property-row"><div class="property-label">Branch</div>
                <select class="property-select" id="gg-branch">${branchOpts}</select></div>`;
        } else {
            fieldsHtml += `
                <div class="property-row"><div class="property-label">Branch Name</div>
                <input class="property-input" id="gg-branch" value="${cmd.branchName || ''}" /></div>`;
        }
    }
    if (cmd.type === 'branch') {
        fieldsHtml += `
            <div class="property-row"><div class="property-label">Order (optional)</div>
            <input class="property-input" id="gg-order" type="number" value="${cmd.order != null ? cmd.order : ''}" /></div>`;
    }
    if (cmd.type === 'cherry-pick') {
        fieldsHtml += `
            <div class="property-row"><div class="property-label">Parent</div>
            <input class="property-input" id="gg-parent" value="${cmd.parent || ''}" /></div>`;
    }

    body.innerHTML = `
        ${fieldsHtml}
        <div class="property-row" style="margin-top:8px">
            <button id="gg-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save</button>
        </div>`;

    document.getElementById('gg-dlg-ok').addEventListener('click', function() {
        const msg = { type: 'gg_commandEdited', index: index };

        if (cmd.type === 'commit' || cmd.type === 'merge' || cmd.type === 'cherry-pick') {
            const id = document.getElementById('gg-id')?.value.trim();
            msg.id = id || null;
            const tag = document.getElementById('gg-tag')?.value.trim();
            msg.tag = tag || null;
        }
        if (cmd.type === 'commit' || cmd.type === 'merge') {
            msg.commitType = document.getElementById('gg-type')?.value || 'NORMAL';
        }
        if (cmd.type === 'branch' || cmd.type === 'checkout' || cmd.type === 'merge') {
            const branch = (document.getElementById('gg-branch')?.value || '').trim();
            if (branch) msg.branchName = branch;
        }
        if (cmd.type === 'branch') {
            const order = document.getElementById('gg-order')?.value;
            msg.order = order !== '' && order !== undefined ? parseInt(order) : null;
        }
        if (cmd.type === 'cherry-pick') {
            const parent = document.getElementById('gg-parent')?.value.trim();
            msg.parent = parent || null;
        }

        window.chrome.webview.postMessage(msg);
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => {
        const firstInput = body.querySelector('input');
        if (firstInput) firstInput.select();
    }, 50);
}

function deleteGitGraphCommand(index) {
    if (!gitGraphModel || !gitGraphModel.commands) return;
    const cmd = gitGraphModel.commands[index];
    if (!cmd) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'Delete Command';
    const body = document.querySelector('.property-panel-body');

    const label = _ggFormatCommandLabel(cmd);
    body.innerHTML = `
        <div class="property-row">
            <p style="margin:0">Delete command ${index + 1}?</p>
            <p style="font-family:monospace;font-size:12px;opacity:0.7;padding:8px;margin:8px 0;background:var(--input-bg);border-radius:4px">${label}</p>
        </div>
        <div class="property-row" style="margin-top:8px">
            <button id="gg-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:#d32f2f;color:#fff;border:none;border-radius:4px">Delete</button>
        </div>`;

    document.getElementById('gg-dlg-ok').addEventListener('click', function() {
        window.chrome.webview.postMessage({ type: 'gg_commandDeleted', index: index });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
}

function editGitGraphSettings() {
    if (!gitGraphModel) return;

    const propertyPanel = document.getElementById('property-panel');
    const propPanelTitle = document.getElementById('property-panel-title');
    propPanelTitle.textContent = 'GitGraph Settings';
    const body = document.querySelector('.property-panel-body');

    body.innerHTML = `
        <div class="property-row"><div class="property-label">Title (optional)</div>
        <input class="property-input" id="gg-title" value="${gitGraphModel.title || ''}" /></div>
        <div class="property-row"><div class="property-label">Orientation</div>
        <select class="property-select" id="gg-orientation">
            <option value="" ${!gitGraphModel.orientation ? 'selected' : ''}>Default (TB - Top to Bottom)</option>
            <option value="LR" ${gitGraphModel.orientation === 'LR' ? 'selected' : ''}>LR - Left to Right</option>
            <option value="TB" ${gitGraphModel.orientation === 'TB' ? 'selected' : ''}>TB - Top to Bottom</option>
            <option value="BT" ${gitGraphModel.orientation === 'BT' ? 'selected' : ''}>BT - Bottom to Top</option>
        </select></div>
        <div class="property-row" style="margin-top:8px">
            <button id="gg-dlg-ok" style="width:100%;padding:6px;cursor:pointer;background:var(--node-selected-stroke);color:#fff;border:none;border-radius:4px">Save Settings</button>
        </div>`;

    document.getElementById('gg-dlg-ok').addEventListener('click', function() {
        const title = document.getElementById('gg-title')?.value.trim();
        const orientation = document.getElementById('gg-orientation')?.value || null;
        window.chrome.webview.postMessage({
            type: 'gg_settingsChanged',
            title: title || null,
            orientation: orientation || null
        });
        propertyPanel.classList.remove('visible');
    });
    propertyPanel.classList.add('visible');
    setTimeout(() => document.getElementById('gg-title')?.select(), 50);
}

// ========== GitGraph Copy/Paste ==========

function _ggCopyCommand(index) {
    if (!gitGraphModel || !gitGraphModel.commands) return;
    const cmd = gitGraphModel.commands[index];
    if (!cmd) return;
    ggClipboard = {
        data: {
            commandType: cmd.type,
            id: cmd.id,
            tag: cmd.tag,
            commitType: cmd.commitType,
            branchName: cmd.branchName,
            order: cmd.order,
            parent: cmd.parent
        }
    };
}

function _ggPasteCommand(insertAtIndex) {
    if (!ggClipboard || !ggClipboard.data) return;
    const msg = Object.assign({ type: 'gg_commandCreated' }, ggClipboard.data);
    if (insertAtIndex !== null && insertAtIndex !== undefined) msg.insertAtIndex = insertAtIndex;
    window.chrome.webview.postMessage(msg);
}

// ========== GitGraph Context Menu ==========

function _ggAddCtxItem(menu, label, action, isLight) {
    const item = document.createElement('div');
    item.textContent = label;
    item.style.cssText = `padding:6px 16px;cursor:pointer;font-size:12px;white-space:nowrap`;
    item.addEventListener('mouseenter', () => item.style.background = isLight ? 'rgba(0,0,0,0.05)' : 'rgba(255,255,255,0.08)');
    item.addEventListener('mouseleave', () => item.style.background = 'transparent');
    item.addEventListener('click', (e) => { e.stopPropagation(); menu.remove(); action(); });
    menu.appendChild(item);
}

function _ggAddCtxSeparator(menu, isLight) {
    const sep = document.createElement('div');
    sep.style.cssText = `height:1px;margin:4px 8px;background:${isLight ? 'rgba(0,0,0,0.1)' : 'rgba(255,255,255,0.1)'}`;
    menu.appendChild(sep);
}

function showGitGraphContextMenu(e) {
    e.preventDefault();
    document.querySelectorAll('.ve-context-menu').forEach(m => m.remove());

    const isLight = document.body.classList.contains('theme-light');
    const target = e.target.closest('[data-gg-index]');
    const cmdIndex = target ? parseInt(target.getAttribute('data-gg-index')) : null;

    const menu = document.createElement('div');
    menu.className = 've-context-menu';
    menu.style.cssText = `position:fixed;left:${e.clientX}px;top:${e.clientY}px;background:${isLight ? '#fff' : '#2D2D30'};border:1px solid ${isLight ? '#ddd' : '#3E3E42'};border-radius:6px;padding:4px 0;z-index:10000;box-shadow:0 4px 12px rgba(0,0,0,0.3);min-width:160px`;

    if (cmdIndex !== null && gitGraphModel && gitGraphModel.commands && gitGraphModel.commands[cmdIndex]) {
        const cmd = gitGraphModel.commands[cmdIndex];
        _ggAddCtxItem(menu, `Edit ${cmd.type}...`, () => editGitGraphCommand(cmdIndex), isLight);
        _ggAddCtxItem(menu, `Delete ${cmd.type}`, () => deleteGitGraphCommand(cmdIndex), isLight);
        _ggAddCtxSeparator(menu, isLight);
        _ggAddCtxItem(menu, 'Copy', () => _ggCopyCommand(cmdIndex), isLight);
        if (ggClipboard) {
            _ggAddCtxItem(menu, 'Paste Above', () => _ggPasteCommand(cmdIndex), isLight);
            _ggAddCtxItem(menu, 'Paste Below', () => _ggPasteCommand(cmdIndex + 1), isLight);
        }
        _ggAddCtxSeparator(menu, isLight);
        if (cmdIndex > 0) {
            _ggAddCtxItem(menu, 'Move Up', () => {
                window.chrome.webview.postMessage({ type: 'gg_commandMoved', fromIndex: cmdIndex, toIndex: cmdIndex - 1 });
            }, isLight);
        }
        if (cmdIndex < gitGraphModel.commands.length - 1) {
            _ggAddCtxItem(menu, 'Move Down', () => {
                window.chrome.webview.postMessage({ type: 'gg_commandMoved', fromIndex: cmdIndex, toIndex: cmdIndex + 1 });
            }, isLight);
        }
        _ggAddCtxSeparator(menu, isLight);
        _ggAddCtxItem(menu, 'Insert Commit Above', () => {
            const msg = { type: 'gg_commandCreated', commandType: 'commit', insertAtIndex: cmdIndex };
            window.chrome.webview.postMessage(msg);
        }, isLight);
        _ggAddCtxItem(menu, 'Insert Commit Below', () => {
            const msg = { type: 'gg_commandCreated', commandType: 'commit', insertAtIndex: cmdIndex + 1 };
            window.chrome.webview.postMessage(msg);
        }, isLight);
    } else {
        // Background context menu
        _ggAddCtxItem(menu, 'Add Commit', () => createGitGraphCommand('commit'), isLight);
        _ggAddCtxItem(menu, 'Add Branch', () => createGitGraphCommand('branch'), isLight);
        _ggAddCtxItem(menu, 'Add Checkout', () => createGitGraphCommand('checkout'), isLight);
        _ggAddCtxItem(menu, 'Add Merge', () => createGitGraphCommand('merge'), isLight);
        _ggAddCtxItem(menu, 'Add Cherry-pick', () => createGitGraphCommand('cherry-pick'), isLight);
        _ggAddCtxSeparator(menu, isLight);
        if (ggClipboard) {
            _ggAddCtxItem(menu, 'Paste at End', () => _ggPasteCommand(null), isLight);
            _ggAddCtxSeparator(menu, isLight);
        }
        _ggAddCtxItem(menu, 'Settings...', () => editGitGraphSettings(), isLight);
    }

    document.body.appendChild(menu);

    // Close on click outside
    const closeMenu = (ev) => {
        if (!menu.contains(ev.target)) {
            menu.remove();
            document.removeEventListener('click', closeMenu);
        }
    };
    setTimeout(() => document.addEventListener('click', closeMenu), 0);
}
