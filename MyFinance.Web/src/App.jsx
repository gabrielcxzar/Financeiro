import React, { useState } from 'react';
import { Layout, Menu, theme, DatePicker, Button } from 'antd';
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
  RiseOutlined
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

const App = () => {
  const [isAuthenticated, setIsAuthenticated] = useState(!!localStorage.getItem('token'));
  const [collapsed, setCollapsed] = useState(false);
  const [activeKey, setActiveKey] = useState('1');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedDate, setSelectedDate] = useState(dayjs());

  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken();

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    setIsAuthenticated(false);
  };

  if (!isAuthenticated) {
    return <Login onLoginSuccess={() => setIsAuthenticated(true)} />;
  }

  const items = [
    { key: '1', icon: <HomeOutlined />, label: 'Dashboard' },
    { key: '2', icon: <UnorderedListOutlined />, label: 'Transações' },
    { key: '8', icon: <CreditCardOutlined />, label: 'Faturas do Cartão' },
    { key: '4', icon: <BankOutlined />, label: 'Minhas Carteiras' },
    { key: '5', icon: <SyncOutlined />, label: 'Recorrências' },
    { key: '6', icon: <TagsOutlined />, label: 'Categorias' },
    { key: '7', icon: <TrophyOutlined />, label: 'Metas/Orçamentos' },
    { key: '10', icon: <RiseOutlined />, label: 'Investimentos' },
    { key: '3', icon: <PieChartOutlined />, label: 'Relatórios' },
    { type: 'divider' },
    { key: '9', icon: <UserOutlined />, label: 'Meu Perfil' },
    { type: 'divider' },
    { key: 'add', icon: <PlusCircleOutlined style={{ color: '#52c41a' }} />, label: 'Nova Transação' },
  ];

  const handleMenuClick = (e) => {
    if (e.key === 'add') setIsModalOpen(true);
    else setActiveKey(e.key);
  };

  const renderContent = () => {
    const month = selectedDate.month() + 1;
    const year = selectedDate.year();

    switch (activeKey) {
      case '1': return <Home key={`${month}-${year}-${refreshKey}`} month={month} year={year} />;
      case '2': return <Transactions key={`${month}-${year}`} month={month} year={year} />;
      case '3': return <Reports month={month} year={year} />;
      case '4': return <Accounts />;
      case '5': return <Recurring />;
      case '6': return <Categories />;
      case '7': return <Budgets month={month} year={year} />;
      case '8': return <Invoices />;
      case '9': return <Profile />;
      case '10': return <Investments />;
      default: return <Home month={month} year={year} />;
    }
  };

  return (
    <Layout style={{ height: '100vh' }}>
      <Sider collapsible collapsed={collapsed} onCollapse={(value) => setCollapsed(value)}>
        <Logo>
          <LogoMark src="/brand-mark.svg" alt="MyFinance" />
          {!collapsed && <LogoText>MyFinance</LogoText>}
        </Logo>
        <Menu theme="dark" defaultSelectedKeys={['1']} mode="inline" items={items} onClick={handleMenuClick} />
      </Sider>

      <Layout style={{ overflowY: 'auto' }}>
        <Header style={{ padding: '0 24px', background: colorBgContainer, display: 'flex', justifyContent: 'space-between', alignItems: 'center', position: 'sticky', top: 0, zIndex: 1, width: '100%' }}>
          <HeaderTitle>
            <HeaderMark src="/brand-mark.svg" alt="" aria-hidden="true" />
            <h2 style={{ margin: 0, color: '#001529' }}>Gestão Financeira</h2>
          </HeaderTitle>

          <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <span style={{ color: '#888' }}>Período:</span>
              <DatePicker
                picker="month"
                format="MMMM/YYYY"
                allowClear={false}
                value={selectedDate}
                onChange={(date) => setSelectedDate(date)}
                style={{ width: 150 }}
              />
            </div>

            <Button type="text" danger icon={<LogoutOutlined />} onClick={handleLogout}>
              Sair
            </Button>
          </div>
        </Header>

        <Content style={{ margin: '16px 16px' }}>
          <div style={{ padding: 24, minHeight: 360, background: colorBgContainer, borderRadius: borderRadiusLG }}>
            {renderContent()}
          </div>
        </Content>
        <Footer style={{ textAlign: 'center', color: '#888' }}>
          MyFinance {new Date().getFullYear()}
        </Footer>
      </Layout>

      <AddTransactionModal
        visible={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSuccess={() => setRefreshKey(old => old + 1)}
      />
    </Layout>
  );
};

export default App;
