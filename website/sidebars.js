// @ts-check
// Locked information architecture — see the plan file.
// Slugs are brand-neutral: no "mcp-for-unity" / "unity-mcp" in any URL.
// Pages flagged TODO land in later milestones (M3 generator, M4 net-new).
// Keep the structure stable to avoid URL breakage.

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  mainSidebar: [
    {
      type: 'category',
      label: 'Getting Started',
      link: { type: 'doc', id: 'getting-started/index' },
      collapsed: false,
      items: [
        'getting-started/install',
        // 'getting-started/setup-wizard',   // M4
        // 'getting-started/first-prompt',   // M4
        // 'getting-started/clients',        // M4
      ],
    },
    {
      type: 'category',
      label: 'Guides',
      collapsed: false,
      items: [
        'guides/cli',
        'guides/cli-examples',
        'guides/client-configurators',
        'guides/cursor',
        'guides/remote-server-auth',
        'guides/custom-tools',
        // 'guides/multi-instance',   // M4
        // 'guides/tool-groups',      // M4
      ],
    },
    {
      type: 'category',
      label: 'Reference',
      collapsed: true,
      items: [
        // Tools/ and Resources/ are auto-generated in M3.
        // 'reference/tools/index',
        // 'reference/resources/index',
        // 'reference/cli',           // M4
        // 'reference/manifest',      // M4
        {
          type: 'link',
          label: 'Tool & resource catalog',
          href: 'https://github.com/CoplayDev/unity-mcp#available-tools',
          description: 'Auto-generated pages land in M3.',
        },
      ],
    },
    {
      type: 'category',
      label: 'Architecture',
      collapsed: true,
      items: [
        // 'architecture/transports',     // M4
        // 'architecture/python-layers',  // M4
        // 'architecture/unity-compat',   // M4
        'architecture/remote-auth',
        'architecture/telemetry',
        'architecture/manage-physics',
        'architecture/roadmap',
      ],
    },
    {
      type: 'category',
      label: 'Contributing',
      collapsed: true,
      items: [
        'contributing/dev-setup',
        // 'contributing/testing',  // M4
        'contributing/releases',
        // 'contributing/docs',     // M4
      ],
    },
    {
      type: 'category',
      label: 'Migrations',
      collapsed: true,
      items: [
        'migrations/v5',
        'migrations/v6',
        'migrations/v8',
      ],
    },
  ],
};

export default sidebars;
