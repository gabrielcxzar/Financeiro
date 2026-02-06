import React, { useEffect, useState, useMemo } from 'react';
import { Card, Row, Col, Spin, Empty, Statistic, message } from 'antd';
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js';
import { Doughnut } from 'react-chartjs-2';
import api from '../services/api';
import HistoryChart from '../components/HistoryChart';

ChartJS.register(ArcElement, Tooltip, Legend);

const formatMoney = (value) => {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};

// RECEBE AS PROPS DE DATA AGORA
export default function Reports({ month, year }) {
  const [loading, setLoading] = useState(true);
  const [transactions, setTransactions] = useState([]);

  useEffect(() => {
    loadData();
  }, [month, year]); // Recarrega quando muda a data

  const loadData = async () => {
    try {
      setLoading(true);
      // AGORA PASSA O FILTRO PARA A API
      const query = month && year ? `?month=${month}&year=${year}` : '';
      const response = await api.get(`/transactions${query}`);
      setTransactions(response.data);
    } catch (error) {
      console.error(error);
      message.error("Erro ao carregar dados");
    } finally {
      setLoading(false);
    }
  };

  const chartData = useMemo(() => {
    const categories = {};
    let totalExpense = 0;

    transactions.forEach(t => {
      if (t.type === 'Expense') {
        const catName = t.category?.name || 'Outros';
        categories[catName] = (categories[catName] || 0) + t.amount;
        totalExpense += t.amount;
      }
    });

    const colors = ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40'];

    return {
      labels: Object.keys(categories),
      datasets: [{
          data: Object.values(categories),
          backgroundColor: colors,
          borderWidth: 0,
      }],
      total: totalExpense,
      hasData: totalExpense > 0 
    };
  }, [transactions]);

  if (loading) {
    return (
      <div style={{ textAlign: 'center', padding: 50 }}>
        <Spin size="large" />
      </div>
    );
  }

  return (
    <div>
      <h2 style={{ marginBottom: 16, color: '#001529' }}>Anlise de Despesas</h2>
      
      {transactions.length === 0 ? (
        <Empty description="Nenhuma transação registrada" style={{ marginTop: 50 }} />
      ) : !chartData.hasData ? ( 
        <div style={{ textAlign: 'center', padding: 50, background: '#fff', borderRadius: 8 }}>
            <Empty description="Voc ainda no cadastrou Despesas." />
            <p>Cadastre uma sada de dinheiro para ver o gráfico.</p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
        
          <Card title="Evoluo Financeira (Ms a Ms)" variant="borderless" style={{ borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
             <HistoryChart transactions={transactions} />
          </Card>

          <Row gutter={24}>
            <Col span={12}>
              <Card title="Distribui o de Gastos" variant="borderless" style={{ height: '100%', minHeight: 400, borderRadius: 8 }}>
                <div style={{ position: 'relative', height: 300, display: 'flex', justifyContent: 'center' }}>
                  <Doughnut 
                    data={chartData} 
                    options={{ 
                      maintainAspectRatio: false,
                      plugins: { legend: { position: 'bottom' } }
                    }} 
                  />
                </div>
              </Card>
            </Col>

            <Col span={12}>
              <Card title="Ranking de Categorias" variant="borderless" style={{ height: '100%', borderRadius: 8 }}>
                {chartData.labels.map((label, index) => {
                  const value = chartData.datasets[0].data[index];
                  const percent = ((value / chartData.total) * 100).toFixed(1);
                  
                  return (
                    <div key={label} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, paddingBottom: 12, borderBottom: '1px solid #f0f0f0' }}>
                      <div style={{ display: 'flex', alignItems: 'center' }}>
                        <span style={{ display: 'inline-block', width: 12, height: 12, backgroundColor: chartData.datasets[0].backgroundColor[index], marginRight: 12, borderRadius: '50%' }}></span>
                        <span style={{ fontSize: 16 }}>{label}</span>
                      </div>
                      <div style={{ textAlign: 'right' }}>
                        <div style={{ fontWeight: 'bold', fontSize: 16 }}>{formatMoney(value)}</div>
                        <div style={{ fontSize: 12, color: '#888' }}>{percent}%</div>
                      </div>
                    </div>
                  );
                })}
                
                <div style={{ marginTop: 30, textAlign: 'right', borderTop: '2px solid #f0f0f0', paddingTop: 10 }}>
                  <Statistic 
                      title="Total de Despesas" 
                      value={chartData.total} 
                      formatter={(value) => <span style={{ color: '#cf1322', fontWeight: 'bold' }}>{formatMoney(value)}</span>}
                  />
                </div>
              </Card>
            </Col>
          </Row>
        </div>
      )}
    </div>
  );
}