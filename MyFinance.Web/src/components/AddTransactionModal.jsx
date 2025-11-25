import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Radio, Select, message, DatePicker, Switch, Row, Col, Divider } from 'antd';
import api from '../services/api';
import dayjs from 'dayjs';
import InputMoney from './InputMoney';

const { Option } = Select;

export default function AddTransactionModal({ visible, onClose, onSuccess, transactionToEdit }) {
  const [form] = Form.useForm();
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);
  
  // Controles
  const [transactionType, setTransactionType] = useState('Expense'); // 'Income' ou 'Expense'
  const [isCreditCard, setIsCreditCard] = useState(false);
  const [isPaid, setIsPaid] = useState(true);

  useEffect(() => {
    if (visible) {
      loadData();
      if (transactionToEdit) {
        // Modo Edição
        form.setFieldsValue({
            ...transactionToEdit,
            date: dayjs(transactionToEdit.date),
            amount: Math.abs(transactionToEdit.amount)
        });
        setTransactionType(transactionToEdit.type);
        setIsPaid(transactionToEdit.paid);
        setIsCreditCard(false); // Simplificação: não muda parcelas na edição
      } else {
        // Modo Criação
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

  // Filtra categorias pelo tipo selecionado
  const filteredCategories = categories.filter(c => c.type === transactionType);

  const handleAccountChange = (value) => {
    const acc = accounts.find(a => a.id === value);
    if (acc && acc.isCreditCard) {
        setIsCreditCard(true);
        setIsPaid(false); // Cartão nasce pendente
    } else {
        setIsCreditCard(false);
        setIsPaid(true); // Conta corrente nasce paga
    }
  };

  const handleOk = () => {
    form.validateFields().then(async (values) => {
      setLoading(true);
      try {
        const payload = {
          id: transactionToEdit ? transactionToEdit.id : 0,
          description: values.description,
          amount: Number(values.amount), // Backend agora espera o valor da parcela aqui
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
      width={600} // Mais largo para caber lado a lado
    >
      <Form form={form} layout="vertical" initialValues={{ type: 'Expense', installments: 1 }}>
        
        {/* Linha 1: Tipo e Data */}
        <Row gutter={16}>
            <Col span={12}>
                <Form.Item name="type" label="Tipo de Movimentação">
                    <Radio.Group buttonStyle="solid" onChange={e => setTransactionType(e.target.value)}>
                        <Radio.Button value="Income" style={{ color: 'green' }}>Receita</Radio.Button>
                        <Radio.Button value="Expense" style={{ color: 'red' }}>Despesa</Radio.Button>
                    </Radio.Group>
                </Form.Item>
            </Col>
            <Col span={12}>
                <Form.Item name="date" label="Data do Lançamento" initialValue={dayjs()}>
                    <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
                </Form.Item>
            </Col>
        </Row>

        {/* Linha 2: Descrição */}
        <Form.Item name="description" label="Descrição" rules={[{ required: true }]}>
            <Input placeholder="Ex: Almoço, Uber, Salário..." />
        </Form.Item>

        {/* Linha 3: Categoria e Conta */}
        <Row gutter={16}>
            <Col span={12}>
                <Form.Item name="categoryId" label="Categoria" rules={[{ required: true }]}>
                    <Select placeholder="Selecione">
                        {filteredCategories.map(c => (
                            <Option key={c.id} value={c.id}>
                                <span style={{ color: c.color, marginRight: 8 }}>●</span>
                                {c.name}
                            </Option>
                        ))}
                    </Select>
                </Form.Item>
            </Col>
            <Col span={12}>
                <Form.Item name="accountId" label="Conta / Cartão" rules={[{ required: true }]}>
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

        {/* Linha 4: Valores e Status */}
        <Row gutter={16}>
            <Col span={isCreditCard ? 8 : 12}>
                <Form.Item name="amount" label={isCreditCard ? "Valor da Parcela" : "Valor Total"} rules={[{ required: true }]}>
                    <InputMoney style={{ width: '100%' }} prefix="R$" precision={2} />
                </Form.Item>
            </Col>
            
            {isCreditCard && !transactionToEdit && (
                <Col span={8}>
                    <Form.Item name="installments" label="Qtd. Parcelas">
                        <InputMoney min={1} max={48} style={{ width: '100%' }} addonAfter="x" />
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