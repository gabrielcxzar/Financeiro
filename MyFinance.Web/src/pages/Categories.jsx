import React, { useEffect, useState } from 'react';
import { Table, Button, Card, Tag, message, Modal, Form, Input, Radio, Popconfirm, ColorPicker } from 'antd';
import { PlusOutlined, DeleteOutlined, TagsOutlined } from '@ant-design/icons';
import api from '../services/api';

export default function Categories() {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [form] = Form.useForm();

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const response = await api.get('/categories');
      setCategories(response.data);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      const values = await form.validateFields();
      
      // Converte o objeto de cor do AntDesign para string Hex (#ffffff)
      const colorHex = typeof values.color === 'string' ? values.color : values.color.toHexString();

      await api.post('/categories', { ...values, color: colorHex });
      
      message.success('Categoria criada!');
      setIsModalOpen(false);
      form.resetFields();
      loadData();
    } catch (error) {
      message.error('Erro ao salvar');
    }
  };

  const handleDelete = async (id) => {
    try {
      await api.delete(`/categories/${id}`);
      message.success('Categoria removida');
      loadData();
    } catch (error) {
      // O backend devolve 400 se tiver transação vinculada
      message.error('No  possvel apagar categoria em uso.');
    }
  };

  const columns = [
    { 
      title: 'Cor', 
      dataIndex: 'color', 
      key: 'color',
      width: 80,
      render: (color) => <div style={{ width: 24, height: 24, borderRadius: 4, background: color || '#ccc' }} />
    },
    { 
      title: 'Nome', 
      dataIndex: 'name', 
      key: 'name',
      render: (text) => <strong>{text}</strong>
    },
    { 
      title: 'Tipo', 
      dataIndex: 'type', 
      key: 'type', 
      render: (type) => (
        <Tag color={type === 'Income' ? 'green' : 'red'}>
            {type === 'Income' ? 'Receita' : 'Despesa'}
        </Tag>
      ) 
    },
    { 
        title: 'Ações', 
        key: 'action',
        render: (_, rec) => (
            <Popconfirm title="Excluir categoria?" onConfirm={() => handleDelete(rec.id)}>
                <Button danger icon={<DeleteOutlined />} type="text" />
            </Popconfirm>
        ) 
    }
  ];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 24 }}>
        <h2 style={{ margin: 0 }}>Categorias</h2>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setIsModalOpen(true)}>
            Nova Categoria
        </Button>
      </div>

      <Card bordered={false} style={{ borderRadius: 8 }}>
        <Table 
            dataSource={categories} 
            columns={columns} 
            rowKey="id" 
            loading={loading} 
            pagination={{ pageSize: 10 }}
        />
      </Card>

      {/* Modal de Cadastro */}
      <Modal title="Nova Categoria" open={isModalOpen} onOk={handleSave} onCancel={() => setIsModalOpen(false)}>
        <Form form={form} layout="vertical" initialValues={{ type: 'Expense', color: '#1677ff' }}>
            <Form.Item name="name" label="Nome" rules={[{ required: true, message: 'Digite o nome' }]}>
                <Input placeholder="Ex: Moto, Assinaturas..." />
            </Form.Item>

            <Form.Item name="type" label="Tipo">
                <Radio.Group buttonStyle="solid">
                    <Radio.Button value="Expense">Despesa</Radio.Button>
                    <Radio.Button value="Income">Receita</Radio.Button>
                </Radio.Group>
            </Form.Item>

            <Form.Item name="color" label="Cor da Etiqueta" rules={[{ required: true }]}>
                <ColorPicker showText />
            </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
