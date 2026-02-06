import React, { useEffect, useState } from 'react';
import { Card, Button, Popconfirm, message, Avatar, Typography } from 'antd';
import { UserOutlined, DeleteOutlined } from '@ant-design/icons';
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
      message.success('Dados apagados e categorias resetadas para o padro.');
      window.location.reload();
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
        <p>Isso apagar todas as transaes, contas, metas e recorrências. As categorias sero resetadas para o padro.</p>
        <Popconfirm
          title="Tem certeza absoluta?"
          description="Essa ao  irreversvel."
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
