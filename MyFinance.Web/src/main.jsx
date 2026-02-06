import React from 'react'
import ReactDOM from 'react-dom/client' // ImportaÃ§Ã£o do React 18
import App from './App.jsx'
import './index.css'

// ImportaÃ§Ãµes do Ant Design Mobile e idioma
import { ConfigProvider } from 'antd-mobile'
import ptBR from 'antd-mobile/es/locales/pt-BR'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <ConfigProvider locale={ptBR}>
      <App />
    </ConfigProvider>
  </React.StrictMode>,
)