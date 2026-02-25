import React, { useState } from 'react';
import { Layout, Menu, theme, DatePicker, Button, Grid, Drawer } from 'antd';
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
import AddTransactionModal from './components/AddTransactionModal';

const { Header, Content, Footer, Sider } = Layout;
const { useBreakpoint } = Grid;

const Logo = styled.div`
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #001529;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
`;

const LogoMark = styled.img`
  width: 30px;
  height: 30px;
  object-fit: contain;
  filter: drop-shadow(0 4px 10px rgba(10, 143, 255, 0.35));
`;

const LogoText = styled.span`
  margin-left: 10px;
  color: #f4f8ff;
  font-family: 'Sora', 'Manrope', sans-serif;
  font-size: 18px;
  font-weight: 700;
  letter-spacing: 0.4px;
`;

const HeaderTitle = styled.div`
  display: flex;
  align-items: center;
  gap: 12px;
`;

const HeaderMark = styled.img`
  width: 24px;
  height: 24px;
  object-fit: contain;
`;

const ContentWrap = styled.div`
  padding: 24px;
  min-height: 360px;

  @media (max-width: 992px) {
    padding: 16px;
  }

  @media (max-width: 576px) {
    padding: 12px;
  }
`;

const InnerBrandBar = styled.div`
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 18px;
  padding: 10px 12px;
  border-radius: 12px;
  background: linear-gradient(90deg, rgba(31, 140, 255, 0.08), rgba(77, 217, 255, 0.04));
  border: 1px solid rgba(31, 140, 255, 0.12);
`;

const InnerBrandMain = styled.div`
  display: flex;
  align-items: center;
  gap: 10px;
`;

const InnerBrandMark = styled.img`
  width: 22px;
  height: 22px;
  object-fit: contain;
`;

const InnerBrandTitle = styled.div`
  display: flex;
  flex-direction: column;
  line-height: 1.1;

  strong {
    color: #12315f;
    font-family: 'Sora', 'Manrope', sans-serif;
    font-size: 0.9rem;
    letter-spacing: 0.02em;
  }

  span {
    color: #5f6d82;
    font-size: 0.78rem;
    font-weight: 600;
  }
`;

const menuItems = [
  { key: '1', icon: <HomeOutlined />, label: 'Dashboard' },
  { key: '2', icon: <UnorderedListOutlined />, label: 'Transacoes' },
  { key: '8', icon: <CreditCardOutlined />, label: 'Faturas do Cartao' },
  { key: '4', icon: <BankOutlined />, label: 'Minhas Carteiras' },
  { key: '5', icon: <SyncOutlined />, label: 'Recorrencias' },
  { key: '6', icon: <TagsOutlined />, label: 'Categorias' },
  { key: '7', icon: <TrophyOutlined />, label: 'Metas/Orcamentos' },
  { key: '10', icon: <RiseOutlined />, label: 'Investimentos' },
  { key: '3', icon: <PieChartOutlined />, label: 'Relatorios' },
  { type: 'divider' },
  { key: '9', icon: <UserOutlined />, label: 'Meu Perfil' },
  { type: 'divider' },
  { key: 'add', icon: <PlusCircleOutlined style={{ color: '#52c41a' }} />, label: 'Nova Transacao' },
];

const pageNames = {
  '1': 'Dashboard',
  '2': 'Transacoes',
  '3': 'Relatorios',
  '4': 'Contas e Carteiras',
  '5': 'Recorrencias',
  '6': 'Categorias',
  '7': 'Metas e Orcamentos',
  '8': 'Faturas',
  '9': 'Perfil',
  '10': 'Investimentos',
};

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(!!localStorage.getItem('token'));
  const [collapsed, setCollapsed] = useState(false);
  const [activeKey, setActiveKey] = useState('1');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedDate, setSelectedDate] = useState(dayjs());
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const screens = useBreakpoint();
  const isMobile = !screens.lg;

  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken();

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    setIsAuthenticated(false);
  };

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
        return <Home key={`${month}-${year}-${refreshKey}`} month={month} year={year} />;
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
        return <Budgets month={month} year={year} />;
      case '8':
        return <Invoices />;
      case '9':
        return <Profile />;
      case '10':
        return <Investments />;
      default:
        return <Home month={month} year={year} />;
    }
  };

  if (!isAuthenticated) {
    return <Login onLoginSuccess={() => setIsAuthenticated(true)} />;
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
    <Layout style={{ minHeight: '100vh' }}>
      {!isMobile ? (
        <Sider
          width={248}
          collapsible
          collapsed={collapsed}
          onCollapse={(value) => setCollapsed(value)}
          breakpoint="lg"
        >
          <Logo>
            <LogoMark src="/brand-mark.svg" alt="Finflow" />
            {!collapsed && <LogoText>Finflow</LogoText>}
          </Logo>
          {sideMenu}
        </Sider>
      ) : (
        <Drawer
          placement="left"
          open={isMobile && mobileMenuOpen}
          onClose={() => setMobileMenuOpen(false)}
          width={264}
          bodyStyle={{ padding: 0, background: '#001529' }}
          styles={{ header: { display: 'none' } }}
        >
          <Logo>
            <LogoMark src="/brand-mark.svg" alt="Finflow" />
            <LogoText>Finflow</LogoText>
          </Logo>
          {sideMenu}
        </Drawer>
      )}

      <Layout style={{ minWidth: 0, overflowY: 'auto' }}>
        <Header
          style={{
            padding: isMobile ? '10px 12px' : '0 24px',
            height: 'auto',
            minHeight: 64,
            background: colorBgContainer,
            display: 'flex',
            flexWrap: 'wrap',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: isMobile ? 10 : 16,
            position: 'sticky',
            top: 0,
            zIndex: 1,
            width: '100%',
            borderBottom: '1px solid #f0f3f8',
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
            <HeaderMark src="/brand-mark.svg" alt="" aria-hidden="true" />
            <h2
              style={{
                margin: 0,
                color: '#001529',
                fontSize: isMobile ? 20 : 26,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              Gestao Financeira
            </h2>
          </HeaderTitle>

          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              justifyContent: 'flex-end',
              gap: isMobile ? 8 : 16,
              alignItems: 'center',
            }}
          >
            <div
              style={{
                display: 'flex',
                gap: 8,
                alignItems: 'center',
                padding: '6px 10px',
                borderRadius: 999,
                background: '#f5f8ff',
              }}
            >
              <span style={{ color: '#5d6a82', fontSize: 12, fontWeight: 600 }}>Periodo:</span>
              <DatePicker
                picker="month"
                format="MMMM/YYYY"
                allowClear={false}
                value={selectedDate}
                onChange={(date) => setSelectedDate(date)}
                style={{ width: isMobile ? 122 : 150 }}
              />
            </div>

            <Button type="text" danger icon={<LogoutOutlined />} onClick={handleLogout}>
              {!isMobile && 'Sair'}
            </Button>
          </div>
        </Header>

        <Content style={{ margin: isMobile ? '10px' : '16px' }}>
          <ContentWrap style={{ background: colorBgContainer, borderRadius: borderRadiusLG }}>
            <InnerBrandBar>
              <InnerBrandMain>
                <InnerBrandMark src="/brand-mark.svg" alt="" aria-hidden="true" />
                <InnerBrandTitle>
                  <strong>Finflow</strong>
                  <span>{pageNames[activeKey] || 'Painel'}</span>
                </InnerBrandTitle>
              </InnerBrandMain>
              {!isMobile && (
                <span style={{ color: '#6f7c92', fontSize: 12, fontWeight: 700 }}>
                  Planejamento e controle
                </span>
              )}
            </InnerBrandBar>
            {renderContent()}
          </ContentWrap>
        </Content>

        <Footer
          style={{
            textAlign: 'center',
            color: '#888',
            padding: isMobile ? '12px 8px' : '24px 50px',
          }}
        >
          Finflow {new Date().getFullYear()}
        </Footer>
      </Layout>

      <AddTransactionModal
        visible={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSuccess={() => setRefreshKey((old) => old + 1)}
      />
    </Layout>
  );
};

export default App;
