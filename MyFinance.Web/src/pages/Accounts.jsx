import React, { useEffect, useState } from 'react';
import { Card, Row, Col, Button, Statistic, Spin, Tag, Divider, Progress, Popconfirm, message, Tooltip } from 'antd';
import { PlusOutlined, BankOutlined, RiseOutlined, CreditCardOutlined, DeleteOutlined, EditOutlined, SwapOutlined, ToolOutlined } from '@ant-design/icons';
import api from '../services/api';
import TransferModal from '../components/TransferModal';
import AddAccountModal from '../components/AddAccountModal';
import AdjustBalanceModal from '../components/AdjustBalanceModal'; // <--- IMPORT CORRIGIDO

const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Accounts() {
  const [loading, setLoading] = useState(true);
  const [accounts, setAccounts] = useState([]);
  
  // Estados dos Modais
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [editingAccount, setEditingAccount] = useState(null);
  const [adjustAccount, setAdjustAccount] = useState(null); // Estado para o modal de ajuste

  useEffect(() => {
    loadAccounts();
  }, []);

  const loadAccounts = async () => {
    setLoading(true);
    try {
      const response = await api.get('/accounts');
      setAccounts(response.data);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id) => {
    try {
      await api.delete(`/accounts/${id}`);
      message.success('Conta excluda!');
      loadAccounts();
    } catch (error) {
      message.error('Erro ao excluir.');
    }
  };

  const handleEdit = (account) => {
    setEditingAccount(account);
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    setIsModalOpen(false);
    setEditingAccount(null);
  };

  const creditCards = accounts.filter(a => a.isCreditCard);
  const checkingAccounts = accounts.filter(a => !a.isCreditCard && a.type !== 'Investment');
  const investmentAccounts = accounts.filter(a => !a.isCreditCard && a.type === 'Investment');

  const totalChecking = checkingAccounts.reduce((acc, val) => acc + val.currentBalance, 0);
  const totalInvested = investmentAccounts.reduce((acc, val) => acc + val.currentBalance, 0);

  const renderActions = (account) => [
    <Tooltip title="Ajustar Saldo" key="adjust">
        <ToolOutlined style={{ color: '#faad14' }} onClick={() => setAdjustAccount(account)} />
    </Tooltip>,
    <Tooltip title="Editar" key="edit">
        <EditOutlined style={{ color: '#1890ff' }} onClick={() => handleEdit(account)} />
    </Tooltip>,
    <Popconfirm
      title="Apagar conta?"
      description="Isso apagar tambm o histrico."
      onConfirm={() => handleDelete(account.id)}
      okText="Sim"
      cancelText="No"
      key="delete"
    >
      <DeleteOutlined style={{ color: '#ff4d4f' }} />
    </Popconfirm>,
  ];

  const renderSimpleAccount = (account) => (
    <Col xs={24} sm={12} md={8} key={account.id}>
      <Card 
        hoverable 
        variant="borderless"
        style={{ borderRadius: 12, marginBottom: 16, border: '1px solid #f0f0f0' }}
        actions={renderActions(account)}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h4 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 16 }}>
            {account.type === 'Investment' ? <RiseOutlined style={{ color: '#1890ff' }} /> : <BankOutlined style={{ color: '#52c41a' }} />}
            {account.name}
          </h4>
          <Tag variant="borderless" color={account.type === 'Investment' ? 'blue' : 'success'}>
            {account.type === 'Investment' ? 'Investimento' : 'Conta'}
          </Tag>
        </div>
        <Divider style={{ margin: '16px 0' }} />
        <Statistic 
          value={account.currentBalance} 
          precision={2} 
          formatter={(val) => <span style={{ fontSize: 20, fontWeight: '600', color: '#333' }}>{formatMoney(val)}</span>} 
        />
      </Card>
    </Col>
  );

  const renderCreditCard = (card) => {
    const faturaAtual = card.invoiceAmount || 0; 
    const limiteUsado = Math.abs(card.currentBalance);
    const limite = card.creditLimit || 1000;
    const disponivel = limite - limiteUsado;
    const percentualUso = (limiteUsado / limite) * 100;

    return (
      <Col xs={24} sm={12} md={8} key={card.id}>
        <Card 
            hoverable 
            variant="borderless"
            style={{ borderRadius: 12, marginBottom: 16, background: 'linear-gradient(145deg, #2b2b2b 0%, #444 100%)' }}
            actions={renderActions(card)}
            className="dark-card"
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', color: '#fff' }}>
            <h4 style={{ margin: 0, color: '#fff', display: 'flex', alignItems: 'center', gap: 8, fontSize: 16 }}>
              <CreditCardOutlined style={{ color: '#faad14' }} />
              {card.name}
            </h4>
            <Tag color="gold" style={{color: '#333'}}>Fatura</Tag>
          </div>
          
          <div style={{ marginTop: 24, color: '#fff' }}>
            <span style={{ fontSize: 12, opacity: 0.7, textTransform: 'uppercase', letterSpacing: 1 }}>Fatura Atual</span>
            <div style={{ fontSize: 24, fontWeight: 'bold', color: '#fff', marginTop: 4 }}>{formatMoney(faturaAtual)}</div>
          </div>

          <div style={{ marginTop: 20 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#ccc', marginBottom: 6 }}>
              <span>Limite Utilizado</span>
              <span>{formatMoney(limiteUsado)}</span>
            </div>
            <Progress 
                percent={percentualUso} 
                showInfo={false} 
                strokeColor={percentualUso > 90 ? '#ff4d4f' : '#1890ff'} 
                railColor="rgba(255,255,255,0.1)"
                size={["100%", 8]}
            />
            <div style={{ textAlign: 'right', fontSize: 12, color: '#8c8c8c', marginTop: 8 }}>
              Disponvel: <span style={{ color: '#fff' }}>{formatMoney(disponivel)}</span>
            </div>
          </div>
        </Card>
      </Col>
    );
  };

  if (loading) return (
    <div style={{ padding: 50, textAlign: 'center' }}>
      <Spin size="large" />
    </div>
  );

  return (
    <div>
      {/* Cabealho */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24, background: '#fff', padding: '16px 24px', borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.03)' }}>
        <div>
            <h2 style={{ margin: 0, fontSize: 20 }}>Carteiras & Contas</h2>
            <span style={{ color: '#888' }}>Gerencie onde seu dinheiro está guardado</span>
        </div>
        
        <div style={{ display: 'flex', gap: 10 }}>
            <Button icon={<SwapOutlined />} onClick={() => setIsTransferOpen(true)} size="large">
              Transferir
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditingAccount(null); setIsModalOpen(true); }} size="large">
              Nova Conta
            </Button>
        </div>
      </div>

      {/* Resumo */}
      <Row gutter={16} style={{ marginBottom: 24 }}>
        <Col span={12}>
          <Card variant="borderless" style={{ borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.03)' }}>
            <Statistic title="Disponvel (Giro)" value={totalChecking} formatter={(v) => formatMoney(v)} prefix={<BankOutlined />} valueStyle={{ fontWeight: 'bold', color: '#3f8600' }} />
          </Card>
        </Col>
        <Col span={12}>
          <Card variant="borderless" style={{ borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.03)' }}>
            <Statistic title="Total Investido" value={totalInvested} formatter={(v) => formatMoney(v)} prefix={<RiseOutlined />} valueStyle={{ fontWeight: 'bold', color: '#1890ff' }} />
          </Card>
        </Col>
      </Row>

      {/* Listas */}
      {creditCards.length > 0 && (
        <>
          <h3 style={ margin: '20px 0 16px', color: '#555' }>Cartões de Crédito</h3>
          <Row gutter={16}>{creditCards.map(renderCreditCard)}</Row>
        </>
      )}

      <h3 style={{ margin: '20px 0 16px', color: '#555' }}>Contas Bancrias</h3>
      <Row gutter={16}>
        {[...checkingAccounts, ...investmentAccounts].map(renderSimpleAccount)}
      </Row>

      {/* Modais */}
      <AddAccountModal 
        visible={isModalOpen} 
        accountToEdit={editingAccount} 
        onClose={handleCloseModal} 
        onSuccess={loadAccounts} 
      />
      <TransferModal 
        visible={isTransferOpen} 
        onClose={() => setIsTransferOpen(false)} 
        onSuccess={loadAccounts} 
      />
      {/* Modal de Ajuste de Saldo (Corrigido) */}
      <AdjustBalanceModal
        visible={!!adjustAccount}
        account={adjustAccount}
        onClose={() => setAdjustAccount(null)}
        onSuccess={loadAccounts}
      />
    </div>
  );
}
