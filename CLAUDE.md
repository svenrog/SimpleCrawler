# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A high-performance single-domain web crawler (CLI: `smpcrawl`) built to gather URLs for load testing, respecting `robots.txt` and meta-robots. The crawler logic lives in `SimpleCrawler.Core` and is parameterized over a pluggable HTML/rendering backend.

## Build, test, run

- Solution file is `SimpleCrawler.slnx` (modern XML solution format), targeting **.NET 10**. `dotnet build` operates on it.
- Tests use **xUnit v3** (pre-release `xunit.v3`, Microsoft.Testing.Platform), FluentAssertions, and Moq — not xUnit v2 idioms. `dotnet test` does **not** work here; run a test project via `dotnet run --project tests/SimpleCrawler.Tests -c Release`.
- Run a single test with the MTP native runner filter: `dotnet run --project tests/SimpleCrawler.Tests -c Release -- --filter "/*/*/ClassName/MethodName"`.
- Benchmarks are a runnable BenchmarkDotNet console app: `dotnet run -c Release --project tests/SimpleCrawler.Benchmarks`. Always run benchmarks in Release.
- The JS-render profiling harness is a separate console app, `tests/SimpleCrawler.ProfileRunner` (CommandLineParser verbs, not a benchmark). It drives the real crawl/render path against a test-host SPA: `dotnet run --project tests/SimpleCrawler.ProfileRunner -- profile <combo> <iterations> [framework]` (crawls repeatedly so `RenderProfiler` with `JSRENDER_PROFILE=1` prints a per-phase table), or `-- rendersize <combo> [framework]` (renders once, dumps element/anchor counts + serialized HTML). `combo` = `jint|v8`; `framework` = `react|preact|vue|svelte|solid`. Pass `--help` or `profile --help` for defaults.

## Architecture

- `SimpleCrawler.Core` — abstract base (`AbstractCrawler.cs`, semaphore-based parallelism), the custom `robots.txt`/sitemap parser, and shared crawling logic.
- One project per HTML/rendering backend: `SimpleCrawler.HtmlAgilityPack` (default, static), `SimpleCrawler.AngleSharp` (static), `SimpleCrawler.Playwright` and `SimpleCrawler.Puppeteer` (headless browser, JS rendering). Every project under `/src` is part of the solution; there are no out-of-solution experiments.
- `SimpleCrawler.TestHost` serves embedded resources as static files so integration test servers run entirely from memory.

## Switching the active crawler backend

The CLI is wired to one backend at a time. To change it, edit `src/SimpleCrawler.Console/Extensions/ServiceCollectionExtensions.cs` (`AddCrawler` calls `services.AddHtmlAgilityPackCrawler(...)`) to call the target backend's `AddXyzCrawler` extension, and add the corresponding project reference to `SimpleCrawler.Console.csproj`.

## Code style

`.editorconfig` deviates from defaults: primary constructors are disabled (`csharp_style_prefer_primary_constructors = false` / IDE0290) — use traditional constructors. `CA1873` (expensive logging) is silenced. `ImplicitUsings` is enabled across projects.

All private fields are `_camelCase`, including static and const ones.

Avoid comments. Default to none — let names and structure carry the meaning. Add a comment only when behaviour is genuinely surprising without it (a non-obvious constraint, workaround, or rationale), never to restate what the code already says.

## Git

Use feature branches and open a PR against `master`.
