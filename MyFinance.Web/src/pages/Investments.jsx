import React, { useEffect, useMemo, useState } from 'react';
import { Card, Table, Button, Modal, Form, Input, InputNumber, Tabs, message, Tag } from 'antd';
import api from '../services/api';

const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Investments() {
  const [tesouro, setTesouro] = useState({ date: '', items: [] });
  const [loadingTesouro, setLoadingTesouro] = useState(true);

  const [holdings, setHoldings] = useState([]);
  const [loadingFii, setLoadingFii] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form] = Form.useForm();

  useEffect(() => {
    loadTesouro();
    loadHoldings();
  }, []);

  const loadTesouro = async () => {
    setLoadingTesouro(true);
    try {
      const { data } = await api.get('/tesouro/latest');
      setTesouro(data);
    } catch (error) {
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
    } catch (error) {
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
        notes: record.notes || ''
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
        avgPrice: Number(values.avgPrice)
      });
      message.success('Posi o salva');
      setIsModalOpen(false);
      loadHoldings();
    } catch (error) {
      message.error('Erro ao salvar');
    }
  };

  const handleDelete = async (id) => {
    try {
      await api.delete(`/fiiholdings/${id}`);
      message.success('Removido');
      loadHoldings();
    } catch (error) {
      message.error('Erro ao remover');
    }
  };

  const fiiColumns = [
    { title: 'Ticker', dataIndex: 'ticker', key: 'ticker', render: (t) => <Tag>{t}</Tag> },
    { title: 'Cotas', dataIndex: 'shares', key: 'shares' },
    { title: 'Preo Mdio', dataIndex: 'avgPrice', key: 'avgPrice', render: (v) => formatMoney(v) },
    { title: 'Anota es', dataIndex: 'notes', key: 'notes' },
    {
      title: 'Ações',
      key: 'actions',
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 8 }}>
          <Button size="small" onClick={() => openModal(record)}>Editar</Button>
          <Button size="small" danger onClick={() => handleDelete(record.id)}>Excluir</Button>
        </div>
      )
    }
  ];

  const tesouroColumns = useMemo(() => [
    { title: 'Ttulo', dataIndex: 'title', key: 'title' },
    { title: 'Tipo', dataIndex: 'type', key: 'type' },
    { title: 'Taxa Compra', dataIndex: 'buyRate', key: 'buyRate', render: (v) => v != null ? `${v}%` : '-' },
    { title: 'Taxa Venda', dataIndex: 'sellRate', key: 'sellRate', render: (v) => v != null ? `${v}%` : '-' },
    { title: 'PU Compra', dataIndex: 'buyPrice', key: 'buyPrice', render: (v) => v != null ? formatMoney(v) : '-' },
    { title: 'PU Venda', dataIndex: 'sellPrice', key: 'sellPrice', render: (v) => v != null ? formatMoney(v) : '-' },
  ], []);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <h2 style={{ margin: 0 }}>Investimentos</h2>

      <Tabs
        items={[
          {
            key: 'tesouro',
            label: 'Tesouro Direto',
            children: (
              <Card variant="borderless">
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12 }}>
                  <span>Última data: <b>{tesouro.date || '-'}</b></span>
                  <Button onClick={loadTesouro}>Atualizar</Button>
                </div>
                <Table
                  dataSource={tesouro.items || []}
                  columns={tesouroColumns}
                  rowKey={(r) => `${r.title}-${r.type}`}
                  loading={loadingTesouro}
                  pagination={{ pageSize: 10 }}
                />
              </Card>
            )
          },
          {
            key: 'fiis',
            label: 'FIIs (Manual)',
            children: (
              <Card variant="borderless">
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12 }}>
                  <span>Gerencie suas posi es manualmente</span>
                  <Button type="primary" onClick={() => openModal(null)}>Nova posi o</Button>
                </div>
                <Table
                  dataSource={holdings}
                  columns={fiiColumns}
                  rowKey="id"
                  loading={loadingFii}
                />
              </Card>
            )
          }
        ]}
      />

      <Modal
        title={editing ? 'Editar FII' : 'Novo FII'}
        open={isModalOpen}
        onOk={handleSave}
        onCancel={() => setIsModalOpen(false)}
        okText="Salvar"
        cancelText="Cancelar"
      >
        <Form form={form} layout="vertical">
          <Form.Item name="ticker" label="Ticker" rules={[{ required: true }]}>
            <Input placeholder="Ex: HGLG11" />
          </Form.Item>
          <Form.Item name="shares" label="Quantidade de cotas" rules={[{ required: true }]}>
            <InputNumber style={{ width: '100%' }} min={0} step={1} />
          </Form.Item>
          <Form.Item name="avgPrice" label="Preo mdio" rules={[{ required: true }]}>
            <InputNumber style={{ width: '100%' }} min={0} step={0.01} prefix="R$" />
          </Form.Item>
          <Form.Item name="notes" label="Anota es">
            <Input.TextArea rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
