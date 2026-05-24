// @ts-check
// Brand-neutral configuration: product name lives here so a future rename
// changes one file rather than every URL slug. Do NOT bake "mcp-for-unity"
// or "unity-mcp" into sidebar slugs, file paths, or docs URLs.

import { themes as prismThemes } from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'MCP for Unity',
  tagline: 'AI-driven game development for the Unity Editor',
  favicon: 'img/favicon.png',

  // Hosted on GitHub Pages under the CoplayDev org.
  // Custom domain (CNAME) deferred — see plan Phase 2.
  url: 'https://coplaydev.github.io',
  baseUrl: '/unity-mcp/',

  organizationName: 'CoplayDev',
  projectName: 'unity-mcp',
  deploymentBranch: 'gh-pages',
  trailingSlash: false,

  onBrokenLinks: 'throw',
  markdown: {
    // Parse .md as CommonMark (no JSX, no {expression} parsing) and .mdx
    // as MDX. Auto-generated tool reference pages contain literals like
    // `{name: value}` and `<T>` in descriptions — MDX would treat those
    // as JS expressions / JSX tags and refuse to compile.
    format: 'detect',
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
    // Chinese translation deferred to a follow-up PR; structure ready
    // (existing docs/i18n/README-zh.md will migrate into i18n/zh/).
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          routeBasePath: '/',
          sidebarPath: './sidebars.js',
          editUrl: 'https://github.com/CoplayDev/unity-mcp/edit/beta/website/',
          showLastUpdateTime: true,
          showLastUpdateAuthor: true,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  plugins: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        hashed: true,
        indexBlog: false,
        docsRouteBasePath: '/',
        highlightSearchTermsOnTargetPage: true,
      },
    ],
    // Redirects: as docs land in M2+, add entries here so external links
    // pointing at old /docs/*.md paths on GitHub keep working.
    [
      '@docusaurus/plugin-client-redirects',
      {
        redirects: [
          // Example pattern (populated as content migrates in M2):
          // { from: '/docs/guides/CLI_USAGE', to: '/guides/cli' },
        ],
      },
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: 'img/social-card.png',
      navbar: {
        title: 'MCP for Unity',
        logo: {
          alt: 'MCP for Unity logo',
          src: 'img/logo.png',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'mainSidebar',
            position: 'left',
            label: 'Docs',
          },
          {
            href: 'https://github.com/CoplayDev/unity-mcp',
            label: 'GitHub',
            position: 'right',
          },
          {
            href: 'https://discord.gg/y4p8KfzrN4',
            label: 'Discord',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              { label: 'Getting Started', to: '/' },
              { label: 'Guides', to: '/guides/cli' },
              { label: 'Reference', to: '/reference/tools/' },
            ],
          },
          {
            title: 'Community',
            items: [
              { label: 'Discord', href: 'https://discord.gg/y4p8KfzrN4' },
              { label: 'GitHub Issues', href: 'https://github.com/CoplayDev/unity-mcp/issues' },
            ],
          },
          {
            title: 'More',
            items: [
              { label: 'GitHub', href: 'https://github.com/CoplayDev/unity-mcp' },
              { label: 'PyPI', href: 'https://pypi.org/p/mcpforunityserver' },
              { label: 'Asset Store', href: 'https://assetstore.unity.com/packages/tools/generative-ai/mcp-for-unity-ai-driven-development-329908' },
            ],
          },
        ],
        copyright: `MIT licensed. Sponsored and maintained by <a href="https://www.tryaura.dev/">Aura</a>. Not affiliated with Unity Technologies.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
        additionalLanguages: ['csharp', 'bash', 'json', 'python'],
      },
      colorMode: {
        defaultMode: 'light',
        respectPrefersColorScheme: true,
      },
    }),
};

export default config;
