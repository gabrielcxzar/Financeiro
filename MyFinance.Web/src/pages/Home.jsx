import React, { useCallback, useEffect, useState } from 'react';
import { Card, Col, Row, Statistic, Table, Tag, Button, Grid } from 'antd';
import {
  ArrowUpOutlined,
  ArrowDownOutlined,
  DollarOutlined,
  EyeOutlined,
  EyeInvisibleOutlined,
} from '@ant-design/icons';
import api from '../services/api';
import DashboardCharts from '../components/DashboardCharts';
import BrandLoading from '../components/BrandLoading';

const { useBreakpoint } = Grid;

export default function Home({ month, year }) {
  const [loading, setLoading] = useState(true);
  const [summary, setSummary] = useState({ total: 0, income: 0, expense: 0 });
  const [transactions, setTransactions] = useState([]);
  const [predictedFixed, setPredictedFixed] = useState(0);
  const [projection, setProjection] = useState([]);
  const [projectionStart, setProjectionStart] = useState(0);
  const [visible, setVisible] = useState(true);

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  const formatMoney = (value) => {
    if (!visible) return '****';
    return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  };

  const formatMonthYear = (monthNum, yearNum) => {
    const date = new Date(yearNum, monthNum - 1, 1);
    return date.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
  };

  const columns = [
    { title: 'Descricao', dataIndex: 'description', key: 'desc' },
    {
      title: 'Categoria',
      dataIndex: ['category', 'name'],
      key: 'cat',
      render: (text) => <Tag color="blue">{text || 'Geral'}</Tag>,
    },
    {
      title: 'Valor',
      dataIndex: 'amount',
      key: 'amt',
      render: (value, record) => (
        <span style={{ color: record.type === 'Expense' ? '#cf1322' : '#3f8600', fontWeight: 'bold' }}>
          {record.type === 'Expense' ? '- ' : '+ '}
          {formatMoney(value)}
        </span>
      ),
    },
    {
      title: 'Data',
      dataIndex: 'date',
      key: 'date',
      render: (d) => new Date(d).toLocaleDateString('pt-BR'),
    },
    {
      title: 'Status',
      dataIndex: 'paid',
      key: 'paid',
      render: (paid) => <Tag color={paid ? 'green' : 'orange'}>{paid ? 'Pago' : 'Pendente'}</Tag>,
    },
  ];

  const fetchData = useCallback(async () => {
    try {
      setLoading(true);

      const recurringRes = await api.get('/recurring');
      const totalFixas = recurringRes.data
        .filter((item) => item.type === 'Expense')
        .reduce((acc, item) => acc + item.amount, 0);
      setPredictedFixed(totalFixas);

      const accResponse = await api.get('/accounts');
      const contas = accResponse.data || [];
      const totalBalance = contas
        .filter((c) => !c.isCreditCard)
        .reduce((acc, conta) => acc + (conta.currentBalance || 0), 0);

      const transResponse = await api.get(`/transactions?month=${month}&year=${year}`);
      const listaTransacoes = transResponse.data;

      let totalIncome = 0;
      let totalExpense = 0;

      listaTransacoes.forEach((t) => {
        if (t.type === 'Income') totalIncome += t.amount;
        else totalExpense += t.amount;
      });

      setSummary({ total: totalBalance, income: totalIncome, expense: totalExpense });
      setTransactions(listaTransacoes);

      const nextMonth = month === 12 ? 1 : month + 1;
      const nextYear = month === 12 ? year + 1 : year;
      const projectionRes = await api.get(
        `/recurring/projection?months=6&startMonth=${nextMonth}&startYear=${nextYear}`,
      );
      setProjection(projectionRes.data.items || []);
      setProjectionStart(projectionRes.data.startBalance ?? totalBalance);
    } catch (error) {
      console.error('Erro ao carregar dashboard:', error);
    } finally {
      setLoading(false);
    }
  }, [month, year]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  if (loading) return <BrandLoading text="Carregando painel financeiro..." />;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card
        variant="borderless"
        style={{ background: '#fff7e6', borderColor: '#ffd591' }}
        bodyStyle={{ padding: isCompact ? 14 : 20 }}
      >
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 12,
          }}
        >
          <div>
            <h3 style={{ margin: 0, color: '#d46b08' }}>Planejamento Mensal</h3>
            <span>
              Despesas fixas cadastradas para este mes: <b>{formatMoney(predictedFixed)}</b>
            </span>
          </div>
          <Button
            type="text"
            size={isCompact ? 'small' : 'middle'}
            icon={visible ? <EyeOutlined /> : <EyeInvisibleOutlined />}
            onClick={() => setVisible(!visible)}
          >
            {visible ? 'Ocultar valores' : 'Mostrar valores'}
          </Button>
        </div>
      </Card>

      <Card variant="borderless" bodyStyle={{ padding: isCompact ? 14 : 20 }}>
        <div
          style={{
            display: 'flex',
            flexWrap: 'wrap',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: 12,
            gap: 8,
          }}
        >
          <h3 style={{ margin: 0 }}>Projecao dos proximos 6 meses</h3>
          <span style={{ color: '#888' }}>
            Saldo base: <b>{formatMoney(projectionStart)}</b>
          </span>
        </div>

        <Table
          dataSource={projection}
          rowKey={(row) => `${row.year}-${row.month}`}
          pagination={false}
          size={isCompact ? 'small' : 'middle'}
          scroll={{ x: 760 }}
          columns={[
            {
              title: 'Mes',
              dataIndex: 'month',
              key: 'month',
              render: (_, row) => formatMonthYear(row.month, row.year),
            },
            {
              title: 'Receitas',
              dataIndex: 'income',
              key: 'income',
              render: (v) => <span style={{ color: '#3f8600' }}>{formatMoney(v)}</span>,
            },
            {
              title: 'Despesas',
              dataIndex: 'expense',
              key: 'expense',
              render: (v) => <span style={{ color: '#cf1322' }}>{formatMoney(v)}</span>,
            },
            {
              title: 'Saldo Liquido',
              dataIndex: 'net',
              key: 'net',
              render: (v) => <span style={{ fontWeight: 'bold' }}>{formatMoney(v)}</span>,
            },
            {
              title: 'Saldo Projetado',
              dataIndex: 'projectedBalance',
              key: 'projectedBalance',
              render: (v) => <span style={{ fontWeight: 'bold' }}>{formatMoney(v)}</span>,
            },
          ]}
        />
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <Card
            variant="borderless"
            style={{ borderTop: '4px solid #1890ff', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
          >
            <Statistic
              title="Saldo Geral (Atual)"
              value={summary.total}
              formatter={(value) => (
                <span style={{ color: '#1890ff', fontWeight: 'bold', fontSize: isCompact ? 20 : 24 }}>
                  {formatMoney(value)}
                </span>
              )}
              prefix={<DollarOutlined />}
            />
          </Card>
        </Col>

        <Col xs={24} sm={12} lg={8}>
          <Card
            variant="borderless"
            style={{ borderTop: '4px solid #3f8600', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
          >
            <Statistic
              title="Receitas (Neste Mes)"
              value={summary.income}
              formatter={(value) => (
                <span style={{ color: '#3f8600', fontSize: isCompact ? 20 : 24 }}>{formatMoney(value)}</span>
              )}
              prefix={<ArrowUpOutlined />}
            />
          </Card>
        </Col>

        <Col xs={24} sm={12} lg={8}>
          <Card
            variant="borderless"
            style={{ borderTop: '4px solid #cf1322', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
          >
            <Statistic
              title="Despesas (Neste Mes)"
              value={summary.expense}
              formatter={(value) => (
                <span style={{ color: '#cf1322', fontSize: isCompact ? 20 : 24 }}>{formatMoney(value)}</span>
              )}
              prefix={<ArrowDownOutlined />}
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={14}>
          <Card
            title="Despesas por Categoria"
            variant="borderless"
            style={{ minHeight: isCompact ? 340 : 400, borderRadius: 8 }}
          >
            <DashboardCharts transactions={transactions} compact={isCompact} />
          </Card>
        </Col>

        <Col xs={24} xl={10}>
          <Card
            title="Transacoes do Mes"
            variant="borderless"
            style={{ minHeight: isCompact ? 340 : 400, borderRadius: 8 }}
          >
            <Table
              dataSource={transactions}
              columns={columns}
              pagination={{ pageSize: isCompact ? 4 : 5 }}
              size={isCompact ? 'small' : 'middle'}
              rowKey="id"
              scroll={{ x: 700 }}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
}
