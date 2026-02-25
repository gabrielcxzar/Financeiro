import React, { useEffect, useState } from 'react';
import {
  Card,
  Row,
  Col,
  Button,
  Statistic,
  Tag,
  Divider,
  Progress,
  Popconfirm,
  message,
  Tooltip,
  Grid,
} from 'antd';
import {
  PlusOutlined,
  BankOutlined,
  RiseOutlined,
  CreditCardOutlined,
  DeleteOutlined,
  EditOutlined,
  SwapOutlined,
  ToolOutlined,
} from '@ant-design/icons';
import api from '../services/api';
import TransferModal from '../components/TransferModal';
import AddAccountModal from '../components/AddAccountModal';
import AdjustBalanceModal from '../components/AdjustBalanceModal';
import BrandLoading from '../components/BrandLoading';

const { useBreakpoint } = Grid;
const formatMoney = (value) => value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

export default function Accounts() {
  const [loading, setLoading] = useState(true);
  const [accounts, setAccounts] = useState([]);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [editingAccount, setEditingAccount] = useState(null);
  const [adjustAccount, setAdjustAccount] = useState(null);

  const screens = useBreakpoint();
  const isCompact = !screens.md;

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
      message.error('Erro ao carregar contas.');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id) => {
    try {
      await api.delete(`/accounts/${id}`);
      message.success('Conta excluida!');
      loadAccounts();
    } catch {
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

  const creditCards = accounts.filter((a) => a.isCreditCard);
  const checkingAccounts = accounts.filter((a) => !a.isCreditCard && a.type !== 'Investment');
  const investmentAccounts = accounts.filter((a) => !a.isCreditCard && a.type === 'Investment');

  const totalChecking = checkingAccounts.reduce((acc, val) => acc + val.currentBalance, 0);
  const totalInvested = investmentAccounts.reduce((acc, val) => acc + val.currentBalance, 0);

  const renderActions = (account) => [
    <Tooltip title="Ajustar saldo" key="adjust">
      <ToolOutlined style={{ color: '#faad14' }} onClick={() => setAdjustAccount(account)} />
    </Tooltip>,
    <Tooltip title="Editar" key="edit">
      <EditOutlined style={{ color: '#1890ff' }} onClick={() => handleEdit(account)} />
    </Tooltip>,
    <Popconfirm
      title="Apagar conta?"
      description="Isso apaga tambem o historico."
      onConfirm={() => handleDelete(account.id)}
      okText="Sim"
      cancelText="Nao"
      key="delete"
    >
      <DeleteOutlined style={{ color: '#ff4d4f' }} />
    </Popconfirm>,
  ];

  const renderSimpleAccount = (account) => (
    <Col xs={24} sm={12} xl={8} key={account.id}>
      <Card
        hoverable
        variant="borderless"
        style={{ borderRadius: 12, marginBottom: 8, border: '1px solid #f0f0f0' }}
        actions={renderActions(account)}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
          <h4 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8, fontSize: 16, minWidth: 0 }}>
            {account.type === 'Investment' ? (
              <RiseOutlined style={{ color: '#1890ff' }} />
            ) : (
              <BankOutlined style={{ color: '#52c41a' }} />
            )}
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{account.name}</span>
          </h4>
          <Tag variant="borderless" color={account.type === 'Investment' ? 'blue' : 'success'}>
            {account.type === 'Investment' ? 'Investimento' : 'Conta'}
          </Tag>
        </div>
        <Divider style={{ margin: '16px 0' }} />
        <Statistic
          value={account.currentBalance}
          precision={2}
          formatter={(val) => <span style={{ fontSize: isCompact ? 18 : 20, fontWeight: 600, color: '#333' }}>{formatMoney(val)}</span>}
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
      <Col xs={24} sm={12} xl={8} key={card.id}>
        <Card
          hoverable
          variant="borderless"
          style={{ borderRadius: 12, marginBottom: 8, background: 'linear-gradient(145deg, #2b2b2b 0%, #444 100%)' }}
          actions={renderActions(card)}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', color: '#fff', gap: 8 }}>
            <h4 style={{ margin: 0, color: '#fff', display: 'flex', alignItems: 'center', gap: 8, fontSize: 16, minWidth: 0 }}>
              <CreditCardOutlined style={{ color: '#faad14' }} />
              <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{card.name}</span>
            </h4>
            <Tag color="gold" style={{ color: '#333' }}>Fatura</Tag>
          </div>

          <div style={{ marginTop: 18, color: '#fff' }}>
            <span style={{ fontSize: 12, opacity: 0.7, textTransform: 'uppercase', letterSpacing: 1 }}>Fatura Atual</span>
            <div style={{ fontSize: isCompact ? 20 : 24, fontWeight: 'bold', color: '#fff', marginTop: 4 }}>
              {formatMoney(faturaAtual)}
            </div>
          </div>

          <div style={{ marginTop: 18 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#ccc', marginBottom: 6 }}>
              <span>Limite Utilizado</span>
              <span>{formatMoney(limiteUsado)}</span>
            </div>
            <Progress
              percent={percentualUso}
              showInfo={false}
              strokeColor={percentualUso > 90 ? '#ff4d4f' : '#1890ff'}
              trailColor="rgba(255,255,255,0.1)"
              size={["100%", 8]}
            />
            <div style={{ textAlign: 'right', fontSize: 12, color: '#8c8c8c', marginTop: 8 }}>
              Disponivel: <span style={{ color: '#fff' }}>{formatMoney(disponivel)}</span>
            </div>
          </div>
        </Card>
      </Col>
    );
  };

  if (loading) return <BrandLoading text="Carregando suas contas..." />;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 12,
          background: '#fff',
          padding: isCompact ? '14px' : '16px 24px',
          borderRadius: 8,
          boxShadow: '0 2px 8px rgba(0,0,0,0.03)',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: 20 }}>Carteiras e Contas</h2>
          <span style={{ color: '#888' }}>Gerencie onde seu dinheiro esta guardado</span>
        </div>

        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', width: isCompact ? '100%' : 'auto' }}>
          <Button
            icon={<SwapOutlined />}
            onClick={() => setIsTransferOpen(true)}
            size={isCompact ? 'middle' : 'large'}
            block={isCompact}
          >
            Transferir
          </Button>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              setEditingAccount(null);
              setIsModalOpen(true);
            }}
            size={isCompact ? 'middle' : 'large'}
            block={isCompact}
          >
            Nova Conta
          </Button>
        </div>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12}>
          <Card variant="borderless" style={{ borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.03)' }}>
            <Statistic
              title="Disponivel (Giro)"
              value={totalChecking}
              formatter={(v) => formatMoney(v)}
              prefix={<BankOutlined />}
              valueStyle={{ fontWeight: 'bold', color: '#3f8600' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12}>
          <Card variant="borderless" style={{ borderRadius: 8, boxShadow: '0 2px 8px rgba(0,0,0,0.03)' }}>
            <Statistic
              title="Total Investido"
              value={totalInvested}
              formatter={(v) => formatMoney(v)}
              prefix={<RiseOutlined />}
              valueStyle={{ fontWeight: 'bold', color: '#1890ff' }}
            />
          </Card>
        </Col>
      </Row>

      {creditCards.length > 0 && (
        <>
          <h3 style={{ margin: '4px 0 0', color: '#555' }}>Cartoes de Credito</h3>
          <Row gutter={[16, 16]}>{creditCards.map(renderCreditCard)}</Row>
        </>
      )}

      <h3 style={{ margin: '4px 0 0', color: '#555' }}>Contas Bancarias</h3>
      <Row gutter={[16, 16]}>{[...checkingAccounts, ...investmentAccounts].map(renderSimpleAccount)}</Row>

      <AddAccountModal
        visible={isModalOpen}
        accountToEdit={editingAccount}
        onClose={handleCloseModal}
        onSuccess={loadAccounts}
      />
      <TransferModal visible={isTransferOpen} onClose={() => setIsTransferOpen(false)} onSuccess={loadAccounts} />
      <AdjustBalanceModal
        visible={!!adjustAccount}
        account={adjustAccount}
        onClose={() => setAdjustAccount(null)}
        onSuccess={loadAccounts}
      />
    </div>
  );
}
