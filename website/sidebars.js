// @ts-check
// Locked information architecture — see plan file.
// Slugs are brand-neutral: no "mcp-for-unity" / "unity-mcp" in any URL.
// Pages flagged TODO land in later milestones (M2 migrate, M3 generator,
// M4 net-new content). Keep the structure stable to avoid URL breakage.

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
    // 'Guides' category populated in M2 (migrate docs/guides/*).
    // 'Reference' category populated in M3 (auto-generated tool/resource pages).
    // 'Architecture' category populated in M2/M4.
    // 'Contributing' category populated in M2.
    // 'Migrations' category populated in M2.
  ],
};

export default sidebars;
