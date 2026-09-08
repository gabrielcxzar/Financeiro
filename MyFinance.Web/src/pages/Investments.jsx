import React, { useEffect, useMemo, useState } from 'react';
import { Card, Table, Button, Modal, Form, Input, InputNumber, Tabs, message, Tag, Grid } from 'antd';
import api from '../services/api';

const { useBreakpoint } = Grid;
const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Investments() {
  const [tesouro, setTesouro] = useState({ date: '', items: [] });
  const [loadingTesouro, setLoadingTesouro] = useState(true);

  const [holdings, setHoldings] = useState([]);
  const [loadingFii, setLoadingFii] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form] = Form.useForm();

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    loadTesouro();
    loadHoldings();
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

  const totalFiiInvested = holdings.reduce(
    (acc, h) => acc + ((Number(h.shares) || 0) * (Number(h.avgPrice) || 0)),
    0,
  );

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
            Acompanhe posições em renda variável e taxas em tempo real do Tesouro Direto
          </span>
        </div>
      </div>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: isCompact ? '1fr' : 'repeat(2, 1fr)',
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
            Total em Custódia (FIIs)
          </div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#0F172A', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            {formatMoney(totalFiiInvested)}
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
            Fundos em Carteira
          </div>
          <div style={{ fontSize: 24, fontWeight: 700, color: '#3B82F6', marginTop: 4, fontFeatureSettings: '"tnum" 1' }}>
            {holdings.length} {holdings.length === 1 ? 'ativo' : 'ativos'}
          </div>
        </div>
      </div>

      <Tabs
        items={[
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
            label: 'FIIs (Manual)',
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
                  <span style={{ color: '#64748B', fontSize: 13 }}>Gerencie suas posições em fundos imobiliários</span>
                  <Button type="primary" onClick={() => openModal(null)} block={isCompact}>
                    Nova Posição
                  </Button>
                </div>
                <Table
                  dataSource={holdings}
                  columns={fiiColumns}
                  rowKey="id"
                  loading={loadingFii}
                  size={isCompact ? 'small' : 'middle'}
                  scroll={{ x: 760 }}
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
    </div>
  );
}
