import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'
import llmstxt from 'vitepress-plugin-llms'

export default withMermaid(
  defineConfig({
    title: 'Cocoar.FileSystem',
    description: 'Resilient file system monitoring, search, and reading for .NET',

    head: [
      ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo_light.svg' }],
      ['link', { rel: 'alternate', type: 'text/plain', href: '/llms.txt', title: 'LLM documentation (summary)' }],
      ['link', { rel: 'alternate', type: 'text/plain', href: '/llms-full.txt', title: 'LLM documentation (full)' }],
    ],

    vite: {
      plugins: [llmstxt({
        excludeUnnecessaryFiles: false,
        ignoreFiles: ['changelog.md'],
      })],
    },

    themeConfig: {
      logo: {
        light: '/logo_light.svg',
        dark: '/logo_dark.svg',
      },

      siteTitle: 'Cocoar.FileSystem v2',

      nav: [
        { text: 'Guide', link: '/guide/getting-started' },
        { text: 'Reference', link: '/reference/api' },
        { text: 'Changelog', link: '/changelog' },
        { text: 'LLM Docs', link: '/llms-full.txt', target: '_blank' },
        { text: 'NuGet', link: 'https://www.nuget.org/packages/Cocoar.FileSystem' },
      ],

      sidebar: {
        '/guide/': [
          {
            text: 'Introduction',
            items: [
              { text: 'Getting Started', link: '/guide/getting-started' },
              { text: 'Why Cocoar.FileSystem?', link: '/guide/why-cocoar-filesystem' },
            ],
          },
          {
            text: 'ResilientFileSystemMonitor',
            items: [
              { text: 'Overview', link: '/guide/monitor/overview' },
              { text: 'Builder API', link: '/guide/monitor/builder-api' },
              { text: 'Events & Channels', link: '/guide/monitor/events-channels' },
              { text: 'Polling Fallback', link: '/guide/monitor/polling-fallback' },
              { text: 'Depth Control', link: '/guide/monitor/depth-control' },
              { text: 'Multiple Patterns', link: '/guide/monitor/multiple-patterns' },
              { text: 'Folder Rename Detection', link: '/guide/monitor/folder-rename' },
              { text: 'Symlink Target Tracking', link: '/guide/monitor/symlink-target-tracking' },
              { text: 'Debouncing', link: '/guide/monitor/debouncing' },
              { text: 'Adaptive Hashing <span class="badge-adv" title="Advanced topic"></span>', link: '/guide/monitor/adaptive-hashing' },
            ],
          },
          {
            text: 'FileSearcher',
            items: [
              { text: 'Overview', link: '/guide/searcher/overview' },
              { text: 'Builder API', link: '/guide/searcher/builder-api' },
              { text: 'Depth & Exclusions', link: '/guide/searcher/depth-exclusions' },
            ],
          },
          {
            text: 'FileReader',
            items: [
              { text: 'Overview', link: '/guide/reader/overview' },
            ],
          },
          {
            text: 'Reactive Patterns',
            items: [
              { text: 'Channel-Based Consumption', link: '/guide/reactive/basics' },
              { text: 'Rx.NET Integration <span class="badge-adv" title="Advanced topic"></span>', link: '/guide/reactive/rxnet' },
            ],
          },
        ],
        '/reference/': [
          {
            text: 'Reference',
            items: [
              { text: 'API Overview', link: '/reference/api' },
              { text: 'Configuration Options', link: '/reference/options' },
              { text: 'Examples', link: '/reference/examples' },
            ],
          },
        ],
      },

      socialLinks: [
        { icon: 'github', link: 'https://github.com/cocoar-dev/Cocoar.FileSystem' },
      ],

      search: {
        provider: 'local',
      },

      footer: {
        message: 'Released under the Apache-2.0 License.',
        copyright: 'Copyright 2025-present Cocoar',
      },
    },

    mermaid: {},

    mermaidPlugin: {
      class: 'mermaid',
    },
  }),
)
