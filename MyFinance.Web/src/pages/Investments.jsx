import React, { useEffect, useMemo, useState } from 'react';
import { Card, Table, Button, Modal, Form, Input, InputNumber, Tabs, message, Tag, Grid, Select } from 'antd';
import api from '../services/api';

const { useBreakpoint } = Grid;
const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
const localDateInputValue = (date = new Date()) => [date.getFullYear(), String(date.getMonth() + 1).padStart(2, '0'), String(date.getDate()).padStart(2, '0')].join('-');

export default function Investments() {
  const [tesouro, setTesouro] = useState({ date: '', items: [] });
  const [loadingTesouro, setLoadingTesouro] = useState(true);

  const [holdings, setHoldings] = useState([]);
  const [loadingFii, setLoadingFii] = useState(true);
  const [fixedIncome, setFixedIncome] = useState([]);
  const [loadingFixedIncome, setLoadingFixedIncome] = useState(true);
  const [fixedModalOpen, setFixedModalOpen] = useState(false);
  const [balanceModalOpen, setBalanceModalOpen] = useState(false);
  const [quoteModalOpen, setQuoteModalOpen] = useState(false);
  const [editingFixed, setEditingFixed] = useState(null);
  const [editingBalance, setEditingBalance] = useState(null);
  const [editingQuote, setEditingQuote] = useState(null);
  const [refreshingQuotes, setRefreshingQuotes] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form] = Form.useForm();
  const [fixedForm] = Form.useForm();
  const [balanceForm] = Form.useForm();
  const [quoteForm] = Form.useForm();

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    loadTesouro();
    loadHoldings();
    loadFixedIncome();
  }, []);

  const loadTesouro = async () => {
    setLoadingTesouro(true);
    try {
      const { data } = await api.get('/tesouro/latest');
      setTesouro(data);
    } catch {
      message.error('Erro ao carregar Tesouro Direto');
    } finally {
      setLoadingTesouro(false);
    }
  };

  const loadHoldings = async () => {
    setLoadingFii(true);
    try {
      const { data } = await api.get('/fiiholdings');
      setHoldings(data);
    } catch {
      message.error('Erro ao carregar FIIs');
    } finally {
      setLoadingFii(false);
    }
  };

  const loadFixedIncome = async () => {
    setLoadingFixedIncome(true);
    try {
      const { data } = await api.get('/fixedincomeholdings');
      setFixedIncome(data);
    } catch {
      message.error('Erro ao carregar renda fixa');
    } finally {
      setLoadingFixedIncome(false);
    }
  };

  const openModal = (record) => {
    setEditing(record || null);
    if (record) {
      form.setFieldsValue({
        ticker: record.ticker,
        shares: record.shares,
        avgPrice: record.avgPrice,
        notes: record.notes || '',
      });
    } else {
      form.resetFields();
    }
    setIsModalOpen(true);
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      await api.post('/fiiholdings', {
        ...values,
        shares: Number(values.shares),
        avgPrice: Number(values.avgPrice),
      });
      message.success('Posicao salva');
      setIsModalOpen(false);
      loadHoldings();
    } catch {
      message.error('Erro ao salvar');
    }
  };

  const handleDelete = async (id) => {
    try {
      await api.delete(`/fiiholdings/${id}`);
      message.success('Removido');
      loadHoldings();
    } catch {
      message.error('Erro ao remover');
    }
  };

  const openFixedModal = (record) => {
    setEditingFixed(record || null);
    if (record) fixedForm.setFieldsValue({ ...record, maturityDate: record.maturityDate || '', balanceAsOfDate: record.balanceAsOfDate || '' });
    else fixedForm.resetFields();
    setFixedModalOpen(true);
  };

  const handleFixedSave = async () => {
    try {
      const values = await fixedForm.validateFields();
      const nullable = (value) => value === '' || value === undefined ? null : value;
      await api.post('/fixedincomeholdings', {
        ...values,
        institution: nullable(values.institution),
        benchmark: nullable(values.benchmark),
        contractedRate: values.contractedRate == null || values.contractedRate === '' ? null : Number(values.contractedRate),
        contractedRateUnit: nullable(values.contractedRateUnit),
        maturityDate: nullable(values.maturityDate),
        liquidity: nullable(values.liquidity),
        principalAmount: values.principalAmount == null || values.principalAmount === '' ? null : Number(values.principalAmount),
        knownBalance: values.knownBalance == null || values.knownBalance === '' ? null : Number(values.knownBalance),
        balanceAsOfDate: nullable(values.balanceAsOfDate),
        valuationSource: nullable(values.valuationSource),
        notes: nullable(values.notes),
      });
      message.success('Investimento de renda fixa salvo');
      setFixedModalOpen(false);
      loadFixedIncome();
    } catch (error) {
      if (error?.errorFields) return;
      message.error('Erro ao salvar renda fixa');
    }
  };

  const openBalanceModal = (record) => {
    setEditingBalance(record);
    balanceForm.setFieldsValue({ knownBalance: Number(record.knownBalance || 0), asOfDate: record.balanceAsOfDate || '', source: record.valuationSource || 'manual' });
    setBalanceModalOpen(true);
  };

  const handleBalanceSave = async () => {
    try {
      const values = await balanceForm.validateFields();
      await api.put(`/fixedincomeholdings/${editingBalance.id}/balance`, {
        knownBalance: Number(values.knownBalance),
        asOfDate: values.asOfDate,
        source: values.source || 'manual',
      });
      message.success('Saldo conhecido atualizado');
      setBalanceModalOpen(false);
      loadFixedIncome();
    } catch (error) {
      if (error?.errorFields) return;
      message.error('Erro ao atualizar saldo');
    }
  };

  const handleFixedDelete = async (id) => {
    try {
      await api.delete(`/fixedincomeholdings/${id}`);
      message.success('Investimento removido');
      loadFixedIncome();
    } catch {
      message.error('Erro ao remover investimento');
    }
  };

  const openQuoteModal = (record) => {
    setEditingQuote(record);
    quoteForm.setFieldsValue({ price: record.currentPrice ?? undefined, asOfDate: record.quoteAsOfDate || localDateInputValue(), source: 'manual quote' });
    setQuoteModalOpen(true);
  };

  const handleQuoteSave = async () => {
    try {
      const values = await quoteForm.validateFields();
      await api.put(`/fiiholdings/${editingQuote.id}/quote`, {
        price: Number(values.price),
        asOfDate: values.asOfDate,
        source: values.source || 'manual',
      });
      message.success('Cotação manual salva');
      setQuoteModalOpen(false);
      loadHoldings();
    } catch (error) {
      if (error?.errorFields) return;
      message.error('Erro ao salvar cotação');
    }
  };

  const refreshQuotes = async () => {
    setRefreshingQuotes(true);
    try {
      const { data } = await api.post('/fiiholdings/quotes/refresh');
      message.info(`Cotações atualizadas: ${data.updatedCount}; sem atualização: ${data.failedCount}`);
      loadHoldings();
    } catch (error) {
      const providerMissing = error?.response?.status === 503;
      message.error(providerMissing ? 'Provider de cotações não configurado. As cotações manuais continuam disponíveis.' : 'Não foi possível atualizar as cotações; os valores salvos foram preservados.');
    } finally {
      setRefreshingQuotes(false);
    }
  };

  const totalFiiMarket = holdings.reduce((acc, h) => acc + ((Number(h.shares) || 0) * (Number(h.currentPrice) || 0)), 0);
  const totalFixedKnown = fixedIncome.reduce((acc, item) => acc + (Number(item.knownBalance) || 0), 0);
  const totalKnown = totalFixedKnown + totalFiiMarket;
  const valuationDates = [...new Set([
    ...fixedIncome.map((item) => item.balanceAsOfDate),
    ...holdings.map((item) => item.quoteAsOfDate),
  ].filter(Boolean))].sort();

  const fiiColumns = [
    {
      title: 'Ticker',
      dataIndex: 'ticker',
      key: 'ticker',
      render: (t) => (
        <span
          style={{
            display: 'inline-block',
            padding: '2px 10px',
            borderRadius: 10,
            background: '#F1F5F9',
            border: '1px solid #E2E8F0',
            fontWeight: 700,
            color: '#0F172A',
            fontSize: 13,
          }}
        >
          {t}
        </span>
      ),
    },
    {
      title: 'Cotas',
      dataIndex: 'shares',
      key: 'shares',
      render: (v) => <span style={{ fontFeatureSettings: '"tnum" 1', fontWeight: 600 }}>{v}</span>,
    },
    {
      title: 'Preço Médio',
      dataIndex: 'avgPrice',
      key: 'avgPrice',
      render: (v) => <span style={{ fontFeatureSettings: '"tnum" 1' }}>{formatMoney(v)}</span>,
    },
    {
      title: 'Total Investido',
      key: 'total',
      render: (_, record) => {
        const total = (Number(record.shares) || 0) * (Number(record.avgPrice) || 0);
        return <strong style={{ color: '#0F172A', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(total)}</strong>;
      },
    },
    {
      title: 'Cotação',
      dataIndex: 'currentPrice',
      key: 'currentPrice',
      render: (value, record) => value == null ? <Tag>Sem cotação</Tag> : <span>{formatMoney(Number(value))}<br /><small>{record.quoteAsOfDate || ''} · {record.quoteSource || 'origem desconhecida'}</small></span>,
    },
    {
      title: 'Valor de mercado',
      key: 'marketValue',
      render: (_, record) => record.currentPrice == null ? '-' : formatMoney((Number(record.shares) || 0) * Number(record.currentPrice)),
    },
    {
      title: 'Ganho / perda',
      key: 'gain',
      render: (_, record) => {
        if (record.currentPrice == null) return '-';
        const gain = (Number(record.shares) || 0) * (Number(record.currentPrice) - Number(record.avgPrice));
        return <span style={{ color: gain >= 0 ? '#059669' : '#DC2626' }}>{formatMoney(gain)}</span>;
      },
    },
    {
      title: 'Retorno',
      key: 'return',
      render: (_, record) => {
        if (record.currentPrice == null) return '-';
        const cost = (Number(record.shares) || 0) * (Number(record.avgPrice) || 0);
        const gain = (Number(record.shares) || 0) * (Number(record.currentPrice) - Number(record.avgPrice));
        return cost <= 0 ? '-' : `${(gain / cost * 100).toLocaleString('pt-BR', { maximumFractionDigits: 2 })}%`;
      },
    },
    {
      title: 'Anotações',
      dataIndex: 'notes',
      key: 'notes',
      render: (t) => <span style={{ color: '#64748B', fontSize: 13 }}>{t || '-'}</span>,
    },
    {
      title: 'Ações',
      key: 'actions',
      fixed: isCompact ? undefined : 'right',
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          <Button size="small" onClick={() => openQuoteModal(record)}>
            Cotação manual
          </Button>
          <Button size="small" onClick={() => openModal(record)}>
            Editar
          </Button>
          <Button size="small" danger onClick={() => handleDelete(record.id)}>
            Excluir
          </Button>
        </div>
      ),
    },
  ];

  const fixedColumns = [
    { title: 'Investimento', dataIndex: 'name', key: 'name', render: (value) => <strong>{value}</strong> },
    { title: 'Instituição', dataIndex: 'institution', key: 'institution', render: (value) => value || '-' },
    { title: 'Produto', dataIndex: 'productType', key: 'productType' },
    {
      title: 'Indexador / taxa',
      key: 'rate',
      render: (_, item) => {
        if (item.contractedRate == null) return item.benchmark || '-';
        const rate = Number(item.contractedRate).toLocaleString('pt-BR', { maximumFractionDigits: 4 });
        return item.contractedRateUnit === 'percent_of_benchmark' ? `${rate}% ${item.benchmark || ''}` : `${rate}% a.a.`;
      },
    },
    {
      title: 'Vencimento / liquidez',
      key: 'terms',
      render: (_, item) => [item.maturityDate, item.liquidity].filter(Boolean).join(' / ') || '-',
    },
    {
      title: 'Último saldo conhecido',
      key: 'knownBalance',
      render: (_, item) => <span>
        {item.knownBalance == null ? <Tag>Sem saldo</Tag> : formatMoney(Number(item.knownBalance))}<br />
        <small>{item.principalAmount == null ? 'Principal não informado' : `Principal conhecido: ${formatMoney(Number(item.principalAmount))}`} · conhecido em {item.balanceAsOfDate || '-'} · {item.valuationSource || 'origem não informada'}</small>
      </span>,
    },
    {
      title: 'Ações',
      key: 'actions',
      render: (_, item) => <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        <Button size="small" onClick={() => openBalanceModal(item)}>Atualizar saldo</Button>
        <Button size="small" onClick={() => openFixedModal(item)}>Editar</Button>
        <Button size="small" danger onClick={() => handleFixedDelete(item.id)}>Excluir</Button>
      </div>,
    },
  ];

  const tesouroColumns = useMemo(
    () => [
      {
        title: 'Título',
        dataIndex: 'title',
        key: 'title',
        render: (t) => <strong style={{ color: '#0F172A' }}>{t}</strong>,
      },
      {
        title: 'Tipo',
        dataIndex: 'type',
        key: 'type',
        render: (t) => (
          <span
            style={{
              padding: '2px 8px',
              borderRadius: 8,
              background: '#F1F5F9',
              fontSize: 12,
              color: '#475569',
            }}
          >
            {t}
          </span>
        ),
      },
      {
        title: 'Taxa Compra',
        dataIndex: 'buyRate',
        key: 'buyRate',
        render: (v) => (v != null ? <span style={{ fontFeatureSettings: '"tnum" 1', color: '#10B981', fontWeight: 600 }}>{v}%</span> : '-'),
      },
      {
        title: 'Taxa Venda',
        dataIndex: 'sellRate',
        key: 'sellRate',
        render: (v) => (v != null ? <span style={{ fontFeatureSettings: '"tnum" 1' }}>{v}%</span> : '-'),
      },
      {
        title: 'PU Compra',
        dataIndex: 'buyPrice',
        key: 'buyPrice',
        render: (v) => (v != null ? <span style={{ fontFeatureSettings: '"tnum" 1' }}>{formatMoney(v)}</span> : '-'),
      },
      {
        title: 'PU Venda',
        dataIndex: 'sellPrice',
        key: 'sellPrice',
        render: (v) => (v != null ? <span style={{ fontFeatureSettings: '"tnum" 1' }}>{formatMoney(v)}</span> : '-'),
      },
    ],
    [],
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 16,
          background: '#FFFFFF',
          padding: isCompact ? '16px' : '20px 24px',
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#0F172A', letterSpacing: '-0.02em' }}>
            Investimentos
          </h2>
          <span style={{ color: '#64748B', fontSize: 13 }}>
            Acompanhe renda fixa, FIIs e dados disponíveis do Tesouro Direto, com datas e origens dos valores.
          </span>
        </div>
      </div>

      <div
        style={{
          display: 'grid',
            gridTemplateColumns: isCompact ? '1fr' : 'repeat(3, 1fr)',
          gap: 16,
        }}
      >
        <div
          style={{
            background: '#FFFFFF',
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            Patrimônio conhecido (posições avaliadas)
          </div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#0F172A', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            {formatMoney(totalKnown)}
          </div>
        </div>

        <div
          style={{
            background: '#FFFFFF',
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            Renda fixa — saldo conhecido
          </div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#3B82F6', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            {formatMoney(totalFixedKnown)}
          </div>
        </div>
        <div style={{ background: '#FFFFFF', borderRadius: 14, border: '1px solid #E2E8F0', padding: '16px 20px', boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)' }}>
          <div style={{ fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            FIIs — valor de mercado conhecido
          </div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#3B82F6', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            {formatMoney(totalFiiMarket)}
          </div>
        </div>
      </div>

      <div style={{ color: '#64748B', fontSize: 13 }}>
        Avaliações registradas em: {valuationDates.length ? valuationDates.map((date) => date.split('-').reverse().join('/')).join(', ') : 'nenhuma data disponível'}.
        Os totais consideram apenas saldos conhecidos e FIIs com cotação salva.
      </div>

      <Tabs
        items={[
          {
            key: 'fixed-income',
            label: 'Renda fixa',
            children: (
              <Card variant="borderless" style={{ borderRadius: 14, border: '1px solid #E2E8F0', boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)' }} bodyStyle={{ padding: isCompact ? 12 : 20 }}>
                <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: 10, marginBottom: 16 }}>
                  <span style={{ color: '#64748B', fontSize: 13 }}>Saldos confirmados manualmente, com a data e a origem informadas.</span>
                  <Button type="primary" onClick={() => openFixedModal(null)} block={isCompact}>Novo investimento</Button>
                </div>
                <Table dataSource={fixedIncome} columns={fixedColumns} rowKey="id" loading={loadingFixedIncome} size={isCompact ? 'small' : 'middle'} scroll={{ x: 1050 }} />
              </Card>
            ),
          },
          {
            key: 'tesouro',
            label: 'Tesouro Direto',
            children: (
              <Card
                variant="borderless"
                style={{
                  borderRadius: 14,
                  border: '1px solid #E2E8F0',
                  boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
                }}
                bodyStyle={{ padding: isCompact ? 12 : 20 }}
              >
                <div
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: 10,
                    marginBottom: 16,
                  }}
                >
                  <span style={{ color: '#64748B', fontSize: 13 }}>
                    Última atualização: <b style={{ color: '#0F172A' }}>{tesouro.date || '-'}</b>
                  </span>
                  <Button onClick={loadTesouro} block={isCompact}>
                    Atualizar Dados
                  </Button>
                </div>
                <Table
                  dataSource={tesouro.items || []}
                  columns={tesouroColumns}
                  rowKey={(r) => `${r.title}-${r.type}`}
                  loading={loadingTesouro}
                  pagination={{ pageSize: isCompact ? 8 : 10 }}
                  size={isCompact ? 'small' : 'middle'}
                  scroll={{ x: 980 }}
                />
              </Card>
            ),
          },
          {
            key: 'fiis',
            label: 'FIIs',
            children: (
              <Card
                variant="borderless"
                style={{
                  borderRadius: 14,
                  border: '1px solid #E2E8F0',
                  boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
                }}
                bodyStyle={{ padding: isCompact ? 12 : 20 }}
              >
                <div
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: 10,
                    marginBottom: 16,
                  }}
                >
                  <span style={{ color: '#64748B', fontSize: 13 }}>Cotações mostram a fonte e a data salvas. Se o provider falhar, o último valor permanece.</span>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    <Button onClick={refreshQuotes} loading={refreshingQuotes} block={isCompact}>Atualizar cotações pelo provider</Button>
                    <Button type="primary" onClick={() => openModal(null)} block={isCompact}>Nova posição</Button>
                  </div>
                </div>
                <Table
                  dataSource={holdings}
                  columns={fiiColumns}
                  rowKey="id"
                  loading={loadingFii}
                  size={isCompact ? 'small' : 'middle'}
                  scroll={{ x: 1120 }}
                />
              </Card>
            ),
          },
        ]}
      />

      <Modal
        title={editing ? 'Editar FII' : 'Novo FII'}
        open={isModalOpen}
        onOk={handleSave}
        onCancel={() => setIsModalOpen(false)}
        okText="Salvar"
        cancelText="Cancelar"
        width={isCompact ? 'calc(100vw - 20px)' : 520}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="ticker" label="Ticker" rules={[{ required: true }]}>
            <Input placeholder="Ex: HGLG11" />
          </Form.Item>
          <Form.Item name="shares" label="Quantidade de cotas" rules={[{ required: true }]}>
            <InputNumber style={{ width: '100%' }} min={0} step={1} />
          </Form.Item>
          <Form.Item name="avgPrice" label="Preco medio" rules={[{ required: true }]}>
            <InputNumber style={{ width: '100%' }} min={0} step={0.01} prefix="R$" />
          </Form.Item>
          <Form.Item name="notes" label="Anotacoes">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal title={editingFixed ? 'Editar renda fixa' : 'Novo investimento de renda fixa'} open={fixedModalOpen} onOk={handleFixedSave} onCancel={() => setFixedModalOpen(false)} okText="Salvar" cancelText="Cancelar" width={isCompact ? 'calc(100vw - 20px)' : 620}>
        <Form form={fixedForm} layout="vertical">
          <Form.Item name="name" label="Nome do investimento" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="institution" label="Instituição"><Input /></Form.Item>
          <Form.Item name="productType" label="Produto" rules={[{ required: true }]}><Input placeholder="RDB, CDB, Tesouro..." /></Form.Item>
          <Form.Item name="benchmark" label="Indexador"><Input placeholder="CDI, IPCA, Prefixado..." /></Form.Item>
          <Form.Item name="contractedRate" label="Taxa contratada"><InputNumber style={{ width: '100%' }} min={0} step={0.01} /></Form.Item>
          <Form.Item name="contractedRateUnit" label="Unidade da taxa">
            <Select allowClear options={[{ value: 'percent_of_benchmark', label: '% do indexador' }, { value: 'annual_percent', label: '% ao ano' }]} />
          </Form.Item>
          <Form.Item name="maturityDate" label="Vencimento"><Input type="date" /></Form.Item>
          <Form.Item name="liquidity" label="Liquidez"><Input placeholder="Deixe vazio se desconhecida" /></Form.Item>
          <Form.Item name="principalAmount" label="Principal / custo conhecido"><InputNumber style={{ width: '100%' }} min={0} step={0.01} prefix="R$" /></Form.Item>
          <Form.Item name="knownBalance" label="Último saldo conhecido"><InputNumber style={{ width: '100%' }} min={0} step={0.01} prefix="R$" /></Form.Item>
          <Form.Item name="balanceAsOfDate" label="Data do saldo"><Input type="date" /></Form.Item>
          <Form.Item name="valuationSource" label="Origem do saldo"><Input placeholder="Ex.: atualização manual / extrato do banco" /></Form.Item>
          <Form.Item name="notes" label="Observações"><Input.TextArea rows={2} /></Form.Item>
        </Form>
      </Modal>

      <Modal title={`Atualizar saldo conhecido${editingBalance ? ` — ${editingBalance.name}` : ''}`} open={balanceModalOpen} onOk={handleBalanceSave} onCancel={() => setBalanceModalOpen(false)} okText="Atualizar saldo" cancelText="Cancelar" width={isCompact ? 'calc(100vw - 20px)' : 480}>
        <Form form={balanceForm} layout="vertical">
          <Form.Item name="knownBalance" label="Saldo confirmado" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0} step={0.01} prefix="R$" /></Form.Item>
          <Form.Item name="asOfDate" label="Data do saldo" rules={[{ required: true }]}><Input type="date" /></Form.Item>
          <Form.Item name="source" label="Origem" rules={[{ required: true }]}><Input /></Form.Item>
        </Form>
      </Modal>

      <Modal title={`Atualizar cotação manual${editingQuote ? ` — ${editingQuote.ticker}` : ''}`} open={quoteModalOpen} onOk={handleQuoteSave} onCancel={() => setQuoteModalOpen(false)} okText="Salvar cotação" cancelText="Cancelar" width={isCompact ? 'calc(100vw - 20px)' : 480}>
        <Form form={quoteForm} layout="vertical">
          <Form.Item name="price" label="Preço por cota" rules={[{ required: true }]}><InputNumber style={{ width: '100%' }} min={0.0001} step={0.01} prefix="R$" /></Form.Item>
          <Form.Item name="asOfDate" label="Data da cotação" rules={[{ required: true }]}><Input type="date" /></Form.Item>
          <Form.Item name="source" label="Origem" rules={[{ required: true }]}><Input placeholder="Investidor10, corretora, etc." /></Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
