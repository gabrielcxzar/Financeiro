import React, { useEffect, useState } from 'react';
import { Card, Button, Popconfirm, message, Avatar, Typography, Select, Space, Tag } from 'antd';
import { UserOutlined, DeleteOutlined, GoogleOutlined, SyncOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Title, Text } = Typography;

export default function Profile() {
  const [user, setUser] = useState({ name: '', email: '' });
  const [gmail, setGmail] = useState({ enabled: false, connected: false, searchQuery: '' });
  const [accounts, setAccounts] = useState([]);
  const [selectedAccount, setSelectedAccount] = useState(null);
  const [gmailLoading, setGmailLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const loadUser = async () => {
      try {
        const res = await api.get('/users/me');
        if (!cancelled) {
          setUser(res.data);
        }
      } catch (error) {
        if (!cancelled) {
          console.error('Erro ao carregar perfil do usuario:', error);
          message.error('Nao foi possivel carregar os dados do perfil.');
        }
      }
    };

    loadUser();

    Promise.all([api.get('/integrations/gmail/status'), api.get('/accounts')])
      .then(([gmailResponse, accountsResponse]) => {
        setGmail(gmailResponse.data);
        setSelectedAccount(gmailResponse.data.defaultAccountId || null);
        setAccounts(accountsResponse.data || []);
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
  }, []);

  const connectGmail = async () => {
    try {
      const response = await api.get('/integrations/gmail/connect');
      window.location.assign(response.data.authorizationUrl);
    } catch (error) {
      message.error(error.response?.data || 'Gmail não está configurado neste ambiente.');
    }
  };

  const configureGmail = async (accountId) => {
    setSelectedAccount(accountId);
    try {
      await api.post('/integrations/gmail/configure', { defaultAccountId: accountId, searchQuery: gmail.searchQuery });
      message.success('Conta de destino do Gmail salva.');
    } catch (error) {
      message.error(error.response?.data || 'Não foi possível salvar a configuração do Gmail.');
    }
  };

  const syncGmail = async () => {
    setGmailLoading(true);
    try {
      const response = await api.post('/integrations/gmail/sync');
      message.success(`${response.data.newBatches} novo(s) lote(s) encontrado(s).`);
      const status = await api.get('/integrations/gmail/status');
      setGmail(status.data);
    } catch (error) {
      message.error(error.response?.data || 'Não foi possível sincronizar o Gmail.');
    } finally {
      setGmailLoading(false);
    }
  };

  const disconnectGmail = async () => {
    setGmailLoading(true);
    try {
      await api.post('/integrations/gmail/disconnect');
      setGmail((current) => ({ ...current, connected: false, googleEmail: null }));
      message.success('Gmail desconectado. Lotes e transações existentes foram preservados.');
    } catch {
      message.error('Não foi possível desconectar o Gmail.');
    } finally {
      setGmailLoading(false);
    }
  };

  const handleWipeData = async () => {
    try {
      await api.post('/users/wipe-data');
      message.success('Dados apagados e categorias resetadas para o padrao.');
      window.location.reload();
    } catch {
      message.error('Erro ao apagar dados');
    }
  };

  return (
    <div style={{ maxWidth: 640, margin: '0 auto' }}>
      <h2 style={{ marginBottom: 20 }}>Meu Perfil</h2>

      <Card style={{ textAlign: 'center', marginBottom: 20 }}>
        <Avatar size={100} icon={<UserOutlined />} style={{ backgroundColor: '#1890ff', marginBottom: 16 }} />
        <Title level={3}>{user.name}</Title>
        <Text type="secondary">{user.email}</Text>
      </Card>

      <Card title="Zona de Perigo" style={{ borderColor: '#ff4d4f' }}>
        <p>
          Isso apaga todas as transacoes, contas, metas e recorrencias. As categorias serao resetadas para o padrao.
        </p>
        <Popconfirm
          title="Tem certeza absoluta?"
          description="Essa acao e irreversivel."
          onConfirm={handleWipeData}
          okText="Sim, apagar tudo"
          cancelText="Cancelar"
        >
          <Button danger icon={<DeleteOutlined />} block size="large">
            ZERAR MINHA CONTA
          </Button>
        </Popconfirm>
      </Card>

      <Card title={<Space><GoogleOutlined /> Integrações</Space>} style={{ marginTop: 20 }}>
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <Space>
            <strong>Gmail</strong>
            <Tag color={gmail.connected ? 'green' : 'default'}>{gmail.connected ? 'Conectado' : 'Desconectado'}</Tag>
          </Space>
          {!gmail.enabled && <Text type="secondary">Gmail não configurado neste ambiente.</Text>}
          {gmail.connected && <>
            <Text type="secondary">Conta Google: {gmail.googleEmail || 'conectada'}</Text>
            <Select
              value={selectedAccount}
              placeholder="Conta FinFlow de destino"
              style={{ width: '100%' }}
              onChange={configureGmail}
              options={accounts.map((account) => ({ value: account.id, label: account.name }))}
            />
            <Text type="secondary">Consulta usada no Gmail: {gmail.searchQuery}</Text>
            <Space wrap>
              <Button icon={<SyncOutlined />} loading={gmailLoading} onClick={syncGmail}>Sincronizar agora</Button>
              <Button danger loading={gmailLoading} onClick={disconnectGmail}>Desconectar</Button>
            </Space>
          </>}
          {!gmail.connected && gmail.enabled && <Button type="primary" icon={<GoogleOutlined />} onClick={connectGmail}>Conectar Gmail</Button>}
        </Space>
      </Card>
    </div>
  );
}
