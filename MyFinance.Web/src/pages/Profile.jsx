import React, { useEffect, useState } from 'react';
import { Card, Button, Popconfirm, message, Avatar, Typography, Divider } from 'antd';
import { UserOutlined, DeleteOutlined, LogoutOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Title, Text } = Typography;

export default function Profile() {
  const [user, setUser] = useState({ name: '', email: '' });

  useEffect(() => {
    api.get('/users/me').then(res => setUser(res.data));
  }, []);

  const handleWipeData = async () => {
    try {
      await api.post('/users/wipe-data');
      message.success('Todos os dados foram apagados. Começando do zero!');
      window.location.reload(); // Recarrega para atualizar tudo
    } catch (error) {
      message.error('Erro ao apagar dados');
    }
  };

  return (
    <div style={{ maxWidth: 600, margin: '0 auto' }}>
      <h2 style={{ marginBottom: 24 }}>Meu Perfil</h2>
      
      <Card style={{ textAlign: 'center', marginBottom: 24 }}>
        <Avatar size={100} icon={<UserOutlined />} style={{ backgroundColor: '#1890ff', marginBottom: 16 }} />
        <Title level={3}>{user.name}</Title>
        <Text type="secondary">{user.email}</Text>
      </Card>

      <Card title="Zona de Perigo" style={{ borderColor: '#ff4d4f' }}>
        <p>Deseja recomeçar do zero? Isso apagará todas as transações, contas e categorias.</p>
        <Popconfirm
            title="Tem certeza absoluta?"
            description="Essa ação é irreversível."
            onConfirm={handleWipeData}
            okText="Sim, apagar tudo"
            cancelText="Cancelar"
        >
            <Button danger icon={<DeleteOutlined />} block size="large">
                ZERAR MINHA CONTA
            </Button>
        </Popconfirm>
      </Card>
    </div>
  );
}