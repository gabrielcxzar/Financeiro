import React, { useEffect, useState } from 'react';
import {
  Card,
  Progress,
  Button,
  Modal,
  Form,
  InputNumber,
  Select,
  message,
  Row,
  Col,
  Empty,
  Popconfirm,
  Grid,
  Switch,
  Tag,
} from 'antd';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import api from '../services/api';
import ActionableEmptyState from '../components/ActionableEmptyState';

const { Option } = Select;
const { useBreakpoint } = Grid;
const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Budgets({ month, year }) {
  const [budgets, setBudgets] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [form] = Form.useForm();

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    const controller = new AbortController();
    loadData(controller.signal);

    return () => controller.abort();
  }, [month, year]);

  const loadData = async (signal) => {
    try {
      setLoading(true);
      setBudgets([]);
      setTransactions([]);
      const [budgetsRes, catRes, transRes] = await Promise.all([
        api.get(`/budgets?month=${month}&year=${year}`, { signal }),
        api.get('/categories', { signal }),
        api.get(`/transactions?month=${month}&year=${year}`, { signal }),
      ]);

      setBudgets(budgetsRes.data);
      setCategories(catRes.data);
      setTransactions(transRes.data);
    } catch (error) {
      if (signal?.aborted || error?.code === 'ERR_CANCELED') {
        return;
      }

      console.error(error);
      message.error('Erro ao carregar metas.');
    } finally {
      if (!signal?.aborted) {
        setLoading(false);
      }
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      await api.post('/budgets', { ...values, month, year });
      message.success('Orcamento definido.');
      setIsModalOpen(false);
      form.resetFields();
      loadData();
    } catch (error) {
      message.error(error?.message || 'Erro ao salvar orcamento.');
    }
  };

  const handleDelete = async (id) => {
    await api.delete(`/budgets/${id}`);
      message.success('Orcamento removido.');
    loadData();
  };

  const renderBudgetCard = (budget) => {
    const spent = transactions
      .filter((t) => t.categoryId === budget.categoryId && t.type === 'Expense')
      .reduce((acc, t) => acc + t.amount, 0);

    const percent = Math.min((spent / budget.amount) * 100, 100);
    const strokeColor = percent >= 100 ? '#F43F5E' : percent > 80 ? '#F59E0B' : '#10B981';

    return (
      <Col xs={24} sm={12} xl={8} key={budget.id}>
        <Card
          variant="borderless"
          title={
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <span
                style={{
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  background: budget.category?.color || '#94A3B8',
                }}
              />
              <span style={{ fontWeight: 700, color: '#0F172A', fontSize: 15 }}>{budget.category?.name}</span>
            </div>
          }
          extra={
            <Popconfirm title="Remover orçamento?" onConfirm={() => handleDelete(budget.id)}>
              <Button type="text" danger icon={<DeleteOutlined />} size="small" />
            </Popconfirm>
          }
          style={{
            borderRadius: 14,
            border: '1px solid #E2E8F0',
            background: '#FFFFFF',
            boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
          }}
          loading={loading}
        >
          <div style={{ marginBottom: 12 }}>
            <span
              style={{
                display: 'inline-block',
                padding: '2px 8px',
                borderRadius: 8,
                fontSize: 11,
                fontWeight: 600,
                background: budget.isEssential ? '#FEF3C7' : '#F1F5F9',
                color: budget.isEssential ? '#92400E' : '#475569',
              }}
            >
              {budget.isEssential ? 'Essencial no planejamento' : 'Discricionário'}
            </span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8, marginBottom: 10 }}>
            <span style={{ color: '#64748B', fontSize: 13 }}>
              Gasto: <strong style={{ color: '#0F172A', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(spent)}</strong>
            </span>
            <span style={{ color: '#64748B', fontSize: 13 }}>
              Meta: <strong style={{ color: '#0F172A', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(budget.amount)}</strong>
            </span>
          </div>

          <Progress
            percent={percent}
            strokeColor={strokeColor}
            showInfo={false}
            size={['100%', 6]}
          />

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 12, fontSize: 12 }}>
            <span style={{ color: '#64748B' }}>Consumo</span>
            <strong style={{ color: strokeColor, fontFeatureSettings: '"tnum" 1' }}>{percent.toFixed(0)}%</strong>
          </div>

          <div style={{ marginTop: 8, fontSize: 12 }}>
            {spent > budget.amount ? (
              <span style={{ color: '#F43F5E', fontWeight: 600 }}>
                Estourou em {formatMoney(spent - budget.amount)}.
              </span>
            ) : (
              <span style={{ color: '#10B981', fontWeight: 600 }}>
                Restam {formatMoney(budget.amount - spent)}.
              </span>
            )}
          </div>
        </Card>
      </Col>
    );
  };

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
            Orçamentos por Categoria
          </h2>
          <span style={{ color: '#64748B', fontSize: 13 }}>
            Defina e monitore limites mensais de despesas para manter as contas sob controle
          </span>
        </div>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setIsModalOpen(true)} block={isCompact}>
          Definir Orçamento
        </Button>
      </div>

      {loading ? (
        <Card variant="borderless" loading bodyStyle={{ minHeight: 180 }} />
      ) : budgets.length === 0 ? (
        <ActionableEmptyState
          title="Nenhum orçamento definido para este mês"
          description="Defina limites mensais por categoria (ex: Alimentação, Lazer) para manter suas finanças sob controle."
          actionLabel="Definir Primeiro Orçamento"
          onAction={() => setIsModalOpen(true)}
        />
      ) : (
        <Row gutter={[16, 16]}>{budgets.map(renderBudgetCard)}</Row>
      )}

      <Modal
        title="Definir Orcamento de Gasto"
        open={isModalOpen}
        onOk={handleSave}
        onCancel={() => setIsModalOpen(false)}
        width={isCompact ? 'calc(100vw - 20px)' : 520}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="categoryId"
            label="Categoria"
            rules={[{ required: true, message: 'Escolha uma categoria' }]}
          >
            <Select placeholder="Ex: Alimentacao">
              {categories.map((c) => (
                <Option key={c.id} value={c.id}>
                  {c.name}
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="amount"
            label="Limite Mensal (R$)"
            rules={[{ required: true, message: 'Informe o limite' }]}
          >
            <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
          </Form.Item>

          <Form.Item
            name="isEssential"
            label="Considerar no calculo de livre para gastar"
            valuePropName="checked"
            initialValue={false}
          >
            <Switch checkedChildren="Sim" unCheckedChildren="Nao" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
