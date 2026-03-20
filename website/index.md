---
layout: home

hero:
  name: Cocoar.FileSystem
  text: Resilient File System Utilities for .NET
  tagline: Production-ready monitoring, high-performance search, and secure reading. Zero dependencies.
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: Why Cocoar.FileSystem?
      link: /guide/why-cocoar-filesystem
    - theme: alt
      text: GitHub
      link: https://github.com/cocoar-dev/Cocoar.FileSystem

features:
  - icon: |-
      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>
    title: Resilient Monitoring
    details: FileSystemWatcher with automatic polling fallback, error recovery, and directory identity tracking. Never miss an event.
  - icon: |-
      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"/><polyline points="1 20 1 14 7 14"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/></svg>
    title: Auto-Recovery
    details: Seamless switching between native watcher and polling. Handles directory disappearing, Docker volumes, network shares.
  - icon: |-
      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>
    title: High-Performance Search
    details: Lazy-evaluated file enumeration with depth limits, exclusion patterns, and LINQ support. Efficient for large directory trees.
  - icon: |-
      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
    title: Secure File Reading
    details: Read files as byte arrays with shared access. Zero-out sensitive data after use. Built-in BOM stripping and try-pattern.
  - icon: |-
      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3v18"/><path d="M8 7l4-4 4 4"/><path d="M20 21H4"/><path d="M2 15h5l2-6 3 9 2-6h8"/></svg>
    title: Event Streams
    details: Unified ChannelReader&lt;FileSystemEvent&gt; for ordered, sequential event delivery. Optional Rx.NET integration.
  - icon: |-
      <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="3" width="20" height="14" rx="2" ry="2"/><line x1="8" y1="21" x2="16" y2="21"/><line x1="12" y1="17" x2="12" y2="21"/></svg>
    title: Cross-Platform & Zero Dependencies
    details: Tested on Windows, Linux, and macOS. Pure .NET 8.0 with zero external NuGet dependencies.
---
