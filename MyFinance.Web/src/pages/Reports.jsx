import React, { useEffect, useMemo, useState } from 'react';
import { Card, Row, Col, Empty, Statistic, message, Grid } from 'antd';
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js';
import { Doughnut } from 'react-chartjs-2';
import api from '../services/api';
import HistoryChart from '../components/HistoryChart';
import BrandLoading from '../components/BrandLoading';

ChartJS.register(ArcElement, Tooltip, Legend);
const { useBreakpoint } = Grid;

const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Reports({ month, year }) {
  const [loading, setLoading] = useState(true);
  const [transactions, setTransactions] = useState([]);
  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    loadData();
  }, [month, year]);

  const loadData = async () => {
    try {
      setLoading(true);
      const query = month && year ? `?month=${month}&year=${year}` : '';
      const response = await api.get(`/transactions${query}`);
      setTransactions(response.data);
    } catch (error) {
      console.error(error);
      message.error('Erro ao carregar dados.');
    } finally {
      setLoading(false);
    }
  };

  const chartData = useMemo(() => {
    const categories = {};
    let totalExpense = 0;

    transactions.forEach((t) => {
      if (t.type === 'Expense') {
        const catName = t.category?.name || 'Outros';
        categories[catName] = (categories[catName] || 0) + t.amount;
        totalExpense += t.amount;
      }
    });

    const colors = ['#FF6384', '#36A2EB', '#FFCE56', '#4BC0C0', '#9966FF', '#FF9F40'];

    return {
      labels: Object.keys(categories),
      datasets: [
        {
          data: Object.values(categories),
          backgroundColor: colors,
          borderWidth: 0,
        },
      ],
      total: totalExpense,
      hasData: totalExpense > 0,
    };
  }, [transactions]);

  if (loading) {
    return <BrandLoading text="Preparando relatorios..." />;
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
      <h2 style={{ margin: 0, color: '#001529' }}>Analise de Despesas</h2>

      {transactions.length === 0 ? (
        <Empty description="Nenhuma transacao registrada" style={{ marginTop: 24 }} />
      ) : !chartData.hasData ? (
        <div style={{ textAlign: 'center', padding: isCompact ? 24 : 50, background: '#fff', borderRadius: 8 }}>
          <Empty description="Voce ainda nao cadastrou despesas." />
          <p>Cadastre uma saida de dinheiro para ver o grafico.</p>
        </div>
      ) : (
        <>
          <Card
            title="Evolucao Financeira (Mes a Mes)"
            variant="borderless"
            style={{ borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
          >
            <HistoryChart transactions={transactions} compact={isCompact} />
          </Card>

          <Row gutter={[16, 16]}>
            <Col xs={24} lg={12}>
              <Card
                title="Distribuicao de Gastos"
                variant="borderless"
                style={{ height: '100%', minHeight: isCompact ? 340 : 400, borderRadius: 8 }}
              >
                <div style={{ position: 'relative', height: isCompact ? 240 : 300, display: 'flex', justifyContent: 'center' }}>
                  <Doughnut
                    data={chartData}
                    options={{
                      maintainAspectRatio: false,
                      plugins: {
                        legend: {
                          position: 'bottom',
                          labels: { boxWidth: isCompact ? 10 : 14, font: { size: isCompact ? 11 : 12 } },
                        },
                      },
                    }}
                  />
                </div>
              </Card>
            </Col>

            <Col xs={24} lg={12}>
              <Card title="Ranking de Categorias" variant="borderless" style={{ height: '100%', borderRadius: 8 }}>
                {chartData.labels.map((label, index) => {
                  const value = chartData.datasets[0].data[index];
                  const percent = ((value / chartData.total) * 100).toFixed(1);

                  return (
                    <div
                      key={label}
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        gap: 8,
                        marginBottom: 12,
                        paddingBottom: 10,
                        borderBottom: '1px solid #f0f0f0',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', minWidth: 0 }}>
                        <span
                          style={{
                            display: 'inline-block',
                            width: 12,
                            height: 12,
                            backgroundColor: chartData.datasets[0].backgroundColor[index],
                            marginRight: 10,
                            borderRadius: '50%',
                            flexShrink: 0,
                          }}
                        />
                        <span
                          style={{
                            fontSize: isCompact ? 14 : 16,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                          }}
                        >
                          {label}
                        </span>
                      </div>

                      <div style={{ textAlign: 'right', flexShrink: 0 }}>
                        <div style={{ fontWeight: 'bold', fontSize: isCompact ? 14 : 16 }}>{formatMoney(value)}</div>
                        <div style={{ fontSize: 12, color: '#888' }}>{percent}%</div>
                      </div>
                    </div>
                  );
                })}

                <div style={{ marginTop: 20, textAlign: 'right', borderTop: '2px solid #f0f0f0', paddingTop: 10 }}>
                  <Statistic
                    title="Total de Despesas"
                    value={chartData.total}
                    formatter={(value) => (
                      <span style={{ color: '#cf1322', fontWeight: 'bold' }}>{formatMoney(value)}</span>
                    )}
                  />
                </div>
              </Card>
            </Col>
          </Row>
        </>
      )}
    </div>
  );
}
