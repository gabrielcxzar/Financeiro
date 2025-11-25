import React, { useEffect, useState } from 'react';
import { Table, Tag, Button, Popconfirm, message, Card } from 'antd';
import { DeleteOutlined } from '@ant-design/icons';
import api from '../services/api';

const formatMoney = (value) => {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};

export default function Transactions() {
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadTransactions();
  }, []);

  const loadTransactions = async () => {
    setLoading(true);
    try {
      const response = await api.get('/transactions');
      setTransactions(response.data);
    } catch (error) {
      message.error('Erro ao carregar transações');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id) => {
    try {
      await api.delete(`/transactions/${id}`);
      message.success('Transação excluída e saldo atualizado!');
      loadTransactions(); // Recarrega a lista
    } catch (error) {
      message.error('Erro ao excluir');
    }
  };

  const columns = [
    {
      title: 'Data',
      dataIndex: 'date',
      key: 'date',
      render: (date) => new Date(date).toLocaleDateString('pt-BR'),
      sorter: (a, b) => new Date(a.date) - new Date(b.date),
    },
    {
      title: 'Descrição',
      dataIndex: 'description',
      key: 'description',
    },
    {
      title: 'Categoria',
      dataIndex: ['category', 'name'],
      key: 'category',
      render: (text) => <Tag color="cyan">{text || 'Geral'}</Tag>,
      filters: [
        // Aqui poderíamos popular dinamicamente depois
        { text: 'Salário', value: 'Salário' },
        { text: 'Alimentação', value: 'Alimentação' },
      ],
      onFilter: (value, record) => record.category?.name === value,
    },
    {
      title: 'Valor',
      dataIndex: 'amount',
      key: 'amount',
      render: (value, record) => (
        <span style={{ color: record.type === 'Expense' ? '#cf1322' : '#3f8600', fontWeight: 'bold' }}>
          {record.type === 'Expense' ? '- ' : '+ '}
          {formatMoney(value)}
        </span>
      ),
      sorter: (a, b) => a.amount - b.amount,
    },
    {
      title: 'Ações',
      key: 'action',
      render: (_, record) => (
        <Popconfirm
          title="Tem certeza?"
          description="Isso vai estornar o valor da conta."
          onConfirm={() => handleDelete(record.id)}
          okText="Sim"
          cancelText="Não"
        >
          <Button type="text" danger icon={<DeleteOutlined />} />
        </Popconfirm>
      ),
    },
  ];

  return (
    <div>
       <h2 style={{ marginBottom: 16 }}>Extrato Completo</h2>
       <Card bordered={false} style={{ borderRadius: 8 }}>
          <Table 
            dataSource={transactions} 
            columns={columns} 
            rowKey="id"
            loading={loading}
            pagination={{ pageSize: 8 }}
          />
       </Card>
    </div>
  );
}