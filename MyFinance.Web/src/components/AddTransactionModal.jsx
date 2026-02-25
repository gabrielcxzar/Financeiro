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
  Divider,
  Grid,
} from 'antd';
import { BankOutlined, CreditCardOutlined } from '@ant-design/icons';
import api from '../services/api';
import dayjs from 'dayjs';

const { Option } = Select;
const { useBreakpoint } = Grid;

export default function AddTransactionModal({ visible, onClose, onSuccess, transactionToEdit }) {
  const [form] = Form.useForm();
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);

  const [transactionType, setTransactionType] = useState('Expense');
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [isPaid, setIsPaid] = useState(true);

  const screens = useBreakpoint();
  const isCompact = !screens.md;

  useEffect(() => {
    if (!visible) return;

    loadData(transactionToEdit?.accountId);

    if (transactionToEdit) {
      form.setFieldsValue({
        ...transactionToEdit,
        date: dayjs(transactionToEdit.date),
        amount: Math.abs(transactionToEdit.amount),
        installments: transactionToEdit.installments || 1,
      });
      setTransactionType(transactionToEdit.type);
      setIsPaid(transactionToEdit.paid);
    } else {
      form.resetFields();
      form.setFieldsValue({ date: dayjs(), type: 'Expense', installments: 1 });
      setTransactionType('Expense');
      setIsPaid(true);
      setIsCreditCard(false);
    }
  }, [visible, transactionToEdit, form]);

  const loadData = async (accountId) => {
    try {
      const [catResponse, accResponse] = await Promise.all([api.get('/categories'), api.get('/accounts')]);
      setCategories(catResponse.data);
      setAccounts(accResponse.data);
      if (accountId) {
        const currentAccount = accResponse.data.find((acc) => acc.id === accountId);
        setIsCreditCard(Boolean(currentAccount?.isCreditCard));
      }
    } catch {
      message.error('Erro ao carregar dados');
    }
  };

  const filteredCategories = categories.filter((c) => c.type === transactionType);

  const handleAccountChange = (value) => {
    const acc = accounts.find((a) => a.id === value);
    if (acc?.isCreditCard) {
      setIsCreditCard(true);
      setIsPaid(false);
      return;
    }

    setIsCreditCard(false);
    setIsPaid(true);
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
        categoryId: values.categoryId,
        accountId: values.accountId,
        date: values.date.toISOString(),
        paid: isPaid,
        installments: values.installments || 1,
      };

      if (transactionToEdit) {
        await api.put(`/transactions/${transactionToEdit.id}`, payload);
        message.success('Atualizado!');
      } else {
        await api.post('/transactions', payload);
        message.success('Criado!');
      }

      onSuccess();
      onClose();
    } catch {
      message.error('Erro ao salvar');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={transactionToEdit ? 'Editar Lancamento' : 'Novo Lancamento'}
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      okText="Salvar"
      cancelText="Cancelar"
      width={isCompact ? 'calc(100vw - 20px)' : 640}
      style={{ top: isCompact ? 8 : 24 }}
      destroyOnClose
    >
      <Form form={form} layout="vertical" initialValues={{ type: 'Expense', installments: 1 }}>
        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item name="type" label="Tipo" rules={[{ required: true }]}>
              <Radio.Group buttonStyle="solid" onChange={(e) => setTransactionType(e.target.value)}>
                <Radio.Button value="Income" style={{ color: 'green' }}>
                  Receita
                </Radio.Button>
                <Radio.Button value="Expense" style={{ color: 'red' }}>
                  Despesa
                </Radio.Button>
              </Radio.Group>
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="date" label="Data" initialValue={dayjs()} rules={[{ required: true }]}>
              <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} allowClear={false} />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item name="description" label="Descricao" rules={[{ required: true }]}>
          <Input placeholder="Ex: Supermercado" />
        </Form.Item>

        <Row gutter={12}>
          <Col xs={24} sm={12}>
            <Form.Item name="categoryId" label="Categoria" rules={[{ required: true }]}>
              <Select placeholder="Selecione">
                {filteredCategories.map((c) => (
                  <Option key={c.id} value={c.id}>
                    <span style={{ color: c.color, marginRight: 8 }}>&bull;</span> {c.name}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item name="accountId" label="Conta" rules={[{ required: true }]}>
              <Select placeholder="Selecione" onChange={handleAccountChange}>
                {accounts.map((a) => (
                  <Option key={a.id} value={a.id}>
                    {a.isCreditCard ? <CreditCardOutlined /> : <BankOutlined />} {a.name}
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </Col>
        </Row>

        <Divider style={{ margin: '12px 0' }} />

        <Row gutter={12}>
          <Col xs={24} md={isCreditCard ? 8 : 12}>
            <Form.Item
              name="amount"
              label={isCreditCard ? 'Valor da Parcela' : 'Valor'}
              rules={[{ required: true }]}
            >
              <InputNumber
                style={{ width: '100%' }}
                prefix="R$"
                decimalSeparator=","
                precision={2}
                step={0.01}
                stringMode
              />
            </Form.Item>
          </Col>

          {isCreditCard && !transactionToEdit && (
            <Col xs={24} md={8}>
              <Form.Item name="installments" label="Qtd. Parcelas">
                <InputNumber min={1} max={48} style={{ width: '100%' }} />
              </Form.Item>
            </Col>
          )}

          <Col xs={24} md={isCreditCard ? 8 : 12}>
            <Form.Item label="Situacao">
              <Switch
                checked={isPaid}
                onChange={setIsPaid}
                checkedChildren="Pago"
                unCheckedChildren="Pendente"
              />
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Modal>
  );
}
