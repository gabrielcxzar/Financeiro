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
  const [projection, setProjection] = useState([]);
  const [projectionStart, setProjectionStart] = useState(0);
  
  // Estado do Olhinho (Privacidade)
  const [visible, setVisible] = useState(true);

  // Helper para formatar ou esconder
  const formatMoney = (value) => {
    if (!visible) return '••••';
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  };

  const formatMonthYear = (monthNum, yearNum) => {
    const date = new Date(yearNum, monthNum - 1, 1);
    return date.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
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
      const totalFixas = recurringRes.data
        .filter(item => item.type === 'Expense')
        .reduce((acc, item) => acc + item.amount, 0);
      setPredictedFixed(totalFixas);

      // 2. Saldo Real
      const accResponse = await api.get('/accounts');
      const contas = accResponse.data || [];
      // Filtra: soma apenas o que NÃO é cartão de crédito
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

      // 4. Projeção para os próximos meses
      const nextMonth = month === 12 ? 1 : month + 1;
      const nextYear = month === 12 ? year + 1 : year;
      const projectionRes = await api.get(`/recurring/projection?months=6&startMonth=${nextMonth}&startYear=${nextYear}`);
      setProjection(projectionRes.data.items || []);
      setProjectionStart(projectionRes.data.startBalance ?? totalBalance);
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

      <Card variant="borderless" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
          <h3 style={{ margin: 0 }}>Projeção dos Próximos 6 Meses</h3>
          <span style={{ color: '#888' }}>Saldo base: <b>{formatMoney(projectionStart)}</b></span>
        </div>
        <Table
          dataSource={projection}
          rowKey={(row) => `${row.year}-${row.month}`}
          pagination={false}
          size="small"
          columns={[
            { title: 'MÃªs', dataIndex: 'month', key: 'month', render: (_, row) => formatMonthYear(row.month, row.year) },
            { title: 'Receitas', dataIndex: 'income', key: 'income', render: (v) => <span style={{ color: '#3f8600' }}>{formatMoney(v)}</span> },
            { title: 'Despesas', dataIndex: 'expense', key: 'expense', render: (v) => <span style={{ color: '#cf1322' }}>{formatMoney(v)}</span> },
            { title: 'Saldo LÃ­quido', dataIndex: 'net', key: 'net', render: (v) => <span style={{ fontWeight: 'bold' }}>{formatMoney(v)}</span> },
            { title: 'Saldo Projetado', dataIndex: 'projectedBalance', key: 'projectedBalance', render: (v) => <span style={{ fontWeight: 'bold' }}>{formatMoney(v)}</span> },
          ]}
        />
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
