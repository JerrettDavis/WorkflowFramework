// mermaid-render.js — helpers for Blazor ↔ Mermaid.js interop

window.mermaidRender = {
    /**
     * Render Mermaid diagram text into the element with the given id.
     * Returns the SVG string on success, or null on failure.
     */
    render: async function (elementId, diagramText) {
        try {
            if (!window.mermaid) return null;
            const element = document.getElementById(elementId);
            if (!element) return null;

            // Unique id per render to avoid Mermaid's dedup cache
            const uid = 'mermaid-' + Date.now() + '-' + Math.random().toString(36).slice(2);
            const { svg } = await window.mermaid.render(uid, diagramText);
            element.innerHTML = svg;
            return svg;
        } catch (err) {
            console.warn('mermaid render failed:', err);
            return null;
        }
    },

    /** Initialize Mermaid with dark theme. Idempotent. */
    init: function () {
        if (!window.mermaid) return;
        window.mermaid.initialize({
            startOnLoad: false,
            theme: 'dark',
            securityLevel: 'loose',
        });
    }
};
