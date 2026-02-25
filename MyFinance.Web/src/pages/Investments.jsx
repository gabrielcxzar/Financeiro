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

  const fiiColumns = [
    { title: 'Ticker', dataIndex: 'ticker', key: 'ticker', render: (t) => <Tag>{t}</Tag> },
    { title: 'Cotas', dataIndex: 'shares', key: 'shares' },
    { title: 'Preco Medio', dataIndex: 'avgPrice', key: 'avgPrice', render: (v) => formatMoney(v) },
    { title: 'Anotacoes', dataIndex: 'notes', key: 'notes' },
    {
      title: 'Acoes',
      key: 'actions',
      fixed: isCompact ? undefined : 'right',
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
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
      { title: 'Titulo', dataIndex: 'title', key: 'title' },
      { title: 'Tipo', dataIndex: 'type', key: 'type' },
      { title: 'Taxa Compra', dataIndex: 'buyRate', key: 'buyRate', render: (v) => (v != null ? `${v}%` : '-') },
      { title: 'Taxa Venda', dataIndex: 'sellRate', key: 'sellRate', render: (v) => (v != null ? `${v}%` : '-') },
      { title: 'PU Compra', dataIndex: 'buyPrice', key: 'buyPrice', render: (v) => (v != null ? formatMoney(v) : '-') },
      { title: 'PU Venda', dataIndex: 'sellPrice', key: 'sellPrice', render: (v) => (v != null ? formatMoney(v) : '-') },
    ],
    [],
  );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>Investimentos</h2>

      <Tabs
        items={[
          {
            key: 'tesouro',
            label: 'Tesouro Direto',
            children: (
              <Card variant="borderless" bodyStyle={{ padding: isCompact ? 12 : 24 }}>
                <div
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: 10,
                    marginBottom: 12,
                  }}
                >
                  <span>
                    Ultima data: <b>{tesouro.date || '-'}</b>
                  </span>
                  <Button onClick={loadTesouro} block={isCompact}>
                    Atualizar
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
              <Card variant="borderless" bodyStyle={{ padding: isCompact ? 12 : 24 }}>
                <div
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    gap: 10,
                    marginBottom: 12,
                  }}
                >
                  <span>Gerencie suas posicoes manualmente</span>
                  <Button type="primary" onClick={() => openModal(null)} block={isCompact}>
                    Nova posicao
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
