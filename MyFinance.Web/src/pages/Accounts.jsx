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
import ActionableEmptyState from '../components/ActionableEmptyState';

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

  const renderActions = (account, isDark = false) => [
    <Tooltip title="Ajustar saldo" key="adjust">
      <Button
        type="text"
        size="small"
        icon={<ToolOutlined style={{ color: isDark ? '#94A3B8' : '#64748B' }} />}
        onClick={() => setAdjustAccount(account)}
      />
    </Tooltip>,
    <Tooltip title="Editar" key="edit">
      <Button
        type="text"
        size="small"
        icon={<EditOutlined style={{ color: isDark ? '#94A3B8' : '#64748B' }} />}
        onClick={() => handleEdit(account)}
      />
    </Tooltip>,
    <Popconfirm
      title="Apagar conta?"
      description="Isso apaga também o histórico."
      onConfirm={() => handleDelete(account.id)}
      okText="Sim"
      cancelText="Não"
      key="delete"
    >
      <Button
        type="text"
        size="small"
        danger
        icon={<DeleteOutlined />}
      />
    </Popconfirm>,
  ];

  const renderSimpleAccount = (account) => (
    <Col xs={24} sm={12} xl={8} key={account.id}>
      <Card
        hoverable
        variant="borderless"
        style={{
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          background: '#FFFFFF',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
        actions={renderActions(account)}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
            <div
              style={{
                width: 36,
                height: 36,
                borderRadius: 10,
                background: account.type === 'Investment' ? '#EFF6FF' : '#F0FDF4',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: account.type === 'Investment' ? '#3B82F6' : '#10B981',
                fontSize: 16,
              }}
            >
              {account.type === 'Investment' ? <RiseOutlined /> : <BankOutlined />}
            </div>
            <span style={{ fontWeight: 600, color: '#0F172A', fontSize: 15, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {account.name}
            </span>
          </div>
          <span
            style={{
              padding: '2px 8px',
              borderRadius: 10,
              fontSize: 11,
              fontWeight: 600,
              background: '#F1F5F9',
              color: '#475569',
            }}
          >
            {account.type === 'Investment' ? 'Investimento' : 'Conta'}
          </span>
        </div>
        <Divider style={{ margin: '14px 0', borderColor: '#F1F5F9' }} />
        <div>
          <span style={{ fontSize: 12, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em', fontWeight: 600 }}>
            Saldo Disponível
          </span>
          <div
            style={{
              fontSize: isCompact ? 20 : 22,
              fontWeight: 700,
              color: account.currentBalance >= 0 ? '#0F172A' : '#F43F5E',
              marginTop: 4,
              fontFeatureSettings: '"tnum" 1',
            }}
          >
            {formatMoney(account.currentBalance)}
          </div>
        </div>
      </Card>
    </Col>
  );

  const renderCreditCard = (card) => {
    const faturaAtual = card.invoiceAmount || 0;
    const passivoAtual = card.outstandingLiability || faturaAtual || 0;
    const limiteUsado = Math.max(passivoAtual, 0);
    const limite = card.creditLimit || 1000;
    const disponivel = Math.max(limite - limiteUsado, 0);
    const percentualUso = limite > 0 ? (limiteUsado / limite) * 100 : 0;

    return (
      <Col xs={24} sm={12} xl={8} key={card.id}>
        <Card
          hoverable
          variant="borderless"
          style={{
            borderRadius: 14,
            background: '#0F172A',
            border: '1px solid #1E293B',
            boxShadow: '0 4px 12px 0 rgba(15, 23, 42, 0.08)',
          }}
          actions={renderActions(card, true)}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
              <div
                style={{
                  width: 36,
                  height: 36,
                  borderRadius: 10,
                  background: 'rgba(255, 255, 255, 0.1)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#F8FAFC',
                  fontSize: 16,
                }}
              >
                <CreditCardOutlined />
              </div>
              <span style={{ fontWeight: 600, color: '#F8FAFC', fontSize: 15, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {card.name}
              </span>
            </div>
            <span
              style={{
                padding: '2px 8px',
                borderRadius: 10,
                fontSize: 11,
                fontWeight: 600,
                background: 'rgba(255, 255, 255, 0.12)',
                color: '#E2E8F0',
              }}
            >
              Crédito
            </span>
          </div>

          <div style={{ marginTop: 16 }}>
            <span style={{ fontSize: 11, color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.04em', fontWeight: 600 }}>
              Fatura Atual
            </span>
            <div
              style={{
                fontSize: isCompact ? 22 : 24,
                fontWeight: 700,
                color: '#F8FAFC',
                marginTop: 2,
                fontFeatureSettings: '"tnum" 1',
              }}
            >
              {formatMoney(faturaAtual)}
            </div>
            <div style={{ marginTop: 4, fontSize: 12, color: '#94A3B8' }}>
              Passivo consolidado: <strong style={{ color: '#E2E8F0', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(passivoAtual)}</strong>
            </div>
          </div>

          <div style={{ marginTop: 16 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#94A3B8', marginBottom: 6 }}>
              <span>Limite Utilizado</span>
              <span style={{ color: '#E2E8F0', fontWeight: 600, fontFeatureSettings: '"tnum" 1' }}>{percentualUso.toFixed(0)}%</span>
            </div>
            <Progress
              percent={percentualUso}
              showInfo={false}
              strokeColor={percentualUso > 90 ? '#F43F5E' : '#3B82F6'}
              trailColor="rgba(255, 255, 255, 0.12)"
              size={['100%', 6]}
            />
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, color: '#94A3B8', marginTop: 10 }}>
              <span>Disponível: <strong style={{ color: '#F8FAFC', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(disponivel)}</strong></span>
              <span>Total: <strong style={{ color: '#94A3B8', fontFeatureSettings: '"tnum" 1' }}>{formatMoney(limite)}</strong></span>
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
          gap: 16,
          background: '#FFFFFF',
          padding: isCompact ? '16px' : '20px 24px',
          borderRadius: 14,
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: '#0F172A', letterSpacing: '-0.02em' }}>
            Contas e Cartões
          </h2>
          <span style={{ color: '#64748B', fontSize: 13 }}>
            Gerencie onde seu dinheiro está alocado e acompanhe limites de crédito
          </span>
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
          <div
            style={{
              background: '#FFFFFF',
              borderRadius: 14,
              border: '1px solid #E2E8F0',
              padding: '16px 20px',
              boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              <BankOutlined style={{ color: '#10B981', fontSize: 14 }} />
              Saldo Disponível (Giro)
            </div>
            <div style={{ fontSize: 24, fontWeight: 700, color: '#10B981', marginTop: 6, fontFeatureSettings: '"tnum" 1' }}>
              {formatMoney(totalChecking)}
            </div>
          </div>
        </Col>
        <Col xs={24} sm={12}>
          <div
            style={{
              background: '#FFFFFF',
              borderRadius: 14,
              border: '1px solid #E2E8F0',
              padding: '16px 20px',
              boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 600, color: '#64748B', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              <RiseOutlined style={{ color: '#3B82F6', fontSize: 14 }} />
              Total em Investimentos
            </div>
            <div style={{ fontSize: 24, fontWeight: 700, color: '#0F172A', marginTop: 6, fontFeatureSettings: '"tnum" 1' }}>
              {formatMoney(totalInvested)}
            </div>
          </div>
        </Col>
      </Row>

      {accounts.length === 0 ? (
        <ActionableEmptyState
          title="Nenhuma conta ou carteira cadastrada"
          description="Cadastre seus bancos (ex: Itaú, Bradesco, Nubank) ou carteira física para acompanhar seu saldo consolidado."
          actionLabel="Adicionar Minha Primeira Conta"
          onAction={() => {
            setEditingAccount(null);
            setIsModalOpen(true);
          }}
        />
      ) : (
        <>
          {creditCards.length > 0 && (
            <>
              <h3 style={{ margin: '16px 0 8px', color: '#0F172A', fontWeight: 700 }}>Cartões de Crédito</h3>
              <Row gutter={[16, 16]}>{creditCards.map(renderCreditCard)}</Row>
            </>
          )}

          <h3 style={{ margin: '16px 0 8px', color: '#0F172A', fontWeight: 700 }}>Contas Bancárias e Investimentos</h3>
          <Row gutter={[16, 16]}>{[...checkingAccounts, ...investmentAccounts].map(renderSimpleAccount)}</Row>
        </>
      )}

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
