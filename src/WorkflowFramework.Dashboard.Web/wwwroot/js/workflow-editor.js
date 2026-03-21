// Workflow Editor - Canvas implementation with vanilla JS
// Structured to match the React Flow API so it can be swapped in later
// Uses SVG for rendering nodes and edges

(function () {
    'use strict';

    let container = null;
    let dotNetRef = null;
    let canvas = null;
    let svgLayer = null;
    let nodesLayer = null;
    let edgesLayer = null;

    let nodes = [];
    let edges = [];
    let selectedNodeId = null;
    let nextNodeId = 1;
    let nextEdgeId = 1;

    // Pan/zoom state
    let viewBox = { x: -400, y: -200, w: 1200, h: 800 };
    let isPanning = false;
    let panStart = { x: 0, y: 0 };
    let connectingFrom = null;

    const GRID_SIZE = 20;

    function snapToGrid(val) {
        return Math.round(val / GRID_SIZE) * GRID_SIZE;
    }

    function nextIdFrom(items, prefix) {
        return items.reduce((max, item) => {
            const match = new RegExp(`^${prefix}_(\\d+)$`).exec(item.id || '');
            return match ? Math.max(max, Number(match[1])) : max;
        }, 0) + 1;
    }

    function reseedIds() {
        nextNodeId = nextIdFrom(nodes, 'node');
        nextEdgeId = nextIdFrom(edges, 'edge');
    }

    function normalizeEdge(edge) {
        const kind = edge?.kind ?? edge?.sourceHandle ?? null;
        const label = edge?.label ?? kind ?? null;
        return { ...edge, kind, label };
    }

    function isSyntheticNode(node) {
        return node?.config?.__syntheticKind === 'container-exit';
    }

    function initialize(containerId, ref, initialNodes, initialEdges) {
        container = document.getElementById(containerId);
        dotNetRef = ref;
        if (!container) return;

        container.innerHTML = '';
        container.style.position = 'relative';

        // Create SVG canvas
        canvas = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        canvas.setAttribute('width', '100%');
        canvas.setAttribute('height', '100%');
        canvas.style.background = '#111827';
        canvas.style.cursor = 'grab';
        updateViewBox();
        container.appendChild(canvas);

        // Grid pattern
        const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
        defs.innerHTML = `
            <pattern id="grid" width="${GRID_SIZE}" height="${GRID_SIZE}" patternUnits="userSpaceOnUse">
                <circle cx="${GRID_SIZE/2}" cy="${GRID_SIZE/2}" r="0.5" fill="#374151"/>
            </pattern>
        `;
        canvas.appendChild(defs);

        const gridRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        gridRect.setAttribute('x', '-10000');
        gridRect.setAttribute('y', '-10000');
        gridRect.setAttribute('width', '20000');
        gridRect.setAttribute('height', '20000');
        gridRect.setAttribute('fill', 'url(#grid)');
        canvas.appendChild(gridRect);

        // Layers
        edgesLayer = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        nodesLayer = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        canvas.appendChild(edgesLayer);
        canvas.appendChild(nodesLayer);

        // Temp edge for connecting
        const tempEdge = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        tempEdge.setAttribute('id', 'temp-edge');
        tempEdge.setAttribute('stroke', '#6b7280');
        tempEdge.setAttribute('stroke-width', '2');
        tempEdge.setAttribute('stroke-dasharray', '5,5');
        tempEdge.setAttribute('fill', 'none');
        tempEdge.style.display = 'none';
        canvas.appendChild(tempEdge);

        // Events
        canvas.addEventListener('mousedown', onCanvasMouseDown);
        canvas.addEventListener('mousemove', onCanvasMouseMove);
        canvas.addEventListener('mouseup', onCanvasMouseUp);
        canvas.addEventListener('wheel', onCanvasWheel, { passive: false });
        container.addEventListener('dragover', onDragOver);
        container.addEventListener('drop', onDrop);
        canvas.addEventListener('click', onCanvasClick);

        // Load initial data
        nodes = (initialNodes || []).map(n => ({ ...n }));
        edges = (initialEdges || []).map(normalizeEdge);
        reseedIds();

        render();
    }

    function updateViewBox() {
        if (canvas) {
            canvas.setAttribute('viewBox', `${viewBox.x} ${viewBox.y} ${viewBox.w} ${viewBox.h}`);
        }
    }

    function svgPoint(clientX, clientY) {
        const rect = canvas.getBoundingClientRect();
        const x = viewBox.x + (clientX - rect.left) / rect.width * viewBox.w;
        const y = viewBox.y + (clientY - rect.top) / rect.height * viewBox.h;
        return { x, y };
    }

    function onCanvasClick(e) {
        if (e.target instanceof Element && e.target.closest('[data-node-id]')) {
            return;
        }

        const isBackground = e.target === canvas
            || (e.target instanceof Element && e.target.getAttribute('fill') === 'url(#grid)');

        if (isBackground) {
            selectNode(null);
        }
    }

    function onCanvasMouseDown(e) {
        if (e.target === canvas || e.target.getAttribute('fill') === 'url(#grid)') {
            isPanning = true;
            panStart = { x: e.clientX, y: e.clientY };
            canvas.style.cursor = 'grabbing';
        }
    }

    function onCanvasMouseMove(e) {
        if (isPanning) {
            const rect = canvas.getBoundingClientRect();
            const dx = (e.clientX - panStart.x) / rect.width * viewBox.w;
            const dy = (e.clientY - panStart.y) / rect.height * viewBox.h;
            viewBox.x -= dx;
            viewBox.y -= dy;
            panStart = { x: e.clientX, y: e.clientY };
            updateViewBox();
        }
        if (connectingFrom) {
            const pt = svgPoint(e.clientX, e.clientY);
            const src = nodes.find(n => n.id === connectingFrom.nodeId);
            if (src) {
                const tempEdge = document.getElementById('temp-edge');
                const handle = getOutputHandlePosition(src, connectingFrom.kind);
                const sx = src.x + handle.cx, sy = src.y + handle.cy;
                const d = `M${sx},${sy} C${sx + 60},${sy} ${pt.x - 60},${pt.y} ${pt.x},${pt.y}`;
                tempEdge.setAttribute('d', d);
                tempEdge.style.display = '';
            }
        }
    }

    function onCanvasMouseUp(e) {
        if (isPanning) {
            isPanning = false;
            canvas.style.cursor = 'grab';
        }
        if (connectingFrom) {
            document.getElementById('temp-edge').style.display = 'none';
            connectingFrom = null;
        }
    }

    function onCanvasWheel(e) {
        e.preventDefault();
        const factor = e.deltaY > 0 ? 1.1 : 0.9;
        const pt = svgPoint(e.clientX, e.clientY);
        viewBox.x = pt.x + (viewBox.x - pt.x) * factor;
        viewBox.y = pt.y + (viewBox.y - pt.y) * factor;
        viewBox.w *= factor;
        viewBox.h *= factor;
        updateViewBox();
    }

    function onDragOver(e) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
    }

    function onDrop(e) {
        e.preventDefault();
        const stepType = e.dataTransfer.getData('stepType');
        const stepName = e.dataTransfer.getData('stepName');
        const stepIcon = e.dataTransfer.getData('stepIcon');
        const stepCategory = e.dataTransfer.getData('stepCategory');
        const stepColor = e.dataTransfer.getData('stepColor');
        if (!stepType) return;

        const pt = svgPoint(e.clientX, e.clientY);
        const id = 'node_' + (nextNodeId++);
        const node = {
            id,
            type: stepType,
            label: stepName,
            icon: stepIcon,
            category: stepCategory,
            color: stepColor,
            x: snapToGrid(pt.x - 70),
            y: snapToGrid(pt.y - 25),
            config: {}
        };
        nodes.push(node);
        render();
        selectNode(id);
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnNodeAdded', id, stepType, node.x, node.y);
        }
    }

    function selectNode(nodeId) {
        const node = nodeId ? nodes.find(n => n.id === nodeId) : null;
        selectedNodeId = node && !isSyntheticNode(node) ? nodeId : null;
        const selectedNode = selectedNodeId ? node : null;
        render();
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnNodeSelected', selectedNodeId, selectedNode?.type || null, selectedNode?.config || null);
        }
    }

    function renderEdge(edge) {
        const src = nodes.find(n => n.id === edge.source);
        const tgt = nodes.find(n => n.id === edge.target);
        if (!src || !tgt) return;

        const sourceHandle = getOutputHandlePosition(src, edge.kind);
        const sx = src.x + sourceHandle.cx, sy = src.y + sourceHandle.cy;
        const tx = tgt.x, ty = tgt.y + 25;
        const midX = (sx + tx) / 2;

        const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        g.style.cursor = 'pointer';

        const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        const d = `M${sx},${sy} C${midX},${sy} ${midX},${ty} ${tx},${ty}`;
        path.setAttribute('d', d);
        path.setAttribute('stroke', '#6b7280');
        path.setAttribute('stroke-width', '2');
        path.setAttribute('fill', 'none');
        path.setAttribute('marker-end', '');

        // Hit area
        const hitPath = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        hitPath.setAttribute('d', d);
        hitPath.setAttribute('stroke', 'transparent');
        hitPath.setAttribute('stroke-width', '12');
        hitPath.setAttribute('fill', 'none');

        // Delete on double click
        g.addEventListener('dblclick', (e) => {
            e.stopPropagation();
            edges = edges.filter(ed => ed.id !== edge.id);
            render();
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnEdgeRemoved', edge.id);
        });

        // Arrow
        const angle = Math.atan2(ty - sy, tx - sx);
        const ax = tx - 8 * Math.cos(angle - 0.4);
        const ay = ty - 8 * Math.sin(angle - 0.4);
        const bx = tx - 8 * Math.cos(angle + 0.4);
        const by = ty - 8 * Math.sin(angle + 0.4);
        const arrow = document.createElementNS('http://www.w3.org/2000/svg', 'polygon');
        arrow.setAttribute('points', `${tx},${ty} ${ax},${ay} ${bx},${by}`);
        arrow.setAttribute('fill', '#6b7280');

        g.appendChild(hitPath);
        g.appendChild(path);
        g.appendChild(arrow);
        edgesLayer.appendChild(g);
    }

    function renderNode(node) {
        const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
        g.setAttribute('transform', `translate(${node.x}, ${node.y})`);
        const synthetic = isSyntheticNode(node);
        g.style.cursor = synthetic ? 'default' : 'pointer';
        g.setAttribute('data-node-id', node.id);
        g.setAttribute('data-node-type', node.type);
        if (synthetic) {
            g.setAttribute('data-node-synthetic', 'true');
        }

        const isSelected = node.id === selectedNodeId;

        // Background
        const rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        rect.setAttribute('width', '140');
        rect.setAttribute('height', '50');
        rect.setAttribute('rx', '8');
        rect.setAttribute('fill', '#1f2937');
        rect.setAttribute('stroke', isSelected ? '#3b82f6' : (node.color || '#4b5563'));
        rect.setAttribute('stroke-width', isSelected ? '3' : '2');
        if (isSelected) {
            rect.setAttribute('filter', 'drop-shadow(0 0 6px rgba(59,130,246,0.4))');
        }
        g.appendChild(rect);

        // Color accent bar
        const accent = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
        accent.setAttribute('width', '4');
        accent.setAttribute('height', '50');
        accent.setAttribute('rx', '8');
        accent.setAttribute('fill', node.color || '#4b5563');
        g.appendChild(accent);

        // Icon
        const icon = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        icon.setAttribute('x', '18');
        icon.setAttribute('y', '22');
        icon.setAttribute('font-size', '16');
        icon.textContent = node.icon || '⬡';
        g.appendChild(icon);

        // Label
        const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        label.setAttribute('x', '38');
        label.setAttribute('y', '22');
        label.setAttribute('fill', '#f3f4f6');
        label.setAttribute('font-size', '12');
        label.setAttribute('font-weight', '600');
        label.textContent = (node.config?.label || node.label || '').substring(0, 14);
        g.appendChild(label);

        // Type subtitle
        const sub = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        sub.setAttribute('x', '38');
        sub.setAttribute('y', '38');
        sub.setAttribute('fill', '#9ca3af');
        sub.setAttribute('font-size', '10');
        sub.textContent = node.type;
        g.appendChild(sub);

        // Run status indicator
        if (node.runStatus) {
            const statusColors = { running: '#eab308', success: '#22c55e', error: '#ef4444' };
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', '132');
            circle.setAttribute('cy', '8');
            circle.setAttribute('r', '5');
            circle.setAttribute('fill', statusColors[node.runStatus] || '#6b7280');
            circle.setAttribute('stroke', '#1f2937');
            circle.setAttribute('stroke-width', '2');
            g.appendChild(circle);
        }

        if (synthetic) {
            g.style.pointerEvents = 'none';
            nodesLayer.appendChild(g);
            return;
        }

        // Input handle (left)
        const inputHandle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        inputHandle.setAttribute('cx', '0');
        inputHandle.setAttribute('cy', '25');
        inputHandle.setAttribute('r', '5');
        inputHandle.setAttribute('fill', '#6b7280');
        inputHandle.setAttribute('stroke', '#374151');
        inputHandle.setAttribute('stroke-width', '2');
        inputHandle.dataset.handleRole = 'input';
        inputHandle.style.cursor = 'crosshair';
        inputHandle.addEventListener('mouseup', (e) => {
            e.stopPropagation();
            if (connectingFrom && connectingFrom.nodeId !== node.id) {
                const sourceNode = nodes.find(n => n.id === connectingFrom.nodeId);
                const shouldKeepMultiple = (sourceNode?.type || '').toLowerCase() === 'parallel';
                if (!shouldKeepMultiple) {
                    edges = edges.filter(existing =>
                        !(existing.source === connectingFrom.nodeId && (existing.kind || null) === (connectingFrom.kind || null)));
                }

                const edgeId = 'edge_' + (nextEdgeId++);
                const label = connectingFrom.kind === 'then' || connectingFrom.kind === 'else'
                    ? connectingFrom.kind
                    : null;
                edges.push({ id: edgeId, source: connectingFrom.nodeId, target: node.id, kind: connectingFrom.kind, label });
                connectingFrom = null;
                document.getElementById('temp-edge').style.display = 'none';
                render();
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnEdgeCreated', edges[edges.length-1].source, node.id);
            }
        });
        g.appendChild(inputHandle);

        getOutputHandles(node).forEach(handle => {
            const outputHandle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            outputHandle.setAttribute('cx', String(handle.cx));
            outputHandle.setAttribute('cy', String(handle.cy));
            outputHandle.setAttribute('r', '5');
            outputHandle.setAttribute('fill', '#6b7280');
            outputHandle.setAttribute('stroke', '#374151');
            outputHandle.setAttribute('stroke-width', '2');
            outputHandle.dataset.handleRole = 'output';
            outputHandle.dataset.handleKind = handle.kind || '';
            outputHandle.style.cursor = 'crosshair';
            outputHandle.addEventListener('mousedown', (e) => {
                e.stopPropagation();
                connectingFrom = { nodeId: node.id, kind: handle.kind };
            });
            g.appendChild(outputHandle);

            if (handle.badge) {
                const badge = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                badge.setAttribute('x', String(handle.cx - 10));
                badge.setAttribute('y', String(handle.cy - 7));
                badge.setAttribute('fill', '#9ca3af');
                badge.setAttribute('font-size', '8');
                badge.setAttribute('text-anchor', 'end');
                badge.textContent = handle.badge;
                g.appendChild(badge);
            }
        });

        // Drag behavior
        let isDragging = false;
        let dragOffset = { x: 0, y: 0 };

        g.addEventListener('mousedown', (e) => {
            if (e.target instanceof Element && e.target.closest('[data-handle-role]')) return;
            e.stopPropagation();
            isDragging = true;
            const pt = svgPoint(e.clientX, e.clientY);
            dragOffset = { x: pt.x - node.x, y: pt.y - node.y };
            selectNode(node.id);

            const onMove = (ev) => {
                if (!isDragging) return;
                const p = svgPoint(ev.clientX, ev.clientY);
                node.x = snapToGrid(p.x - dragOffset.x);
                node.y = snapToGrid(p.y - dragOffset.y);
                render();
            };
            const onUp = () => {
                isDragging = false;
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnCanvasChanged');
            };
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
        });

        // Delete on Delete/Backspace key
        g.setAttribute('tabindex', '0');
        g.addEventListener('keydown', (e) => {
            if (e.key === 'Delete' || e.key === 'Backspace') {
                nodes = nodes.filter(n => n.id !== node.id);
                edges = edges.filter(ed => ed.source !== node.id && ed.target !== node.id);
                selectNode(null);
                render();
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnNodeRemoved', node.id);
            }
        });

        nodesLayer.appendChild(g);
    }

    function render() {
        if (!edgesLayer || !nodesLayer) return;
        edgesLayer.innerHTML = '';
        nodesLayer.innerHTML = '';
        edges.forEach(renderEdge);
        nodes.forEach(renderNode);
    }

    function getOutputHandles(node) {
        const type = (node.type || '').toLowerCase();
        if (type === 'conditional') {
            return [
                { kind: 'then', cx: 140, cy: 12, badge: 'then' },
                { kind: 'continue', cx: 140, cy: 25, badge: 'next' },
                { kind: 'else', cx: 140, cy: 38, badge: 'else' }
            ];
        }

        if (type === 'trycatch') {
            return [
                { kind: 'try', cx: 140, cy: 12, badge: 'try' },
                { kind: 'finally', cx: 140, cy: 25, badge: 'finally' },
                { kind: 'continue', cx: 140, cy: 38, badge: 'next' }
            ];
        }

        if (type === 'timeout') {
            return [
                { kind: 'inner', cx: 140, cy: 16, badge: 'inner' },
                { kind: 'continue', cx: 140, cy: 34, badge: 'next' }
            ];
        }

        if (['retry', 'foreach', 'while', 'dowhile', 'saga'].includes(type)) {
            return [
                { kind: 'body', cx: 140, cy: 16, badge: 'body' },
                { kind: 'continue', cx: 140, cy: 34, badge: 'next' }
            ];
        }

        if (type === 'parallel') {
            return [
                { kind: null, cx: 140, cy: 16, badge: 'branch' },
                { kind: 'continue', cx: 140, cy: 34, badge: 'next' }
            ];
        }

        return [{ kind: null, cx: 140, cy: 25, badge: null }];
    }

    function getOutputHandlePosition(node, kind) {
        const handles = getOutputHandles(node);
        return handles.find(h => h.kind === kind) || handles[0];
    }

    // Public API
    window.workflowEditor = {
        initialize,

        handlePaletteDragStart(event, type, name, icon, category, color) {
            event.dataTransfer.setData('stepType', type);
            event.dataTransfer.setData('stepName', name);
            event.dataTransfer.setData('stepIcon', icon);
            event.dataTransfer.setData('stepCategory', category);
            event.dataTransfer.setData('stepColor', color);
            event.dataTransfer.effectAllowed = 'copy';
        },

        addNode(nodeData) {
            const id = 'node_' + (nextNodeId++);
            nodes.push({ id, ...nodeData });
            render();
            return id;
        },

        removeNode(nodeId) {
            const node = nodes.find(n => n.id === nodeId);
            if (node && isSyntheticNode(node)) {
                return;
            }
            nodes = nodes.filter(n => n.id !== nodeId);
            edges = edges.filter(e => e.source !== nodeId && e.target !== nodeId);
            if (selectedNodeId === nodeId) selectNode(null);
            render();
        },

        updateNode(nodeId, config) {
            const node = nodes.find(n => n.id === nodeId);
            if (node) {
                node.config = { ...node.config, ...config };
                render();
            }
        },

        getWorkflowDefinition() {
            return { nodes: nodes.map(n => ({ ...n })), edges: edges.map(e => ({ ...e })) };
        },

        setWorkflowDefinition(newNodes, newEdges) {
            nodes = (newNodes || []).map(n => ({ ...n }));
            edges = (newEdges || []).map(normalizeEdge);
            reseedIds();
            selectedNodeId = null;
            render();
        },

        fitView() {
            if (nodes.length === 0) {
                viewBox = { x: -400, y: -200, w: 1200, h: 800 };
            } else {
                const minX = Math.min(...nodes.map(n => n.x)) - 100;
                const minY = Math.min(...nodes.map(n => n.y)) - 100;
                const maxX = Math.max(...nodes.map(n => n.x + 140)) + 100;
                const maxY = Math.max(...nodes.map(n => n.y + 50)) + 100;
                viewBox = { x: minX, y: minY, w: maxX - minX, h: maxY - minY };
            }
            updateViewBox();
        },

        setRunStatus(nodeId, status) {
            const node = nodes.find(n => n.id === nodeId);
            if (node) {
                node.runStatus = status;
                render();
            }
        },

        getNodeConnections(nodeId) {
            const inputs = edges.filter(e => e.target === nodeId).map(e => {
                const src = nodes.find(n => n.id === e.source);
                return src && !isSyntheticNode(src)
                    ? { id: e.source, label: src.config?.label || src.label || e.source, edgeLabel: e.label || e.kind || null, kind: e.kind || null }
                    : null;
            }).filter(Boolean);
            const outputs = edges.filter(e => e.source === nodeId).map(e => {
                const tgt = nodes.find(n => n.id === e.target);
                return tgt && !isSyntheticNode(tgt)
                    ? { id: e.target, label: tgt.config?.label || tgt.label || e.target, edgeLabel: e.label || e.kind || null, kind: e.kind || null }
                    : null;
            }).filter(Boolean);
            return { inputs, outputs };
        },

        focusNode(nodeId) {
            const node = nodes.find(n => n.id === nodeId);
            if (!node) return;
            const padding = 200;
            viewBox = { x: node.x - padding, y: node.y - padding, w: padding * 2 + 140, h: padding * 2 + 50 };
            updateViewBox();
            selectNode(nodeId);
        },

        selectNodeByName(name) {
            const node = nodes.find(n => (n.config?.label || n.label) === name);
            if (node) {
                this.focusNode(node.id);
            }
        },

        getAllNodes() {
            return nodes
                .filter(n => !isSyntheticNode(n))
                .map(n => ({ id: n.id, type: n.type, label: n.config?.label || n.label || '', icon: n.icon || '⬡', category: n.category || '', color: n.color || '#4b5563' }));
        },

        getAllEdges() {
            return edges
                .filter(e => {
                    const sourceNode = nodes.find(n => n.id === e.source);
                    const targetNode = nodes.find(n => n.id === e.target);
                    return !isSyntheticNode(sourceNode) && !isSyntheticNode(targetNode);
                })
                .map(e => ({ id: e.id, source: e.source, target: e.target, label: e.label || e.kind || null, kind: e.kind || null }));
        },

        updateNodeStatus(stepName, status) {
            const node = nodes.find(n => (n.config?.label || n.label) === stepName);
            if (node) {
                node.runStatus = status === 'Running' ? 'running' : status === 'Completed' ? 'success' : status === 'Failed' ? 'error' : null;
                render();
            }
        },

        deleteSelected() {
            if (selectedNodeId) {
                const nodeId = selectedNodeId;
                const node = nodes.find(n => n.id === nodeId);
                if (node && isSyntheticNode(node)) {
                    selectNode(null);
                    return;
                }
                nodes = nodes.filter(n => n.id !== nodeId);
                edges = edges.filter(e => e.source !== nodeId && e.target !== nodeId);
                selectNode(null);
                render();
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnNodeRemoved', nodeId);
            }
        },

        destroy() {
            if (container) container.innerHTML = '';
            nodes = [];
            edges = [];
            dotNetRef = null;
        }
    };
})();
