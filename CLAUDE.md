# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Directory-scoped CLAUDE.md files carry area-specific rules and load automatically when you touch files there: `tests/` (running tests, benchmarks, profiling, TestHost) and the `src/SimpleCrawler.Js*` projects (JS rendering). Keep this root file lean; put area-specific guidance in the scoped file.

## What this is

A high-performance single-domain web crawler (CLI: `smpcrawl`) built to gather URLs for load testing, respecting `robots.txt` and meta-robots. The crawler logic lives in `SimpleCrawler.Core` and is parameterized over a pluggable HTML/rendering backend.

## Build, test, run

- Solution file is `SimpleCrawler.slnx` (modern XML solution format), targeting **.NET 10**. `dotnet build` operates on it.
- `dotnet test` does **not** work here (xUnit v3 on Microsoft.Testing.Platform). Run a test project directly: `dotnet run --project tests/SimpleCrawler.Tests -c Release`. Filters, benchmarks, and the profiling harness are documented in `tests/CLAUDE.md`.

## Architecture

- `SimpleCrawler.Core` — abstract crawler base (`AbstractCrawler.cs`, semaphore-based parallelism), the custom `robots.txt`/sitemap parser, retry, throttling, checkpointing, and shared crawling logic.
- One project per HTML/rendering backend:
  - `SimpleCrawler.HtmlAgilityPack` (default, static) and `SimpleCrawler.AngleSharp` (static).
  - `SimpleCrawler.Js` (in-process JS rendering over a pure-JS DOM) with one engine project each for `SimpleCrawler.Js.Jint` and `SimpleCrawler.Js.V8`; `SimpleCrawler.Js.Dom` is the TypeScript source the embedded JS preludes are built from.
  - `SimpleCrawler.Playwright` and `SimpleCrawler.Puppeteer` (headless browser).
- Every project under `/src` is part of the solution; there are no out-of-solution experiments.
- The CLI (`SimpleCrawler.Console`) is wired to one backend at a time, selected at build/publish time via `-p:CrawlerBackend=<HtmlAgilityPack|V8|AngleSharp|Jint|Playwright|Puppeteer>` (defaults to `HtmlAgilityPack`). The property drives a `BACKEND_*` define constant, the matching `AddXyzCrawler(...)` call in `src/SimpleCrawler.Console/Extensions/ServiceCollectionExtensions.cs`, the conditional `ProjectReference` in `SimpleCrawler.Console.csproj`, and the output `AssemblyName` (`smpcrawl`, or `smpcrawl-<backend>` for non-default backends). `publish.ps1` publishes all six. Playwright/Puppeteer drive an out-of-process browser via reflection-heavy CDP/BiDi client libraries that aren't trim-safe, so those two backends publish self-contained JIT rather than NativeAOT (`PublishAot` is forced off for them); the rest publish NativeAOT.

## Code style

- Primary constructors are disabled (`csharp_style_prefer_primary_constructors = false` / IDE0290) — use traditional constructors. `CA1873` (expensive logging) is silenced. `ImplicitUsings` and `Nullable` are enabled across all projects.
- All private fields are `_camelCase`, including static and const ones.
- Exactly one top-level class per `.cs` file; the filename matches the class name.
- Avoid comments. Default to none — let names and structure carry the meaning. Add a comment only when behaviour is genuinely surprising without it (a non-obvious constraint, workaround, or rationale), never to restate what the code already says. Never name specific crawled sites/domains in comments — describe the failure pattern generically.

## Git

Use feature branches and open a PR against `master`.
