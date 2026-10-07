import { expect, test } from '@playwright/test';

const password = process.env.TASKMGR_ADMIN_PASSWORD || 'change-me';
const hostId = process.env.TASKMGR_E2E_HOST || 'docker-host';

test.beforeEach(async ({ page }) => {
  page.on('pageerror', (e) => { throw e; });
  await page.goto('/');
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Hosts' })).toBeVisible();
});

test('fleet page lists the monitored host as online', async ({ page }) => {
  const card = page.locator(`a.host[href="/hosts/${hostId}"]`);
  await expect(card).toBeVisible();
  await expect(card.getByText('online')).toBeVisible();
});

test('overview shows tiles, per-core bars, and live charts', async ({ page }) => {
  await page.goto(`/hosts/${hostId}`);
  await expect(page.locator('.tile').first()).toBeVisible();
  await expect(page.locator('.core').first()).toBeVisible();
  await expect(page.locator('.uplot').first()).toBeVisible();
  await expect(page.getByText('live', { exact: true })).toBeVisible({ timeout: 15000 });
});

test('processes: filter, open details, and see history charts', async ({ page }) => {
  await page.goto(`/hosts/${hostId}/processes`);
  const rows = page.locator('.ptable tbody tr');
  await expect(rows.first()).toBeVisible();
  await page.getByLabel('Filter processes').fill('taskmgr-agent');
  await expect(rows).toHaveCount(1);
  await rows.first().click();
  const detail = page.getByLabel('Process details');
  await expect(detail).toBeVisible();
  await expect(detail.getByText('/app/taskmgr-agent').first()).toBeVisible();
  await expect(detail.locator('.uplot')).toHaveCount(3);
  await expect(detail.getByRole('button', { name: 'Kill', exact: true })).toBeEnabled();
});

test('network, disks, accounting, and audit tabs render', async ({ page }) => {
  await page.goto(`/hosts/${hostId}/network`);
  await expect(page.getByText(/TCP connections/)).toBeVisible();
  await page.getByRole('link', { name: 'Disks' }).click();
  await expect(page.getByRole('columnheader', { name: 'Volume' })).toBeVisible();
  await page.getByRole('link', { name: 'Accounting' }).click();
  await expect(page.getByText(/from completed minutes/)).toBeVisible();
  await page.getByRole('link', { name: 'Audit' }).click();
  await expect(page.locator('section')).toBeVisible();
});

test('history ranges load from the server', async ({ page }) => {
  await page.goto(`/hosts/${hostId}`);
  await page.getByRole('button', { name: '15m' }).click();
  await expect(page.locator('.uplot').first()).toBeVisible();
  await expect(page.getByRole('button', { name: '15m' })).toHaveAttribute('aria-pressed', 'true');
});
