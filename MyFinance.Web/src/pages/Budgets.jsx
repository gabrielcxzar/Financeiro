import React, { useEffect, useState } from 'react';
import { Card, Progress, Button, Modal, Form, InputNumber, Select, message, Row, Col, Statistic, Empty, Popconfirm } from 'antd';
import { PlusOutlined, DeleteOutlined, TrophyOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Option } = Select;
const formatMoney = (val) => val.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Budgets({ month, year }) {
  const [budgets, setBudgets] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    loadData();
  }, [month, year]); // Recarrega se mudar o ms no filtro global

  const loadData = async () => {
    try {
      setLoading(true);
      // Busca Metas, Categorias e Transações do Mês
      const [budgetsRes, catRes, transRes] = await Promise.all([
        api.get('/budgets'),
        api.get('/categories'),
        api.get(`/transactions?month=${month}&year=${year}`)
      ]);

      setBudgets(budgetsRes.data);
      setCategories(catRes.data);
      setTransactions(transRes.data);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      await api.post('/budgets', values);
      message.success('Meta definida!');
      setIsModalOpen(false);
      form.resetFields();
      loadData();
    } catch (error) {
      message.error('Erro ao salvar meta');
    }
  };

  const handleDelete = async (id) => {
    await api.delete(`/budgets/${id}`);
    message.success('Meta removida');
    loadData();
  };

  // Fun o que calcula o progresso de cada meta
  const renderBudgetCard = (budget) => {
    // Soma gastos dessa categoria no mês atual
    const spent = transactions
        .filter(t => t.categoryId === budget.categoryId && t.type === 'Expense')
        .reduce((acc, t) => acc + t.amount, 0);

    const percent = Math.min(((spent / budget.amount) * 100), 100);
    const status = percent >= 100 ? 'exception' : percent > 80 ? 'active' : 'success';
    const strokeColor = percent >= 100 ? '#ff4d4f' : percent > 80 ? '#faad14' : '#52c41a';

    return (
      <Col xs={24} sm={12} md={8} key={budget.id}>
        <Card 
            title={
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span style={{ width: 12, height: 12, borderRadius: '50%', background: budget.category?.color || '#ccc' }} />
                    {budget.category?.name}
                </div>
            }
            extra={
                <Popconfirm title="Remover meta?" onConfirm={() => handleDelete(budget.id)}>
                    <Button type="text" danger icon={<DeleteOutlined />} size="small" />
                </Popconfirm>
            }
            style={{ borderRadius: 12, boxShadow: '0 2px 8px rgba(0,0,0,0.05)' }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
            <span style={{ color: '#888' }}>Gasto: <b>{formatMoney(spent)}</b></span>
            <span style={{ color: '#888' }}>Meta: <b>{formatMoney(budget.amount)}</b></span>
          </div>
          
          <Progress 
            percent={percent} 
            strokeColor={strokeColor} 
            status={status} 
            format={(p) => `${p.toFixed(0)}%`}
          />
          
          <div style={{ marginTop: 16, textAlign: 'center' }}>
            {spent > budget.amount ? (
                <span style={{ color: '#ff4d4f', fontWeight: 'bold' }}>Voc estourou R$ {formatMoney(spent - budget.amount)}!</span>
            ) : (
                <span style={{ color: '#52c41a' }}>Resta R$ {formatMoney(budget.amount - spent)}</span>
            )}
          </div>
        </Card>
      </Col>
    );
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>
        <h2 style={{ margin: 0 }}>Metas de Orçamento</h2>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setIsModalOpen(true)}>
            Definir Meta
        </Button>
      </div>

      {budgets.length === 0 ? (
        <Empty description="Nenhuma meta definida para controlar seus gastos." />
      ) : (
        <Row gutter={[16, 16]}>
            {budgets.map(renderBudgetCard)}
        </Row>
      )}

      <Modal title="Definir Meta de Gasto" open={isModalOpen} onOk={handleSave} onCancel={() => setIsModalOpen(false)}>
        <Form form={form} layout="vertical">
            <Form.Item name="categoryId" label="Categoria" rules={[{ required: true, message: 'Escolha uma categoria' }]}>
                <Select placeholder="Ex: Alimenta o">
                    {categories.map(c => <Option key={c.id} value={c.id}>{c.name}</Option>)}
                </Select>
            </Form.Item>

            <Form.Item name="amount" label="Limite Mensal (R$)" rules={[{ required: true, message: 'Informe o limite' }]}>
                <InputNumber style={{ width: '100%' }} prefix="R$" precision={2} />
            </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}