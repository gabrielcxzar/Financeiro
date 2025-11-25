import React, { useEffect, useState } from 'react';
import { Table, Tag, Button, Popconfirm, message, Card } from 'antd';
import { DeleteOutlined, EditOutlined,CloudUploadOutlined } from '@ant-design/icons';
import AddTransactionModal from '../components/AddTransactionModal';
import api from '../services/api';
import ImportModal from '../components/ImportModal';

const formatMoney = (value) => {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};


export default function Transactions({ month, year }) {
  const [isImportOpen, setIsImportOpen] = useState(false);
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState(null);

  useEffect(() => {
    loadTransactions();
  }, [month, year]); // Recarrega se mudar a data lá no topo

  const loadTransactions = async () => {
    setLoading(true);
    try {
      // Passa o filtro de data se existir
      const query = month && year ? `?month=${month}&year=${year}` : '';
      const response = await api.get(`/transactions${query}`);
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
      loadTransactions(); 
    } catch (error) {
      message.error('Erro ao excluir');
    }
  };

  const handleEdit = (record) => {
    setEditingItem(record); // Guarda o item que vamos editar
    setIsModalOpen(true);   // Abre o modal
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
      title: 'Status',
      dataIndex: 'paid',
      key: 'paid',
      render: (paid) => (
          <Tag color={paid ? 'green' : 'orange'}>
              {paid ? 'Pago' : 'Pendente'}
          </Tag>
      )
    },
    {
      title: 'Ações',
      key: 'action',
      render: (_, record) => (
        <div style={{ display: 'flex', gap: 8 }}>
            <Button 
                type="text" 
                icon={<EditOutlined style={{ color: '#1890ff' }} />} 
                onClick={() => handleEdit(record)} 
            />
            <Popconfirm
              title="Tem certeza?"
              description="Isso vai estornar o valor da conta."
              onConfirm={() => handleDelete(record.id)}
              okText="Sim"
              cancelText="Não"
            >
               <Button type="text" danger icon={<DeleteOutlined />} />
            </Popconfirm>
        </div>
      ),
    },
  ];

  return (
    <div>
       <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h2 style={{ margin: 0 }}>Extrato Completo</h2>
          <Button icon={<CloudUploadOutlined />} onClick={() => setIsImportOpen(true)}>
              Importar CSV
          </Button>
      </div>
       <Card bordered={false} style={{ borderRadius: 8 }}>
          <Table 
            dataSource={transactions} 
            columns={columns} 
            rowKey="id"
            loading={loading}
            pagination={{ pageSize: 10 }}
          />
       </Card>

       {/* MODAL DE EDIÇÃO/CRIAÇÃO PRECISA ESTAR AQUI */}
       <AddTransactionModal 
           visible={isModalOpen}
           transactionToEdit={editingItem}
           onClose={() => {
               setIsModalOpen(false);
               setEditingItem(null); // Limpa a edição ao fechar
           }}
           onSuccess={loadTransactions} 
       />
       <ImportModal 
          visible={isImportOpen}
          onClose={() => setIsImportOpen(false)}
          onSuccess={loadTransactions}
        />
    </div>
  );
}