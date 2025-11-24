import React, { useEffect, useState } from 'react';
import { Card, Grid, SpinLoading, Button } from 'antd-mobile'; // <--- Mudou aqui
import { EyeOutline, EyeInvisibleOutline } from 'antd-mobile-icons';
import styled from 'styled-components';
import api from '../services/api';

const BalanceContainer = styled.div`
  text-align: center;
  margin-bottom: 20px;
  
  h3 { margin: 0; color: #666; font-weight: normal; font-size: 16px; }
  h1 { margin: 5px 0; color: #333; font-size: 32px; font-weight: bold; }
`;

export default function Home() {
  const [loading, setLoading] = useState(true);
  const [totalBalance, setTotalBalance] = useState(0);
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    fetchSaldo();
  }, []);

  const fetchSaldo = async () => {
    try {
      const response = await api.get('/accounts');
      const contas = response.data;
      const total = contas.reduce((acc, conta) => acc + conta.currentBalance, 0);
      setTotalBalance(total);
    } catch (error) {
      console.error("Erro ao buscar saldo:", error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    // <--- Mudou aqui embaixo também
    return <div style={{ display: 'flex', justifyContent: 'center', padding: 20 }}><SpinLoading color='primary' /></div>;
  }

  return (
    <div style={{ padding: '20px' }}>
      <BalanceContainer>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8 }}>
          <h3>Saldo Atual</h3>
          <div onClick={() => setVisible(!visible)}>
            {visible ? <EyeOutline /> : <EyeInvisibleOutline />}
          </div>
        </div>
        
        <h1>
          {visible 
            ? `R$ ${totalBalance.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` 
            : '••••••'}
        </h1>
      </BalanceContainer>

      <Grid columns={2} gap={12}>
        <Grid.Item>
          <Card title="Receitas do Mês">
            <span style={{ color: 'green', fontWeight: 'bold' }}>R$ 0,00</span>
          </Card>
        </Grid.Item>
        <Grid.Item>
          <Card title="Despesas do Mês">
            <span style={{ color: 'red', fontWeight: 'bold' }}>R$ 0,00</span>
          </Card>
        </Grid.Item>
      </Grid>
      
      <div style={{ marginTop: 20 }}>
        <h3>Contas</h3>
        <Card>
            Você ainda não cadastrou contas ou transações.
        </Card>
      </div>
    </div>
  );
}