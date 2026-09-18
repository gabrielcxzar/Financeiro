import React, { useState, useEffect } from 'react';
import { Layout, Menu, theme, DatePicker, Button, Grid, Drawer, ConfigProvider, Tooltip, Tag, Avatar } from 'antd';
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
import BottomNavigation from './components/BottomNavigation';
import CommandKModal from './components/CommandKModal';
import api, { authExpiredEvent, clearStoredAuth, getStoredAuthToken } from './services/api';

const { Header, Content, Footer, Sider } = Layout;
const { useBreakpoint } = Grid;

const Logo = styled.div`
  height: 68px;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  background: #FFFFFF;
  border-bottom: 1px solid #E2E8F0;
  padding: 0 20px;
  gap: 12px;
`;

const LogoMark = styled.img`
  width: 32px;
  height: 32px;
  object-fit: contain;
`;

const LogoText = styled.div`
  display: flex;
  flex-direction: column;

  strong {
    color: #0F172A;
    font-family: 'Plus Jakarta Sans', sans-serif;
    font-size: 18px;
    font-weight: 800;
    letter-spacing: -0.02em;
    line-height: 1.1;

    span {
      color: #10B981;
    }
  }

  small {
    color: #94A3B8;
    font-size: 10px;
    font-weight: 600;
    letter-spacing: 0.06em;
    text-transform: uppercase;
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
    padding-bottom: 80px;
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
  border-radius: 12px;
  background: #FFFFFF;
  border: 1px solid #E2E8F0;
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
    font-family: 'Plus Jakarta Sans', sans-serif;
    font-size: 0.95rem;
    font-weight: 700;
  }

  span {
    color: #64748B;
    font-size: 0.8rem;
    font-weight: 500;
  }
`;

const UserProfileCard = styled.div`
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 14px 16px;
  border-top: 1px solid #E2E8F0;
  background: #FFFFFF;
  margin-top: auto;
`;

const UserInfo = styled.div`
  display: flex;
  flex-direction: column;
  overflow: hidden;
  flex: 1;

  strong {
    color: #0F172A;
    font-size: 13px;
    font-weight: 600;
    white-space: nowrap;
    text-overflow: ellipsis;
    overflow: hidden;
  }

  span {
    color: #94A3B8;
    font-size: 11px;
    white-space: nowrap;
    text-overflow: ellipsis;
    overflow: hidden;
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

  // Auto trigger Onboarding Wizard only for brand new users without accounts
  useEffect(() => {
    if (isAuthenticated) {
      const completed = localStorage.getItem('finflow_onboarding_completed');
      if (!completed) {
        api.get('/accounts').then((res) => {
          if (res.data && res.data.length > 0) {
            localStorage.setItem('finflow_onboarding_completed', 'true');
          } else {
            setIsOnboardingOpen(true);
          }
        }).catch(() => {
          setIsOnboardingOpen(true);
        });
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
            colorPrimary: '#0F172A',
            colorLink: '#2563EB',
            colorSuccess: '#10B981',
            colorError: '#F43F5E',
            fontFamily: "'Plus Jakarta Sans', 'Inter', sans-serif",
            borderRadius: 10,
          },
        }}
      >
        <Login onLoginSuccess={() => setIsAuthenticated(true)} />
      </ConfigProvider>
    );
  }

  const sideMenu = (
    <div style={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 68px)', justifyContent: 'space-between' }}>
      <div style={{ overflowY: 'auto', flex: 1 }}>
        <Menu
          theme="light"
          selectedKeys={[activeKey]}
          mode="inline"
          items={menuItems}
          onClick={handleMenuClick}
          style={{ borderRight: 'none', background: 'transparent' }}
        />
      </div>
      <UserProfileCard>
        <Avatar style={{ backgroundColor: '#F1F5F9', color: '#0F172A', fontWeight: 700, border: '1px solid #E2E8F0' }}>
          G
        </Avatar>
        {!collapsed && (
          <UserInfo>
            <strong>Gabriel</strong>
            <span>gabriel@email.com</span>
          </UserInfo>
        )}
        {!collapsed && (
          <Button
            type="text"
            size="small"
            icon={<LogoutOutlined style={{ color: '#94A3B8' }} />}
            onClick={handleLogout}
            title="Sair"
          />
        )}
      </UserProfileCard>
    </div>
  );

  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#0F172A',
          colorLink: '#2563EB',
          colorLinkHover: '#1D4ED8',
          colorSuccess: '#10B981',
          colorError: '#F43F5E',
          colorWarning: '#F59E0B',
          colorBgLayout: '#F8FAFC',
          colorBgContainer: '#FFFFFF',
          colorBorder: '#E2E8F0',
          borderRadius: 10,
          fontFamily: "'Plus Jakarta Sans', 'Inter', system-ui, -apple-system, sans-serif",
        },
      }}
    >
      <Layout style={{ minHeight: '100vh', background: '#F8FAFC' }}>
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
              overflow: 'hidden',
              left: 0,
              background: '#FFFFFF',
              borderRight: '1px solid #E2E8F0',
            }}
          >
            <Logo>
              <LogoMark src="/brand-mark.svg" alt="Finflow" />
              {!collapsed && (
                <LogoText>
                  <strong>Fin<span>flow</span></strong>
                  <small>Inteligência Financeira</small>
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
            bodyStyle={{ padding: 0, background: '#FFFFFF' }}
            styles={{ header: { display: 'none' } }}
          >
            <Logo>
              <LogoMark src="/brand-mark.svg" alt="Finflow" />
              <LogoText>
                <strong>Fin<span>flow</span></strong>
                <small>Inteligência Financeira</small>
              </LogoText>
            </Logo>
            {sideMenu}
          </Drawer>
        )}

        <Layout style={{ minWidth: 0, background: '#F8FAFC' }}>
          <Header
            className="responsive-header"
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
              zIndex: 10,
              width: '100%',
              borderBottom: '1px solid #E2E8F0',
              boxShadow: '0 1px 3px 0 rgba(15, 23, 42, 0.02)',
            }}
          >
            <HeaderTitle className="responsive-header__title" style={{ minWidth: 0 }}>
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
              className="responsive-header__actions"
              style={{
                display: 'flex',
                flexWrap: 'wrap',
                justifyContent: 'flex-end',
                gap: isMobile ? 8 : 12,
                alignItems: 'center',
              }}
            >
              {!isMobile && (
                <Tooltip title="Buscar comandos e atalhos (Ctrl + K)">
                  <Button
                    type="default"
                    icon={<SearchOutlined style={{ color: '#0F172A' }} />}
                    onClick={() => setIsCommandKOpen(true)}
                    style={{ borderRadius: 8, background: '#F8FAFC', border: '1px solid #E2E8F0', color: '#64748B' }}
                  >
                    Buscar... <Tag style={{ borderRadius: 4, marginLeft: 6, fontSize: 10, border: 'none', background: '#E2E8F0' }}>Ctrl+K</Tag>
                  </Button>
                </Tooltip>
              )}

              <Tooltip title="Abrir Guia de Primeiros Passos">
                <Button
                  className="responsive-header__guide"
                  type="default"
                  icon={<StarOutlined style={{ color: '#0F172A' }} />}
                  onClick={() => setIsOnboardingOpen(true)}
                  style={{ borderRadius: 8 }}
                >
                  {!isMobile && 'Guia de Início'}
                </Button>
              </Tooltip>

              <div
                className="responsive-month-picker"
                style={{
                  display: 'flex',
                  gap: 8,
                  alignItems: 'center',
                  padding: '4px 10px',
                  borderRadius: 8,
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

              {!isMobile && (
                <Button
                  type="primary"
                  icon={<PlusCircleOutlined />}
                  onClick={() => setIsModalOpen(true)}
                  style={{
                    background: '#0F172A',
                    borderColor: '#0F172A',
                    borderRadius: 8,
                    fontWeight: 600,
                    boxShadow: '0 1px 2px rgba(15, 23, 42, 0.08)',
                  }}
                >
                  + Nova Transação
                </Button>
              )}

              <Button className="responsive-header__logout" type="text" danger icon={<LogoutOutlined />} onClick={handleLogout}>
                {!isMobile && 'Sair'}
              </Button>
            </div>
          </Header>

          <Content className="responsive-content" style={{ margin: isMobile ? '10px' : '20px' }}>
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
