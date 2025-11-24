import React, { useState } from 'react'
import { TabBar, NavBar } from 'antd-mobile'
import { 
  AppOutline, 
  UnorderedListOutline, 
  PieOutline,
  AddCircleOutline 
} from 'antd-mobile-icons'
import styled from 'styled-components'

// Importações
import Home from './pages/Home';
import AddTransactionModal from './components/AddTransactionModal'; // <--- Importamos o modal

// Placeholders
const Transactions = () => <div style={{ padding: 20 }}><h2>Transações</h2></div>
const Statistics = () => <div style={{ padding: 20 }}><h2>Gráficos</h2></div>

const AddButton = styled.div`
  background-color: #6236FF;
  color: white;
  width: 50px;
  height: 50px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  margin-top: -15px; 
  box-shadow: 0 4px 10px rgba(98, 54, 255, 0.4);
`

export default function App() {
  const [activeKey, setActiveKey] = useState('home');
  const [isModalOpen, setIsModalOpen] = useState(false); // Estado para abrir/fechar modal
  const [refreshKey, setRefreshKey] = useState(0); // Um truque para forçar a Home atualizar

  const renderContent = () => {
    switch (activeKey) {
      // Passamos a key para forçar recarregamento quando salvar algo novo
      case 'home': return <Home key={refreshKey} /> 
      case 'transactions': return <Transactions />
      case 'stats': return <Statistics />
      default: return <Home />
    }
  }

  // Função especial para quando clicar na TabBar
  const handleTabChange = (key) => {
    if (key === 'add') {
      setIsModalOpen(true); // Se for o botão (+), abre o modal e não muda de tela
    } else {
      setActiveKey(key); // Se for outro, navega normal
    }
  }

  return (
    <div style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}>
      <div style={{ background: '#fff' }}>
        <NavBar backArrow={false}>MyFinance</NavBar>
      </div>
      
      <div style={{ flex: 1, overflowY: 'auto', background: '#f5f5f5' }}>
        {renderContent()}
      </div>

      <TabBar 
        activeKey={activeKey} 
        onChange={handleTabChange} // <--- Usamos nossa função customizada
        style={{ background: '#fff', borderTop: '1px solid #eee' }}
      >
        <TabBar.Item key='home' icon={<AppOutline />} title='Início' />
        <TabBar.Item key='transactions' icon={<UnorderedListOutline />} title='Extrato' />
        
        {/* Botão Central */}
        <TabBar.Item key='add' icon={<AddButton><AddCircleOutline /></AddButton>} title='' />
        
        <TabBar.Item key='stats' icon={<PieOutline />} title='Gráficos' />
      </TabBar>

      {/* O Modal fica aqui, "escondido" até ser chamado */}
      <AddTransactionModal 
        visible={isModalOpen} 
        onClose={() => setIsModalOpen(false)}
        onSuccess={() => setRefreshKey(old => old + 1)} // Atualiza a Home
      />
    </div>
  )
}