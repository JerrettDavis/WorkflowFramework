// React Flow Workflow Editor - Production-grade visual workflow editor
// Replaces the hand-rolled SVG canvas with React Flow
// Maintains the same JS interop API for Blazor components

(function () {
    'use strict';

    const { createElement: h, useState, useCallback, useRef, useEffect, useMemo, Fragment } = React;
    const RF = window.ReactFlow;
    const ReactFlowComponent = RF.default || RF.ReactFlow;
    const {
        Background,
        Controls,
        MiniMap,
        Handle,
        Position,
        useNodesState,
        useEdgesState,
        useReactFlow,
        ReactFlowProvider,
        MarkerType,
        BaseEdge,
        getBezierPath,
        useOnSelectionChange,
    } = RF;

    // ─── Constants ────────────────────────────────────────────────
    const GRID_SIZE = 20;
    const DEBOUNCE_MS = 300;

    const CATEGORY_COLORS = {
        'Core': { bg: '#1e3a5f', border: '#3b82f6', accent: '#60a5fa' },
        'Integration': { bg: '#1a3d2e', border: '#22c55e', accent: '#4ade80' },
        'AI/Agents': { bg: '#2d1f4e', border: '#a855f7', accent: '#c084fc' },
        'Data': { bg: '#3d2a0f', border: '#f97316', accent: '#fb923c' },
        'HTTP': { bg: '#0f3d3d', border: '#14b8a6', accent: '#2dd4bf' },
        'Events': { bg: '#3d3a0f', border: '#eab308', accent: '#facc15' },
        'Human Tasks': { bg: '#3d1f2e', border: '#ec4899', accent: '#f472b6' },
    };

    const STATUS_COLORS = {
        idle: '#6b7280',
        running: '#eab308',
        success: '#22c55e',
        error: '#ef4444',
    };

    const CATEGORY_ICONS = {
        'Core': '⚙',
        'Integration': '🔗',
        'AI/Agents': '🤖',
        'Data': '📊',
        'HTTP': '🌐',
        'Events': '⚡',
        'Human Tasks': '👤',
    };

    function isSyntheticNodeData(data) {
        return data?.config?.__syntheticKind === 'container-exit';
    }

    function getSemanticEdgeKind(edge) {
        return edge?.sourceHandle ?? edge?.kind ?? edge?.data?.kind ?? null;
    }

    function getEdgeDisplayLabel(kind, explicitLabel) {
        if (explicitLabel) return explicitLabel;
        if (!kind || kind === 'output' || kind === 'input' || kind.startsWith('output-')) return null;
        return kind;
    }

    function createHandle(type, position, id, style, key) {
        return h(Handle, {
            key,
            type,
            position,
            id,
            style,
        });
    }

    function createHandleLabel(key, text, style) {
        return h('div', { key, style }, text);
    }

    function toReactFlowNode(node) {
        const synthetic = isSyntheticNodeData({ config: node.config || {}, type: node.type });
        return {
            id: node.id,
            type: 'workflowNode',
            position: { x: node.x || 0, y: node.y || 0 },
            selectable: !synthetic,
            draggable: !synthetic,
            deletable: !synthetic,
            connectable: !synthetic,
            focusable: !synthetic,
            data: {
                type: node.type,
                label: node.label,
                icon: node.icon,
                category: node.category,
                color: node.color,
                config: node.config || {},
                runStatus: node.runStatus || 'idle',
            },
        };
    }

    function toReactFlowEdge(edge) {
        const kind = getSemanticEdgeKind(edge);
        return {
            id: edge.id,
            source: edge.source,
            target: edge.target,
            sourceHandle: kind || 'output',
            targetHandle: edge.targetHandle || 'input',
            type: 'workflowEdge',
            markerEnd: { type: MarkerType.ArrowClosed, color: '#6b7280' },
            data: {
                label: getEdgeDisplayLabel(kind, edge.label || ''),
                kind,
                animated: edge.animated || false,
            },
        };
    }

    function reseedNextNodeId(nodes) {
        return (nodes || []).reduce((max, node) => {
            const match = /^node_(\d+)$/i.exec(node?.id || '');
            return match ? Math.max(max, Number(match[1])) : max;
        }, 0) + 1;
    }

    function allowsMultipleOutputs(nodeType, handleId) {
        return nodeType === 'parallel' && typeof handleId === 'string' && handleId.startsWith('output-');
    }

    function normalizeNodeType(nodeType) {
        return (nodeType || '').trim().toLowerCase();
    }

    function normalizeHandleId(handleId) {
        return handleId && String(handleId).trim() ? String(handleId).trim() : 'output';
    }

    function isParallelBranchHandle(handleId) {
        return /^output-\d+$/i.test(handleId || '');
    }

    function isSupportedSourceHandle(nodeData, handleId) {
        const normalizedType = normalizeNodeType(nodeData?.type);
        const normalizedHandle = normalizeHandleId(handleId).toLowerCase();

        switch (normalizedType) {
            case 'conditional':
                return normalizedHandle === 'then' || normalizedHandle === 'else' || normalizedHandle === 'continue';
            case 'trycatch':
                return normalizedHandle === 'try' || normalizedHandle === 'finally' || normalizedHandle === 'continue';
            case 'timeout':
                return normalizedHandle === 'inner' || normalizedHandle === 'continue';
            case 'retry':
            case 'foreach':
            case 'while':
            case 'dowhile':
            case 'saga':
                return normalizedHandle === 'body' || normalizedHandle === 'continue';
            case 'parallel':
                return normalizedHandle === 'continue' || isParallelBranchHandle(normalizedHandle);
            default:
                return normalizedHandle === 'output';
        }
    }

    // ─── Dagre Layout ─────────────────────────────────────────────
    function autoLayoutNodes(nodes, edges) {
        if (typeof dagre === 'undefined' || nodes.length === 0) return nodes;
        const g = new dagre.graphlib.Graph();
        g.setDefaultEdgeLabel(() => ({}));
        g.setGraph({ rankdir: 'TB', nodesep: 60, ranksep: 80, marginx: 40, marginy: 40 });
        nodes.forEach(n => g.setNode(n.id, { width: 200, height: 70 }));
        edges.forEach(e => g.setEdge(e.source, e.target));
        dagre.layout(g);
        return nodes.map(n => {
            const pos = g.node(n.id);
            return { ...n, position: { x: pos.x - 100, y: pos.y - 35 } };
        });
    }

    // ─── History (Undo/Redo) ──────────────────────────────────────
    class HistoryManager {
        constructor(maxSize = 50) {
            this.stack = [];
            this.pointer = -1;
            this.maxSize = maxSize;
        }
        push(state) {
            this.stack = this.stack.slice(0, this.pointer + 1);
            this.stack.push(JSON.parse(JSON.stringify(state)));
            if (this.stack.length > this.maxSize) this.stack.shift();
            this.pointer = this.stack.length - 1;
        }
        undo() {
            if (this.pointer > 0) { this.pointer--; return JSON.parse(JSON.stringify(this.stack[this.pointer])); }
            return null;
        }
        redo() {
            if (this.pointer < this.stack.length - 1) { this.pointer++; return JSON.parse(JSON.stringify(this.stack[this.pointer])); }
            return null;
        }
    }

    // ─── Custom Node Component ────────────────────────────────────
    function WorkflowNode({ id, data, selected }) {
        const colors = CATEGORY_COLORS[data.category] || CATEGORY_COLORS['Core'];
        const status = data.runStatus || 'idle';
        const statusColor = STATUS_COLORS[status];
        const icon = data.icon || CATEGORY_ICONS[data.category] || '📦';
        const type = (data.type || '').toLowerCase();
        const isSynthetic = isSyntheticNodeData(data);

        const nodeStyle = {
            background: colors.bg,
            border: `2px solid ${selected ? '#fff' : colors.border}`,
            borderRadius: '10px',
            padding: '10px 14px',
            minWidth: '180px',
            color: '#f3f4f6',
            fontFamily: 'Inter, system-ui, sans-serif',
            position: 'relative',
            boxShadow: selected
                ? `0 0 12px ${colors.accent}66`
                : '0 2px 8px rgba(0,0,0,0.3)',
            transition: 'box-shadow 0.2s, border-color 0.2s',
        };

        if (isSynthetic) {
            nodeStyle.minWidth = '120px';
            nodeStyle.opacity = 0.7;
            nodeStyle.pointerEvents = 'none';
        }

        if (status === 'running') {
            nodeStyle.animation = 'wf-pulse 1.5s ease-in-out infinite';
        } else if (status === 'error') {
            nodeStyle.border = '2px solid #ef4444';
            nodeStyle.animation = 'wf-shake 0.5s ease-in-out';
        }

        const accentBar = h('div', {
            style: {
                position: 'absolute', left: 0, top: 0, bottom: 0, width: '4px',
                borderRadius: '10px 0 0 10px', background: colors.border,
            }
        });

        const statusIndicator = h('div', {
            style: {
                position: 'absolute', top: '6px', right: '8px',
                width: '8px', height: '8px', borderRadius: '50%',
                background: statusColor,
                boxShadow: status === 'running' ? `0 0 6px ${statusColor}` : 'none',
            }
        });

        const successBadge = status === 'success' ? h('div', {
            style: {
                position: 'absolute', top: '-6px', right: '-6px',
                width: '18px', height: '18px', borderRadius: '50%',
                background: '#22c55e', display: 'flex', alignItems: 'center',
                justifyContent: 'center', fontSize: '10px', fontWeight: 'bold',
                border: '2px solid #1f2937',
            }
        }, '✓') : null;

        const header = h('div', { style: { display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '4px' } },
            h('span', { style: { fontSize: '16px' } }, icon),
            h('div', { style: { fontSize: '12px', fontWeight: '600', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '130px' } },
                data.config?.label || data.label || data.type),
        );

        const subtitle = h('div', {
            style: { fontSize: '10px', color: '#9ca3af', marginLeft: '26px' }
        }, data.type);

        const configSummary = data.config && Object.keys(data.config).length > 0
            ? h('div', { style: { fontSize: '9px', color: '#6b7280', marginLeft: '26px', marginTop: '2px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', maxWidth: '150px' } },
                Object.entries(data.config).filter(([k]) => k !== 'label').map(([k, v]) => `${k}: ${v}`).join(', ').substring(0, 40))
            : null;

        // Handles
        const inputHandle = h(Handle, {
            type: 'target', position: Position.Top, id: 'input',
            isConnectable: !isSynthetic,
            style: { background: '#6b7280', border: '2px solid #374151', width: '10px', height: '10px' },
        });

        const handles = [inputHandle];
        const sourceHandleStyle = { border: '2px solid #374151', width: '10px', height: '10px' };
        if (isSynthetic) {
            handles.push(createHandle('source', Position.Bottom, 'output', { ...sourceHandleStyle, background: '#6b7280' }, 'source-output'));
        } else if (type === 'conditional') {
            handles.push(createHandle('source', Position.Bottom, 'then', { ...sourceHandleStyle, background: '#22c55e', left: '25%' }, 'source-then'));
            handles.push(createHandle('source', Position.Bottom, 'continue', { ...sourceHandleStyle, background: '#9ca3af', left: '50%' }, 'source-continue'));
            handles.push(createHandle('source', Position.Bottom, 'else', { ...sourceHandleStyle, background: '#ef4444', left: '75%' }, 'source-else'));
            handles.push(createHandleLabel('label-then', 'then', { position: 'absolute', bottom: '-16px', left: '15%', fontSize: '8px', color: '#22c55e' }));
            handles.push(createHandleLabel('label-continue', 'continue', { position: 'absolute', bottom: '-16px', left: '38%', fontSize: '8px', color: '#9ca3af' }));
            handles.push(createHandleLabel('label-else', 'else', { position: 'absolute', bottom: '-16px', left: '68%', fontSize: '8px', color: '#ef4444' }));
        } else if (type === 'trycatch') {
            handles.push(createHandle('source', Position.Bottom, 'try', { ...sourceHandleStyle, background: '#22c55e', left: '25%' }, 'source-try'));
            handles.push(createHandle('source', Position.Bottom, 'continue', { ...sourceHandleStyle, background: '#9ca3af', left: '50%' }, 'source-continue'));
            handles.push(createHandle('source', Position.Bottom, 'finally', { ...sourceHandleStyle, background: '#f59e0b', left: '75%' }, 'source-finally'));
            handles.push(createHandleLabel('label-try', 'try', { position: 'absolute', bottom: '-16px', left: '17%', fontSize: '8px', color: '#22c55e' }));
            handles.push(createHandleLabel('label-continue', 'continue', { position: 'absolute', bottom: '-16px', left: '38%', fontSize: '8px', color: '#9ca3af' }));
            handles.push(createHandleLabel('label-finally', 'finally', { position: 'absolute', bottom: '-16px', left: '66%', fontSize: '8px', color: '#f59e0b' }));
        } else if (type === 'timeout') {
            handles.push(createHandle('source', Position.Bottom, 'inner', { ...sourceHandleStyle, background: '#22c55e', left: '35%' }, 'source-inner'));
            handles.push(createHandle('source', Position.Bottom, 'continue', { ...sourceHandleStyle, background: '#9ca3af', left: '65%' }, 'source-continue'));
            handles.push(createHandleLabel('label-inner', 'inner', { position: 'absolute', bottom: '-16px', left: '25%', fontSize: '8px', color: '#22c55e' }));
            handles.push(createHandleLabel('label-continue', 'continue', { position: 'absolute', bottom: '-16px', left: '53%', fontSize: '8px', color: '#9ca3af' }));
        } else if (['retry', 'foreach', 'while', 'dowhile', 'saga'].includes(type)) {
            handles.push(createHandle('source', Position.Bottom, 'body', { ...sourceHandleStyle, background: '#22c55e', left: '35%' }, 'source-body'));
            handles.push(createHandle('source', Position.Bottom, 'continue', { ...sourceHandleStyle, background: '#9ca3af', left: '65%' }, 'source-continue'));
            handles.push(createHandleLabel('label-body', 'body', { position: 'absolute', bottom: '-16px', left: '26%', fontSize: '8px', color: '#22c55e' }));
            handles.push(createHandleLabel('label-continue', 'continue', { position: 'absolute', bottom: '-16px', left: '53%', fontSize: '8px', color: '#9ca3af' }));
        } else if (type === 'parallel') {
            const branchCount = Math.max(1, Number.parseInt(data.config?.__parallelBranchCount || '3', 10) || 3);
            for (let branchIndex = 0; branchIndex < branchCount; branchIndex++) {
                const left = branchCount === 1
                    ? 40
                    : 12 + ((66 * branchIndex) / Math.max(1, branchCount - 1));
                const handleId = `output-${branchIndex + 1}`;
                handles.push(createHandle('source', Position.Bottom, handleId, { ...sourceHandleStyle, background: colors.border, left: `${left}%` }, `source-${handleId}`));
            }
            handles.push(createHandle('source', Position.Bottom, 'continue', { ...sourceHandleStyle, background: '#9ca3af', left: '88%' }, 'source-continue'));
            handles.push(createHandleLabel('label-continue', 'continue', { position: 'absolute', bottom: '-16px', left: '68%', fontSize: '8px', color: '#9ca3af' }));
        } else {
            handles.push(createHandle('source', Position.Bottom, 'output', { ...sourceHandleStyle, background: colors.border }, 'source-output'));
        }

        return h('div', { style: nodeStyle, 'data-node-id': id, 'data-node-type': data.type, 'data-node-synthetic': isSynthetic ? 'true' : 'false' },
            accentBar, statusIndicator, successBadge, header, subtitle, configSummary, ...handles
        );
    }

    // ─── Custom Edge with Label ───────────────────────────────────
    function WorkflowEdge({ id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, style, markerEnd }) {
        const [edgePath, labelX, labelY] = getBezierPath({ sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition });
        const isAnimated = data?.animated;
        const edgeStyle = {
            ...style,
            stroke: isAnimated ? '#3b82f6' : '#6b7280',
            strokeWidth: 2,
            strokeDasharray: isAnimated ? '5,5' : 'none',
            animation: isAnimated ? 'wf-edge-flow 1s linear infinite' : 'none',
        };

        return h(Fragment, null,
            h(BaseEdge, { path: edgePath, markerEnd, style: edgeStyle }),
            data?.label ? h('text', {
                x: labelX, y: labelY - 8,
                style: { fontSize: '10px', fill: '#9ca3af' },
                textAnchor: 'middle', dominantBaseline: 'middle',
            }, data.label) : null
        );
    }

    // ─── Node Types Registry ──────────────────────────────────────
    const nodeTypes = {
        workflowNode: WorkflowNode,
    };

    const edgeTypes = {
        workflowEdge: WorkflowEdge,
    };

    // ─── Main Editor Component ────────────────────────────────────
    let _editorInstance = null;

    function WorkflowEditor({ initialNodes, initialEdges, dotNetRef, containerId }) {
        const [nodes, setNodes, onNodesChange] = useNodesState(initialNodes);
        const [edges, setEdges, onEdgesChange] = useEdgesState(initialEdges);
        const reactFlowInstance = useReactFlow();
        const historyRef = useRef(new HistoryManager());
        const clipboardRef = useRef([]);
        const debounceTimerRef = useRef(null);
        const nextIdRef = useRef(1);
        const dotNetRefRef = useRef(dotNetRef);

        const invokeDotNet = useCallback((method, ...args) => {
            const ref = dotNetRefRef.current;
            if (!ref) return;

            try {
                const pending = ref.invokeMethodAsync(method, ...args);
                if (pending && typeof pending.catch === 'function') {
                    pending.catch(() => { });
                }
            } catch {
                // Best-effort interop only.
            }
        }, []);

        // Expose instance for external API
        useEffect(() => {
            _editorInstance = {
                getNodes: () => nodes,
                getEdges: () => edges,
                setNodes,
                setEdges,
                onNodesChange,
                onEdgesChange,
                reactFlowInstance,
                historyRef,
                clipboardRef,
                nextIdRef,
                dotNetRefRef,
            };
            return () => { _editorInstance = null; };
        });

        // Update refs
        useEffect(() => { dotNetRefRef.current = dotNetRef; }, [dotNetRef]);

        // Push initial state to history
        useEffect(() => {
            historyRef.current.push({ nodes: initialNodes, edges: initialEdges });
            nextIdRef.current = reseedNextNodeId(initialNodes);
        }, []);

        // Debounced canvas change notification
        const notifyCanvasChanged = useCallback(() => {
            if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
            debounceTimerRef.current = setTimeout(() => {
                invokeDotNet('OnCanvasChanged');
            }, DEBOUNCE_MS);
        }, [invokeDotNet]);

        // Push history on meaningful changes
        const pushHistory = useCallback((n, e) => {
            historyRef.current.push({ nodes: n, edges: e });
            notifyCanvasChanged();
        }, [notifyCanvasChanged]);

        const onConnect = useCallback((connection) => {
            // Connection validation: prevent connecting output to output or input to input
            const edgeId = 'edge_' + Date.now();
            const kind = connection.sourceHandle || null;
            const sourceNode = nodes.find(n => n.id === connection.source);
            const sourceType = (sourceNode?.data?.type || '').toLowerCase();
            const handleId = kind || 'output';
            const newEdge = {
                id: edgeId,
                source: connection.source,
                target: connection.target,
                sourceHandle: handleId,
                targetHandle: connection.targetHandle,
                type: 'workflowEdge',
                markerEnd: { type: MarkerType.ArrowClosed, color: '#6b7280' },
                data: { label: getEdgeDisplayLabel(kind, null), kind },
            };
            setEdges(eds => {
                const updated = allowsMultipleOutputs(sourceType, handleId)
                    ? [...eds, newEdge]
                    : [
                        ...eds.filter(e => !(e.source === connection.source && ((e.sourceHandle || 'output') === handleId))),
                        newEdge
                    ];
                setTimeout(() => pushHistory(nodes, updated), 0);
                return updated;
            });
            invokeDotNet('OnEdgeCreated', connection.source, connection.target, connection.sourceHandle || 'output');
        }, [setEdges, pushHistory, nodes, invokeDotNet]);

        const onEdgesDelete = useCallback((deletedEdges) => {
            deletedEdges.forEach(e => {
                invokeDotNet('OnEdgeRemoved', e.id);
            });
            notifyCanvasChanged();
        }, [notifyCanvasChanged, invokeDotNet]);

        const onNodesDelete = useCallback((deletedNodes) => {
            deletedNodes.forEach(n => {
                invokeDotNet('OnNodeRemoved', n.id);
            });
            notifyCanvasChanged();
        }, [notifyCanvasChanged, invokeDotNet]);

        const onNodeClick = useCallback((event, node) => {
            invokeDotNet('OnNodeSelected', node.id, node.data?.type || null, node.data?.config || null);
        }, [invokeDotNet]);

        const onPaneClick = useCallback(() => {
            invokeDotNet('OnNodeSelected', null, null, null);
        }, [invokeDotNet]);

        const onNodeDragStop = useCallback(() => {
            notifyCanvasChanged();
        }, [notifyCanvasChanged]);

        // Selection change
        const onSelectionChange = useCallback(({ nodes: selNodes }) => {
            if (dotNetRefRef.current && selNodes) {
                invokeDotNet('OnSelectionChanged', selNodes.map(n => n.id));
            }
        }, [invokeDotNet]);

        useOnSelectionChange({ onChange: onSelectionChange });

        // Drag and drop from palette
        const onDragOver = useCallback((event) => {
            event.preventDefault();
            event.dataTransfer.dropEffect = 'copy';
        }, []);

        const onDrop = useCallback((event) => {
            event.preventDefault();
            const stepType = event.dataTransfer.getData('stepType');
            const stepName = event.dataTransfer.getData('stepName');
            const stepIcon = event.dataTransfer.getData('stepIcon');
            const stepCategory = event.dataTransfer.getData('stepCategory');
            const stepColor = event.dataTransfer.getData('stepColor');
            if (!stepType) return;

            const position = reactFlowInstance.screenToFlowPosition({ x: event.clientX, y: event.clientY });
            position.x = Math.round(position.x / GRID_SIZE) * GRID_SIZE;
            position.y = Math.round(position.y / GRID_SIZE) * GRID_SIZE;

            const id = 'node_' + (nextIdRef.current++);
            const newNode = {
                id,
                type: 'workflowNode',
                position,
                data: {
                    type: stepType,
                    label: stepName,
                    icon: stepIcon,
                    category: stepCategory,
                    color: stepColor,
                    config: {},
                    runStatus: 'idle',
                },
            };

            setNodes(nds => {
                const updated = [...nds, newNode];
                setTimeout(() => pushHistory(updated, edges), 0);
                return updated;
            });

            invokeDotNet('OnNodeAdded', id, stepType, position.x, position.y);
        }, [reactFlowInstance, setNodes, pushHistory, edges, invokeDotNet]);

        // Keyboard shortcuts
        useEffect(() => {
            const handler = (e) => {
                if (e.ctrlKey && e.key === 'z' && !e.shiftKey) {
                    e.preventDefault();
                    window.workflowEditor.undo();
                } else if (e.ctrlKey && (e.key === 'Z' || (e.key === 'z' && e.shiftKey))) {
                    e.preventDefault();
                    window.workflowEditor.redo();
                } else if (e.ctrlKey && e.key === 'c') {
                    window.workflowEditor.copySelection();
                } else if (e.ctrlKey && e.key === 'v') {
                    window.workflowEditor.pasteSelection();
                }
            };
            document.addEventListener('keydown', handler);
            return () => document.removeEventListener('keydown', handler);
        }, []);

        // Connection validation
        const isValidConnection = useCallback((connection) => {
            if (connection.source === connection.target) return false;

            const sourceNode = nodes.find(n => n.id === connection.source);
            const targetNode = nodes.find(n => n.id === connection.target);
            if (!sourceNode || !targetNode) return false;
            if (isSyntheticNodeData(sourceNode.data) || isSyntheticNodeData(targetNode.data)) return false;

            const handleId = normalizeHandleId(connection.sourceHandle);
            if (!isSupportedSourceHandle(sourceNode.data, handleId)) return false;

            const targetAlreadyConnected = edges.some(e => e.target === connection.target);
            if (targetAlreadyConnected) return false;

            return true;
        }, [edges, nodes]);

        const defaultEdgeOptions = useMemo(() => ({
            type: 'workflowEdge',
            markerEnd: { type: MarkerType.ArrowClosed, color: '#6b7280' },
            data: {},
        }), []);

        return h(ReactFlowComponent, {
            nodes,
            edges,
            onNodesChange,
            onEdgesChange,
            onConnect,
            onEdgesDelete,
            onNodesDelete,
            onNodeClick,
            onPaneClick,
            onNodeDragStop,
            onDragOver,
            onDrop,
            nodeTypes,
            edgeTypes,
            defaultEdgeOptions,
            isValidConnection,
            snapToGrid: true,
            snapGrid: [GRID_SIZE, GRID_SIZE],
            fitView: true,
            deleteKeyCode: ['Backspace', 'Delete'],
            selectionOnDrag: true,
            selectionMode: 'partial',
            multiSelectionKeyCode: 'Shift',
            proOptions: { hideAttribution: true },
            style: { background: '#111827' },
        },
            h(Background, { variant: 'dots', gap: GRID_SIZE, size: 1, color: '#374151' }),
            h(Controls, {
                showInteractive: true,
                style: { background: '#1f2937', border: '1px solid #374151', borderRadius: '8px' },
            }),
            h(MiniMap, {
                nodeColor: (n) => {
                    const cat = n.data?.category;
                    return CATEGORY_COLORS[cat]?.border || '#6b7280';
                },
                maskColor: 'rgba(0,0,0,0.7)',
                style: { background: '#1f2937', border: '1px solid #374151', borderRadius: '8px' },
                position: 'bottom-right',
            }),
        );
    }

    // ─── Mount/Unmount ────────────────────────────────────────────
    let _root = null;
    let _dotNetRef = null;

    function mountEditor(containerId, initialNodes, initialEdges, dotNetRef) {
        const container = document.getElementById(containerId);
        if (!container) return;

        _dotNetRef = dotNetRef;
        container.style.width = '100%';
        container.style.height = '100%';

        // Map raw node data to React Flow format
        const rfNodes = (initialNodes || []).map(toReactFlowNode);
        const rfEdges = (initialEdges || []).map(toReactFlowEdge);

        const app = h(ReactFlowProvider, null,
            h(WorkflowEditor, { initialNodes: rfNodes, initialEdges: rfEdges, dotNetRef, containerId })
        );

        _root = ReactDOM.createRoot(container);
        _root.render(app);
    }

    function unmountEditor() {
        if (_root) { _root.unmount(); _root = null; }
        _editorInstance = null;
        _dotNetRef = null;
    }

    // ─── Public API (matches Blazor interop contract) ─────────────
    window.workflowEditor = {
        initialize(containerId, ref, initialNodes, initialEdges) {
            unmountEditor();
            mountEditor(containerId, initialNodes || [], initialEdges || [], ref);
        },

        handlePaletteDragStart(event, type, name, icon, category, color) {
            event.dataTransfer.setData('stepType', type);
            event.dataTransfer.setData('stepName', name);
            event.dataTransfer.setData('stepIcon', icon);
            event.dataTransfer.setData('stepCategory', category);
            event.dataTransfer.setData('stepColor', color);
            event.dataTransfer.effectAllowed = 'copy';
        },

        addNode(nodeData) {
            if (!_editorInstance) return null;
            const id = 'node_' + (_editorInstance.nextIdRef.current++);
            const newNode = {
                id,
                type: 'workflowNode',
                position: { x: nodeData.x || 100, y: nodeData.y || 100 },
                data: {
                    type: nodeData.type,
                    label: nodeData.label,
                    icon: nodeData.icon,
                    category: nodeData.category,
                    color: nodeData.color,
                    config: nodeData.config || {},
                    runStatus: 'idle',
                },
            };
            _editorInstance.setNodes(nds => [...nds, newNode]);
            return id;
        },

        removeNode(nodeId) {
            if (!_editorInstance) return;
            const node = _editorInstance.reactFlowInstance.getNodes().find(n => n.id === nodeId);
            if (node && isSyntheticNodeData(node.data)) return;
            _editorInstance.setNodes(nds => nds.filter(n => n.id !== nodeId));
            _editorInstance.setEdges(eds => eds.filter(e => e.source !== nodeId && e.target !== nodeId));
        },

        updateNode(nodeId, config) {
            if (!_editorInstance) return;
            _editorInstance.setNodes(nds => nds.map(n =>
                n.id === nodeId ? { ...n, data: { ...n.data, config: { ...n.data.config, ...config } } } : n
            ));
        },

        getWorkflowDefinition() {
            if (!_editorInstance) return { nodes: [], edges: [] };
            const inst = _editorInstance.reactFlowInstance;
            const nodes = inst.getNodes().map(n => ({
                id: n.id,
                type: n.data.type,
                label: n.data.label,
                icon: n.data.icon,
                category: n.data.category,
                color: n.data.color,
                x: n.position.x,
                y: n.position.y,
                config: n.data.config,
            }));
            const edges = inst.getEdges().map(e => ({
                id: e.id,
                source: e.source,
                target: e.target,
                sourceHandle: e.sourceHandle ?? null,
                kind: getSemanticEdgeKind(e),
                label: e.data?.label || '',
            }));
            return { nodes, edges };
        },

        getAllNodes() {
            if (!_editorInstance) return [];
            return _editorInstance.reactFlowInstance.getNodes()
                .filter(n => !isSyntheticNodeData(n.data))
                .map(n => ({
                    id: n.id,
                    type: n.data.type,
                    label: n.data?.config?.label || n.data.label || '',
                    icon: n.data.icon || '⬡',
                    category: n.data.category || '',
                    color: n.data.color || '#4b5563',
                }));
        },

        getAllEdges() {
            if (!_editorInstance) return [];
            const nodes = _editorInstance.reactFlowInstance.getNodes();
            return _editorInstance.reactFlowInstance.getEdges()
                .filter(e => {
                    const sourceNode = nodes.find(n => n.id === e.source);
                    const targetNode = nodes.find(n => n.id === e.target);
                    return !isSyntheticNodeData(sourceNode?.data) && !isSyntheticNodeData(targetNode?.data);
                })
                .map(e => ({
                    id: e.id,
                    source: e.source,
                    target: e.target,
                    label: e.data?.label || getSemanticEdgeKind(e) || null,
                }));
        },

        setWorkflowDefinition(newNodes, newEdges) {
            if (!_editorInstance) return;
            const rfNodes = (newNodes || []).map(toReactFlowNode);
            const rfEdges = (newEdges || []).map(toReactFlowEdge);
            _editorInstance.setNodes(rfNodes);
            _editorInstance.setEdges(rfEdges);
            _editorInstance.nextIdRef.current = reseedNextNodeId(rfNodes);
            _editorInstance.historyRef.current = new HistoryManager();
            _editorInstance.historyRef.current.push({ nodes: rfNodes, edges: rfEdges });
        },

        fitView() {
            if (_editorInstance?.reactFlowInstance) {
                _editorInstance.reactFlowInstance.fitView({ padding: 0.2, duration: 300 });
            }
        },

        setRunStatus(nodeId, status) {
            if (!_editorInstance) return;
            _editorInstance.setNodes(nds => nds.map(n =>
                n.id === nodeId ? { ...n, data: { ...n.data, runStatus: status } } : n
            ));
            // Animate edges from this node when running
            if (status === 'running') {
                _editorInstance.setEdges(eds => eds.map(e =>
                    e.source === nodeId ? { ...e, data: { ...e.data, animated: true } } : e
                ));
            } else {
                _editorInstance.setEdges(eds => eds.map(e =>
                    e.source === nodeId ? { ...e, data: { ...e.data, animated: false } } : e
                ));
            }
        },

        updateNodeStatus(stepName, status) {
            if (!_editorInstance) return;
            const normalizedStatus = status === 'Running'
                ? 'running'
                : status === 'Completed'
                    ? 'success'
                    : status === 'Failed'
                        ? 'error'
                        : 'idle';
            _editorInstance.setNodes(nds => nds.map(n =>
                (n.data?.config?.label || n.data?.label) === stepName
                    ? { ...n, data: { ...n.data, runStatus: normalizedStatus } }
                    : n
            ));
        },

        undo() {
            if (!_editorInstance) return;
            const state = _editorInstance.historyRef.current.undo();
            if (state) {
                _editorInstance.setNodes(state.nodes);
                _editorInstance.setEdges(state.edges);
            }
        },

        redo() {
            if (!_editorInstance) return;
            const state = _editorInstance.historyRef.current.redo();
            if (state) {
                _editorInstance.setNodes(state.nodes);
                _editorInstance.setEdges(state.edges);
            }
        },

        autoLayout() {
            if (!_editorInstance) return;
            const inst = _editorInstance.reactFlowInstance;
            const nodes = inst.getNodes();
            const edges = inst.getEdges();
            const laid = autoLayoutNodes(nodes, edges);
            _editorInstance.setNodes(laid);
            setTimeout(() => inst.fitView({ padding: 0.2, duration: 300 }), 50);
        },

        getSelectedNodes() {
            if (!_editorInstance) return [];
            return _editorInstance.reactFlowInstance.getNodes().filter(n => n.selected).map(n => n.id);
        },

        copySelection() {
            if (!_editorInstance) return;
            const selected = _editorInstance.reactFlowInstance.getNodes().filter(n => n.selected);
            _editorInstance.clipboardRef.current = JSON.parse(JSON.stringify(selected));
        },

        pasteSelection() {
            if (!_editorInstance || _editorInstance.clipboardRef.current.length === 0) return;
            const offset = 40;
            const newNodes = _editorInstance.clipboardRef.current.map(n => ({
                ...n,
                id: 'node_' + (_editorInstance.nextIdRef.current++),
                position: { x: n.position.x + offset, y: n.position.y + offset },
                selected: true,
            }));
            _editorInstance.setNodes(nds => [...nds.map(n => ({ ...n, selected: false })), ...newNodes]);
        },

        zoomIn() {
            if (_editorInstance?.reactFlowInstance) _editorInstance.reactFlowInstance.zoomIn({ duration: 200 });
        },

        zoomOut() {
            if (_editorInstance?.reactFlowInstance) _editorInstance.reactFlowInstance.zoomOut({ duration: 200 });
        },

        zoomToFit() {
            this.fitView();
        },

        deleteSelected() {
            if (!_editorInstance) return;
            const selectedNodeIds = _editorInstance.reactFlowInstance
                .getNodes()
                .filter(n => n.selected && !isSyntheticNodeData(n.data))
                .map(n => n.id);
            if (selectedNodeIds.length === 0) return;

            _editorInstance.setNodes(nds => nds.filter(n => !selectedNodeIds.includes(n.id)));
            _editorInstance.setEdges(eds => eds.filter(e => !selectedNodeIds.includes(e.source) && !selectedNodeIds.includes(e.target)));
            selectedNodeIds.forEach(nodeId => {
                const ref = _editorInstance.dotNetRefRef.current;
                if (ref) {
                    try { ref.invokeMethodAsync('OnNodeRemoved', nodeId); } catch { }
                }
            });
        },

        focusNode(nodeId) {
            if (!_editorInstance) return;
            const inst = _editorInstance.reactFlowInstance;
            const node = inst.getNodes().find(n => n.id === nodeId && !isSyntheticNodeData(n.data));
            if (!node) return;

            _editorInstance.setNodes(nds => nds.map(n => ({ ...n, selected: n.id === nodeId })));
            if (typeof inst.setCenter === 'function') {
                inst.setCenter(node.position.x + 100, node.position.y + 35, { zoom: 1.1, duration: 300 });
            } else if (typeof inst.fitView === 'function') {
                setTimeout(() => inst.fitView({ padding: 0.4, duration: 300 }), 0);
            }

            const ref = _editorInstance.dotNetRefRef.current;
            if (ref) {
                try { ref.invokeMethodAsync('OnNodeSelected', node.id, node.data?.type || null, node.data?.config || null); } catch { }
            }
        },

        selectNodeByName(name) {
            if (!_editorInstance) return;
            const node = _editorInstance.reactFlowInstance
                .getNodes()
                .find(n => !isSyntheticNodeData(n.data) && (n.data?.config?.label || n.data?.label) === name);
            if (node) {
                this.focusNode(node.id);
            }
        },

        getNodeConnections(nodeId) {
            if (!_editorInstance) return { inputs: [], outputs: [] };
            const inst = _editorInstance.reactFlowInstance;
            const nodes = inst.getNodes();
            const edges = inst.getEdges();

            const inputs = edges
                .filter(e => e.target === nodeId)
                .map(e => {
                    const sourceNode = nodes.find(n => n.id === e.source);
                    if (!sourceNode || isSyntheticNodeData(sourceNode.data)) return null;
                    const kind = getSemanticEdgeKind(e);
                    return {
                        id: e.source,
                        label: sourceNode.data?.config?.label || sourceNode.data?.label || e.source,
                        edgeLabel: e.data?.label || kind || null,
                    };
                })
                .filter(Boolean);

            const outputs = edges
                .filter(e => e.source === nodeId)
                .map(e => {
                    const targetNode = nodes.find(n => n.id === e.target);
                    if (!targetNode || isSyntheticNodeData(targetNode.data)) return null;
                    const kind = getSemanticEdgeKind(e);
                    return {
                        id: e.target,
                        label: targetNode.data?.config?.label || targetNode.data?.label || e.target,
                        edgeLabel: e.data?.label || kind || null,
                    };
                })
                .filter(Boolean);

            return { inputs, outputs };
        },

        destroy() {
            unmountEditor();
        },
    };
})();
