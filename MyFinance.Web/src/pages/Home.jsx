import React, { useEffect, useState } from 'react';
import { Card, Col, Row, Statistic, Spin, Table, Tag, message, Button } from 'antd';
import { ArrowUpOutlined, ArrowDownOutlined, DollarOutlined, EyeOutlined, EyeInvisibleOutlined } from '@ant-design/icons';
import api from '../services/api';
import DashboardCharts from '../components/DashboardCharts';

export default function Home({ month, year }) {
  const [loading, setLoading] = useState(true);
  const [summary, setSummary] = useState({ total: 0, income: 0, expense: 0 });
  const [transactions, setTransactions] = useState([]);
  const [predictedFixed, setPredictedFixed] = useState(0);
  
  // Estado do Olhinho (Privacidade)
  const [visible, setVisible] = useState(true);

  // Helper para formatar ou esconder
  const formatMoney = (value) => {
    if (!visible) return '••••';
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  };

  const columns = [
    { title: 'Descrição', dataIndex: 'description', key: 'desc' },
    { 
      title: 'Categoria', dataIndex: ['category', 'name'], key: 'cat',
      render: (text) => <Tag color="blue">{text || 'Geral'}</Tag>
    },
    { 
      title: 'Valor', dataIndex: 'amount', key: 'amt',
      render: (value, record) => (
        <span style={{ color: record.type === 'Expense' ? '#cf1322' : '#3f8600', fontWeight: 'bold' }}>
          {record.type === 'Expense' ? '- ' : '+ '}
          {formatMoney(value)}
        </span>
      ),
    },
    { title: 'Data', dataIndex: 'date', key: 'date', render: (d) => new Date(d).toLocaleDateString('pt-BR') },
    { 
      title: 'Status', dataIndex: 'paid', key: 'paid',
      render: (paid) => <Tag color={paid ? 'green' : 'orange'}>{paid ? 'Pago' : 'Pendente'}</Tag>
    }
  ];

  useEffect(() => {
    fetchData();
  }, [month, year]);

  const fetchData = async () => {
    try {
      setLoading(true);
      
      // 1. Previsão Fixa
      const recurringRes = await api.get('/recurring');
      const totalFixas = recurringRes.data.reduce((acc, item) => acc + item.amount, 0);
      setPredictedFixed(totalFixas);

      // 2. Saldo Real
      const accResponse = await api.get('/accounts');
      const contas = accResponse.data || [];
      // Filtra: Soma apenas o que NÃO é cartão de crédito
      const totalBalance = contas
        .filter(c => !c.isCreditCard) 
        .reduce((acc, conta) => acc + (conta.currentBalance || 0), 0);
      // 3. Transações do Mês
      const transResponse = await api.get(`/transactions?month=${month}&year=${year}`);
      const listaTransacoes = transResponse.data;
      
      let totalIncome = 0;
      let totalExpense = 0;

      listaTransacoes.forEach(t => {
        if (t.type === 'Income') totalIncome += t.amount;
        else totalExpense += t.amount;
      });

      setSummary({ total: totalBalance, income: totalIncome, expense: totalExpense });
      setTransactions(listaTransacoes);

    } catch (error) {
      console.error("Erro:", error);
      // message.error("Erro ao carregar dados"); // Opcional: comentei para evitar spam de erro
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div style={{ textAlign: 'center', padding: 50 }}><Spin size="large" /></div>;

  return (
    <div>
      {/* CARD DE PREVISÃO (PLANEJAMENTO) */}
      <Card variant="borderless" style={{ marginBottom: 24, background: '#fff7e6', borderColor: '#ffd591' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
                <h3 style={{ margin: 0, color: '#d46b08' }}>Planejamento Mensal</h3>
                <span>Despesas fixas cadastradas para este mês: <b>{formatMoney(predictedFixed)}</b></span>
            </div>
            {/* Botão de Olhinho */}
            <Button 
                type="text" 
                icon={visible ? <EyeOutlined /> : <EyeInvisibleOutlined />} 
                onClick={() => setVisible(!visible)}
            >
                {visible ? 'Ocultar Valores' : 'Mostrar Valores'}
            </Button>
        </div>
      </Card>

      {/* KPIs */}
      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col span={8}>
          <Card variant="borderless" style={{ borderTop: '4px solid #1890ff', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Saldo Geral (Atual)"
              value={summary.total}
              formatter={(value) => <span style={{ color: '#1890ff', fontWeight: 'bold', fontSize: '24px' }}>{formatMoney(value)}</span>}
              prefix={<DollarOutlined />}
            />
          </Card>
        </Col>
        <Col span={8}>
          <Card variant="borderless" style={{ borderTop: '4px solid #3f8600', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Receitas (Neste Mês)"
              value={summary.income}
              formatter={(value) => <span style={{ color: '#3f8600', fontSize: '24px' }}>{formatMoney(value)}</span>}
              prefix={<ArrowUpOutlined />}
            />
          </Card>
        </Col>
        <Col span={8}>
          <Card variant="borderless" style={{ borderTop: '4px solid #cf1322', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}>
            <Statistic
              title="Despesas (Neste Mês)"
              value={summary.expense}
              formatter={(value) => <span style={{ color: '#cf1322', fontSize: '24px' }}>{formatMoney(value)}</span>}
              prefix={<ArrowDownOutlined />}
            />
          </Card>
        </Col>
      </Row>

      {/* GRÁFICOS E TABELA */}
      <Row gutter={24}>
        <Col span={14}>
          <Card title="Despesas por Categoria" variant="borderless" style={{ minHeight: 400, borderRadius: 8 }}>
             <DashboardCharts transactions={transactions} />
          </Card>
        </Col>

        <Col span={10}>
          <Card title="Transações do Mês" variant="borderless" style={{ minHeight: 400, borderRadius: 8 }}>
            <Table 
                dataSource={transactions} 
                columns={columns} 
                pagination={{ pageSize: 5 }} 
                size="middle"
                rowKey="id"
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
}