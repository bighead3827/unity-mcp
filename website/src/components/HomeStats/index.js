import React from 'react';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import styles from './styles.module.css';

export default function HomeStats() {
  const { siteConfig } = useDocusaurusContext();
  const clientCount = siteConfig.customFields?.supportedClientCount ?? 0;

  const stats = [
    { value: '43', label: 'MCP tools' },
    { value: '25', label: 'read-only resources' },
    { value: String(clientCount), label: 'MCP clients supported' },
    { value: 'Unity 2021.3+', label: 'through Unity 6.x' },
  ];

  return (
    <section className={styles.statsSection}>
      <div className={styles.inner}>
        <div className={styles.grid}>
          {stats.map((s) => (
            <div className={styles.cell} key={s.label}>
              <div className={styles.value}>{s.value}</div>
              <div className={styles.label}>{s.label}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
