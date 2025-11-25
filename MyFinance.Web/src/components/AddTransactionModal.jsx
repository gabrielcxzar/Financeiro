import React, { useState, useEffect } from 'react';
import { Modal, Form, Input, Radio, Select, message, InputNumber, DatePicker, Switch } from 'antd';
import api from '../services/api';
import dayjs from 'dayjs';

const { Option } = Select;

export default function AddTransactionModal({ visible, onClose, onSuccess }) {
  const [form] = Form.useForm();
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(false);
  
  // Controle do Switch de Pagamento
  const [isPaid, setIsPaid] = useState(true);

  useEffect(() => {
    if (visible) {
      loadData();
      form.resetFields();
      setIsPaid(true); // Reseta para pago ao abrir
    }
  }, [visible]);

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

  const handleOk = () => {
    form.validateFields().then(async (values) => {
      setLoading(true);
      try {
        const transaction = {
          description: values.description,
          amount: Number(values.amount),
          type: values.type,
          categoryId: values.categoryId,
          accountId: values.accountId,
          date: values.date ? values.date.toISOString() : new Date().toISOString(),
          paid: isPaid // <--- Envia o status do Switch
        };

        await api.post('/transactions', transaction);
        
        message.success('Transação salva!');
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
      title="Nova Transação"
      open={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={loading}
      okText="Salvar"
      cancelText="Cancelar"
    >
      <Form form={form} layout="vertical" initialValues={{ type: 'Expense' }}>
        
        <Form.Item name="type" label="Tipo">
          <Radio.Group buttonStyle="solid">
            <Radio.Button value="Income" style={{ color: 'green' }}>Receita</Radio.Button>
            <Radio.Button value="Expense" style={{ color: 'red' }}>Despesa</Radio.Button>
          </Radio.Group>
        </Form.Item>

        <Form.Item name="date" label="Data" initialValue={dayjs()}>
          <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
        </Form.Item>

        <Form.Item 
            name="amount" 
            label="Valor" 
            rules={[{ required: true, message: 'Informe o valor' }]}
        >
          <InputNumber 
            style={{ width: '100%' }} 
            prefix="R$" 
            precision={2}
            placeholder="0,00"
          />
        </Form.Item>

        {/* STATUS DE PAGAMENTO */}
        <Form.Item label="Situação">
           <Switch 
              checked={isPaid} 
              onChange={setIsPaid} 
              checkedChildren="Pago / Recebido" 
              unCheckedChildren="Pendente / Agendado" 
              defaultChecked
           />
        </Form.Item>

        <Form.Item 
            name="description" 
            label="Descrição" 
            rules={[{ required: true, message: 'Informe a descrição' }]}
        >
          <Input placeholder="Ex: Supermercado" />
        </Form.Item>

        <Form.Item 
            name="categoryId" 
            label="Categoria" 
            rules={[{ required: true, message: 'Selecione a categoria' }]}
        >
          <Select placeholder="Selecione">
            {categories.map(c => (
              <Option key={c.id} value={c.id}>{c.name}</Option>
            ))}
          </Select>
        </Form.Item>

        <Form.Item 
            name="accountId" 
            label="Conta" 
            rules={[{ required: true, message: 'Selecione a conta' }]}
        >
          <Select placeholder="Selecione">
            {accounts.map(a => (
              <Option key={a.id} value={a.id}>{a.name}</Option>
            ))}
          </Select>
        </Form.Item>

      </Form>
    </Modal>
  );
}