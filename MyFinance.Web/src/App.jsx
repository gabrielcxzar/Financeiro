import React, { useState, useEffect } from 'react';
import { Layout, Menu, theme, DatePicker, Button, Grid, Drawer, ConfigProvider, Tooltip } from 'antd';
import {
  HomeOutlined,
  UnorderedListOutlined,
  PieChartOutlined,
  PlusCircleOutlined,
  BankOutlined,
  SyncOutlined,
  TagsOutlined,
  LogoutOutlined,
  CreditCardOutlined,
  UserOutlined,
  TrophyOutlined,
  RiseOutlined,
  MenuOutlined,
  FundOutlined,
  StarOutlined,
  SearchOutlined,
  QuestionCircleOutlined,
} from '@ant-design/icons';
import styled from 'styled-components';
import dayjs from 'dayjs';
import 'dayjs/locale/pt-br';

import Login from './pages/Login';
import Home from './pages/Home';
import Transactions from './pages/Transactions';
import Reports from './pages/Reports';
import Accounts from './pages/Accounts';
import Recurring from './pages/Recurring';
import Categories from './pages/Categories';
import Invoices from './pages/Invoices';
import Profile from './pages/Profile';
import Budgets from './pages/Budgets';
import Investments from './pages/Investments';
import Goals from './pages/Goals';

import AddTransactionModal from './components/AddTransactionModal';
import OnboardingWizard from './components/OnboardingWizard';
import FloatingActionButton from './components/FloatingActionButton';
import BottomNavigation from './components/BottomNavigation';
import CommandKModal from './components/CommandKModal';
import { authExpiredEvent, clearStoredAuth, getStoredAuthToken } from './services/api';

const { Header, Content, Footer, Sider } = Layout;
const { useBreakpoint } = Grid;

const Logo = styled.div`
  height: 72px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #0B0D12;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  padding: 0 16px;
`;

const LogoMark = styled.img`
  width: 36px;
  height: 36px;
  object-fit: contain;
  filter: drop-shadow(0 4px 12px rgba(255, 102, 0, 0.4));
`;

const LogoText = styled.span`
  margin-left: 12px;
  color: #FFFFFF;
  font-family: 'Sora', 'Plus Jakarta Sans', sans-serif;
  font-size: 20px;
  font-weight: 800;
  letter-spacing: -0.02em;
  
  span {
    color: #FF6600;
  }
`;

const HeaderTitle = styled.div`
  display: flex;
  align-items: center;
  gap: 12px;
`;

const ContentWrap = styled.div`
  padding: 24px;
  min-height: 480px;

  @media (max-width: 992px) {
    padding: 16px;
    padding-bottom: 80px; /* Space for bottom nav */
  }

  @media (max-width: 576px) {
    padding: 12px;
    padding-bottom: 80px;
  }
`;

const InnerBrandBar = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 20px;
  padding: 12px 18px;
  border-radius: 14px;
  background: linear-gradient(90deg, rgba(255, 102, 0, 0.06), rgba(255, 136, 0, 0.02));
  border: 1px solid rgba(255, 102, 0, 0.12);
`;

const InnerBrandMain = styled.div`
  display: flex;
  align-items: center;
  gap: 12px;
`;

const InnerBrandTitle = styled.div`
  display: flex;
  flex-direction: column;

  strong {
    color: #0F172A;
    font-family: 'Sora', 'Plus Jakarta Sans', sans-serif;
    font-size: 1rem;
    font-weight: 700;
  }

  span {
    color: #64748B;
    font-size: 0.8rem;
    font-weight: 500;
  }
`;

const menuItems = [
  {
    type: 'group',
    label: 'VISÃO GERAL',
    children: [
      { key: '1', icon: <HomeOutlined />, label: 'Dashboard' },
      { key: '3', icon: <PieChartOutlined />, label: 'Relatórios' },
    ],
  },
  {
    type: 'group',
    label: 'OPERACIONAL',
    children: [
      { key: '2', icon: <UnorderedListOutlined />, label: 'Transações' },
      { key: '4', icon: <BankOutlined />, label: 'Contas e Carteiras' },
      { key: '8', icon: <CreditCardOutlined />, label: 'Faturas do Cartão' },
      { key: '5', icon: <SyncOutlined />, label: 'Recorrências' },
    ],
  },
  {
    type: 'group',
    label: 'PLANEJAMENTO',
    children: [
      { key: '7', icon: <TrophyOutlined />, label: 'Metas Financeiras' },
      { key: '11', icon: <FundOutlined />, label: 'Orçamentos' },
      { key: '10', icon: <RiseOutlined />, label: 'Investimentos' },
    ],
  },
  {
    type: 'group',
    label: 'SISTEMA',
    children: [
      { key: '6', icon: <TagsOutlined />, label: 'Categorias' },
      { key: '9', icon: <UserOutlined />, label: 'Meu Perfil' },
      { key: 'add', icon: <PlusCircleOutlined style={{ color: '#FF6600' }} />, label: 'Nova Transação' },
    ],
  },
];

const pageNames = {
  '1': 'Dashboard Inteligente',
  '2': 'Extrato de Transações',
  '3': 'Relatórios & Análise',
  '4': 'Contas e Carteiras',
  '5': 'Despesas Recorrentes',
  '6': 'Categorias de Gastos',
  '7': 'Metas Financeiras',
  '8': 'Gestão de Faturas',
  '9': 'Perfil e Configurações',
  '10': 'Carteira de Investimentos',
  '11': 'Orçamentos Mensais',
};

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(!!getStoredAuthToken());
  const [collapsed, setCollapsed] = useState(false);
  const [activeKey, setActiveKey] = useState('1');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isOnboardingOpen, setIsOnboardingOpen] = useState(false);
  const [isCommandKOpen, setIsCommandKOpen] = useState(false);
  const [isValuesVisible, setIsValuesVisible] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedDate, setSelectedDate] = useState(dayjs());
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const screens = useBreakpoint();
  const isMobile = !screens.lg;

  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken();

  const handleLogout = () => {
    clearStoredAuth();
    setIsAuthenticated(false);
  };

  useEffect(() => {
    const handleAuthExpired = () => {
      setIsAuthenticated(false);
    };

    window.addEventListener(authExpiredEvent, handleAuthExpired);
    return () => window.removeEventListener(authExpiredEvent, handleAuthExpired);
  }, []);

  // Global Ctrl+K / Cmd+K listener
  useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        setIsCommandKOpen((prev) => !prev);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  // Auto trigger Onboarding Wizard if not completed yet
  useEffect(() => {
    if (isAuthenticated) {
      const completed = localStorage.getItem('finflow_onboarding_completed');
      if (!completed) {
        setIsOnboardingOpen(true);
      }
    }
  }, [isAuthenticated]);

  const handleMenuClick = (e) => {
    if (e.key === 'add') {
      setIsModalOpen(true);
    } else {
      setActiveKey(e.key);
    }

    if (isMobile) {
      setMobileMenuOpen(false);
    }
  };

  const renderContent = () => {
    const month = selectedDate.month() + 1;
    const year = selectedDate.year();

    switch (activeKey) {
      case '1':
        return <Home key={`${month}-${year}-${refreshKey}`} month={month} year={year} onOpenOnboarding={() => setIsOnboardingOpen(true)} />;
      case '2':
        return <Transactions key={`${month}-${year}`} month={month} year={year} />;
      case '3':
        return <Reports month={month} year={year} />;
      case '4':
        return <Accounts />;
      case '5':
        return <Recurring />;
      case '6':
        return <Categories />;
      case '7':
        return <Goals />;
      case '8':
        return <Invoices />;
      case '9':
        return <Profile />;
      case '10':
        return <Investments />;
      case '11':
        return <Budgets key={`${month}-${year}`} month={month} year={year} />;
      default:
        return <Home month={month} year={year} />;
    }
  };

  if (!isAuthenticated) {
    return (
      <ConfigProvider
        theme={{
          token: {
            colorPrimary: '#FF6600',
            fontFamily: "'Plus Jakarta Sans', sans-serif",
            borderRadius: 12,
          },
        }}
      >
        <Login onLoginSuccess={() => setIsAuthenticated(true)} />
      </ConfigProvider>
    );
  }

  const sideMenu = (
    <Menu
      theme="dark"
      selectedKeys={[activeKey]}
      mode="inline"
      items={menuItems}
      onClick={handleMenuClick}
    />
  );

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#FF6600',
          colorLink: '#FF6600',
          colorLinkHover: '#FF8800',
          borderRadius: 12,
          fontFamily: "'Plus Jakarta Sans', sans-serif",
        },
      }}
    >
      <Layout style={{ minHeight: '100vh' }}>
        {!isMobile ? (
          <Sider
            width={260}
            collapsible
            collapsed={collapsed}
            onCollapse={(value) => setCollapsed(value)}
            breakpoint="lg"
            style={{
              position: 'sticky',
              top: 0,
              height: '100vh',
              overflow: 'auto',
              left: 0,
              background: '#0B0D12',
            }}
          >
            <Logo>
              <LogoMark src="/brand-mark.svg" alt="Finflow" />
              {!collapsed && (
                <LogoText>
                  Fin<span>flow</span>
                </LogoText>
              )}
            </Logo>
            {sideMenu}
          </Sider>
        ) : (
          <Drawer
            placement="left"
            open={isMobile && mobileMenuOpen}
            onClose={() => setMobileMenuOpen(false)}
            width={264}
            bodyStyle={{ padding: 0, background: '#0B0D12' }}
            styles={{ header: { display: 'none' } }}
          >
            <Logo>
              <LogoMark src="/brand-mark.svg" alt="Finflow" />
              <LogoText>
                Fin<span>flow</span>
              </LogoText>
            </Logo>
            {sideMenu}
          </Drawer>
        )}

        <Layout style={{ minWidth: 0 }}>
          <Header
            style={{
              padding: isMobile ? '10px 14px' : '0 28px',
              height: 'auto',
              minHeight: 68,
              background: '#FFFFFF',
              display: 'flex',
              flexWrap: 'wrap',
              justifyContent: 'space-between',
              alignItems: 'center',
              gap: isMobile ? 10 : 16,
              position: 'sticky',
              top: 0,
              zIndex: 1,
              width: '100%',
              borderBottom: '1px solid #F1F5F9',
              boxShadow: '0 4px 14px rgba(0, 0, 0, 0.02)',
            }}
          >
            <HeaderTitle style={{ minWidth: 0 }}>
              {isMobile && (
                <Button
                  type="text"
                  icon={<MenuOutlined />}
                  onClick={() => setMobileMenuOpen(true)}
                  aria-label="Abrir menu"
                />
              )}
              <h2
                style={{
                  margin: 0,
                  color: '#0F172A',
                  fontSize: isMobile ? 18 : 22,
                  fontWeight: 800,
                  letterSpacing: '-0.02em',
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                {pageNames[activeKey] || 'Gestão Financeira'}
              </h2>
            </HeaderTitle>

            <div
              style={{
                display: 'flex',
                flexWrap: 'wrap',
                justifyContent: 'flex-end',
                gap: isMobile ? 8 : 14,
                alignItems: 'center',
              }}
            >
              {!isMobile && (
                <Tooltip title="Buscar comandos e atalhos (Ctrl + K)">
                  <Button
                    type="default"
                    icon={<SearchOutlined style={{ color: '#FF6600' }} />}
                    onClick={() => setIsCommandKOpen(true)}
                    style={{ borderRadius: 10, background: '#F8FAFC', border: '1px solid #E2E8F0', color: '#64748B' }}
                  >
                    Buscar... <Tag style={{ borderRadius: 4, marginLeft: 6, fontSize: 10 }}>Ctrl+K</Tag>
                  </Button>
                </Tooltip>
              )}

              <Tooltip title="Abrir Guia de Primeiros Passos">
                <Button
                  type="default"
                  icon={<StarOutlined style={{ color: '#FF6600' }} />}
                  onClick={() => setIsOnboardingOpen(true)}
                  style={{ borderRadius: 10 }}
                >
                  {!isMobile && 'Guia de Início'}
                </Button>
              </Tooltip>

              <div
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'center',
                  padding: '4px 10px',
                  borderRadius: 10,
                  background: '#F8FAFC',
                  border: '1px solid #E2E8F0',
                }}
              >
                <span style={{ color: '#64748B', fontSize: 12, fontWeight: 600 }}>Mês:</span>
                <DatePicker
                  picker="month"
                  format="MMMM/YYYY"
                  allowClear={false}
                  value={selectedDate}
                  onChange={(date) => setSelectedDate(date)}
                  style={{ width: isMobile ? 122 : 150, border: 'none', background: 'transparent' }}
                />
              </div>

              <Button type="text" danger icon={<LogoutOutlined />} onClick={handleLogout}>
                {!isMobile && 'Sair'}
              </Button>
            </div>
          </Header>

          <Content style={{ margin: isMobile ? '10px' : '20px' }}>
            <ContentWrap style={{ background: colorBgContainer, borderRadius: borderRadiusLG }}>
              <InnerBrandBar>
                <InnerBrandMain>
                  <img src="/brand-mark.svg" alt="" style={{ width: 26, height: 26 }} />
                  <InnerBrandTitle>
                    <strong>Finflow</strong>
                    <span>{pageNames[activeKey] || 'Painel'}</span>
                  </InnerBrandTitle>
                </InnerBrandMain>
                {!isMobile && (
                  <span style={{ color: '#64748B', fontSize: 12, fontWeight: 700 }}>
                    Planejamento & Controle Financeiro
                  </span>
                )}
              </InnerBrandBar>
              {renderContent()}
            </ContentWrap>
          </Content>

          <Footer
            style={{
              textAlign: 'center',
              color: '#94A3B8',
              fontSize: 13,
              padding: isMobile ? '12px 8px 80px' : '24px 50px',
            }}
          >
            Finflow © {new Date().getFullYear()} — Gestão Financeira Inteligente
          </Footer>
        </Layout>

        <AddTransactionModal
          visible={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          onSuccess={() => setRefreshKey((old) => old + 1)}
        />

        <OnboardingWizard
          open={isOnboardingOpen}
          onClose={() => setIsOnboardingOpen(false)}
          onComplete={() => setRefreshKey((old) => old + 1)}
        />

        <CommandKModal
          open={isCommandKOpen}
          onClose={() => setIsCommandKOpen(false)}
          onNavigate={(key) => setActiveKey(key)}
          onAddTransaction={() => setIsModalOpen(true)}
          onOpenOnboarding={() => setIsOnboardingOpen(true)}
          onToggleVisibility={() => {}}
        />

        <FloatingActionButton onClick={() => setIsModalOpen(true)} />

        {isMobile && (
          <BottomNavigation
            activeKey={activeKey}
            onSelect={(key) => setActiveKey(key)}
            onOpenMenu={() => setMobileMenuOpen(true)}
            onAddTransaction={() => setIsModalOpen(true)}
          />
        )}
      </Layout>
    </ConfigProvider>
  );
};

export default App;
