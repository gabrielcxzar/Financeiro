import React from 'react'
import ReactDOM from 'react-dom/client' // Importao do React 18
import App from './App.jsx'
import './index.css'

// Importaes do Ant Design Mobile e idioma
import { ConfigProvider } from 'antd-mobile'
import ptBR from 'antd-mobile/es/locales/pt-BR'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <ConfigProvider locale={ptBR}>
      <App />
    </ConfigProvider>
  </React.StrictMode>,
)