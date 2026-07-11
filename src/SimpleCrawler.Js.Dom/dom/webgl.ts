// A layout-less renderer has no GPU, but map/3D libraries (Mapbox GL, Three.js, deck.gl) grab a WebGL
// context synchronously while constructing — `canvas.getContext("webgl2")`, then immediately querying
// limits, compiling shaders and creating buffers — and throw "Failed to initialize WebGL." when getContext
// returns null. That throw is uncaught inside the bundle, trips the SPA error boundary, and loses every
// anchor on the page. This hands back a stub context that never faults: create* calls return opaque truthy
// handles, capability queries report plausible desktop-GL limits, and shader compile / program link /
// framebuffer status all report success, so the library finishes initializing. Nothing is drawn — the map
// itself yields no anchors — but the surrounding page (its real navigation) renders.
//
// Opt-in (JsRenderOptions.EnableWebGl → __crawlerEnableWebGl): a normal render leaves getContext("webgl*")
// returning null, so it neither builds these stubs nor lets a map library start fetching tiles/style.

let _enabled = false;

export function enableWebGl(): void {
    _enabled = true;
}

export function isWebGlEnabled(): boolean {
    return _enabled;
}

export function isWebGlContextType(type: string): boolean {
    return type === "webgl" || type === "webgl2" || type === "experimental-webgl" || type === "experimental-webgl2";
}

// Real GL enum values for the constants a library reads back by name off the context (or an extension) and
// then feeds to getParameter — these must be genuine so the getParameter switch below can recognize them.
// Every other UPPER_SNAKE constant a bundle touches is handed a stable synthetic number on first access.
const CONSTANTS: Record<string, number> = {
    VENDOR: 0x1f00,
    RENDERER: 0x1f01,
    VERSION: 0x1f02,
    SHADING_LANGUAGE_VERSION: 0x8b8c,
    UNMASKED_VENDOR_WEBGL: 0x9245,
    UNMASKED_RENDERER_WEBGL: 0x9246,
    MAX_TEXTURE_SIZE: 0x0d33,
    MAX_CUBE_MAP_TEXTURE_SIZE: 0x851c,
    MAX_RENDERBUFFER_SIZE: 0x84e8,
    MAX_3D_TEXTURE_SIZE: 0x8073,
    MAX_ARRAY_TEXTURE_LAYERS: 0x88ff,
    MAX_VIEWPORT_DIMS: 0x0d3a,
    MAX_VERTEX_ATTRIBS: 0x8869,
    MAX_VERTEX_UNIFORM_VECTORS: 0x8dfb,
    MAX_VARYING_VECTORS: 0x8dfc,
    MAX_FRAGMENT_UNIFORM_VECTORS: 0x8dfd,
    MAX_TEXTURE_IMAGE_UNITS: 0x8872,
    MAX_VERTEX_TEXTURE_IMAGE_UNITS: 0x8b4c,
    MAX_COMBINED_TEXTURE_IMAGE_UNITS: 0x8b4d,
    MAX_TEXTURE_MAX_ANISOTROPY_EXT: 0x84ff,
    MAX_DRAW_BUFFERS: 0x8824,
    MAX_COLOR_ATTACHMENTS: 0x8cdf,
    MAX_SAMPLES: 0x8d57,
    ALIASED_LINE_WIDTH_RANGE: 0x846e,
    ALIASED_POINT_SIZE_RANGE: 0x846d,
    SAMPLES: 0x80a9,
    SAMPLE_BUFFERS: 0x80a8,
    RED_BITS: 0x0d52,
    GREEN_BITS: 0x0d53,
    BLUE_BITS: 0x0d54,
    ALPHA_BITS: 0x0d55,
    DEPTH_BITS: 0x0d56,
    STENCIL_BITS: 0x0d57,
    SUBPIXEL_BITS: 0x0d50,
    COMPILE_STATUS: 0x8b81,
    LINK_STATUS: 0x8b82,
    VALIDATE_STATUS: 0x8b83,
    DELETE_STATUS: 0x8b80,
    ACTIVE_UNIFORMS: 0x8b86,
    ACTIVE_ATTRIBUTES: 0x8b89,
    FRAMEBUFFER_COMPLETE: 0x8cd5,
    NO_ERROR: 0,
};

let _nextSynthetic = 0x9000_0000;

function constantFor(name: string): number {
    let value = CONSTANTS[name];
    if (value === undefined) {
        value = _nextSynthetic++;
        CONSTANTS[name] = value;
    }
    return value;
}

// Values returned by getParameter, keyed by the numeric pname. Anything not listed falls back to 0, which
// covers state getters (BLEND, CURRENT_PROGRAM, …) whose result a mount-time capability probe ignores.
function getParameterValue(pname: number, isWebGl2: boolean): unknown {
    switch (pname) {
        case CONSTANTS.VERSION:
            return isWebGl2 ? "WebGL 2.0" : "WebGL 1.0";
        case CONSTANTS.SHADING_LANGUAGE_VERSION:
            return isWebGl2 ? "WebGL GLSL ES 3.00" : "WebGL GLSL ES 1.0";
        case CONSTANTS.VENDOR:
        case CONSTANTS.UNMASKED_VENDOR_WEBGL:
            return "SimpleCrawler";
        case CONSTANTS.RENDERER:
        case CONSTANTS.UNMASKED_RENDERER_WEBGL:
            return "SimpleCrawler WebGL";
        case CONSTANTS.MAX_TEXTURE_SIZE:
        case CONSTANTS.MAX_CUBE_MAP_TEXTURE_SIZE:
        case CONSTANTS.MAX_RENDERBUFFER_SIZE:
        case CONSTANTS.MAX_3D_TEXTURE_SIZE:
            return 4096;
        case CONSTANTS.MAX_VIEWPORT_DIMS:
            return new Int32Array([4096, 4096]);
        case CONSTANTS.MAX_VERTEX_ATTRIBS:
        case CONSTANTS.MAX_TEXTURE_IMAGE_UNITS:
        case CONSTANTS.MAX_VERTEX_TEXTURE_IMAGE_UNITS:
        case CONSTANTS.MAX_TEXTURE_MAX_ANISOTROPY_EXT:
            return 16;
        case CONSTANTS.MAX_COMBINED_TEXTURE_IMAGE_UNITS:
            return 32;
        case CONSTANTS.MAX_VERTEX_UNIFORM_VECTORS:
        case CONSTANTS.MAX_FRAGMENT_UNIFORM_VECTORS:
            return 1024;
        case CONSTANTS.MAX_VARYING_VECTORS:
            return 30;
        case CONSTANTS.MAX_DRAW_BUFFERS:
        case CONSTANTS.MAX_COLOR_ATTACHMENTS:
            return 8;
        case CONSTANTS.MAX_ARRAY_TEXTURE_LAYERS:
            return 256;
        case CONSTANTS.MAX_SAMPLES:
            return 4;
        case CONSTANTS.ALIASED_LINE_WIDTH_RANGE:
        case CONSTANTS.ALIASED_POINT_SIZE_RANGE:
            return new Float32Array([1, 1024]);
        case CONSTANTS.RED_BITS:
        case CONSTANTS.GREEN_BITS:
        case CONSTANTS.BLUE_BITS:
        case CONSTANTS.ALPHA_BITS:
            return 8;
        case CONSTANTS.DEPTH_BITS:
            return 24;
        case CONSTANTS.STENCIL_BITS:
            return 8;
        case CONSTANTS.SUBPIXEL_BITS:
            return 4;
        default:
            return 0;
    }
}

const _noop = (): void => { };

// A truthy opaque object stands in for every GL resource handle (buffers, textures, shaders, programs,
// framebuffers, VAOs, …): the library only stores and re-passes it, never inspecting its shape.
function handle(): any {
    return {};
}

// Wraps a backing object in a Proxy that fabricates whatever a bundle reads that isn't already defined:
// an UPPER_SNAKE name resolves to its GL enum value, anything else resolves to a no-op method. This absorbs
// the long tail of state/draw calls (bindBuffer, uniform4fv, drawArrays, …) and any extension method without
// enumerating the full ~300-entry WebGL surface.
function stub(backing: any): any {
    return new Proxy(backing, {
        get(target, prop, receiver) {
            if (prop in target) return Reflect.get(target, prop, receiver);
            if (typeof prop === "symbol") return undefined;
            const name = String(prop);
            if (/^[0-9A-Z_]+$/.test(name)) return constantFor(name);
            return _noop;
        },
    });
}

const _extensions: Record<string, any> = {};

function getExtension(name: string): any {
    return _extensions[name] || (_extensions[name] = stub({ name }));
}

export function createWebGLContext(canvas: any, contextType: string, attributes?: any): any {
    const isWebGl2 = contextType === "webgl2" || contextType === "experimental-webgl2";
    const contextAttributes = {
        alpha: true,
        antialias: true,
        depth: true,
        premultipliedAlpha: true,
        preserveDrawingBuffer: false,
        stencil: false,
        ...(attributes && typeof attributes === "object" ? attributes : {}),
    };

    const impl: any = {
        canvas,
        drawingBufferWidth: canvas && canvas.width ? canvas.width : 300,
        drawingBufferHeight: canvas && canvas.height ? canvas.height : 150,

        getContextAttributes: () => contextAttributes,
        isContextLost: () => false,
        getError: () => 0,
        getParameter: (pname: number) => getParameterValue(pname, isWebGl2),
        getExtension: (name: string) => getExtension(name),
        getSupportedExtensions: () => [],
        getShaderPrecisionFormat: () => ({ rangeMin: 127, rangeMax: 127, precision: 23 }),

        createShader: handle,
        createProgram: handle,
        createBuffer: handle,
        createTexture: handle,
        createFramebuffer: handle,
        createRenderbuffer: handle,
        createVertexArray: handle,
        createSampler: handle,
        createQuery: handle,
        createTransformFeedback: handle,
        fenceSync: handle,

        // Compilation, linking and framebuffer completeness must report success or the library aborts setup.
        getShaderParameter: (_shader: any, pname: number) => (pname === CONSTANTS.COMPILE_STATUS ? true : 0),
        getProgramParameter: (_program: any, pname: number) =>
            pname === CONSTANTS.LINK_STATUS || pname === CONSTANTS.VALIDATE_STATUS ? true : 0,
        checkFramebufferStatus: () => CONSTANTS.FRAMEBUFFER_COMPLETE,
        getShaderInfoLog: () => "",
        getProgramInfoLog: () => "",

        // A non-null uniform location keeps the library on its "uniform exists, set it" path; attrib slots are
        // plain indices. Both are only stored and re-passed, so any stable value works.
        getUniformLocation: () => ({}),
        getAttribLocation: () => 0,
        getActiveUniform: () => null,
        getActiveAttrib: () => null,
    };

    for (const name in CONSTANTS) impl[name] = CONSTANTS[name];

    return stub(impl);
}
