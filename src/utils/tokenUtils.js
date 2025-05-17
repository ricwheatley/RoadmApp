export function formatTenant(tenant) {
  if (!tenant) return '';
  return `${tenant.name} (${tenant.id})`;
}
