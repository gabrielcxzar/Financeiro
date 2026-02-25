import React, { useEffect, useState } from 'react';
import {
  Table,
  Button,
  Card,
  Tag,
  message,
  Modal,
  Form,
  Input,
  InputNumber,
  Select,
  Radio,
  Popconfirm,
  Row,
  Col,
  Grid,
} from 'antd';
import { PlusOutlined, DeleteOutlined, ThunderboltOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Option } = Select;
const { useBreakpoint } = Grid;

const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Recurring() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [form] = Form.useForm();

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const [recResponse, catResponse, accResponse] = await Promise.all([
        api.get('/recurring'),
        api.get('/categories'),
        api.get('/accounts'),
      ]);
      setItems(recResponse.data);
      setCategories(catResponse.data);
      setAccounts(accResponse.data);
    } catch (error) {
      console.error(error);
      message.error('Erro ao carregar recorrencias.');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      await api.post('/recurring', values);
      message.success('Regra criada!');
      setIsModalOpen(false);
      form.resetFields();
      loadData();
    } catch {
      message.error('Erro ao salvar.');
    }
  };

  const handleDelete = async (id) => {
    await api.delete(`/recurring/${id}`);
    message.success('Regra removida.');
    loadData();
  };

  const handleGenerate = async () => {
    const today = new Date();
    const month = today.getMonth() + 1;
    const year = today.getFullYear();

    try {
      const response = await api.post(`/recurring/generate?month=${month}&year=${year}`);
      message.success(response.data.message);
      loadData();
    } catch {
      message.error('Erro ao gerar transacoes.');
    }
  };

  const columns = [
    { title: 'Descricao', dataIndex: 'description', key: 'desc' },
    { title: 'Dia', dataIndex: 'dayOfMonth', key: 'day', render: (d) => <Tag>Todo dia {d}</Tag> },
    {
      title: 'Valor',
      dataIndex: 'amount',
      render: (val, rec) => (
        <span style={{ color: rec.type === 'Expense' ? 'red' : 'green', fontWeight: 'bold' }}>{formatMoney(val)}</span>
      ),
    },
    {
      title: 'Tipo',
      dataIndex: 'type',
      render: (type) => <Tag color={type === 'Income' ? 'green' : 'red'}>{type === 'Income' ? 'Receita' : 'Despesa'}</Tag>,
    },
    { title: 'Conta', dataIndex: ['account', 'name'] },
    {
      title: 'Acoes',
      render: (_, rec) => (
        <Popconfirm title="Remover recorrencia?" onConfirm={() => handleDelete(rec.id)}>
          <Button danger icon={<DeleteOutlined />} type="text" />
        </Popconfirm>
      ),
    },
  ];

  return (
    <div>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          marginBottom: 16,
          gap: 12,
        }}
      >
        <h2 style={{ margin: 0 }}>Recorrencias (Receitas e Despesas)</h2>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, width: isCompact ? '100%' : 'auto' }}>
          <Button icon={<ThunderboltOutlined />} onClick={handleGenerate} block={isCompact}>
            Gerar neste mes
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setIsModalOpen(true)} block={isCompact}>
            Nova Recorrencia
          </Button>
        </div>
      </div>

      <Card variant="borderless" bodyStyle={{ padding: isCompact ? 12 : 24 }}>
        <Table
          dataSource={items}
          columns={columns}
          rowKey="id"
          loading={loading}
          scroll={{ x: 820 }}
          size={isCompact ? 'small' : 'middle'}
        />
      </Card>

      <Modal
        title="Nova Recorrencia"
        open={isModalOpen}
        onOk={handleSave}
        onCancel={() => setIsModalOpen(false)}
        width={isCompact ? 'calc(100vw - 20px)' : 620}
        destroyOnClose
      >
        <Form form={form} layout="vertical" initialValues={{ type: 'Expense', dayOfMonth: 5 }}>
          <Form.Item name="description" label="Descricao" rules={[{ required: true }]}>
            <Input placeholder="Ex: Salario, Netflix, Aluguel" />
          </Form.Item>

          <Row gutter={12}>
            <Col xs={24} sm={12}>
              <Form.Item name="amount" label="Valor" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="dayOfMonth" label="Dia do mes" rules={[{ required: true }]}>
                <InputNumber min={1} max={31} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item name="type" label="Tipo">
            <Radio.Group>
              <Radio value="Expense">Despesa</Radio>
              <Radio value="Income">Receita fixa</Radio>
            </Radio.Group>
          </Form.Item>

          <Form.Item name="categoryId" label="Categoria" rules={[{ required: true }]}>
            <Select>
              {categories.map((c) => (
                <Option key={c.id} value={c.id}>
                  {c.name}
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item name="accountId" label="Debitar de" rules={[{ required: true }]}>
            <Select>
              {accounts.map((a) => (
                <Option key={a.id} value={a.id}>
                  {a.name}
                </Option>
              ))}
            </Select>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
