import React, { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Form,
  Grid,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Progress,
  Row,
  Select,
  Space,
  Tag,
  message,
} from 'antd';
import { DeleteOutlined, EditOutlined, PauseOutlined, PlusOutlined, CheckOutlined, PlayCircleOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import api from '../services/api';
import ActionableEmptyState from '../components/ActionableEmptyState';

const { TextArea } = Input;
const { useBreakpoint } = Grid;

const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

const goalTypeOptions = [
  { value: 'Saving', label: 'Economia' },
  { value: 'DebtPaydown', label: 'Quitacao de divida' },
];

const statusOptions = [
  { value: 'Active', label: 'Ativa' },
  { value: 'Paused', label: 'Pausada' },
  { value: 'Completed', label: 'Concluida' },
];

const statusColors = {
  Active: 'processing',
  Paused: 'warning',
  Completed: 'success',
};

export default function Goals() {
  const [goals, setGoals] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingGoal, setEditingGoal] = useState(null);
  const [form] = Form.useForm();

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    try {
      setLoading(true);
      const [goalsResponse, accountsResponse] = await Promise.all([
        api.get('/goals'),
        api.get('/accounts'),
      ]);

      setGoals(goalsResponse.data);
      setAccounts(accountsResponse.data);
    } catch (error) {
      message.error(error?.message || 'Nao foi possivel carregar as metas.');
    } finally {
      setLoading(false);
    }
  };

  const openCreate = () => {
    setEditingGoal(null);
    form.resetFields();
    form.setFieldsValue({
      goalType: 'Saving',
      status: 'Active',
      currentAmount: 0,
      monthlyContribution: 0,
    });
    setIsModalOpen(true);
  };

  const openEdit = (goal) => {
    setEditingGoal(goal);
    form.setFieldsValue({
      ...goal,
      targetDate: goal.targetDate ? dayjs(goal.targetDate) : null,
    });
    setIsModalOpen(true);
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setEditingGoal(null);
    form.resetFields();
  };

  const buildPayload = (values, statusOverride) => ({
    name: values.name,
    goalType: values.goalType,
    targetAmount: Number(values.targetAmount),
    currentAmount: Number(values.currentAmount || 0),
    targetDate: values.targetDate ? values.targetDate.toISOString() : null,
    monthlyContribution: Number(values.monthlyContribution || 0),
    status: statusOverride || values.status,
    linkedAccountId: values.linkedAccountId || null,
    notes: values.notes || null,
  });

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      setSaving(true);

      const payload = buildPayload(values);
      if (editingGoal) {
        await api.put(`/goals/${editingGoal.id}`, payload);
        message.success('Meta atualizada.');
      } else {
        await api.post('/goals', payload);
        message.success('Meta criada.');
      }

      closeModal();
      loadData();
    } catch (error) {
      message.error(error?.message || 'Erro ao salvar meta.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (goalId) => {
    try {
      await api.delete(`/goals/${goalId}`);
      message.success('Meta removida.');
      loadData();
    } catch (error) {
      message.error(error?.message || 'Erro ao excluir meta.');
    }
  };

  const handleStatusChange = async (goal, status) => {
    try {
      const payload = {
        name: goal.name,
        goalType: goal.goalType,
        targetAmount: goal.targetAmount,
        currentAmount: goal.currentAmount,
        targetDate: goal.targetDate,
        monthlyContribution: goal.monthlyContribution,
        status,
        linkedAccountId: goal.linkedAccountId,
        notes: goal.notes,
      };

      await api.put(`/goals/${goal.id}`, payload);
      message.success('Status da meta atualizado.');
      loadData();
    } catch (error) {
      message.error(error?.message || 'Erro ao atualizar status da meta.');
    }
  };

  const renderActions = (goal) => (
    <Space wrap>
      <Button type="text" icon={<EditOutlined />} onClick={() => openEdit(goal)}>
        Editar
      </Button>
      {goal.status === 'Active' && (
        <Button type="text" icon={<PauseOutlined />} onClick={() => handleStatusChange(goal, 'Paused')}>
          Pausar
        </Button>
      )}
      {goal.status === 'Paused' && (
        <Button type="text" icon={<PlayCircleOutlined />} onClick={() => handleStatusChange(goal, 'Active')}>
          Retomar
        </Button>
      )}
      {goal.status !== 'Completed' && (
        <Button type="text" icon={<CheckOutlined />} onClick={() => handleStatusChange(goal, 'Completed')}>
          Concluir
        </Button>
      )}
      <Popconfirm title="Excluir esta meta?" onConfirm={() => handleDelete(goal.id)}>
        <Button type="text" danger icon={<DeleteOutlined />}>
          Excluir
        </Button>
      </Popconfirm>
    </Space>
  );

  return (
    <div>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 16,
          gap: 12,
        }}
      >
        <h2 style={{ margin: 0 }}>Metas Financeiras</h2>
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate} block={isCompact}>
          Nova Meta
        </Button>
      </div>

      {goals.length === 0 ? (
        <ActionableEmptyState
          title="Nenhuma meta financeira cadastrada"
          description="Crie objetivos como 'Reserva de Emergência', 'Viagem' ou 'Carro Novo' e acompanhe o progresso das suas economias."
          actionLabel="Criar Minha Primeira Meta"
          onAction={openCreate}
        />
      ) : (
        <Row gutter={[16, 16]}>
          {goals.map((goal) => (
            <Col xs={24} lg={12} xl={8} key={goal.id}>
              <Card
                loading={loading}
                title={goal.name}
                extra={<Tag color={statusColors[goal.status] || 'default'}>{statusOptions.find((item) => item.value === goal.status)?.label || goal.status}</Tag>}
                style={{ borderRadius: 12, height: '100%' }}
              >
                <Space direction="vertical" size={12} style={{ width: '100%' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
                    <Tag>{goalTypeOptions.find((item) => item.value === goal.goalType)?.label || goal.goalType}</Tag>
                    {goal.linkedAccount?.name && <Tag color="blue">Conta: {goal.linkedAccount.name}</Tag>}
                  </div>

                  <Progress percent={Number(goal.progressPercent)} />

                  <div style={{ display: 'grid', gap: 6 }}>
                    <span>Acumulado: <b>{formatMoney(goal.currentAmount)}</b></span>
                    <span>Valor alvo: <b>{formatMoney(goal.targetAmount)}</b></span>
                    <span>Valor restante: <b>{formatMoney(goal.remainingAmount)}</b></span>
                    <span>Contribuicao mensal planejada: <b>{formatMoney(goal.monthlyContribution)}</b></span>
                    <span>
                      Contribuicao mensal sugerida:{' '}
                      <b>{goal.suggestedMonthlyContribution != null ? formatMoney(goal.suggestedMonthlyContribution) : 'Nao se aplica'}</b>
                    </span>
                    <span>
                      Prazo final:{' '}
                      <b>{goal.targetDate ? new Date(goal.targetDate).toLocaleDateString('pt-BR') : 'Nao definido'}</b>
                    </span>
                  </div>

                  {goal.notes && (
                    <div style={{ color: '#595959', fontSize: 13 }}>
                      {goal.notes}
                    </div>
                  )}

                  {renderActions(goal)}
                </Space>
              </Card>
            </Col>
          ))}
        </Row>
      )}

      <Modal
        title={editingGoal ? 'Editar Meta' : 'Nova Meta'}
        open={isModalOpen}
        onOk={handleSave}
        onCancel={closeModal}
        confirmLoading={saving}
        width={isCompact ? 'calc(100vw - 20px)' : 620}
        destroyOnClose
      >
        <Form form={form} layout="vertical" initialValues={{ goalType: 'Saving', status: 'Active', currentAmount: 0, monthlyContribution: 0 }}>
          <Form.Item name="name" label="Nome" rules={[{ required: true, message: 'Informe o nome da meta.' }]}>
            <Input placeholder="Ex: Reserva de emergencia" />
          </Form.Item>

          <Row gutter={12}>
            <Col xs={24} sm={12}>
              <Form.Item name="goalType" label="Tipo da meta" rules={[{ required: true }]}>
                <Select options={goalTypeOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="status" label="Status" rules={[{ required: true }]}>
                <Select options={statusOptions} />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={12}>
            <Col xs={24} sm={12}>
              <Form.Item name="targetAmount" label="Valor alvo" rules={[{ required: true, message: 'Informe o valor alvo.' }]}>
                <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} stringMode />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="currentAmount" label="Valor atual" rules={[{ required: true, message: 'Informe o valor atual.' }]}>
                <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} stringMode />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={12}>
            <Col xs={24} sm={12}>
              <Form.Item name="monthlyContribution" label="Contribuicao mensal planejada">
                <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} stringMode />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="targetDate" label="Prazo final">
                <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item name="linkedAccountId" label="Conta vinculada">
            <Select
              allowClear
              placeholder="Opcional"
              options={accounts.map((account) => ({
                value: account.id,
                label: account.name,
              }))}
            />
          </Form.Item>

          <Form.Item name="notes" label="Observacao">
            <TextArea rows={3} placeholder="Detalhes opcionais sobre a meta." />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
