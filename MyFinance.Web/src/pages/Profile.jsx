import React, { useEffect, useState } from 'react';
import { Card, Button, Popconfirm, message, Avatar, Typography, Select, Space, Tag, List, Input, Modal, Switch, Alert } from 'antd';
import { UserOutlined, DeleteOutlined, GoogleOutlined, SyncOutlined } from '@ant-design/icons';
import api from '../services/api';

const { Title, Text } = Typography;

export default function Profile() {
  const [user, setUser] = useState({ name: '', email: '' });
  const [gmail, setGmail] = useState({ enabled: false, connected: false, searchQuery: '' });
  const [rules, setRules] = useState([]);
  const [accounts, setAccounts] = useState([]);
  const [selectedAccount, setSelectedAccount] = useState(null);
  const [gmailLoading, setGmailLoading] = useState(false);
  const [gmailConfiguring, setGmailConfiguring] = useState(false);
  const [syncResult, setSyncResult] = useState(null);
  const [ruleModalOpen, setRuleModalOpen] = useState(false);
  const [ruleDraft, setRuleDraft] = useState({ name: '', searchQuery: '', targetAccountId: null, enabled: true });

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
        setRules(gmailResponse.data.rules || []);
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
    setGmailConfiguring(true);
    try {
      await api.post('/integrations/gmail/configure', { defaultAccountId: accountId, searchQuery: gmail.searchQuery });
      const status = await api.get('/integrations/gmail/status');
      setGmail(status.data);
      setSelectedAccount(status.data.defaultAccountId || null);
      setRules(status.data.rules || []);
      message.success('Conta de destino do Gmail salva.');
    } catch (error) {
      message.error(error.response?.data || 'Não foi possível salvar a configuração do Gmail.');
    } finally {
      setGmailConfiguring(false);
    }
  };

  const syncGmail = async () => {
    setGmailLoading(true);
    try {
      const response = await api.post('/integrations/gmail/sync');
      setSyncResult(response.data);
      const status = await api.get('/integrations/gmail/status');
      setGmail(status.data);
      setRules(status.data.rules || []);
    } catch (error) {
      message.error(error.response?.data || 'Não foi possível sincronizar o Gmail.');
    } finally {
      setGmailLoading(false);
    }
  };

  const saveRule = async () => {
    if (!ruleDraft.name.trim() || !ruleDraft.searchQuery.trim() || !ruleDraft.targetAccountId) return message.error('Preencha nome, consulta e destino.');
    try {
      await api.post('/integrations/gmail/rules', { ...ruleDraft, name: ruleDraft.name.trim(), searchQuery: ruleDraft.searchQuery.trim() });
      const status = await api.get('/integrations/gmail/status');
      setGmail(status.data); setRules(status.data.rules || []); setRuleModalOpen(false); setRuleDraft({ name: '', searchQuery: '', targetAccountId: null, enabled: true });
      message.success('Regra de importação criada.');
    } catch (error) { message.error(error.response?.data || 'Não foi possível criar a regra.'); }
  };

  const toggleRule = async (rule, enabled) => {
    try {
      await api.put(`/integrations/gmail/rules/${rule.id}`, { name: rule.name, searchQuery: rule.searchQuery, targetAccountId: rule.targetAccountId, enabled });
      setRules((current) => current.map((item) => item.id === rule.id ? { ...item, enabled } : item));
    } catch (error) { message.error(error.response?.data || 'Não foi possível atualizar a regra.'); }
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
              loading={gmailConfiguring}
              disabled={gmailConfiguring || gmailLoading}
              placeholder="Conta FinFlow de destino"
              style={{ width: '100%' }}
              onChange={configureGmail}
              options={accounts.map((account) => ({ value: account.id, label: account.name }))}
            />
            {selectedAccount && <Text type="secondary">Conta de destino salva: {accounts.find((account) => account.id === selectedAccount)?.name || gmail.defaultAccountName}</Text>}
            <Space direction="vertical" style={{ width: '100%' }}>
              <Space style={{ justifyContent: 'space-between', width: '100%' }}><strong>Regras de importação</strong><Button size="small" onClick={() => setRuleModalOpen(true)}>Adicionar regra</Button></Space>
              <List size="small" bordered dataSource={rules} locale={{ emptyText: 'Nenhuma regra configurada.' }} renderItem={(rule) => <List.Item actions={[<Switch key="enabled" checked={rule.enabled} onChange={(enabled) => toggleRule(rule, enabled)} />]}><List.Item.Meta title={rule.name} description={<><div>Busca: {rule.searchQuery}</div><div>Destino: {rule.targetAccountName || 'conta configurada'}</div></>} /></List.Item>} />
            </Space>
            {syncResult && <Alert type={syncResult.ruleErrors ? 'warning' : 'success'} showIcon message="Sincronização concluída" description={<Space direction="vertical"><span>{syncResult.found} anexo(s) OFX encontrado(s) · {syncResult.newBatches} novo(s) lote(s) · {syncResult.alreadyProcessed} já processado(s) · {syncResult.invalid} inválido(s) · {syncResult.reviewItems} item(ns) aguardando revisão.</span>{syncResult.found === 0 && <span>Nenhum anexo OFX foi encontrado com as regras atuais.</span>}{syncResult.found > 0 && syncResult.newBatches === 0 && syncResult.alreadyProcessed > 0 && <span>Todos os extratos encontrados já haviam sido processados.</span>}</Space>} />}
            <Space wrap>
              <Button icon={<SyncOutlined />} loading={gmailLoading} disabled={gmailConfiguring || !rules.some((rule) => rule.enabled)} onClick={syncGmail}>Sincronizar agora</Button>
              <Button danger loading={gmailLoading} onClick={disconnectGmail}>Desconectar</Button>
            </Space>
          </>}
          {!gmail.connected && gmail.enabled && <Button type="primary" icon={<GoogleOutlined />} onClick={connectGmail}>Conectar Gmail</Button>}
        </Space>
      </Card>
      <Modal title="Adicionar regra de importação Gmail" open={ruleModalOpen} onCancel={() => setRuleModalOpen(false)} onOk={saveRule} okText="Salvar">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input placeholder="Nome, ex.: Nubank — Cartão" value={ruleDraft.name} onChange={(event) => setRuleDraft({ ...ruleDraft, name: event.target.value })} />
          <Input placeholder="Consulta Gmail" value={ruleDraft.searchQuery} onChange={(event) => setRuleDraft({ ...ruleDraft, searchQuery: event.target.value })} />
          <Select style={{ width: '100%' }} placeholder="Conta ou cartão destino" value={ruleDraft.targetAccountId} onChange={(targetAccountId) => setRuleDraft({ ...ruleDraft, targetAccountId })} options={accounts.map((account) => ({ value: account.id, label: `${account.isCreditCard ? 'Cartão' : 'Conta'} · ${account.name}` }))} />
        </Space>
      </Modal>
    </div>
  );
}
