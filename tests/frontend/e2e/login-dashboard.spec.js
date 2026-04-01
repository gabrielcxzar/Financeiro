import { test, expect } from '@playwright/test';

const email = process.env.FINFLOW_E2E_EMAIL;
const password = process.env.FINFLOW_E2E_PASSWORD;

test.describe('Login e Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('valida obrigatoriedade dos campos de login', async ({ page }) => {
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByText('Insira seu email.')).toBeVisible();
    await expect(page.getByText('Insira sua senha.')).toBeVisible();
  });

  test('realiza login e carrega o dashboard sem erro de timeout', async ({ page }) => {
    test.skip(!email || !password, 'Defina FINFLOW_E2E_EMAIL e FINFLOW_E2E_PASSWORD para executar o fluxo autenticado.');

    await page.getByPlaceholder('Email').fill(email);
    await page.getByPlaceholder('Senha').fill(password);
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page.getByText('Dashboard')).toBeVisible({ timeout: 20000 });
    await expect(page.getByText('Planejamento Mensal')).toBeVisible({ timeout: 20000 });
    await expect(page.getByText('Projecao dos proximos 6 meses')).toBeVisible({ timeout: 20000 });
    await expect(page.getByText('Timeout de conexao com a API')).toHaveCount(0);
  });
});
