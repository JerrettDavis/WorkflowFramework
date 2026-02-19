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
        const isConditional = data.type === 'IfCondition' || data.type === 'Switch';
        const isParallel = data.type === 'Parallel' || data.type === 'ForEach';

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
            style: { background: '#6b7280', border: '2px solid #374151', width: '10px', height: '10px' },
        });

        const handles = [inputHandle];
        if (isConditional) {
            handles.push(h(Handle, {
                type: 'source', position: Position.Bottom, id: 'then',
                style: { background: '#22c55e', border: '2px solid #374151', width: '10px', height: '10px', left: '35%' },
            }));
            handles.push(h(Handle, {
                type: 'source', position: Position.Bottom, id: 'else',
                style: { background: '#ef4444', border: '2px solid #374151', width: '10px', height: '10px', left: '65%' },
            }));
            // Labels for then/else
            handles.push(h('div', { style: { position: 'absolute', bottom: '-16px', left: '25%', fontSize: '8px', color: '#22c55e' } }, 'then'));
            handles.push(h('div', { style: { position: 'absolute', bottom: '-16px', left: '57%', fontSize: '8px', color: '#ef4444' } }, 'else'));
        } else if (isParallel) {
            handles.push(h(Handle, {
                type: 'source', position: Position.Bottom, id: 'output-1',
                style: { background: colors.border, border: '2px solid #374151', width: '10px', height: '10px', left: '25%' },
            }));
            handles.push(h(Handle, {
                type: 'source', position: Position.Bottom, id: 'output-2',
                style: { background: colors.border, border: '2px solid #374151', width: '10px', height: '10px', left: '50%' },
            }));
            handles.push(h(Handle, {
                type: 'source', position: Position.Bottom, id: 'output-3',
                style: { background: colors.border, border: '2px solid #374151', width: '10px', height: '10px', left: '75%' },
            }));
        } else {
            handles.push(h(Handle, {
                type: 'source', position: Position.Bottom, id: 'output',
                style: { background: colors.border, border: '2px solid #374151', width: '10px', height: '10px' },
            }));
        }

        return h('div', { style: nodeStyle },
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
        }, []);

        // Debounced canvas change notification
        const notifyCanvasChanged = useCallback(() => {
            if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
            debounceTimerRef.current = setTimeout(() => {
                if (dotNetRefRef.current) {
                    try { dotNetRefRef.current.invokeMethodAsync('OnCanvasChanged'); } catch (e) { }
                }
            }, DEBOUNCE_MS);
        }, []);

        // Push history on meaningful changes
        const pushHistory = useCallback((n, e) => {
            historyRef.current.push({ nodes: n, edges: e });
            notifyCanvasChanged();
        }, [notifyCanvasChanged]);

        const onConnect = useCallback((connection) => {
            // Connection validation: prevent connecting output to output or input to input
            const edgeId = 'edge_' + Date.now();
            const newEdge = {
                id: edgeId,
                source: connection.source,
                target: connection.target,
                sourceHandle: connection.sourceHandle,
                targetHandle: connection.targetHandle,
                type: 'workflowEdge',
                markerEnd: { type: MarkerType.ArrowClosed, color: '#6b7280' },
                data: {},
            };
            setEdges(eds => {
                const updated = [...eds, newEdge];
                setTimeout(() => pushHistory(nodes, updated), 0);
                return updated;
            });
            if (dotNetRefRef.current) {
                try { dotNetRefRef.current.invokeMethodAsync('OnEdgeCreated', connection.source, connection.target, connection.sourceHandle || 'output'); } catch (e) { }
            }
        }, [setEdges, pushHistory, nodes]);

        const onEdgesDelete = useCallback((deletedEdges) => {
            deletedEdges.forEach(e => {
                if (dotNetRefRef.current) {
                    try { dotNetRefRef.current.invokeMethodAsync('OnEdgeRemoved', e.id); } catch (ex) { }
                }
            });
            notifyCanvasChanged();
        }, [notifyCanvasChanged]);

        const onNodesDelete = useCallback((deletedNodes) => {
            deletedNodes.forEach(n => {
                if (dotNetRefRef.current) {
                    try { dotNetRefRef.current.invokeMethodAsync('OnNodeRemoved', n.id); } catch (ex) { }
                }
            });
            notifyCanvasChanged();
        }, [notifyCanvasChanged]);

        const onNodeClick = useCallback((event, node) => {
            if (dotNetRefRef.current) {
                try { dotNetRefRef.current.invokeMethodAsync('OnNodeSelected', node.id, node.data?.type || null, node.data?.config || null); } catch (e) { }
            }
        }, []);

        const onPaneClick = useCallback(() => {
            if (dotNetRefRef.current) {
                try { dotNetRefRef.current.invokeMethodAsync('OnNodeSelected', null, null, null); } catch (e) { }
            }
        }, []);

        const onNodeDragStop = useCallback(() => {
            notifyCanvasChanged();
        }, [notifyCanvasChanged]);

        // Selection change
        const onSelectionChange = useCallback(({ nodes: selNodes }) => {
            if (dotNetRefRef.current && selNodes) {
                try { dotNetRefRef.current.invokeMethodAsync('OnSelectionChanged', selNodes.map(n => n.id)); } catch (e) { }
            }
        }, []);

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

            if (dotNetRefRef.current) {
                try { dotNetRefRef.current.invokeMethodAsync('OnNodeAdded', id, stepType, position.x, position.y); } catch (e) { }
            }
        }, [reactFlowInstance, setNodes, pushHistory, edges]);

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
            // Prevent self-connections
            if (connection.source === connection.target) return false;
            return true;
        }, []);

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
        const rfNodes = (initialNodes || []).map(n => ({
            id: n.id,
            type: 'workflowNode',
            position: { x: n.x || 0, y: n.y || 0 },
            data: {
                type: n.type,
                label: n.label,
                icon: n.icon,
                category: n.category,
                color: n.color,
                config: n.config || {},
                runStatus: n.runStatus || 'idle',
            },
        }));

        const rfEdges = (initialEdges || []).map(e => ({
            id: e.id,
            source: e.source,
            target: e.target,
            sourceHandle: e.sourceHandle || 'output',
            targetHandle: e.targetHandle || 'input',
            type: 'workflowEdge',
            markerEnd: { type: MarkerType.ArrowClosed, color: '#6b7280' },
            data: { label: e.label || '', animated: e.animated || false },
        }));

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
                sourceHandle: e.sourceHandle,
                label: e.data?.label || '',
            }));
            return { nodes, edges };
        },

        setWorkflowDefinition(newNodes, newEdges) {
            if (!_editorInstance) return;
            const rfNodes = (newNodes || []).map(n => ({
                id: n.id,
                type: 'workflowNode',
                position: { x: n.x || 0, y: n.y || 0 },
                data: {
                    type: n.type, label: n.label, icon: n.icon,
                    category: n.category, color: n.color,
                    config: n.config || {}, runStatus: 'idle',
                },
            }));
            const rfEdges = (newEdges || []).map(e => ({
                id: e.id,
                source: e.source, target: e.target,
                sourceHandle: e.sourceHandle || 'output',
                targetHandle: e.targetHandle || 'input',
                type: 'workflowEdge',
                markerEnd: { type: MarkerType.ArrowClosed, color: '#6b7280' },
                data: { label: e.label || '', animated: false },
            }));
            _editorInstance.setNodes(rfNodes);
            _editorInstance.setEdges(rfEdges);
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

        destroy() {
            unmountEditor();
        },
    };
})();
