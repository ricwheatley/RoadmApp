import React from 'react';

export default function HomePage({ tenants = [] }) {
  return (
    <div>
      <h1>Authorised Tenants</h1>
      <ul>
        {tenants.map((t) => (
          <li key={t.id}>{t.name}</li>
        ))}
      </ul>
    </div>
  );
}
