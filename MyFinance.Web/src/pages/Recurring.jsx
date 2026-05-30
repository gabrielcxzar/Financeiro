import React, { useEffect, useMemo, useState } from 'react';
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
  Alert,
} from 'antd';
import { PlusOutlined, DeleteOutlined, ThunderboltOutlined, CreditCardOutlined, BankOutlined } from '@ant-design/icons';
import api from '../services/api';

const { useBreakpoint } = Grid;

const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Recurring() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [selectedAccountId, setSelectedAccountId] = useState(null);
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

  const selectedAccount = useMemo(
    () => accounts.find((account) => account.id === selectedAccountId) ?? null,
    [accounts, selectedAccountId],
  );

  const accountOptions = useMemo(() => {
    const regularAccounts = accounts.filter((account) => !account.isCreditCard);
    const creditCards = accounts.filter((account) => account.isCreditCard);

    return [
      {
        label: 'Contas',
        options: regularAccounts.map((account) => ({
          label: account.name,
          value: account.id,
        })),
      },
      {
        label: 'Cartoes',
        options: creditCards.map((account) => ({
          label: `${account.name} (fatura)`,
          value: account.id,
        })),
      },
    ].filter((group) => group.options.length > 0);
  }, [accounts]);

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      await api.post('/recurring', values);
      message.success('Regra criada.');
      setIsModalOpen(false);
      setSelectedAccountId(null);
      form.resetFields();
      loadData();
    } catch (error) {
      const apiMessage = error?.response?.data;
      message.error(typeof apiMessage === 'string' ? apiMessage : 'Erro ao salvar.');
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

  const handleAccountChange = (accountId) => {
    setSelectedAccountId(accountId);
    const account = accounts.find((item) => item.id === accountId);
    if (account?.isCreditCard) {
      form.setFieldValue('type', 'Expense');
    }
  };

  const columns = [
    { title: 'Descricao', dataIndex: 'description', key: 'desc' },
    { title: 'Dia', dataIndex: 'dayOfMonth', key: 'day', render: (day) => <Tag>Todo dia {day}</Tag> },
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
    {
      title: 'Origem',
      key: 'account',
      render: (_, rec) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {rec.account?.isCreditCard ? <CreditCardOutlined /> : <BankOutlined />}
          <span>{rec.account?.name || '-'}</span>
          <Tag color={rec.account?.isCreditCard ? 'gold' : 'blue'}>
            {rec.account?.isCreditCard ? 'Cartao' : 'Conta'}
          </Tag>
        </div>
      ),
    },
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
        <div>
          <h2 style={{ margin: 0 }}>Recorrencias</h2>
          <span style={{ color: '#666' }}>Crie regras para contas correntes, investimentos e cartoes de credito.</span>
        </div>
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
          scroll={{ x: 960 }}
          size={isCompact ? 'small' : 'middle'}
        />
      </Card>

      <Modal
        title="Nova Recorrencia"
        open={isModalOpen}
        onOk={handleSave}
        onCancel={() => {
          setIsModalOpen(false);
          setSelectedAccountId(null);
        }}
        width={isCompact ? 'calc(100vw - 20px)' : 620}
        destroyOnClose
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{ type: 'Expense', dayOfMonth: 5 }}
        >
          <Form.Item name="description" label="Descricao" rules={[{ required: true }]}>
            <Input placeholder="Ex: Salario, Netflix, Aluguel, Academia" />
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

          <Form.Item name="accountId" label="Lancar em" rules={[{ required: true }]}>
            <Select
              options={accountOptions}
              placeholder="Escolha a conta ou o cartao"
              onChange={handleAccountChange}
            />
          </Form.Item>

          {selectedAccount?.isCreditCard && (
            <Alert
              type="info"
              showIcon
              message="Recorrencia de cartao"
              description="Esse lancamento sera criado como despesa no cartao selecionado e entrara na fatura do periodo conforme o dia informado."
              style={{ marginBottom: 16 }}
            />
          )}

          <Form.Item name="type" label="Tipo">
            <Radio.Group>
              <Radio value="Expense">Despesa</Radio>
              <Radio value="Income" disabled={selectedAccount?.isCreditCard}>
                Receita fixa
              </Radio>
            </Radio.Group>
          </Form.Item>

          <Form.Item name="categoryId" label="Categoria" rules={[{ required: true }]}>
            <Select>
              {categories.map((category) => (
                <Select.Option key={category.id} value={category.id}>
                  {category.name}
                </Select.Option>
              ))}
            </Select>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
