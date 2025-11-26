import React, { useEffect, useState } from 'react';
import { Table, Tag, Button, Modal, message, Card, Tooltip } from 'antd';
import { DeleteOutlined, EditOutlined, CloudUploadOutlined } from '@ant-design/icons';
import AddTransactionModal from '../components/AddTransactionModal';
import ImportModal from '../components/ImportModal';
import api from '../services/api';

const formatMoney = (value) => {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};

export default function Transactions({ month, year }) {
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isImportOpen, setIsImportOpen] = useState(false);
  const [editingItem, setEditingItem] = useState(null);

  useEffect(() => {
    loadTransactions();
  }, [month, year]);

  const loadTransactions = async () => {
    setLoading(true);
    try {
      const query = month && year ? `?month=${month}&year=${year}` : '';
      const response = await api.get(`/transactions${query}`);
      setTransactions(response.data);
    } catch (error) {
      message.error('Erro ao carregar transações');
    } finally {
      setLoading(false);
    }
  };

  // Lógica inteligente de exclusão
  const handleDelete = (record) => {
      if (record.installmentId) { // <--- Só pergunta se tiver ID de grupo
        Modal.confirm({
            title: 'Excluir Parcelamento',
            content: 'Esta transação faz parte de uma série. O que deseja fazer?',
            okText: 'Apagar TODAS',
            cancelText: 'Apenas ESTA',
            okButtonProps: { danger: true },
            onOk: () => executeDelete(record.id, true), // <--- TRUE = Apaga Série
            onCancel: () => executeDelete(record.id, false) // <--- FALSE = Apaga Só Essa
        });
      } else {
          // Se não tiver ID (antigas), apaga direto sem perguntar
          executeDelete(record.id, false);
      }
  };

  const executeDelete = async (id, deleteAll) => {
    try {
      await api.delete(`/transactions/${id}?deleteAll=${deleteAll}`);
      message.success('Excluído com sucesso!');
      loadTransactions();
    } catch (error) {
      message.error('Erro ao excluir');
    }
  };

  const handleEdit = (record) => {
    setEditingItem(record);
    setIsModalOpen(true);
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
            <Tooltip title="Editar">
                <Button 
                    type="text" 
                    icon={<EditOutlined style={{ color: '#1890ff' }} />} 
                    onClick={() => handleEdit(record)} 
                />
            </Tooltip>
            <Tooltip title="Excluir">
                <Button 
                    type="text" 
                    danger 
                    icon={<DeleteOutlined />} 
                    onClick={() => handleDelete(record)} 
                />
            </Tooltip>
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

       <Card variant="borderless" style={{ borderRadius: 8 }}>
          <Table 
            dataSource={transactions} 
            columns={columns} 
            rowKey="id"
            loading={loading}
            pagination={{ pageSize: 10 }}
          />
       </Card>

       <AddTransactionModal 
           visible={isModalOpen}
           transactionToEdit={editingItem}
           onClose={() => {
               setIsModalOpen(false);
               setEditingItem(null);
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