import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Radio, Select, message, InputNumber, DatePicker, Switch, Row, Col, Divider } from 'antd';
import api from '../services/api';
import dayjs from 'dayjs';

const { Option } = Select;

export default function AddTransactionModal({ visible, onClose, onSuccess, transactionToEdit }) {
  const [form] = Form.useForm();
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);
  
  const [transactionType, setTransactionType] = useState('Expense');
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [isPaid, setIsPaid] = useState(true);

  useEffect(() => {
    if (visible) {
      loadData();
      if (transactionToEdit) {
        form.setFieldsValue({
            ...transactionToEdit,
            date: dayjs(transactionToEdit.date),
            amount: Math.abs(transactionToEdit.amount)
        });
        setTransactionType(transactionToEdit.type);
        setIsPaid(transactionToEdit.paid);
        setIsCreditCard(false); 
      } else {
        form.resetFields();
        setTransactionType('Expense');
        setIsPaid(true);
        setIsCreditCard(false);
      }
    }
  }, [visible, transactionToEdit]);

  const loadData = async () => {
    try {
      const [catResponse, accResponse] = await Promise.all([
        api.get('/categories'),
        api.get('/accounts')
      ]);
      setCategories(catResponse.data);
      setAccounts(accResponse.data);
    } catch (error) {
      message.error('Erro ao carregar dados');
    }
  };

  const filteredCategories = categories.filter(c => c.type === transactionType);

  const handleAccountChange = (value) => {
    const acc = accounts.find(a => a.id === value);
    if (acc && acc.isCreditCard) {
        setIsCreditCard(true);
        setIsPaid(false);
    } else {
        setIsCreditCard(false);
        setIsPaid(true);
    }
  };

  const handleOk = () => {
    form.validateFields().then(async (values) => {
      setLoading(true);
      try {
        const payload = {
          id: transactionToEdit ? transactionToEdit.id : 0,
          description: values.description,
          amount: Number(values.amount),
          type: values.type,
          categoryId: values.categoryId,
          accountId: values.accountId,
          date: values.date.toISOString(),
          paid: isPaid,
          installments: values.installments || 1
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
      } catch (error) {
        console.error(error);
        message.error('Erro ao salvar');
      } finally {
        setLoading(false);
      }
    });
  };

  return (
    <Modal
      title={transactionToEdit ? "Editar Lançamento" : "Novo Lançamento"}
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      okText="Salvar"
      cancelText="Cancelar"
      width={600}
    >
      <Form form={form} layout="vertical" initialValues={{ type: 'Expense', installments: 1 }}>
        
        <Row gutter={16}>
            <Col span={12}>
                <Form.Item name="type" label="Tipo">
                    <Radio.Group buttonStyle="solid" onChange={e => setTransactionType(e.target.value)}>
                        <Radio.Button value="Income" style={{ color: 'green' }}>Receita</Radio.Button>
                        <Radio.Button value="Expense" style={{ color: 'red' }}>Despesa</Radio.Button>
                    </Radio.Group>
                </Form.Item>
            </Col>
            <Col span={12}>
                <Form.Item name="date" label="Data" initialValue={dayjs()}>
                    <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
                </Form.Item>
            </Col>
        </Row>

        <Form.Item name="description" label="Descrição" rules={[{ required: true }]}>
            <Input placeholder="Ex: Supermercado" />
        </Form.Item>

        <Row gutter={16}>
            <Col span={12}>
                <Form.Item name="categoryId" label="Categoria" rules={[{ required: true }]}>
                    <Select placeholder="Selecione">
                        {filteredCategories.map(c => (
                            <Option key={c.id} value={c.id}>
                                <span style={{ color: c.color, marginRight: 8 }}>&bull;</span> {c.name}
                            </Option>
                        ))}
                    </Select>
                </Form.Item>
            </Col>
            <Col span={12}>
                <Form.Item name="accountId" label="Conta" rules={[{ required: true }]}>
                    <Select placeholder="Selecione" onChange={handleAccountChange}>
                        {accounts.map(a => (
                            <Option key={a.id} value={a.id}>
                                {a.isCreditCard ? `💳 ${a.name}` : `🏦 ${a.name}`}
                            </Option>
                        ))}
                    </Select>
                </Form.Item>
            </Col>
        </Row>

        <Divider style={{ margin: '12px 0' }} />

        <Row gutter={16}>
            <Col span={isCreditCard ? 8 : 12}>
                {/* INPUT DE DINHEIRO SIMPLIFICADO E FUNCIONAL */}
                <Form.Item name="amount" label={isCreditCard ? "Valor da Parcela" : "Valor"} rules={[{ required: true }]}>
                    <InputNumber 
                        style={{ width: '100%' }} 
                        prefix="R$" 
                        decimalSeparator="," 
                        precision={2}
                        step={0.01}
                        stringMode // Garante precisão
                    />
                </Form.Item>
            </Col>
            
            {isCreditCard && !transactionToEdit && (
                <Col span={8}>
                    <Form.Item name="installments" label="Qtd. Parcelas">
                        <InputNumber min={1} max={48} style={{ width: '100%' }} /> 
                    </Form.Item>
                </Col>
            )}

            <Col span={isCreditCard ? 8 : 12}>
                <Form.Item label="Situação">
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
