import React, { useEffect, useState } from 'react';
import { Table, Button, Card, Tag, message, Modal, Form, Input, InputNumber, Select, Radio, Popconfirm } from 'antd';
import { SyncOutlined, PlusOutlined, DeleteOutlined, ThunderboltOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Option } = Select;

const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Recurring() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  
  // Dados para o formulário
  const [categories, setCategories] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [form] = Form.useForm();

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const [recResponse, catResponse, accResponse] = await Promise.all([
        api.get('/recurring'),
        api.get('/categories'),
        api.get('/accounts')
      ]);
      setItems(recResponse.data);
      setCategories(catResponse.data);
      setAccounts(accResponse.data);
    } catch (error) {
      console.error(error);
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
    } catch (error) {
      message.error('Erro ao salvar');
    }
  };

  const handleDelete = async (id) => {
    await api.delete(`/recurring/${id}`);
    message.success('Regra removida');
    loadData();
  };

  // BOTÃO MÁGICO: Gera as despesas para o mês atual
  const handleGenerate = async () => {
    const today = new Date();
    const month = today.getMonth() + 1;
    const year = today.getFullYear();
    
    try {
      const response = await api.post(`/recurring/generate?month=${month}&year=${year}`);
      message.success(response.data.message);
    } catch (error) {
      message.error('Erro ao gerar');
    }
  };

  const columns = [
    { title: 'Descrição', dataIndex: 'description', key: 'desc' },
    { title: 'Dia', dataIndex: 'dayOfMonth', key: 'day', render: (d) => <Tag>Todo dia {d}</Tag> },
    { 
      title: 'Valor', 
      dataIndex: 'amount', 
      render: (val, rec) => <span style={{color: rec.type === 'Expense' ? 'red' : 'green', fontWeight: 'bold'}}>{formatMoney(val)}</span> 
    },
    { title: 'Conta', dataIndex: ['account', 'name'] },
    { 
        title: 'Ações', 
        render: (_, rec) => (
            <Popconfirm title="Remover recorrência?" onConfirm={() => handleDelete(rec.id)}>
                <Button danger icon={<DeleteOutlined />} type="text" />
            </Popconfirm>
        ) 
    }
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 24 }}>
        <h2 style={{ margin: 0 }}>Despesas Fixas & Assinaturas</h2>
        <div style={{ display: 'flex', gap: 10 }}>
            <Button icon={<ThunderboltOutlined />} onClick={handleGenerate}>
                Gerar neste Mês
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setIsModalOpen(true)}>
                Nova Fixa
            </Button>
        </div>
      </div>

      <Card>
        <Table dataSource={items} columns={columns} rowKey="id" loading={loading} />
      </Card>

      {/* Modal de Cadastro */}
      <Modal title="Nova Despesa Fixa" open={isModalOpen} onOk={handleSave} onCancel={() => setIsModalOpen(false)}>
        <Form form={form} layout="vertical" initialValues={{ type: 'Expense', dayOfMonth: 5 }}>
            <Form.Item name="description" label="Descrição" rules={[{ required: true }]}>
                <Input placeholder="Ex: Netflix, Aluguel" />
            </Form.Item>
            
            <div style={{ display: 'flex', gap: 16 }}>
                <Form.Item name="amount" label="Valor" style={{ flex: 1 }} rules={[{ required: true }]}>
                    <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
                </Form.Item>
                <Form.Item name="dayOfMonth" label="Dia do Mês" style={{ flex: 1 }} rules={[{ required: true }]}>
                    <InputNumber min={1} max={31} style={{ width: '100%' }} />
                </Form.Item>
            </div>

            <Form.Item name="type" label="Tipo">
                <Radio.Group>
                    <Radio value="Expense">Despesa</Radio>
                    <Radio value="Income">Receita Fixa</Radio>
                </Radio.Group>
            </Form.Item>

            <Form.Item name="categoryId" label="Categoria" rules={[{ required: true }]}>
                <Select>
                    {categories.map(c => <Option key={c.id} value={c.id}>{c.name}</Option>)}
                </Select>
            </Form.Item>

            <Form.Item name="accountId" label="Debitar de" rules={[{ required: true }]}>
                <Select>
                    {accounts.map(a => <Option key={a.id} value={a.id}>{a.name}</Option>)}
                </Select>
            </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}