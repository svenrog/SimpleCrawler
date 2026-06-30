// Severities mirror Microsoft.Extensions.Logging.LogLevel's numeric values so the host can cast the
// level it receives straight back to a LogLevel. The host embeds __crawlerLog and pushes a minimum
// level through __crawlerSetLogLevel only when JsRenderOptions.ScriptLogging is set; until then minLevel

import { LEVEL_DEBUG, LEVEL_ERROR, LEVEL_INFO, LEVEL_TRACE, LEVEL_WARN } from "./constants";
import { formatArgs, stringify } from "./utils";

export function installConsole(global: any): void {
    let minLevel = Number.POSITIVE_INFINITY;
    const timers = new Map<string, number>();
    const counters = new Map<string, number>();
    let groupDepth = 0;

    const emit = (level: number, build: () => string): void => {
        if (level < minLevel) return;
        const log = global.__crawlerLog;
        if (typeof log !== "function") return;
        const indent = groupDepth > 0 ? " ".repeat(groupDepth * 2) : "";
        log(level, indent + build());
    };

    const label = (args: any[]): string => args.length > 0 ? stringify(args[0]) : "default";

    global.console = {
        log: (...args: any[]) => emit(LEVEL_INFO, () => formatArgs(args)),
        info: (...args: any[]) => emit(LEVEL_INFO, () => formatArgs(args)),
        debug: (...args: any[]) => emit(LEVEL_DEBUG, () => formatArgs(args)),
        warn: (...args: any[]) => emit(LEVEL_WARN, () => formatArgs(args)),
        error: (...args: any[]) => emit(LEVEL_ERROR, () => formatArgs(args)),
        trace: (...args: any[]) => emit(LEVEL_TRACE, () => formatArgs(args)),
        dir: (...args: any[]) => emit(LEVEL_DEBUG, () => formatArgs(args)),
        dirxml: (...args: any[]) => emit(LEVEL_DEBUG, () => formatArgs(args)),

        assert: (...args: any[]) => {
            if (args.length > 0 && args[0]) return;
            emit(LEVEL_ERROR, () => args.length > 1
                ? "Assertion failed: " + formatArgs(args.slice(1))
                : "Assertion failed");
        },

        group: (...args: any[]) => {
            emit(LEVEL_DEBUG, () => "▶ " + (args.length > 0 ? formatArgs(args) : ""));
            groupDepth++;
        },
        groupCollapsed: (...args: any[]) => {
            emit(LEVEL_DEBUG, () => "▶ " + (args.length > 0 ? formatArgs(args) : ""));
            groupDepth++;
        },
        groupEnd: () => {
            if (groupDepth > 0) groupDepth--;
        },

        count: (...args: any[]) => {
            const key = label(args);
            const value = (counters.get(key) ?? 0) + 1;
            counters.set(key, value);
            emit(LEVEL_DEBUG, () => `${key}: ${value}`);
        },
        countReset: (...args: any[]) => {
            const key = label(args);
            if (!counters.has(key)) emit(LEVEL_WARN, () => `Count for '${key}' does not exist`);
            else counters.set(key, 0);
        },

        time: (...args: any[]) => {
            const key = label(args);
            if (timers.has(key)) emit(LEVEL_WARN, () => `Timer '${key}' already exists`);
            else timers.set(key, Date.now());
        },
        timeLog: (...args: any[]) => {
            const key = label(args);
            const start = timers.get(key);
            if (start === undefined) { emit(LEVEL_WARN, () => `Timer '${key}' does not exist`); return; }
            const extra = args.length > 1 ? " " + formatArgs(args.slice(1)) : "";
            emit(LEVEL_DEBUG, () => `${key}: ${Date.now() - start}ms${extra}`);
        },
        timeEnd: (...args: any[]) => {
            const key = label(args);
            const start = timers.get(key);
            if (start === undefined) { emit(LEVEL_WARN, () => `Timer '${key}' does not exist`); return; }
            timers.delete(key);
            emit(LEVEL_DEBUG, () => `${key}: ${Date.now() - start}ms - timer ended`);
        },

        table: (...args: any[]) => emit(LEVEL_DEBUG, () => args.length > 0 ? stringify(args[0]) : "(empty table)"),
        clear: () => { },
    };

    global.__crawlerSetLogLevel = (level: number) => { minLevel = level; };
}