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
    invoicePayments: 0,
  });
  const [prevExpense, setPrevExpense] = useState(0);
  const [recentTransactions, setRecentTransactions] = useState([]);
  const [recentSettlements, setRecentSettlements] = useState([]);
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
        if (typeof performance !== 'undefined') {
          performance.mark('finflow:dashboard:load-start');
        }
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
          invoicePayments: apiSummary.invoicePayments || 0,
        });

        // Fetch previous month for Month-over-Month comparison
        const prevMonth = month === 1 ? 12 : month - 1;
        const prevYear = month === 1 ? year - 1 : year;
        try {
          const prevRes = await api.get(`/dashboard/summary?month=${prevMonth}&year=${prevYear}`, {
            signal: controller.signal,
          });
          setPrevExpense(prevRes.data?.summary?.expense || 0);
        } catch {
          setPrevExpense(0);
        }

        setRecentTransactions(payload.recentTransactions || []);
        setRecentSettlements(payload.recentSettlements || []);
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
          if (typeof performance !== 'undefined') {
            performance.mark('finflow:dashboard:usable');
            try {
              performance.measure('finflow:dashboard:load', 'finflow:dashboard:load-start', 'finflow:dashboard:usable');
            } catch {
              // Timing instrumentation must never affect dashboard behavior.
            }
          }
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
      
      {/* HEADER BAR */}
      <Card
        bordered={false}
        style={{
          background: '#FFFFFF',
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
        bodyStyle={{ padding: isCompact ? '16px 14px' : '20px 24px' }}
      >
        <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
              <span style={{ display: 'inline-block', width: 8, height: 8, borderRadius: '50%', background: '#10B981' }} />
              <span style={{ color: '#64748B', fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                Painel Geral • {formatMonthYear(month, year)}
              </span>
            </div>
            <h2 style={{ color: '#0F172A', margin: 0, fontSize: isCompact ? 19 : 24, fontWeight: 800, letterSpacing: '-0.02em' }}>
              {isSystemEmpty ? 'Bem-vindo ao Finflow' : 'Visão Geral das Suas Finanças'}
            </h2>
          </div>

          <Space wrap>
            {onOpenOnboarding && (
              <Button
                type="default"
                icon={<StarOutlined style={{ color: '#0F172A' }} />}
                onClick={onOpenOnboarding}
                style={{ borderRadius: 8, fontWeight: 600 }}
              >
                Guia de Início
              </Button>
            )}
            <Button
              type="text"
              icon={visible ? <EyeOutlined /> : <EyeInvisibleOutlined />}
              onClick={() => setVisible(!visible)}
              style={{ color: '#64748B', fontWeight: 500 }}
            >
              {visible ? 'Ocultar Valores' : 'Mostrar Valores'}
            </Button>
          </Space>
        </div>
      </Card>

      {/* ZERO STATE BANNER FOR NEW USERS */}
      {isSystemEmpty && (
        <Alert
          type="info"
          showIcon
          message={<strong>Você ainda não possui lançamentos cadastrados neste mês.</strong>}
          description={
            <div style={{ marginTop: 6, color: '#475569' }}>
              Clique em <strong>Guia de Início</strong> para configurar suas contas e categorias com 1 clique, ou use o botão <strong>+ Nova Transação</strong> para criar seu primeiro registro.
            </div>
          }
          style={{ borderRadius: 12, border: '1px solid #E2E8F0', background: '#FFFFFF' }}
        />
      )}

      {/* TOP PRIMARY METRIC CARDS */}
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={8}>
          <Card
            bordered={false}
            style={{
              background: '#FFFFFF',
              borderRadius: 14,
              border: '1px solid #E2E8F0',
            }}
            bodyStyle={{ padding: '20px 22px' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ color: '#64748B', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                Saldo em Contas
              </span>
              <div style={{ width: 32, height: 32, borderRadius: 8, background: '#F1F5F9', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <WalletOutlined style={{ fontSize: 16, color: '#0F172A' }} />
              </div>
            </div>
            <div style={{ fontSize: isCompact ? 24 : 28, fontWeight: 800, color: '#0F172A', letterSpacing: '-0.03em' }} className="font-tabular">
              {formatMoney(summary.total)}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 10 }}>
              <Tag style={{ background: '#F8FAFC', border: '1px solid #E2E8F0', color: '#475569', margin: 0, fontSize: 11 }}>
                Patrimônio: {formatMoney(summary.netWorth)}
              </Tag>
            </div>
          </Card>
        </Col>

        <Col xs={24} sm={12} lg={8}>
          <Card
            bordered={false}
            style={{
              background: '#FFFFFF',
              borderRadius: 14,
              border: '1px solid #E2E8F0',
            }}
            bodyStyle={{ padding: '20px 22px' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ color: '#64748B', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                Receitas do Mês
              </span>
              <div style={{ width: 32, height: 32, borderRadius: 8, background: '#ECFDF5', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <ArrowUpOutlined style={{ fontSize: 16, color: '#10B981' }} />
              </div>
            </div>
            <div style={{ fontSize: isCompact ? 24 : 28, fontWeight: 800, color: '#10B981', letterSpacing: '-0.03em' }} className="font-tabular">
              {formatMoney(summary.income)}
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 10 }}>
              <Tag style={{ background: '#ECFDF5', border: '1px solid #A7F3D0', color: '#047857', margin: 0, fontSize: 11 }}>
                Confirmadas + previstas
              </Tag>
            </div>
          </Card>
        </Col>

        <Col xs={24} sm={12} lg={8}>
          <Card
            bordered={false}
            style={{
              background: '#FFFFFF',
              borderRadius: 14,
              border: '1px solid #E2E8F0',
            }}
            bodyStyle={{ padding: '20px 22px' }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ color: '#64748B', fontSize: 11, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                Despesas do Mês
              </span>
              <div style={{ width: 32, height: 32, borderRadius: 8, background: '#FFF1F2', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <ArrowDownOutlined style={{ fontSize: 16, color: '#F43F5E' }} />
              </div>
            </div>
            <div style={{ fontSize: isCompact ? 24 : 28, fontWeight: 800, color: '#0F172A', letterSpacing: '-0.03em' }} className="font-tabular">
              {formatMoney(summary.expense)}
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 10, flexWrap: 'wrap', gap: 6 }}>
              <span style={{ fontSize: 12, color: '#64748B' }}>
                Fixas: {formatMoney(predictedFixed)}
              </span>
              {prevExpense > 0 && summary.expense > 0 && (
                (() => {
                  const momDiff = Math.round(((summary.expense - prevExpense) / prevExpense) * 100);
                  const isBetter = momDiff <= 0;
                  return (
                    <Tag style={{ background: isBetter ? '#ECFDF5' : '#FFF1F2', border: `1px solid ${isBetter ? '#A7F3D0' : '#FECDD3'}`, color: isBetter ? '#047857' : '#BE123C', borderRadius: 9999, margin: 0, fontSize: 11 }}>
                      {momDiff > 0 ? `+${momDiff}%` : `${momDiff}%`} vs mês ant.
                    </Tag>
                  );
                })()
              )}
            </div>
          </Card>
        </Col>
      </Row>

      {summary.invoicePayments > 0 && (
        <Card size="small" title={<strong>Liquidação de Faturas</strong>} bordered={false} style={{ borderRadius: 12, border: '1px solid #E2E8F0' }}>
          <Statistic title="Total Liquidado no Mês" value={summary.invoicePayments} formatter={formatMoney} />
          {recentSettlements.length > 0 && (
            <div style={{ marginTop: 8, color: '#64748B', fontSize: 13 }}>
              {recentSettlements.slice(0, 3).map((item) => (
                <div key={item.id}>{item.description} · {formatMoney(item.amount)}</div>
              ))}
            </div>
          )}
        </Card>
      )}

      {/* FINANCIAL HEALTH - LIVRE PARA GASTAR */}
      <Card
        bordered={false}
        style={{
          borderRadius: 14,
          border: `1px solid ${freeToSpend.isNegative ? '#FECDD3' : '#E2E8F0'}`,
          background: freeToSpend.isNegative ? '#FFF1F2' : '#FFFFFF',
        }}
        bodyStyle={{ padding: '18px 22px' }}
      >
        <Row gutter={[16, 16]} align="middle">
          <Col xs={24} md={10}>
            <Statistic
              title={
                <Space size={6}>
                  <strong style={{ color: '#0F172A', fontSize: 14 }}>Livre para Gastar este mês</strong>
                  <Tooltip title="Calculado a partir das receitas confirmadas/previstas subtraindo despesas recorrentes, metas, orçamentos e cartões.">
                    <InfoCircleOutlined style={{ color: '#64748B', cursor: 'help' }} />
                  </Tooltip>
                </Space>
              }
              value={freeToSpend.freeToSpendAmount}
              formatter={(val) => (
                <div style={{ color: freeToSpend.isNegative ? '#BE123C' : '#0F172A', fontWeight: 800, fontSize: isCompact ? 24 : 30, margin: '4px 0' }} className="font-tabular">
                  {formatMoney(val)}
                </div>
              )}
            />
          </Col>
          <Col xs={24} md={14}>
            <div style={{ fontSize: 13, color: '#475569', lineHeight: 1.6 }}>
              {freeToSpend.isNegative ? (
                <span style={{ color: '#BE123C', fontWeight: 600 }}>
                  ⚠️ Atenção: Suas despesas e metas superam o valor livre estimado para este mês.
                </span>
              ) : (
                <span style={{ color: '#334155' }}>
                  Saldo disponível estimado para gastos discricionários sem comprometer contas fixas ou metas planejadas.
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
