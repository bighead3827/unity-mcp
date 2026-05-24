import React from 'react';
import Link from '@docusaurus/Link';
import styles from './styles.module.css';

const features = [
  {
    title: 'Talk to the Editor',
    body: 'Drive scenes, GameObjects, scripts, assets, prefabs, and materials with natural language. 43 tools across 9 groups expose Unity’s editing surface to your MCP client.',
    href: '/reference/tools',
    cta: 'Browse tools',
  },
  {
    title: 'Multiple Editors, one session',
    body: 'Open several Unity Editors at once and aim a single MCP session at any of them. Per-call routing for cross-project prompts; session isolation across MCP clients.',
    href: '/guides/multi-instance',
    cta: 'How routing works',
  },
  {
    title: 'Two transports',
    body: 'HTTP for multi-agent, remote-hosted, and shared workflows. Stdio for single-client setups like Claude Desktop. Auto-detected and auto-configured for every supported client.',
    href: '/architecture/transports',
    cta: 'HTTP vs stdio',
  },
  {
    title: 'Your tools, on demand',
    body: 'Per-session visibility. Activate animation, vfx, ui, testing, or probuilder tools only when you need them. Smaller prompt, sharper routing, lower cost.',
    href: '/guides/tool-groups',
    cta: 'Tool groups',
  },
  {
    title: 'Auto-generated reference',
    body: 'Every tool and resource page is generated from the Python @mcp_for_unity_tool registry. CI fails if the docs drift. Examples you write are preserved across regenerations.',
    href: '/contributing/docs',
    cta: 'Docs workflow',
  },
  {
    title: 'Extend with custom tools',
    body: 'Write a C# attribute, register a new domain. The MCP client picks it up automatically. Project-scoped or global. Full reflection-based dispatch.',
    href: '/guides/custom-tools',
    cta: 'Custom tools',
  },
];

export default function HomeFeatures() {
  return (
    <section className={styles.features}>
      <div className={styles.inner}>
        <h2 className={styles.sectionTitle}>What you can do</h2>
        <div className={styles.grid}>
          {features.map((f) => (
            <Link className={styles.card} to={f.href} key={f.title}>
              <h3 className={styles.cardTitle}>{f.title}</h3>
              <p className={styles.cardBody}>{f.body}</p>
              <span className={styles.cardCta}>{f.cta} &rarr;</span>
            </Link>
          ))}
        </div>
      </div>
    </section>
  );
}
