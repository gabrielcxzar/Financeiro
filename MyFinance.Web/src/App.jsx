import React, { useState } from 'react';
import { Layout, Menu, theme, DatePicker, Button } from 'antd';
import {
  HomeOutlined,
  UnorderedListOutlined,
  PieChartOutlined,
  PlusCircleOutlined,
  WalletOutlined,
  BankOutlined,
  SyncOutlined,
  TagsOutlined,
  LogoutOutlined,
  CreditCardOutlined,
  UserOutlined,
  TrophyOutlined
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
import AddTransactionModal from './components/AddTransactionModal';

const { Header, Content, Footer, Sider } = Layout;

const Logo = styled.div`
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #001529;
  font-size: 18px;
  font-weight: bold;
  color: #fff;
  letter-spacing: 1px;
  border-bottom: 1px solid rgba(255,255,255,0.1);

  svg {
    margin-right: 10px;
    font-size: 22px;
    color: #1890ff;
  }
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
    { key: '2', icon: <UnorderedListOutlined />, label: 'Transa??es' },
    { key: '8', icon: <CreditCardOutlined />, label: 'Faturas do Cart?o' },
    { key: '4', icon: <BankOutlined />, label: 'Minhas Carteiras' },
    { key: '5', icon: <SyncOutlined />, label: 'Recorr?ncias' },
    { key: '6', icon: <TagsOutlined />, label: 'Categorias' },
    { key: '7', icon: <TrophyOutlined />, label: 'Metas/Or?amentos' },
    { key: '3', icon: <PieChartOutlined />, label: 'Relat?rios' },
    { type: 'divider' },
    { key: '9', icon: <UserOutlined />, label: 'Meu Perfil' },
    { type: 'divider' },
    { key: 'add', icon: <PlusCircleOutlined style={{ color: '#52c41a' }} />, label: 'Nova Transa??o' },
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
      default: return <Home month={month} year={year} />;
    }
  };

  return (
    <Layout style={{ height: '100vh' }}>
      <Sider collapsible collapsed={collapsed} onCollapse={(value) => setCollapsed(value)}>
        <Logo>
          <WalletOutlined />
          {!collapsed && 'MyFinance'}
        </Logo>
        <Menu theme="dark" defaultSelectedKeys={['1']} mode="inline" items={items} onClick={handleMenuClick} />
      </Sider>

      <Layout style={{ overflowY: 'auto' }}>
        <Header style={{ padding: '0 24px', background: colorBgContainer, display: 'flex', justifyContent: 'space-between', alignItems: 'center', position: 'sticky', top: 0, zIndex: 1, width: '100%' }}>
          <h2 style={{ margin: 0, color: '#001529' }}>Gest?o Financeira</h2>

          <div style={{ display: 'flex', gap: 16, alignItems: 'center' }}>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
              <span style={{ color: '#888' }}>Per?odo:</span>
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
          MyFinance ?{new Date().getFullYear()}
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
