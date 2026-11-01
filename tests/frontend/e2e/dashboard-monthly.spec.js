import { test, expect } from '@playwright/test';

test('dashboard troca o mês e compara despesas confirmadas', async ({ page }) => {
  await page.clock.setFixedTime(new Date('2026-11-15T12:00:00Z'));
  await page.addInitScript(() => {
    localStorage.setItem('token', 'synthetic-test-token');
    localStorage.setItem('finflow_onboarding_completed', 'true');
  });
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    if (!url.pathname.endsWith('/dashboard/summary')) {
      await route.fulfill({ json: {} });
      return;
    }
    const month = Number(url.searchParams.get('month'));
    const data = {
      9: { income: 90, expense: 200, pendingExpense: 0 },
      10: { income: 100, expense: 100, pendingExpense: 25 },
      11: { income: 0, expense: 0, pendingExpense: 148.63 },
    }[month] || { income: 0, expense: 0, pendingExpense: 0 };
    await route.fulfill({ json: { summary: { total: 1000, netWorth: 900, ...data },
      categorySummary: month === 10 ? [{ name: 'Mercado', total: 100 }] : [],
      recentTransactions: [], recentSettlements: [], projection: { items: [] } } });
  });
  await page.goto('/');
  await expect(page.getByText('Pendentes cadastradas: R$ 148,63', { exact: false })).toBeVisible();
  await expect(page.getByText('-100% vs mês ant.')).toBeVisible();
  await expect(page.getByText('Sem despesas confirmadas neste mês')).toBeVisible();

  await page.locator('.ant-picker').click();
  await page.locator('.ant-picker-cell').filter({ hasText: /^out\.?$/i }).click();
  await expect(page.getByText('Pendentes cadastradas: R$ 25,00', { exact: false })).toBeVisible();
  await expect(page.getByText('-50% vs mês ant.')).toBeVisible();
  await expect(page.getByText('Despesas confirmadas por Categoria')).toBeVisible();
  await expect(page.getByText('Sem despesas confirmadas neste mês')).toHaveCount(0);
});
