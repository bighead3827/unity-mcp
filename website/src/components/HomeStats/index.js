import React from 'react';
import styles from './styles.module.css';

const stats = [
  { value: '43', label: 'MCP tools' },
  { value: '25', label: 'read-only resources' },
  { value: '12+', label: 'MCP clients supported' },
  { value: 'Unity 2021.3+', label: 'through Unity 6.x' },
];

export default function HomeStats() {
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
