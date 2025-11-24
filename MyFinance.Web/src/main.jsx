import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.jsx'

// Importações para traduzir o Ant Design Mobile
import { ConfigProvider } from 'antd-mobile'
import ptBR from 'antd-mobile/es/locales/pt-BR'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <ConfigProvider locale={ptBR}>
      <App />
    </ConfigProvider>
  </StrictMode>,
)