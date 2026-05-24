import React from 'react';
import Link from '@docusaurus/Link';
import CodeBlock from '@theme/CodeBlock';
import styles from './styles.module.css';

export default function HomeHero() {
  return (
    <header className={styles.hero}>
      <div className={styles.inner}>
        <span className={styles.eyebrow}>Maintained by Aura · MIT licensed</span>

        <h1 className={styles.headline}>
          Create your Unity apps with LLMs.
        </h1>

        <p className={styles.tagline}>
          MCP for Unity bridges AI assistants — Claude, Cursor, VS Code,
          Windsurf, and more — with your Unity Editor via the
          {' '}
          <a
            href="https://modelcontextprotocol.io/introduction"
            target="_blank"
            rel="noopener noreferrer"
          >
            Model Context Protocol
          </a>
          . Manage assets, control scenes, edit scripts, run tests, and automate workflows.
        </p>

        <div className={styles.ctaRow}>
          <Link
            className={`${styles.cta} ${styles.ctaPrimary}`}
            to="/getting-started/install"
          >
            Get started
          </Link>
          <Link
            className={`${styles.cta} ${styles.ctaSecondary}`}
            to="/reference/tools"
          >
            Browse the tools
          </Link>
        </div>

        <div className={styles.installBlock}>
          <span className={styles.installLabel}>Install via Unity Package Manager</span>
          <CodeBlock
            language="text"
            className={styles.installCode}
          >
            https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
          </CodeBlock>
        </div>

        <figure className={styles.demo}>
          <img
            src="/unity-mcp/img/building_scene.gif"
            alt="An LLM building a Unity scene through MCP for Unity"
            loading="lazy"
            width="1200"
            height="675"
          />
          <figcaption className={styles.demoCaption}>
            An MCP client creating GameObjects, materials, and lights in the Unity Editor.
          </figcaption>
        </figure>
      </div>
    </header>
  );
}
