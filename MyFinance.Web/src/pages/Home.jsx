import React, { useEffect, useState } from 'react';
import { Card, Col, Row, Statistic, Table, Tag, Button, Grid, message, Alert, Tooltip, Space, Tabs } from 'antd';
import {
  ArrowUpOutlined,
  ArrowDownOutlined,
  CreditCardOutlined,
  DollarOutlined,
  EyeOutlined,
  EyeInvisibleOutlined,
  StarOutlined,
  WalletOutlined,
  LineChartOutlined,
  PlusOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons';
import api from '../services/api';
import DashboardCharts from '../components/DashboardCharts';
import BrandLoading from '../components/BrandLoading';
import ActionableEmptyState from '../components/ActionableEmptyState';

const { useBreakpoint } = Grid;

export default function Home({ month, year, onOpenOnboarding }) {
  const [loading, setLoading] = useState(true);
  const [summary, setSummary] = useState({
    total: 0,
    income: 0,
    expense: 0,
    pendingTotal: 0,
    projectedTotal: 0,
    cardLiability: 0,
    pendingCardLiability: 0,
    projectedCardLiability: 0,
    netWorth: 0,
    pendingNetWorth: 0,
    projectedNetWorth: 0,
  });
  const [recentTransactions, setRecentTransactions] = useState([]);
  const [categorySummary, setCategorySummary] = useState([]);
  const [predictedFixed, setPredictedFixed] = useState(0);
  const [projection, setProjection] = useState([]);
  const [projectionStart, setProjectionStart] = useState(0);
  const [visible, setVisible] = useState(true);
  const [nextOpenInvoice, setNextOpenInvoice] = useState(null);
  const [freeToSpend, setFreeToSpend] = useState({
    freeToSpendAmount: 0,
    confirmedIncome: 0,
    predictedIncome: 0,
    recurringExpenses: 0,
    essentialBudgets: 0,
    goalsContribution: 0,
    cardInvoices: 0,
    isNegative: false,
    explanation: '',
  });

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  const formatMoney = (value) => {
    if (!visible) return '••••••';
    return (value || 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  };

  const formatMonthYear = (monthNum, yearNum) => {
    const date = new Date(yearNum, monthNum - 1, 1);
    return date.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
  };

  const getStatusLabel = (record) => {
    if (record.paid) return 'Confirmado';
    return new Date(record.date) > new Date() ? 'Previsto' : 'Pendente';
  };

  const columns = [
    { title: 'Descrição', dataIndex: 'description', key: 'desc', render: (text) => <strong>{text}</strong> },
    {
      title: 'Categoria',
      dataIndex: ['category', 'name'],
      key: 'cat',
      render: (text) => <Tag color="orange" style={{ borderRadius: 6 }}>{text || 'Geral'}</Tag>,
    },
    {
      title: 'Valor',
      dataIndex: 'amount',
      key: 'amt',
      render: (value, record) => (
        <span style={{ color: record.type === 'Expense' ? '#EF4444' : '#10B981', fontWeight: 'bold' }}>
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
      render: (_, record) => {
        const label = getStatusLabel(record);
        const color = label === 'Confirmado' ? 'green' : label === 'Previsto' ? 'blue' : 'orange';
        return <Tag color={color}>{label}</Tag>;
      },
    },
  ];

  useEffect(() => {
    const controller = new AbortController();
    let isActive = true;

    const fetchData = async () => {
      try {
        setLoading(true);

        const response = await api.get(`/dashboard/summary?month=${month}&year=${year}`, {
          signal: controller.signal,
        });

        if (!isActive) return;

        const payload = response.data || {};
        const apiSummary = payload.summary || {};

        setPredictedFixed(apiSummary.predictedFixed || 0);
        setSummary({
          total: apiSummary.total || 0,
          income: apiSummary.income || 0,
          expense: apiSummary.expense || 0,
          pendingTotal: apiSummary.pendingTotal || 0,
          projectedTotal: apiSummary.projectedTotal || 0,
          cardLiability: apiSummary.cardLiability || 0,
          pendingCardLiability: apiSummary.pendingCardLiability || 0,
          projectedCardLiability: apiSummary.projectedCardLiability || 0,
          netWorth: apiSummary.netWorth || 0,
          pendingNetWorth: apiSummary.pendingNetWorth || 0,
          projectedNetWorth: apiSummary.projectedNetWorth || 0,
        });
        setRecentTransactions(payload.recentTransactions || []);
        setCategorySummary(payload.categorySummary || []);
        setProjection(payload.projection?.items || []);
        setProjectionStart(payload.projection?.startBalance ?? apiSummary.total ?? 0);
        setNextOpenInvoice(payload.nextOpenInvoice || null);
        setFreeToSpend(payload.freeToSpend || {
          freeToSpendAmount: 0,
          confirmedIncome: 0,
          predictedIncome: 0,
          recurringExpenses: 0,
          essentialBudgets: 0,
          goalsContribution: 0,
          cardInvoices: 0,
          isNegative: false,
          explanation: '',
        });
      } catch (error) {
        if (controller.signal.aborted) return;
        console.error('Erro ao carregar dashboard:', error);
        message.error(error?.message || 'Não foi possível carregar o dashboard.');
      } finally {
        if (isActive) {
          setLoading(false);
        }
      }
    };

    fetchData();

    return () => {
      isActive = false;
      controller.abort();
    };
  }, [month, year]);

  if (loading) return <BrandLoading text="Carregando painel financeiro..." />;

  // Check if system is empty (new user)
  const isSystemEmpty = summary.total === 0 && summary.income === 0 && summary.expense === 0 && recentTransactions.length === 0;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }} className="animate-fade-in">
      
      {/* HERO HEADER BAR */}
      <Card
        bordered={false}
        style={{
          background: 'linear-gradient(135deg, #0B0D12 0%, #161922 100%)',
          color: '#FFFFFF',
          borderRadius: 20,
          boxShadow: '0 10px 30px rgba(0, 0, 0, 0.12)',
        }}
        bodyStyle={{ padding: isCompact ? '18px 16px' : '24px 28px' }}
      >
        <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
              <SparklesOutlined style={{ color: '#FF6600', fontSize: 18 }} />
              <span style={{ color: '#94A3B8', fontSize: 13, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Resumo Financeiro • {formatMonthYear(month, year)}
              </span>
            </div>
            <h2 style={{ color: '#FFFFFF', margin: 0, fontSize: isCompact ? 20 : 26, fontWeight: 800 }}>
              {isSystemEmpty ? 'Bem-vindo ao seu novo painel!' : 'Visão Geral das Suas Finanças'}
            </h2>
          </div>

          <Space wrap>
            {onOpenOnboarding && (
              <Button
                type="default"
                icon={<SparklesOutlined />}
                onClick={onOpenOnboarding}
                style={{ background: 'rgba(255, 102, 0, 0.15)', borderColor: '#FF6600', color: '#FF6600', borderRadius: 10, fontWeight: 600 }}
              >
                Guia de Início
              </Button>
            )}
            <Button
              type="text"
              icon={visible ? <EyeOutlined /> : <EyeInvisibleOutlined />}
              onClick={() => setVisible(!visible)}
              style={{ color: '#CBD5E1' }}
            >
              {visible ? 'Ocultar Valores' : 'Mostrar Valores'}
            </Button>
          </Space>
        </div>
      </Card>

      {/* ZERO STATE BANNER FOR NEW USERS */}
      {isSystemEmpty && (
        <Alert
          type="warning"
          showIcon
          icon={<SparklesOutlined style={{ color: '#FF6600' }} />}
          message={<strong>Você ainda não possui dados cadastrados este mês.</strong>}
          description={
            <div style={{ marginTop: 6 }}>
              Clique em <strong>Guia de Início</strong> para configurar suas contas e categorias com 1 clique, ou use o botão <strong>+</strong> no canto da tela para criar sua primeira transação.
            </div>
          }
          style={{ borderRadius: 14, border: '1px solid rgba(255, 102, 0, 0.3)', background: 'rgba(255, 102, 0, 0.05)' }}
        />
      )}

      {/* TOP 3 PRIMARY METRIC CARDS */}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <Card
            bordered={false}
            style={{
              background: 'linear-gradient(135deg, #FF6600 0%, #FF8800 100%)',
              color: '#FFFFFF',
              borderRadius: 18,
              boxShadow: '0 8px 24px rgba(255, 102, 0, 0.3)',
            }}
            bodyStyle={{ padding: '20px 22px' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <span style={{ color: 'rgba(255, 255, 255, 0.85)', fontSize: 13, fontWeight: 600 }}>Saldo em Contas</span>
              <div style={{ width: 36, height: 36, borderRadius: 10, background: 'rgba(255, 255, 255, 0.2)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <WalletOutlined style={{ fontSize: 18, color: '#FFF' }} />
              </div>
            </div>
            <div style={{ fontSize: isCompact ? 24 : 30, fontWeight: 800, color: '#FFFFFF', letterSpacing: '-0.02em' }}>
              {formatMoney(summary.total)}
            </div>
            <div style={{ fontSize: 12, color: 'rgba(255, 255, 255, 0.8)', marginTop: 6 }}>
              Patrimônio Líquido: {formatMoney(summary.netWorth)}
            </div>
          </Card>
        </Col>

        <Col xs={24} sm={12} lg={8}>
          <Card
            bordered={false}
            style={{ borderRadius: 18, borderLeft: '5px solid #10B981' }}
            bodyStyle={{ padding: '20px 22px' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <span style={{ color: '#64748B', fontSize: 13, fontWeight: 600 }}>Receitas (Neste Mês)</span>
              <div style={{ width: 36, height: 36, borderRadius: 10, background: 'rgba(16, 185, 129, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <ArrowUpOutlined style={{ fontSize: 18, color: '#10B981' }} />
              </div>
            </div>
            <div style={{ fontSize: isCompact ? 24 : 30, fontWeight: 800, color: '#10B981', letterSpacing: '-0.02em' }}>
              {formatMoney(summary.income)}
            </div>
            <div style={{ fontSize: 12, color: '#64748B', marginTop: 6 }}>
              Confirmadas + previstas
            </div>
          </Card>
        </Col>

        <Col xs={24} sm={12} lg={8}>
          <Card
            bordered={false}
            style={{ borderRadius: 18, borderLeft: '5px solid #EF4444' }}
            bodyStyle={{ padding: '20px 22px' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <span style={{ color: '#64748B', fontSize: 13, fontWeight: 600 }}>Despesas (Neste Mês)</span>
              <div style={{ width: 36, height: 36, borderRadius: 10, background: 'rgba(239, 68, 68, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <ArrowDownOutlined style={{ fontSize: 18, color: '#EF4444' }} />
              </div>
            </div>
            <div style={{ fontSize: isCompact ? 24 : 30, fontWeight: 800, color: '#EF4444', letterSpacing: '-0.02em' }}>
              {formatMoney(summary.expense)}
            </div>
            <div style={{ fontSize: 12, color: '#64748B', marginTop: 6 }}>
              Fixas cadastradas: {formatMoney(predictedFixed)}
            </div>
          </Card>
        </Col>
      </Row>

      {/* FINANCIAL HEALTH - LIVRE PARA GASTAR */}
      <Card
        bordered={false}
        style={{
          borderRadius: 18,
          border: `1px solid ${freeToSpend.isNegative ? '#FEE2E2' : '#E0F2FE'}`,
          background: freeToSpend.isNegative ? '#FEF2F2' : '#F0F9FF',
        }}
        bodyStyle={{ padding: '20px 24px' }}
      >
        <Row gutter={[16, 16]} align="middle">
          <Col xs={24} md={10}>
            <Statistic
              title={
                <Space size={6}>
                  <strong style={{ color: '#0F172A', fontSize: 15 }}>Livre para Gastar este mês</strong>
                  <Tooltip title="Calculado a partir das receitas confirmadas/previstas subtraindo despesas recorrentes, metas, orçamentos e cartões.">
                    <InfoCircleOutlined style={{ color: '#0284C7', cursor: 'help' }} />
                  </Tooltip>
                </Space>
              }
              value={freeToSpend.freeToSpendAmount}
              formatter={(val) => (
                <div style={{ color: freeToSpend.isNegative ? '#EF4444' : '#0284C7', fontWeight: 800, fontSize: isCompact ? 26 : 32, margin: '4px 0' }}>
                  {formatMoney(val)}
                </div>
              )}
            />
          </Col>
          <Col xs={24} md={14}>
            <div style={{ fontSize: 13, color: '#334155', lineHeight: 1.6 }}>
              {freeToSpend.isNegative ? (
                <span style={{ color: '#DC2626', fontWeight: 600 }}>
                  ⚠️ Atenção: Suas despesas e metas superam o valor livre estimado para este mês.
                </span>
              ) : (
                <span style={{ color: '#0369A1', fontWeight: 600 }}>
                  ✨ Excelente! Este valor indica o saldo disponível livre para gastos discricionários sem comprometer suas contas.
                </span>
              )}
            </div>
          </Col>
        </Row>
      </Card>

      {/* CHARTS & RECENT TRANSACTIONS */}
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <Card
            title={<strong>Despesas por Categoria</strong>}
            bordered={false}
            style={{ minHeight: isCompact ? 340 : 420 }}
          >
            {categorySummary.length > 0 ? (
              <DashboardCharts categorySummary={categorySummary} compact={isCompact} />
            ) : (
              <ActionableEmptyState
                title="Sem despesas neste mês"
                description="Cadastre transações para visualizar o gráfico de categorias."
                actionLabel="Adicionar Lançamento"
                onAction={() => {}}
              />
            )}
          </Card>
        </Col>

        <Col xs={24} xl={12}>
          <Card
            title={<strong>Últimas Transações</strong>}
            bordered={false}
            style={{ minHeight: isCompact ? 340 : 420 }}
          >
            {recentTransactions.length > 0 ? (
              <Table
                dataSource={recentTransactions}
                columns={columns}
                pagination={{ pageSize: 5 }}
                size={isCompact ? 'small' : 'middle'}
                rowKey="id"
                scroll={{ x: 600 }}
              />
            ) : (
              <ActionableEmptyState
                title="Nenhuma transação cadastrada"
                description="Suas últimas movimentações aparecerão aqui assim que forem lançadas."
                actionLabel="Criar Primeira Transação"
                onAction={() => {}}
              />
            )}
          </Card>
        </Col>
      </Row>

      {/* PROJECTION & ADVANCED INDICATORS (TABBED FOR CLEANLINESS) */}
      <Card bordered={false} title={<strong>Análise Avançada & Projeção</strong>}>
        <Tabs
          items={[
            {
              key: '1',
              label: 'Projeção (6 Meses)',
              children: (
                <div>
                  <div style={{ marginBottom: 14, color: '#64748B', fontSize: 13 }}>
                    Saldo de partida considerado: <strong>{formatMoney(projectionStart)}</strong>
                  </div>
                  <Table
                    dataSource={projection}
                    rowKey={(row) => `${row.year}-${row.month}`}
                    pagination={false}
                    size="small"
                    scroll={{ x: 760 }}
                    columns={[
                      { title: 'Mês', key: 'month', render: (_, r) => formatMonthYear(r.month, r.year) },
                      { title: 'Receitas', dataIndex: 'income', key: 'inc', render: (v) => <span style={{ color: '#10B981' }}>{formatMoney(v)}</span> },
                      { title: 'Despesas', dataIndex: 'expense', key: 'exp', render: (v) => <span style={{ color: '#EF4444' }}>{formatMoney(v)}</span> },
                      { title: 'Saldo Líquido', dataIndex: 'net', key: 'net', render: (v) => <strong>{formatMoney(v)}</strong> },
                      { title: 'Saldo Projetado', dataIndex: 'projectedBalance', key: 'proj', render: (v) => <strong style={{ color: '#FF6600' }}>{formatMoney(v)}</strong> },
                    ]}
                  />
                </div>
              ),
            },
            {
              key: '2',
              label: 'Passivos & Cartões',
              children: (
                <Row gutter={[16, 16]}>
                  <Col xs={24} sm={12}>
                    <Statistic title="Passivo Atual em Cartões" value={summary.cardLiability} formatter={(v) => formatMoney(v)} prefix={<CreditCardOutlined />} />
                  </Col>
                  <Col xs={24} sm={12}>
                    <Statistic title="Passivo Projetado em Cartões" value={summary.projectedCardLiability} formatter={(v) => formatMoney(v)} />
                  </Col>
                </Row>
              ),
            },
          ]}
        />
      </Card>

    </div>
  );
}
