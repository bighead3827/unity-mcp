import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import HomeHero from '@site/src/components/HomeHero';
import HomeFeatures from '@site/src/components/HomeFeatures';
import HomeStats from '@site/src/components/HomeStats';

export default function Home() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title="MCP for Unity"
      description={siteConfig.tagline}
    >
      <main>
        <HomeHero />
        <HomeStats />
        <HomeFeatures />
      </main>
    </Layout>
  );
}
