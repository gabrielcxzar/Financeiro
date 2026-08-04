import React, { useState, useEffect } from 'react';
import {
  Modal,
  Form,
  Input,
  Radio,
  Select,
  message,
  InputNumber,
  DatePicker,
  Switch,
  Row,
  Col,
  Tag,
  Space,
  Grid,
} from 'antd';
import { BankOutlined, CreditCardOutlined, ThunderboltOutlined } from '@ant-design/icons';
import api from '../services/api';
import dayjs from 'dayjs';
import InputMoney from './InputMoney';

const { Option } = Select;
const { useBreakpoint } = Grid;

const EXPENSE_PRESETS = [
  { label: '☕ Café R$ 10', desc: 'Café da manhã / Lanche', amount: 10, catKeyword: 'alimentação' },
  { label: '🍱 Almoço R$ 35', desc: 'Almoço em Restaurante', amount: 35, catKeyword: 'alimentação' },
  { label: '⛽ Combustível R$ 100', desc: 'Abastecimento / Posto', amount: 100, catKeyword: 'transporte' },
  { label: '🛒 Mercado R$ 150', desc: 'Compras de Mercado', amount: 150, catKeyword: 'alimentação' },
  { label: '💊 Farmácia R$ 50', desc: 'Farmácia / Medicamentos', amount: 50, catKeyword: 'saúde' },
];

const INCOME_PRESETS = [
  { label: '💵 Salário R$ 3.000', desc: 'Salário Mensal', amount: 3000, catKeyword: 'salário' },
  { label: '💻 Freelance R$ 500', desc: 'Serviço Freelance / Extra', amount: 500, catKeyword: 'renda' },
  { label: '📈 Dividendos R$ 100', desc: 'Rendimentos de Investimento', amount: 100, catKeyword: 'investimento' },
];

export default function AddTransactionModal({ visible, onClose, onSuccess, transactionToEdit }) {
  const [form] = Form.useForm();
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);

  const [transactionType, setTransactionType] = useState('Expense');
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [isPaid, setIsPaid] = useState(true);
  const isSeriesEdit = Boolean(transactionToEdit?.installmentId);
  const [applyToSeries, setApplyToSeries] = useState(false);

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    if (!visible) return;

    loadData(transactionToEdit?.accountId);

    if (transactionToEdit) {
      form.setFieldsValue({
        ...transactionToEdit,
        description: transactionToEdit.baseDescription || transactionToEdit.description,
        date: dayjs(transactionToEdit.date),
        amount: Math.abs(transactionToEdit.amount),
        installments: transactionToEdit.installments || 1,
        installmentNumber: transactionToEdit.installmentNumber || 1,
        totalInstallments: transactionToEdit.totalInstallments || transactionToEdit.installments || 1,
      });
      setTransactionType(transactionToEdit.type);
      setIsPaid(transactionToEdit.paid);
      setApplyToSeries(Boolean(transactionToEdit.installmentId));
    } else {
      form.resetFields();
      form.setFieldsValue({ date: dayjs(), type: 'Expense', installments: 1, installmentNumber: 1, totalInstallments: 1 });
      setTransactionType('Expense');
      setIsPaid(true);
      setIsCreditCard(false);
      setApplyToSeries(false);
    }
  }, [visible, transactionToEdit, form]);

  const loadData = async (accountId) => {
    try {
      const [catResponse, accResponse] = await Promise.all([api.get('/categories'), api.get('/accounts')]);
      setCategories(catResponse.data || []);
      setAccounts(accResponse.data || []);
      
      // Auto assign first account if creating new and no account set
      if (!accountId && accResponse.data?.length > 0) {
        form.setFieldValue('accountId', accResponse.data[0].id);
        setIsCreditCard(Boolean(accResponse.data[0].isCreditCard));
      } else if (accountId) {
        const currentAccount = accResponse.data.find((acc) => acc.id === accountId);
        setIsCreditCard(Boolean(currentAccount?.isCreditCard));
      }
    } catch {
      message.error('Erro ao carregar contas e categorias.');
    }
  };

  const filteredCategories = categories.filter((c) => c.type === transactionType);

  const handleAccountChange = (value) => {
    const acc = accounts.find((a) => a.id === value);
    if (acc?.isCreditCard) {
      setIsCreditCard(true);
      return;
    }
    setIsCreditCard(false);
    setIsPaid(true);
  };

  const handlePresetClick = (preset) => {
    form.setFieldValue('description', preset.desc);
    form.setFieldValue('amount', preset.amount);

    // Try to auto match category
    const matchedCategory = filteredCategories.find((c) =>
      c.name.toLowerCase().includes(preset.catKeyword)
    );

    if (matchedCategory) {
      form.setFieldValue('categoryId', matchedCategory.id);
    }
  };

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      setLoading(true);

      const payload = {
        id: transactionToEdit ? transactionToEdit.id : 0,
        description: values.description,
        amount: Number(values.amount),
        type: values.type,
        date: values.date.toISOString(),
        categoryId: Number(values.categoryId),
        accountId: Number(values.accountId),
        paid: isCreditCard ? false : isPaid,
        installments: isCreditCard ? Number(values.totalInstallments || 1) : 1,
        installmentNumber: isCreditCard ? Number(values.installmentNumber || 1) : 1,
        applyToSeries: isSeriesEdit ? applyToSeries : false,
      };

      if (transactionToEdit) {
        await api.put(`/transactions/${transactionToEdit.id}`, payload);
        message.success('Transação atualizada!');
      } else {
        await api.post('/transactions', payload);
        message.success('Transação lançada!');
      }

      onSuccess();
      onClose();
    } catch (error) {
      if (error?.errorFields) return;
      message.error('Erro ao salvar transação.');
    } finally {
      setLoading(false);
    }
  };

  const presets = transactionType === 'Expense' ? EXPENSE_PRESETS : INCOME_PRESETS;

  return (
    <Modal
      title={transactionToEdit ? 'Editar Transação' : 'Nova Transação'}
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      okText="Salvar Lançamento"
      cancelText="Cancelar"
      width={isCompact ? 'calc(100vw - 20px)' : 640}
      style={{ top: isCompact ? 8 : 24 }}
      destroyOnClose
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ type: 'Expense', installments: 1, installmentNumber: 1, totalInstallments: 1 }}
      >
        {/* PRESETS BAR */}
        {!transactionToEdit && (
          <div style={{ marginBottom: 16, background: '#F8FAFC', padding: 12, borderRadius: 12, border: '1px solid #E2E8F0' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, color: '#64748B', fontWeight: 700, marginBottom: 8 }}>
              <ThunderboltOutlined style={{ color: '#FF6600' }} /> PREENCHIMENTO RÁPIDO (1-CLIQUE):
            </div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {presets.map((p, idx) => (
                <Tag
                  key={idx}
                  color="orange"
                  style={{ cursor: 'pointer', borderRadius: 8, padding: '4px 10px', fontWeight: 600 }}
                  onClick={() => handlePresetClick(p)}
                >
                  {p.label}
                </Tag>
              ))}
            </div>
          </div>
        )}

        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item name="type" label="Tipo de Lançamento" rules={[{ required: true }]}>
              <Radio.Group buttonStyle="solid" onChange={(e) => setTransactionType(e.target.value)}>
                <Radio.Button value="Expense" style={{ fontWeight: 600 }}>Despesa</Radio.Button>
                <Radio.Button value="Income" style={{ fontWeight: 600 }}>Receita</Radio.Button>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="date" label="Data do Lançamento" initialValue={dayjs()} rules={[{ required: true }]}>
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%', borderRadius: 10 }} allowClear={false} />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item name="description" label="Descrição" rules={[{ required: true, message: 'Digite uma descrição' }]}>
          <Input size="large" placeholder="Ex: Supermercado, Salário, Gasolina..." style={{ borderRadius: 10 }} />
        </Form.Item>

        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item name="categoryId" label="Categoria" rules={[{ required: true, message: 'Selecione uma categoria' }]}>
              <Select placeholder="Selecione" size="large" style={{ borderRadius: 10 }}>
                {filteredCategories.map((c) => (
                  <Option key={c.id} value={c.id}>
                    <span style={{ color: c.color, marginRight: 8 }}>&bull;</span> {c.name}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="accountId" label="Conta / Carteira" rules={[{ required: true, message: 'Selecione a conta' }]}>
              <Select placeholder="Selecione" size="large" onChange={handleAccountChange} style={{ borderRadius: 10 }}>
                {accounts.map((a) => (
                  <Option key={a.id} value={a.id}>
                    {a.isCreditCard ? <CreditCardOutlined /> : <BankOutlined />} {a.name}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Col>
        </Row>

        <Row gutter={12}>
          <Col xs={24} md={isCreditCard ? 8 : 12}>
            <Form.Item name="amount" label={isCreditCard ? 'Valor da Parcela' : 'Valor (R$)'} rules={[{ required: true, message: 'Informe o valor' }]}>
              <InputMoney placeholder="0,00" size="large" />
            </Form.Item>
          </Col>

          {isCreditCard && (!transactionToEdit || applyToSeries) && (
            <>
              <Col xs={24} md={8}>
                <Form.Item name="totalInstallments" label="Total Parcelas">
                  <InputNumber min={1} max={48} size="large" style={{ width: '100%', borderRadius: 10 }} />
                </Form.Item>
              </Col>
              <Col xs={24} md={8}>
                <Form.Item name="installmentNumber" label="Parcela Atual">
                  <InputNumber min={1} max={48} size="large" style={{ width: '100%', borderRadius: 10 }} />
                </Form.Item>
              </Col>
            </>
          )}

          <Col xs={24} md={isCreditCard ? 24 : 12}>
            <Form.Item label="Situação">
              <Switch
                checked={isPaid}
                onChange={setIsPaid}
                checkedChildren="Confirmado"
                unCheckedChildren="Pendente"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>
  );
}
